using PMMIS.Domain.Entities;

namespace PMMIS.Web.Services;

/// <summary>
/// Сервис управления задачами
/// </summary>
public interface ITaskService
{
    #region CRUD Operations
    
    Task<ProjectTask> CreateAsync(ProjectTask task, string creatorId);
    Task<ProjectTask?> GetByIdAsync(int id, bool includeRelations = false);
    Task<ProjectTask> UpdateAsync(ProjectTask task, string updaterId);
    Task DeleteAsync(int taskId, string userId);
    
    #endregion

    #region Task Assignment
    
    /// <summary>
    /// Назначить задачу исполнителю
    /// </summary>
    Task AssignAsync(int taskId, string assigneeId, string assignerId);
    
    /// <summary>
    /// Проверить можно ли пользователю назначить задачу другому
    /// </summary>
    Task<bool> CanAssignToAsync(string assignerId, string assigneeId);
    
    #endregion

    #region Status Management
    
    Task ChangeStatusAsync(int taskId, ProjectTaskStatus newStatus, string userId);
    Task UpdateProgressAsync(int taskId, decimal completionPercent, string userId);
    
    /// <summary>
    /// Автозавершение связанных задач при выполнении действия в workflow
    /// </summary>
    Task CompleteRelatedTasksAsync(int? contractId, int? workProgressId, string completedByUserId);
    
    #endregion

    #region Queries
    
    /// <summary>
    /// Получить задачи пользователя
    /// </summary>
    Task<(List<ProjectTask> Items, int TotalCount)> GetUserTasksAsync(
        string userId, 
        TaskFilterDto? filter = null,
        int page = 1,
        int pageSize = 20);
    
    /// <summary>
    /// Получить задачи команды (для руководителей)
    /// </summary>
    Task<(List<ProjectTask> Items, int TotalCount)> GetTeamTasksAsync(
        string managerId,
        TaskFilterDto? filter = null,
        int page = 1,
        int pageSize = 20);
    
    /// <summary>
    /// Получить задачи по контракту
    /// </summary>
    Task<List<ProjectTask>> GetByContractAsync(int contractId);
    
    /// <summary>
    /// Получить задачи по закупке
    /// </summary>
    Task<List<ProjectTask>> GetByProcurementAsync(int procurementId);
    
    /// <summary>
    /// Получить просроченные задачи
    /// </summary>
    Task<List<ProjectTask>> GetOverdueTasksAsync(string? userId = null);
    
    /// <summary>
    /// Получить задачи с приближающимся дедлайном
    /// </summary>
    Task<List<ProjectTask>> GetUpcomingDeadlinesAsync(int daysAhead = 3, string? userId = null);
    
    #endregion

    #region Extension Requests
    
    Task<TaskExtensionRequest> RequestExtensionAsync(int taskId, string reason, DateTime newDueDate, string userId);
    Task ApproveExtensionAsync(int requestId, string approverId);
    Task RejectExtensionAsync(int requestId, string reason, string rejecterId);
    Task<List<TaskExtensionRequest>> GetPendingExtensionsAsync(string? approverId = null);
    
    #endregion

    #region Comments & Attachments
    
    Task<TaskComment> AddCommentAsync(int taskId, string content, string userId, int? parentCommentId = null);
    Task<TaskAttachment> AddAttachmentAsync(int taskId, string fileName, string filePath, long fileSize, string contentType, string userId);
    Task DeleteAttachmentAsync(int attachmentId, string userId);
    
    #endregion

    #region KPI
    
    Task<TaskKpiDto> CalculateKpiAsync(string userId, DateTime from, DateTime to);
    Task<TaskKpiDto> CalculateTeamKpiAsync(DateTime from, DateTime to);
    
    #endregion
}

/// <summary>
/// Фильтр задач
/// </summary>
public class TaskFilterDto
{
    public ProjectTaskStatus? Status { get; set; }
    public TaskPriority? Priority { get; set; }
    public string? AssigneeId { get; set; }
    public int? ContractId { get; set; }
    public int? ProcurementPlanId { get; set; }
    public int? ProjectId { get; set; }
    public bool? IsOverdue { get; set; }
    public DateTime? DueDateFrom { get; set; }
    public DateTime? DueDateTo { get; set; }
    public string? SearchTerm { get; set; }
    public string OrderBy { get; set; } = "DueDate";
    public bool OrderDesc { get; set; } = false;
}

/// <summary>
/// KPI модель
/// </summary>
public class TaskKpiDto
{
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int CompletedOnTime { get; set; }
    public int CompletedLate { get; set; }
    public int ActiveTasks { get; set; }
    public int OverdueTasks { get; set; }
    public int PendingExtensions { get; set; }
    
    public decimal CompletionRate => TotalTasks > 0 ? (decimal)CompletedTasks / TotalTasks * 100 : 0;
    public decimal OnTimeRate => CompletedTasks > 0 ? (decimal)CompletedOnTime / CompletedTasks * 100 : 0;
    public decimal AverageCompletionDays { get; set; }
    
    public List<UserKpiSummary> TopPerformers { get; set; } = new();
}

/// <summary>
/// KPI пользователя
/// </summary>
public class UserKpiSummary
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public int CompletedTasks { get; set; }
    public decimal OnTimeRate { get; set; }
}
