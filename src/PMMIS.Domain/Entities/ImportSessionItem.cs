namespace PMMIS.Domain.Entities;

/// <summary>
/// Данные по одной позиции в рамках одной сессии импорта
/// </summary>
public class ImportSessionItem : BaseEntity
{
    /// <summary>
    /// Сессия импорта
    /// </summary>
    public int ImportSessionId { get; set; }
    public ImportSession ImportSession { get; set; } = null!;
    
    /// <summary>
    /// Позиция объёма работ
    /// </summary>
    public int ContractWorkItemId { get; set; }
    public ContractWorkItem ContractWorkItem { get; set; } = null!;
    
    /// <summary>
    /// Количество за этот период
    /// </summary>
    public decimal ThisPeriodQuantity { get; set; }
    
    /// <summary>
    /// Сумма за этот период
    /// </summary>
    public decimal ThisPeriodAmount { get; set; }
    
    /// <summary>
    /// Кумулятивное количество на момент импорта
    /// </summary>
    public decimal CumulativeQuantity { get; set; }
    
    /// <summary>
    /// Кумулятивная сумма на момент импорта
    /// </summary>
    public decimal CumulativeAmount { get; set; }
}
