using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;

namespace PMMIS.Web.Services;

/// <summary>
/// Фоновый сервис для ежедневного скачивания курсов валют НБТ.
/// Запускается каждый день в 06:00 UTC (11:00 по Душанбе).
/// При старте также проверяет и заполняет пропущенные дни за последние 7 дней.
/// </summary>
public class CurrencyRateFetchJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CurrencyRateFetchJob> _logger;

    public CurrencyRateFetchJob(
        IServiceScopeFactory scopeFactory,
        ILogger<CurrencyRateFetchJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit for the app to fully start
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        // On startup: fill any gaps from the last 7 days
        await FillRecentGapsAsync(stoppingToken);

        // Then run daily
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var nowUtc = DateTime.UtcNow;
            // Run at 06:00 UTC (11:00 Dushanbe) — check if we're in the 06:xx hour
            if (nowUtc.Hour == 6)
            {
                await FetchTodayRatesAsync(stoppingToken);
            }
        }
    }

    private async Task FillRecentGapsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var currencyService = scope.ServiceProvider.GetRequiredService<INbtCurrencyService>();

            var today = DateTime.UtcNow.Date;
            var startDate = today.AddDays(-7);

            // Get existing dates from DB
            var existingDates = await db.CurrencyRates
                .Where(r => r.Date >= startDate && r.Date <= today)
                .Select(r => r.Date.Date)
                .Distinct()
                .ToListAsync(ct);

            var existingSet = new HashSet<DateTime>(existingDates);

            var current = startDate;
            var fetched = 0;
            while (current <= today)
            {
                if (current.DayOfWeek != DayOfWeek.Saturday && 
                    current.DayOfWeek != DayOfWeek.Sunday &&
                    !existingSet.Contains(current))
                {
                    var rates = await currencyService.GetRatesForDateAsync(current);
                    if (rates.Any()) fetched++;
                    await Task.Delay(500, ct); // Be polite to NBT API
                }
                current = current.AddDays(1);
            }

            if (fetched > 0)
                _logger.LogInformation("CurrencyRateFetchJob: filled {Count} missing recent days", fetched);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CurrencyRateFetchJob: error filling recent gaps");
        }
    }

    private async Task FetchTodayRatesAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var currencyService = scope.ServiceProvider.GetRequiredService<INbtCurrencyService>();

            var today = DateTime.UtcNow.Date;
            var rates = await currencyService.GetRatesForDateAsync(today);

            if (rates.Any())
                _logger.LogInformation("CurrencyRateFetchJob: fetched {Count} rates for {Date}", rates.Count, today.ToString("yyyy-MM-dd"));
            else
                _logger.LogWarning("CurrencyRateFetchJob: no rates returned for {Date}", today.ToString("yyyy-MM-dd"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CurrencyRateFetchJob: error fetching today's rates");
        }
    }
}
