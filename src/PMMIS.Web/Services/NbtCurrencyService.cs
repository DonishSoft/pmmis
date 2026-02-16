using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;
using PMMIS.Web.ViewModels.Currency;

namespace PMMIS.Web.Services;

public interface INbtCurrencyService
{
    /// <summary>
    /// Получить курсы всех валют на заданную дату
    /// </summary>
    Task<List<CurrencyRateItem>> GetRatesForDateAsync(DateTime date);
    
    /// <summary>
    /// Получить историю курса одной валюты за период
    /// </summary>
    Task<List<CurrencyChartPoint>> GetRatesForRangeAsync(string charCode, DateTime from, DateTime to);
    
    /// <summary>
    /// Получить курс USD/TJS на заданную дату
    /// </summary>
    Task<decimal?> GetUsdRateForDateAsync(DateTime date);
}

public class NbtCurrencyService : INbtCurrencyService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<NbtCurrencyService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private const string BaseUrl = "https://nbt.tj/ru/kurs/export_xml.php";

    public NbtCurrencyService(
        HttpClient httpClient, 
        IMemoryCache cache, 
        ILogger<NbtCurrencyService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task<List<CurrencyRateItem>> GetRatesForDateAsync(DateTime date)
    {
        var dateOnly = date.Date;
        var dateUtc = DateTime.SpecifyKind(dateOnly, DateTimeKind.Utc);
        var cacheKey = $"nbt_rates_{dateOnly:yyyy-MM-dd}";
        
        // 1. In-memory cache (fastest)
        if (_cache.TryGetValue(cacheKey, out List<CurrencyRateItem>? cached) && cached != null)
            return cached;

        // 2. Database cache
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var dbRates = await db.CurrencyRates
            .Where(r => r.Date == dateUtc)
            .ToListAsync();

        if (dbRates.Any())
        {
            var result = dbRates.Select(r => new CurrencyRateItem
            {
                CharCode = r.CharCode,
                Nominal = r.Nominal,
                Name = r.Name,
                Value = r.Value,
                Date = r.Date
            }).ToList();

            _cache.Set(cacheKey, result, TimeSpan.FromHours(1));
            return result;
        }

        // 3. External API → save to DB
        try
        {
            var rates = await FetchRatesFromNbtAsync(date);
            
            if (rates.Any())
            {
                // Save to DB
                var entities = rates.Select(r => new CurrencyRate
                {
                    Date = dateUtc,
                    CharCode = r.CharCode,
                    Name = r.Name,
                    Nominal = r.Nominal,
                    Value = r.Value,
                    FetchedAt = DateTime.UtcNow
                }).ToList();

                db.CurrencyRates.AddRange(entities);
                
                try
                {
                    await db.SaveChangesAsync();
                    _logger.LogInformation("Cached {Count} currency rates for {Date}", entities.Count, date.ToString("yyyy-MM-dd"));
                }
                catch (DbUpdateException)
                {
                    // Duplicate key — another request already cached it, that's fine
                    _logger.LogDebug("Currency rates for {Date} already exist in DB", date.ToString("yyyy-MM-dd"));
                }
            }

            _cache.Set(cacheKey, rates, TimeSpan.FromHours(1));
            return rates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch NBT rates for {Date}", date);
            return new List<CurrencyRateItem>();
        }
    }

    public async Task<List<CurrencyChartPoint>> GetRatesForRangeAsync(string charCode, DateTime from, DateTime to)
    {
        var maxDate = from.AddDays(180) < to ? from.AddDays(180) : to;
        
        var fromUtc = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
        var maxDateUtc = DateTime.SpecifyKind(maxDate.Date, DateTimeKind.Utc);
        
        // Batch load from DB first
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var dbRates = await db.CurrencyRates
            .Where(r => r.CharCode == charCode && r.Date >= fromUtc && r.Date <= maxDateUtc)
            .OrderBy(r => r.Date)
            .ToListAsync();

        // Build set of dates we already have
        var cachedDates = new HashSet<DateTime>(dbRates.Select(r => r.Date.Date));
        
        // Build list of business days we need
        var missingDates = new List<DateTime>();
        var current = from;
        while (current <= maxDate)
        {
            if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
            {
                if (!cachedDates.Contains(current.Date))
                    missingDates.Add(current);
            }
            current = current.AddDays(1);
        }

        // Fetch only missing dates from API
        if (missingDates.Any())
        {
            _logger.LogInformation("Fetching {Count} missing date(s) from NBT API", missingDates.Count);
            foreach (var date in missingDates)
            {
                var rates = await GetRatesForDateAsync(date); // This will cache to DB
                var rate = rates.FirstOrDefault(r => r.CharCode.Equals(charCode, StringComparison.OrdinalIgnoreCase));
                if (rate != null)
                {
                    dbRates.Add(new CurrencyRate
                    {
                        Date = date,
                        CharCode = rate.CharCode,
                        Value = rate.Value
                    });
                }
            }
        }

        // Convert to chart points
        var points = dbRates
            .Where(r => r.Date.DayOfWeek != DayOfWeek.Saturday && r.Date.DayOfWeek != DayOfWeek.Sunday)
            .OrderBy(r => r.Date)
            .Select(r => new CurrencyChartPoint
            {
                Date = r.Date.ToString("yyyy-MM-dd"),
                Value = r.Value
            })
            .ToList();

        return points;
    }

    public async Task<decimal?> GetUsdRateForDateAsync(DateTime date)
    {
        var rates = await GetRatesForDateAsync(date);
        var usd = rates.FirstOrDefault(r => r.CharCode.Equals("USD", StringComparison.OrdinalIgnoreCase));
        return usd?.Value;
    }

    /// <summary>
    /// Загрузить курсы из API НБТ (внутренний метод)
    /// </summary>
    private async Task<List<CurrencyRateItem>> FetchRatesFromNbtAsync(DateTime date)
    {
        var url = $"{BaseUrl}?date={date:yyyy-MM-dd}&export=xmlout";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        // NBT actually sends UTF-8 content, but Content-Type header and XML declaration
        // may falsely claim windows-1251, causing HttpClient/XDocument to use wrong encoding.
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var xml = Encoding.UTF8.GetString(bytes);
        xml = xml.Replace("encoding=\"windows-1251\"", "encoding=\"utf-8\"");
        
        var doc = XDocument.Parse(xml);
        var rates = new List<CurrencyRateItem>();

        foreach (var valute in doc.Descendants("Valute"))
        {
            var charCode = valute.Element("CharCode")?.Value ?? "";
            var nominalStr = valute.Element("Nominal")?.Value ?? "1";
            var name = valute.Element("Name")?.Value ?? "";
            var valueStr = valute.Element("Value")?.Value ?? "0";

            if (int.TryParse(nominalStr, out var nominal) &&
                decimal.TryParse(valueStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                rates.Add(new CurrencyRateItem
                {
                    CharCode = charCode,
                    Nominal = nominal,
                    Name = name,
                    Value = value,
                    Date = date
                });
            }
        }

        return rates;
    }
}
