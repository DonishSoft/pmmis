using System.ComponentModel.DataAnnotations;

namespace PMMIS.Domain.Entities;

/// <summary>
/// Задача проекта
/// </summary>
public class ProjectTask : BaseEntity
{
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;
    public string TitleTj { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    public string? DescriptionTj { get; set; }
    public string? DescriptionEn { get; set; }
    
    public ProjectTaskStatus Status { get; set; } = ProjectTaskStatus.New;
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    
    // Сроки
    public DateTime? StartDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    // Исполнители
    [Required]
    public string AssigneeId { get; set; } = string.Empty;
    public ApplicationUser? Assignee { get; set; }
    
    [Required]
    public string AssignedById { get; set; } = string.Empty;
    public ApplicationUser? AssignedBy { get; set; }
    
    // Иерархия задач (подзадачи)
    public int? ParentTaskId { get; set; }
    public ProjectTask? ParentTask { get; set; }
    public List<ProjectTask> SubTasks { get; set; } = new();
    
    // Связи с модулями
    public int? ContractId { get; set; }
    public Contract? Contract { get; set; }
    
    public int? ProcurementPlanId { get; set; }
    public ProcurementPlan? ProcurementPlan { get; set; }
    
    public int? WorkProgressId { get; set; }
    public WorkProgress? WorkProgress { get; set; }
    
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
    
    /// <summary>
    /// Привязка к этапу контракта (для автозадач по просрочке)
    /// </summary>
    public int? MilestoneId { get; set; }
    public ContractMilestone? Milestone { get; set; }
    
    // Оценка трудозатрат
    public int EstimatedHours { get; set; }
    public int ActualHours { get; set; }
    public decimal CompletionPercent { get; set; }
    
    // Навигационные свойства
    public List<TaskAttachment> Attachments { get; set; } = new();
    public List<TaskComment> Comments { get; set; } = new();
    public List<TaskExtensionRequest> ExtensionRequests { get; set; } = new();
    public List<TaskHistory> History { get; set; } = new();
    public List<TaskChecklist> Checklists { get; set; } = new();
    
    // Вычисляемые свойства
    public bool IsOverdue => Status != ProjectTaskStatus.Completed && 
                             Status != ProjectTaskStatus.Cancelled && 
                             DateTime.UtcNow > DueDate;
    
    public int DaysUntilDue => (DueDate.Date - DateTime.UtcNow.Date).Days;
    
    public int SubTasksCompletedCount => SubTasks.Count(t => t.Status == ProjectTaskStatus.Completed);
    
    public string GetTitle(string language) => language switch
    {
        "tj" => string.IsNullOrEmpty(TitleTj) ? Title : TitleTj,
        "en" => string.IsNullOrEmpty(TitleEn) ? Title : TitleEn,
        _ => Title
    };
    
    public string? GetDescription(string language) => language switch
    {
        "tj" => string.IsNullOrEmpty(DescriptionTj) ? Description : DescriptionTj,
        "en" => string.IsNullOrEmpty(DescriptionEn) ? Description : DescriptionEn,
        _ => Description
    };
}

/// <summary>
/// Статус задачи
/// </summary>
public enum ProjectTaskStatus
{
    New = 0,            // Новая
    InProgress = 1,     // В работе
    OnHold = 2,         // На паузе
    UnderReview = 3,    // На проверке
    Completed = 4,      // Завершена
    Cancelled = 5       // Отменена
}

/// <summary>
/// Приоритет задачи
/// </summary>
public enum TaskPriority
{
    Low = 0,        // Низкий
    Normal = 1,     // Нормальный
    High = 2,       // Высокий
    Critical = 3    // Критический
}
