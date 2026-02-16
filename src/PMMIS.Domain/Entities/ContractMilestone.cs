namespace PMMIS.Domain.Entities;

/// <summary>
/// Промежуточный результат контракта (milestone)
/// </summary>
public class ContractMilestone : BaseEntity
{
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    
    /// <summary>
    /// Название результата
    /// </summary>
    public string Title { get; set; } = string.Empty;
    public string? TitleTj { get; set; }
    public string? TitleEn { get; set; }
    public string? Description { get; set; }
    
    /// <summary>
    /// Дата сдачи АВР
    /// </summary>
    public DateTime DueDate { get; set; }
    
    /// <summary>
    /// Статус milestone
    /// </summary>
    public MilestoneStatus Status { get; set; } = MilestoneStatus.Pending;
    
    /// <summary>
    /// Частота (разовый, ежемесячный, ежеквартальный)
    /// </summary>
    public MilestoneFrequency Frequency { get; set; } = MilestoneFrequency.OneTime;
    
    /// <summary>
    /// Привязанный АВР (заполняется при сдаче)
    /// </summary>
    public int? WorkProgressId { get; set; }
    public WorkProgress? WorkProgress { get; set; }
    
    /// <summary>
    /// Порядок сортировки
    /// </summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Статус промежуточного результата
/// </summary>
public enum MilestoneStatus
{
    Pending,        // Ожидает
    InProgress,     // В процессе
    Completed,      // Завершён
    Overdue         // Просрочен
}

/// <summary>
/// Частота сдачи
/// </summary>
public enum MilestoneFrequency
{
    OneTime,        // Разовый
    Monthly,        // Ежемесячный
    Quarterly       // Ежеквартальный
}
