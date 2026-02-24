namespace PMMIS.Domain.Entities;

/// <summary>
/// Позиция объёма работ по контракту
/// </summary>
public class ContractWorkItem : BaseEntity
{
    /// <summary>
    /// Контракт
    /// </summary>
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    
    /// <summary>
    /// Наименование работы (например: "Копать 500 кубов земли")
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Единица измерения (куб.м, тонна, шт, км, м², Да/Нет)
    /// </summary>
    public string Unit { get; set; } = string.Empty;
    
    /// <summary>
    /// Плановый объём (целевое значение)
    /// </summary>
    public decimal TargetQuantity { get; set; }
    
    /// <summary>
    /// Выполненный объём (сумма из всех АВР)
    /// </summary>
    public decimal AchievedQuantity { get; set; }
    
    /// <summary>
    /// Порядок сортировки
    /// </summary>
    public int SortOrder { get; set; }
    
    /// <summary>
    /// Процент выполнения
    /// </summary>
    public decimal ProgressPercent => TargetQuantity > 0 ? AchievedQuantity / TargetQuantity * 100 : 0;
    
    /// <summary>
    /// Записи прогресса по АВР
    /// </summary>
    public ICollection<WorkItemProgress> Progresses { get; set; } = new List<WorkItemProgress>();
}
