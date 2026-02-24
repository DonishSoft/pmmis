namespace PMMIS.Domain.Entities;

/// <summary>
/// Прогресс по позиции объёма работ в рамках АВР
/// </summary>
public class WorkItemProgress : BaseEntity
{
    /// <summary>
    /// Позиция объёма работ
    /// </summary>
    public int ContractWorkItemId { get; set; }
    public ContractWorkItem ContractWorkItem { get; set; } = null!;
    
    /// <summary>
    /// АВР, в рамках которого зафиксирован прогресс
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
}
