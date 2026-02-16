namespace PMMIS.Web.ViewModels.Currency;

/// <summary>
/// Один курс валюты на определённую дату
/// </summary>
public class CurrencyRateItem
{
    public string CharCode { get; set; } = "";
    public int Nominal { get; set; } = 1;
    public string Name { get; set; } = "";
    public decimal Value { get; set; }
    public DateTime Date { get; set; }
}

/// <summary>
/// Точка данных для графика
/// </summary>
public class CurrencyChartPoint
{
    public string Date { get; set; } = "";
    public decimal Value { get; set; }
}

/// <summary>
/// ViewModel для страницы курсов валют
/// </summary>
public class CurrencyRateViewModel
{
    /// <summary>
    /// Курсы на сегодня (все валюты)
    /// </summary>
    public List<CurrencyRateItem> TodayRates { get; set; } = new();
    
    /// <summary>
    /// Данные для графика
    /// </summary>
    public List<CurrencyChartPoint> ChartData { get; set; } = new();
    
    /// <summary>
    /// Выбранная валюта
    /// </summary>
    public string SelectedCurrency { get; set; } = "USD";
    
    /// <summary>
    /// Начало периода
    /// </summary>
    public DateTime DateFrom { get; set; }
    
    /// <summary>
    /// Конец периода
    /// </summary>
    public DateTime DateTo { get; set; }
    
    /// <summary>
    /// Доступные валюты для выбора
    /// </summary>
    public List<CurrencyOption> AvailableCurrencies { get; set; } = new();
}

public class CurrencyOption
{
    public string CharCode { get; set; } = "";
    public string Name { get; set; } = "";
}
