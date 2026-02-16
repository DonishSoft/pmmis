using PMMIS.Domain.Entities;
using PMMIS.Web.Services;

namespace PMMIS.Web.ViewModels.Tasks;

/// <summary>
/// ViewModel для списка задач
/// </summary>
public class TaskIndexViewModel
{
    public List<ProjectTask> Tasks { get; set; } = new();
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    
    // Фильтры
    public ProjectTaskStatus? StatusFilter { get; set; }
    public TaskPriority? PriorityFilter { get; set; }
    public string? AssigneeFilter { get; set; }
    public int? ContractFilter { get; set; }
    public bool? IsOverdueFilter { get; set; }
    public string? SearchTerm { get; set; }
    
    // Для выпадающих списков
    public List<ApplicationUser> Users { get; set; } = new();
    public List<Contract> Contracts { get; set; } = new();
    
    // Статистика
    public int OverdueCount { get; set; }
    public int UpcomingCount { get; set; }
    public int NewTasksCount { get; set; }
    public int InProgressCount { get; set; }
}

/// <summary>
/// ViewModel для создания/редактирования задачи
/// </summary>
public class TaskFormViewModel
{
    public int Id { get; set; }
    
    public string Title { get; set; } = string.Empty;
    public string TitleTj { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    public string? DescriptionTj { get; set; }
    public string? DescriptionEn { get; set; }
    
    public ProjectTaskStatus Status { get; set; } = ProjectTaskStatus.New;
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    
    public DateTime? StartDate { get; set; }
    public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(7);
    
    public string? AssigneeId { get; set; }
    public int? ParentTaskId { get; set; }
    
    public int? ContractId { get; set; }
    public int? ProcurementPlanId { get; set; }
    public int? ProjectId { get; set; }
    
    public int EstimatedHours { get; set; }
    
    // Чек-листы
    public List<ChecklistFormDto> Checklists { get; set; } = new();
    public string? ChecklistsJson { get; set; } // For form binding
    
    // Для выпадающих списков
    public List<ApplicationUser> AvailableAssignees { get; set; } = new();
    public List<Contract> Contracts { get; set; } = new();
    public List<ProcurementPlan> ProcurementPlans { get; set; } = new();
    public List<Project> Projects { get; set; } = new();
    public List<ProjectTask> ParentTasks { get; set; } = new();
    
    public bool IsEdit => Id > 0;
}

/// <summary>
/// ViewModel для деталей задачи
/// </summary>
public class TaskDetailsViewModel
{
    public ProjectTask Task { get; set; } = null!;
    public List<TaskComment> Comments { get; set; } = new();
    public List<TaskAttachment> Attachments { get; set; } = new();
    public List<TaskHistory> History { get; set; } = new();
    public List<TaskExtensionRequest> ExtensionRequests { get; set; } = new();
    public List<ProjectTask> SubTasks { get; set; } = new();
    public List<TaskChecklist> Checklists { get; set; } = new();
    
    // Права доступа
    public bool CanEdit { get; set; }
    public bool CanChangeStatus { get; set; }
    public bool CanApproveExtension { get; set; }
    public bool CanRequestExtension { get; set; }
    public bool CanAddComment { get; set; }
    public bool CanAddAttachment { get; set; }
    
    // Связанные сущности
    public Contract? LinkedContract { get; set; }
    public ProcurementPlan? LinkedProcurement { get; set; }
}

/// <summary>
/// ViewModel для запроса продления
/// </summary>
public class TaskExtensionFormViewModel
{
    public int TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public DateTime CurrentDueDate { get; set; }
    public DateTime NewDueDate { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// ViewModel для KPI дашборда
/// </summary>
public class TaskKpiViewModel
{
    public TaskKpiDto UserKpi { get; set; } = new();
    public TaskKpiDto TeamKpi { get; set; } = new();
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    
    public List<TaskStatusCount> StatusBreakdown { get; set; } = new();
    public List<TaskPriorityCount> PriorityBreakdown { get; set; } = new();
}

public class TaskStatusCount
{
    public ProjectTaskStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Color { get; set; } = "#6c757d";
}

public class TaskPriorityCount
{
    public TaskPriority Priority { get; set; }
    public string PriorityName { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Color { get; set; } = "#6c757d";
}

/// <summary>
/// DTO для чек-листа в форме
/// </summary>
public class ChecklistFormDto
{
    public int? Id { get; set; }
    public string Name { get; set; } = "Чек-лист";
    public List<ChecklistItemDto> Items { get; set; } = new();
}

/// <summary>
/// DTO для пункта чек-листа
/// </summary>
public class ChecklistItemDto
{
    public int? Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public bool IsImportant { get; set; }
    public bool IsIndented { get; set; }
    public string? CoExecutorId { get; set; }
    public string? CoExecutorName { get; set; }
}
