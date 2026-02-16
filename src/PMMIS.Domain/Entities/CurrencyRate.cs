namespace PMMIS.Domain.Entities;

/// <summary>
/// Кэш курса валюты НБТ на конкретную дату
/// </summary>
public class CurrencyRate
{
    public int Id { get; set; }
    
    /// <summary>
    /// Дата курса
    /// </summary>
    public DateTime Date { get; set; }
    
    /// <summary>
    /// Код валюты (USD, EUR, RUB и т.д.)
    /// </summary>
    public string CharCode { get; set; } = "";
    
    /// <summary>
    /// Название валюты
    /// </summary>
    public string Name { get; set; } = "";
    
    /// <summary>
    /// Номинал (1, 10, 100)
    /// </summary>
    public int Nominal { get; set; } = 1;
    
    /// <summary>
    /// Курс в TJS
    /// </summary>
    public decimal Value { get; set; }
    
    /// <summary>
    /// Когда данные были загружены из НБТ
    /// </summary>
    public DateTime FetchedAt { get; set; }
}
