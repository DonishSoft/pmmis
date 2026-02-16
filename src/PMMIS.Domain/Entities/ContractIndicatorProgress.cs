namespace PMMIS.Domain.Entities;

/// <summary>
/// Прогресс по индикатору в рамках АВР (акта выполненных работ)
/// </summary>
public class ContractIndicatorProgress : BaseEntity
{
    /// <summary>
    /// Связь контракт-индикатор
    /// </summary>
    public int ContractIndicatorId { get; set; }
    public ContractIndicator ContractIndicator { get; set; } = null!;
    
    /// <summary>
    /// АВР (Прогресс работ), в рамках которого зафиксирован прогресс
    /// </summary>
    public int WorkProgressId { get; set; }
    public WorkProgress WorkProgress { get; set; } = null!;
    
    /// <summary>
    /// Значение прогресса в данном АВР
    /// </summary>
    public decimal Value { get; set; }
    
    /// <summary>
    /// Примечания
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// Детальные элементы прогресса (сёла, школы, медучреждения)
    /// </summary>
    public ICollection<IndicatorProgressItem> Items { get; set; } = new List<IndicatorProgressItem>();
}
