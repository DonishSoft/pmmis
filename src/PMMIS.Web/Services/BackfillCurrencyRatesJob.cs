using Microsoft.EntityFrameworkCore;
using PMMIS.Infrastructure.Data;

namespace PMMIS.Web.Services;

/// <summary>
/// Одноразовый фоновый сервис для загрузки исторических курсов валют за 4 года (2022–2026).
/// Если данные уже загружены — ничего не делает.
/// </summary>
public class BackfillCurrencyRatesJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackfillCurrencyRatesJob> _logger;

    // Backfill from January 2022 until yesterday
    private static readonly DateTime BackfillStart = new(2022, 1, 3, 0, 0, 0, DateTimeKind.Utc); // First business day of 2022

    public BackfillCurrencyRatesJob(
        IServiceScopeFactory scopeFactory,
        ILogger<BackfillCurrencyRatesJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for app to start
        await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Check if backfill is already done: do we have any data from 2022?
            var has2022Data = await db.CurrencyRates
                .AnyAsync(r => r.Date.Year == 2022, stoppingToken);

            if (has2022Data)
            {
                _logger.LogInformation("BackfillCurrencyRatesJob: historical data already exists, skipping");
                return;
            }

            _logger.LogInformation("BackfillCurrencyRatesJob: starting historical backfill from {Start}", BackfillStart.ToString("yyyy-MM-dd"));

            var currencyService = scope.ServiceProvider.GetRequiredService<INbtCurrencyService>();
            var yesterday = DateTime.UtcNow.Date.AddDays(-1);

            // Build list of business days that need fetching
            var datesToFetch = new List<DateTime>();
            var current = BackfillStart;
            while (current <= yesterday)
            {
                if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
                {
                    datesToFetch.Add(current);
                }
                current = current.AddDays(1);
            }

            _logger.LogInformation("BackfillCurrencyRatesJob: {Count} business days to fetch", datesToFetch.Count);

            var fetched = 0;
            var failed = 0;

            // Process in batches to avoid overwhelming the NBT API
            foreach (var date in datesToFetch)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    var rates = await currencyService.GetRatesForDateAsync(date);
                    if (rates.Any())
                        fetched++;
                    else
                        failed++;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogDebug(ex, "BackfillCurrencyRatesJob: failed to fetch rates for {Date}", date.ToString("yyyy-MM-dd"));
                }

                // Rate limit: ~2 requests per second
                await Task.Delay(500, stoppingToken);

                // Log progress every 100 days
                if ((fetched + failed) % 100 == 0)
                {
                    _logger.LogInformation("BackfillCurrencyRatesJob: progress {Done}/{Total} (fetched={Fetched}, failed={Failed})",
                        fetched + failed, datesToFetch.Count, fetched, failed);
                }
            }

            _logger.LogInformation("BackfillCurrencyRatesJob: completed. Fetched={Fetched}, Failed={Failed}, Total={Total}",
                fetched, failed, datesToFetch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BackfillCurrencyRatesJob: fatal error during backfill");
        }
    }
}
