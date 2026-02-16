using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMMIS.Domain.Entities;
using PMMIS.Web.Authorization;
using PMMIS.Web.Services;
using PMMIS.Web.ViewModels.Currency;

namespace PMMIS.Web.Controllers;

/// <summary>
/// Курсы валют НБТ (Национальный банк Таджикистана)
/// </summary>
[Authorize]
[RequirePermission(MenuKeys.CurrencyRates, PermissionType.View)]
public class CurrencyController : Controller
{
    private readonly INbtCurrencyService _currencyService;

    public CurrencyController(INbtCurrencyService currencyService)
    {
        _currencyService = currencyService;
    }

    /// <summary>
    /// Страница курсов валют с графиком и таблицей
    /// </summary>
    public async Task<IActionResult> Index(string? currency, string? from, string? to)
    {
        var selectedCurrency = currency ?? "USD";
        
        // Parse dates
        DateTime dateTo;
        DateTime dateFrom;
        
        if (DateTime.TryParse(to, out var parsedTo))
            dateTo = parsedTo;
        else
            dateTo = DateTime.Today;
        
        if (DateTime.TryParse(from, out var parsedFrom))
            dateFrom = parsedFrom;
        else
            dateFrom = dateTo.AddMonths(-3);

        // Get today's rates for the sidebar table
        var todayRates = await _currencyService.GetRatesForDateAsync(DateTime.Today);

        // Get chart data for selected currency
        var chartData = await _currencyService.GetRatesForRangeAsync(selectedCurrency, dateFrom, dateTo);

        var viewModel = new CurrencyRateViewModel
        {
            TodayRates = todayRates,
            ChartData = chartData,
            SelectedCurrency = selectedCurrency,
            DateFrom = dateFrom,
            DateTo = dateTo,
            AvailableCurrencies = todayRates
                .OrderBy(r => r.CharCode)
                .Select(r => new CurrencyOption { CharCode = r.CharCode, Name = r.Name })
                .ToList()
        };

        return View(viewModel);
    }

    /// <summary>
    /// API для AJAX-обновления данных графика
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetChartData(string currency, string from, string to)
    {
        if (!DateTime.TryParse(from, out var dateFrom) || !DateTime.TryParse(to, out var dateTo))
            return BadRequest("Invalid date format");

        var chartData = await _currencyService.GetRatesForRangeAsync(currency, dateFrom, dateTo);
        
        // Also get rate info for the header
        var todayRates = await _currencyService.GetRatesForDateAsync(DateTime.Today);
        var selectedRate = todayRates.FirstOrDefault(r => r.CharCode == currency);

        return Json(new
        {
            labels = chartData.Select(p => p.Date),
            values = chartData.Select(p => p.Value),
            currencyName = selectedRate?.Name ?? currency,
            currentRate = selectedRate?.Value ?? 0,
            nominal = selectedRate?.Nominal ?? 1
        });
    }

    /// <summary>
    /// API: Получить курс USD/TJS на заданную дату
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUsdRate(string date)
    {
        if (!DateTime.TryParse(date, out var parsedDate))
            return BadRequest(new { error = "Invalid date format" });

        var rate = await _currencyService.GetUsdRateForDateAsync(parsedDate);
        
        if (rate == null)
            return Json(new { rate = (decimal?)null, date, error = "Rate not available for this date" });

        return Json(new { rate = rate.Value, date });
    }
}
