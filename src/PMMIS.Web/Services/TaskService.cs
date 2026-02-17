using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;

namespace PMMIS.Web.Services;

/// <summary>
/// Реализация сервиса управления задачами
/// </summary>
public class TaskService : ITaskService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notificationService;
    private readonly ILogger<TaskService> _logger;

    public TaskService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        INotificationService notificationService,
        ILogger<TaskService> logger)
    {
        _context = context;
        _userManager = userManager;
        _notificationService = notificationService;
        _logger = logger;
    }

    #region CRUD Operations

    public async Task<ProjectTask> CreateAsync(ProjectTask task, string creatorId)
    {
        task.AssignedById = creatorId;
        task.CreatedAt = DateTime.UtcNow;
        
        _context.ProjectTasks.Add(task);
        await _context.SaveChangesAsync();

        // Log history
        await AddHistoryAsync(task.Id, creatorId, TaskChangeType.Created, description: "Задача создана");

        // Notify assignee
        if (task.AssigneeId != creatorId)
        {
            var creator = await _userManager.FindByIdAsync(creatorId);
            await _notificationService.SendToUserAsync(
                task.AssigneeId,
                "Новая задача",
                $"Вам назначена задача: {task.Title}",
                NotificationType.TaskAssigned,
                task.Priority == TaskPriority.Critical ? NotificationPriority.Urgent : NotificationPriority.Normal,
                NotificationChannel.All,
                "Task",
                task.Id,
                $"/Tasks/Details/{task.Id}");
        }

        _logger.LogInformation("Task {TaskId} created by {UserId}", task.Id, creatorId);
        return task;
    }

    public async Task<ProjectTask?> GetByIdAsync(int id, bool includeRelations = false)
    {
        var query = _context.ProjectTasks.AsQueryable();

        if (includeRelations)
        {
            query = query
                .Include(t => t.Assignee)
                .Include(t => t.AssignedBy)
                .Include(t => t.SubTasks)
                .Include(t => t.Contract)
                .Include(t => t.ProcurementPlan)
                .Include(t => t.Attachments)
                .Include(t => t.Comments).ThenInclude(c => c.Author)
                .Include(t => t.ExtensionRequests).ThenInclude(r => r.RequestedBy)
                .Include(t => t.History).ThenInclude(h => h.ChangedBy);
        }

        return await query.FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<ProjectTask> UpdateAsync(ProjectTask task, string updaterId)
    {
        var existing = await _context.ProjectTasks.FindAsync(task.Id);
        if (existing == null) throw new InvalidOperationException("Task not found");

        // Track changes
        if (existing.Title != task.Title)
            await AddHistoryAsync(task.Id, updaterId, TaskChangeType.DescriptionChanged, "Title", existing.Title, task.Title);
        if (existing.DueDate != task.DueDate)
            await AddHistoryAsync(task.Id, updaterId, TaskChangeType.DueDateChanged, "DueDate", existing.DueDate.ToString("d"), task.DueDate.ToString("d"));
        if (existing.Priority != task.Priority)
            await AddHistoryAsync(task.Id, updaterId, TaskChangeType.PriorityChanged, "Priority", existing.Priority.ToString(), task.Priority.ToString());

        existing.Title = task.Title;
        existing.TitleTj = task.TitleTj;
        existing.TitleEn = task.TitleEn;
        existing.Description = task.Description;
        existing.Priority = task.Priority;
        existing.DueDate = task.DueDate;
        existing.StartDate = task.StartDate;
        existing.EstimatedHours = task.EstimatedHours;
        existing.ActualHours = task.ActualHours;
        existing.CompletionPercent = task.CompletionPercent;
        existing.ContractId = task.ContractId;
        existing.ProcurementPlanId = task.ProcurementPlanId;
        existing.ProjectId = task.ProjectId;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int taskId, string userId)
    {
        var task = await _context.ProjectTasks.FindAsync(taskId);
        if (task == null) return;

        _context.ProjectTasks.Remove(task);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Task {TaskId} deleted by {UserId}", taskId, userId);
    }

    #endregion

    #region Task Assignment

    public async Task AssignAsync(int taskId, string assigneeId, string assignerId)
    {
        var task = await _context.ProjectTasks.FindAsync(taskId);
        if (task == null) throw new InvalidOperationException("Task not found");

        if (!await CanAssignToAsync(assignerId, assigneeId))
            throw new UnauthorizedAccessException("Cannot assign task to this user");

        var oldAssigneeId = task.AssigneeId;
        task.AssigneeId = assigneeId;
        task.UpdatedAt = DateTime.UtcNow;

        await AddHistoryAsync(taskId, assignerId, TaskChangeType.AssigneeChanged, "AssigneeId", oldAssigneeId, assigneeId);
        await _context.SaveChangesAsync();

        // Notify new assignee
        if (assigneeId != assignerId)
        {
            await _notificationService.SendToUserAsync(
                assigneeId,
                "Задача назначена",
                $"Вам назначена задача: {task.Title}",
                NotificationType.TaskAssigned,
                NotificationPriority.Normal,
                NotificationChannel.All,
                "Task",
                task.Id,
                $"/Tasks/Details/{task.Id}");
        }
    }

    public async Task<bool> CanAssignToAsync(string assignerId, string assigneeId)
    {
        var assigner = await _userManager.FindByIdAsync(assignerId);
        var assignee = await _userManager.FindByIdAsync(assigneeId);
        if (assigner == null || assignee == null) return false;

        var assignerRoles = await _userManager.GetRolesAsync(assigner);
        var assigneeRoles = await _userManager.GetRolesAsync(assignee);

        // PMU_ADMIN can assign to anyone
        if (assignerRoles.Contains(UserRoles.PmuAdmin)) return true;

        // PMU_STAFF can assign to PMU_STAFF, ACCOUNTANT and CONTRACTOR
        if (assignerRoles.Contains(UserRoles.PmuStaff) || assignerRoles.Contains(UserRoles.Accountant))
        {
            return assigneeRoles.Contains(UserRoles.PmuStaff) || 
                   assigneeRoles.Contains(UserRoles.Accountant) ||
                   assigneeRoles.Contains(UserRoles.Contractor);
        }

        // WORLD_BANK can only view
        if (assignerRoles.Contains(UserRoles.WorldBank)) return false;

        // CONTRACTOR can only assign to themselves
        return assignerId == assigneeId;
    }

    #endregion

    #region Status Management

    public async Task ChangeStatusAsync(int taskId, ProjectTaskStatus newStatus, string userId)
    {
        var task = await _context.ProjectTasks.FindAsync(taskId);
        if (task == null) throw new InvalidOperationException("Task not found");

        var oldStatus = task.Status;
        task.Status = newStatus;
        task.UpdatedAt = DateTime.UtcNow;

        if (newStatus == ProjectTaskStatus.Completed)
        {
            task.CompletedAt = DateTime.UtcNow;
            task.CompletionPercent = 100;
        }

        await AddHistoryAsync(taskId, userId, TaskChangeType.StatusChanged, "Status", oldStatus.ToString(), newStatus.ToString());
        await _context.SaveChangesAsync();

        // Notify task creator about status change
        if (task.AssignedById != userId)
        {
            await _notificationService.SendToUserAsync(
                task.AssignedById,
                "Статус задачи изменён",
                $"Задача «{task.Title}» изменила статус на {GetStatusName(newStatus)}",
                NotificationType.TaskStatusChanged,
                NotificationPriority.Normal,
                NotificationChannel.InApp,
                "Task",
                task.Id,
                $"/Tasks/Details/{task.Id}");
        }
    }

    public async Task UpdateProgressAsync(int taskId, decimal completionPercent, string userId)
    {
        var task = await _context.ProjectTasks.FindAsync(taskId);
        if (task == null) throw new InvalidOperationException("Task not found");

        task.CompletionPercent = Math.Clamp(completionPercent, 0, 100);
        task.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
    }

    public async Task CompleteRelatedTasksAsync(int? contractId, int? workProgressId, string completedByUserId)
    {
        var query = _context.ProjectTasks
            .Where(t => t.Status != ProjectTaskStatus.Completed && t.Status != ProjectTaskStatus.Cancelled);

        // Prefer workProgressId match (more specific), fallback to contractId
        if (workProgressId.HasValue)
        {
            query = query.Where(t => t.WorkProgressId == workProgressId.Value);
        }
        else if (contractId.HasValue)
        {
            query = query.Where(t => t.ContractId == contractId.Value);
        }
        else
        {
            return; // Nothing to match
        }

        var tasks = await query.ToListAsync();
        foreach (var task in tasks)
        {
            task.Status = ProjectTaskStatus.Completed;
            task.CompletedAt = DateTime.UtcNow;
            task.CompletionPercent = 100;
            task.UpdatedAt = DateTime.UtcNow;

            await AddHistoryAsync(task.Id, completedByUserId, TaskChangeType.StatusChanged,
                "Status", task.Status.ToString(), ProjectTaskStatus.Completed.ToString(),
                "Автозавершение: действие выполнено в системе");
        }

        if (tasks.Any())
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Auto-completed {Count} tasks for ContractId={ContractId}, WorkProgressId={WorkProgressId} by {UserId}",
                tasks.Count, contractId, workProgressId, completedByUserId);
        }
    }

    #endregion

    #region Queries

    public async Task<(List<ProjectTask> Items, int TotalCount)> GetUserTasksAsync(
        string userId,
        TaskFilterDto? filter = null,
        int page = 1,
        int pageSize = 20)
    {
        var query = _context.ProjectTasks
            .Include(t => t.Assignee)
            .Include(t => t.Contract)
            .Where(t => t.AssigneeId == userId);

        query = ApplyFilter(query, filter);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(List<ProjectTask> Items, int TotalCount)> GetTeamTasksAsync(
        string managerId,
        TaskFilterDto? filter = null,
        int page = 1,
        int pageSize = 20)
    {
        var query = _context.ProjectTasks
            .Include(t => t.Assignee)
            .Include(t => t.Contract)
            .Where(t => t.AssignedById == managerId || t.AssigneeId == managerId);

        query = ApplyFilter(query, filter);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<List<ProjectTask>> GetByContractAsync(int contractId)
    {
        return await _context.ProjectTasks
            .Include(t => t.Assignee)
            .Where(t => t.ContractId == contractId)
            .OrderBy(t => t.DueDate)
            .ToListAsync();
    }

    public async Task<List<ProjectTask>> GetByProcurementAsync(int procurementId)
    {
        return await _context.ProjectTasks
            .Include(t => t.Assignee)
            .Where(t => t.ProcurementPlanId == procurementId)
            .OrderBy(t => t.DueDate)
            .ToListAsync();
    }

    public async Task<List<ProjectTask>> GetOverdueTasksAsync(string? userId = null)
    {
        var query = _context.ProjectTasks
            .Include(t => t.Assignee)
            .Where(t => t.Status != ProjectTaskStatus.Completed && 
                        t.Status != ProjectTaskStatus.Cancelled &&
                        t.DueDate < DateTime.UtcNow);

        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(t => t.AssigneeId == userId || t.AssignedById == userId);
        }

        return await query.OrderBy(t => t.DueDate).ToListAsync();
    }

    public async Task<List<ProjectTask>> GetUpcomingDeadlinesAsync(int daysAhead = 3, string? userId = null)
    {
        var deadline = DateTime.UtcNow.AddDays(daysAhead);
        
        var query = _context.ProjectTasks
            .Include(t => t.Assignee)
            .Where(t => t.Status != ProjectTaskStatus.Completed && 
                        t.Status != ProjectTaskStatus.Cancelled &&
                        t.DueDate >= DateTime.UtcNow &&
                        t.DueDate <= deadline);

        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(t => t.AssigneeId == userId || t.AssignedById == userId);
        }

        return await query.OrderBy(t => t.DueDate).ToListAsync();
    }

    private IQueryable<ProjectTask> ApplyFilter(IQueryable<ProjectTask> query, TaskFilterDto? filter)
    {
        if (filter == null) return query.OrderBy(t => t.DueDate);

        if (filter.Status.HasValue)
            query = query.Where(t => t.Status == filter.Status.Value);
        if (filter.Priority.HasValue)
            query = query.Where(t => t.Priority == filter.Priority.Value);
        if (!string.IsNullOrEmpty(filter.AssigneeId))
            query = query.Where(t => t.AssigneeId == filter.AssigneeId);
        if (filter.ContractId.HasValue)
            query = query.Where(t => t.ContractId == filter.ContractId);
        if (filter.ProcurementPlanId.HasValue)
            query = query.Where(t => t.ProcurementPlanId == filter.ProcurementPlanId);
        if (filter.ProjectId.HasValue)
            query = query.Where(t => t.ProjectId == filter.ProjectId);
        if (filter.IsOverdue == true)
            query = query.Where(t => t.DueDate < DateTime.UtcNow && t.Status != ProjectTaskStatus.Completed && t.Status != ProjectTaskStatus.Cancelled);
        if (filter.DueDateFrom.HasValue)
            query = query.Where(t => t.DueDate >= filter.DueDateFrom.Value);
        if (filter.DueDateTo.HasValue)
            query = query.Where(t => t.DueDate <= filter.DueDateTo.Value);
        if (!string.IsNullOrEmpty(filter.SearchTerm))
            query = query.Where(t => t.Title.Contains(filter.SearchTerm) || (t.Description != null && t.Description.Contains(filter.SearchTerm)));

        query = filter.OrderBy switch
        {
            "Priority" => filter.OrderDesc ? query.OrderByDescending(t => t.Priority) : query.OrderBy(t => t.Priority),
            "Status" => filter.OrderDesc ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status),
            "CreatedAt" => filter.OrderDesc ? query.OrderByDescending(t => t.CreatedAt) : query.OrderBy(t => t.CreatedAt),
            _ => filter.OrderDesc ? query.OrderByDescending(t => t.DueDate) : query.OrderBy(t => t.DueDate)
        };

        return query;
    }

    #endregion

    #region Extension Requests

    public async Task<TaskExtensionRequest> RequestExtensionAsync(int taskId, string reason, DateTime newDueDate, string userId)
    {
        var task = await _context.ProjectTasks.FindAsync(taskId);
        if (task == null) throw new InvalidOperationException("Task not found");

        var request = new TaskExtensionRequest
        {
            TaskId = taskId,
            RequestedById = userId,
            Reason = reason,
            OriginalDueDate = task.DueDate,
            NewDueDate = newDueDate,
            Status = ExtensionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.TaskExtensionRequests.Add(request);
        await AddHistoryAsync(taskId, userId, TaskChangeType.ExtensionRequested, description: $"Запрос продления до {newDueDate:d}");
        await _context.SaveChangesAsync();

        // Notify task creator/manager
        await _notificationService.SendToUserAsync(
            task.AssignedById,
            "Запрос на продление срока",
            $"Запрошено продление срока задачи «{task.Title}» до {newDueDate:d}",
            NotificationType.TaskExtensionRequested,
            NotificationPriority.High,
            NotificationChannel.All,
            "TaskExtension",
            request.Id,
            $"/Tasks/Extension/{request.Id}");

        return request;
    }

    public async Task ApproveExtensionAsync(int requestId, string approverId)
    {
        var request = await _context.TaskExtensionRequests
            .Include(r => r.Task)
            .FirstOrDefaultAsync(r => r.Id == requestId);
        
        if (request == null) throw new InvalidOperationException("Extension request not found");

        request.Status = ExtensionStatus.Approved;
        request.ApprovedById = approverId;
        request.ApprovedAt = DateTime.UtcNow;

        // Update task due date
        if (request.Task != null)
        {
            request.Task.DueDate = request.NewDueDate;
            request.Task.UpdatedAt = DateTime.UtcNow;
        }

        await AddHistoryAsync(request.TaskId, approverId, TaskChangeType.ExtensionApproved, description: $"Продление одобрено до {request.NewDueDate:d}");
        await _context.SaveChangesAsync();

        // Notify requester
        await _notificationService.SendToUserAsync(
            request.RequestedById,
            "Продление одобрено",
            $"Запрос на продление задачи одобрен. Новый срок: {request.NewDueDate:d}",
            NotificationType.TaskExtensionApproved,
            NotificationPriority.Normal,
            NotificationChannel.All,
            "Task",
            request.TaskId,
            $"/Tasks/Details/{request.TaskId}");
    }

    public async Task RejectExtensionAsync(int requestId, string reason, string rejecterId)
    {
        var request = await _context.TaskExtensionRequests
            .Include(r => r.Task)
            .FirstOrDefaultAsync(r => r.Id == requestId);
        
        if (request == null) throw new InvalidOperationException("Extension request not found");

        request.Status = ExtensionStatus.Rejected;
        request.ApprovedById = rejecterId;
        request.ApprovedAt = DateTime.UtcNow;
        request.RejectionReason = reason;

        await AddHistoryAsync(request.TaskId, rejecterId, TaskChangeType.ExtensionRejected, description: $"Продление отклонено: {reason}");
        await _context.SaveChangesAsync();

        // Notify requester
        await _notificationService.SendToUserAsync(
            request.RequestedById,
            "Продление отклонено",
            $"Запрос на продление задачи отклонён. Причина: {reason}",
            NotificationType.TaskExtensionRejected,
            NotificationPriority.High,
            NotificationChannel.All,
            "Task",
            request.TaskId,
            $"/Tasks/Details/{request.TaskId}");
    }

    public async Task<List<TaskExtensionRequest>> GetPendingExtensionsAsync(string? approverId = null)
    {
        var query = _context.TaskExtensionRequests
            .Include(r => r.Task)
            .Include(r => r.RequestedBy)
            .Where(r => r.Status == ExtensionStatus.Pending);

        if (!string.IsNullOrEmpty(approverId))
        {
            query = query.Where(r => r.Task != null && r.Task.AssignedById == approverId);
        }

        return await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
    }

    #endregion

    #region Comments & Attachments

    public async Task<TaskComment> AddCommentAsync(int taskId, string content, string userId, int? parentCommentId = null)
    {
        var comment = new TaskComment
        {
            TaskId = taskId,
            AuthorId = userId,
            Content = content,
            ParentCommentId = parentCommentId,
            CreatedAt = DateTime.UtcNow
        };

        _context.TaskComments.Add(comment);
        await AddHistoryAsync(taskId, userId, TaskChangeType.CommentAdded, description: "Добавлен комментарий");
        await _context.SaveChangesAsync();

        // Notify task participants
        var task = await _context.ProjectTasks.FindAsync(taskId);
        if (task != null && task.AssigneeId != userId)
        {
            await _notificationService.SendToUserAsync(
                task.AssigneeId,
                "Новый комментарий",
                $"Новый комментарий к задаче «{task.Title}»",
                NotificationType.TaskCommented,
                NotificationPriority.Low,
                NotificationChannel.InApp,
                "Task",
                taskId,
                $"/Tasks/Details/{taskId}");
        }

        return comment;
    }

    public async Task<TaskAttachment> AddAttachmentAsync(int taskId, string fileName, string filePath, long fileSize, string contentType, string userId)
    {
        var attachment = new TaskAttachment
        {
            TaskId = taskId,
            FileName = fileName,
            OriginalFileName = fileName,
            FilePath = filePath,
            FileSize = fileSize,
            ContentType = contentType,
            UploadedById = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.TaskAttachments.Add(attachment);
        await AddHistoryAsync(taskId, userId, TaskChangeType.AttachmentAdded, description: $"Добавлен файл: {fileName}");
        await _context.SaveChangesAsync();

        return attachment;
    }

    public async Task DeleteAttachmentAsync(int attachmentId, string userId)
    {
        var attachment = await _context.TaskAttachments.FindAsync(attachmentId);
        if (attachment == null) return;

        await AddHistoryAsync(attachment.TaskId, userId, TaskChangeType.AttachmentRemoved, description: $"Удалён файл: {attachment.FileName}");
        _context.TaskAttachments.Remove(attachment);
        await _context.SaveChangesAsync();
    }

    #endregion

    #region KPI

    public async Task<TaskKpiDto> CalculateKpiAsync(string userId, DateTime from, DateTime to)
    {
        var tasks = await _context.ProjectTasks
            .Where(t => t.AssigneeId == userId && t.CreatedAt >= from && t.CreatedAt <= to)
            .ToListAsync();

        var completedTasks = tasks.Where(t => t.Status == ProjectTaskStatus.Completed).ToList();
        var completedOnTime = completedTasks.Count(t => t.CompletedAt.HasValue && t.CompletedAt.Value <= t.DueDate);
        var completedLate = completedTasks.Count - completedOnTime;

        var avgDays = completedTasks.Any() && completedTasks.All(t => t.CompletedAt.HasValue)
            ? (decimal)completedTasks.Average(t => (t.CompletedAt!.Value - t.CreatedAt).TotalDays)
            : 0;

        return new TaskKpiDto
        {
            TotalTasks = tasks.Count,
            CompletedTasks = completedTasks.Count,
            CompletedOnTime = completedOnTime,
            CompletedLate = completedLate,
            ActiveTasks = tasks.Count(t => t.Status == ProjectTaskStatus.InProgress || t.Status == ProjectTaskStatus.New),
            OverdueTasks = tasks.Count(t => t.IsOverdue),
            PendingExtensions = await _context.TaskExtensionRequests.CountAsync(r => r.Task != null && r.Task.AssigneeId == userId && r.Status == ExtensionStatus.Pending),
            AverageCompletionDays = avgDays
        };
    }

    public async Task<TaskKpiDto> CalculateTeamKpiAsync(DateTime from, DateTime to)
    {
        var tasks = await _context.ProjectTasks
            .Where(t => t.CreatedAt >= from && t.CreatedAt <= to)
            .ToListAsync();

        var completedTasks = tasks.Where(t => t.Status == ProjectTaskStatus.Completed).ToList();
        var completedOnTime = completedTasks.Count(t => t.CompletedAt.HasValue && t.CompletedAt.Value <= t.DueDate);

        // Top performers
        var topPerformers = await _context.ProjectTasks
            .Where(t => t.Status == ProjectTaskStatus.Completed && t.CreatedAt >= from && t.CreatedAt <= to)
            .GroupBy(t => t.AssigneeId)
            .Select(g => new UserKpiSummary
            {
                UserId = g.Key,
                CompletedTasks = g.Count()
            })
            .OrderByDescending(x => x.CompletedTasks)
            .Take(5)
            .ToListAsync();

        // Load user names
        foreach (var perf in topPerformers)
        {
            var user = await _userManager.FindByIdAsync(perf.UserId);
            perf.UserName = user?.FullName ?? "Unknown";
        }

        return new TaskKpiDto
        {
            TotalTasks = tasks.Count,
            CompletedTasks = completedTasks.Count,
            CompletedOnTime = completedOnTime,
            CompletedLate = completedTasks.Count - completedOnTime,
            ActiveTasks = tasks.Count(t => t.Status == ProjectTaskStatus.InProgress || t.Status == ProjectTaskStatus.New),
            OverdueTasks = tasks.Count(t => t.IsOverdue),
            TopPerformers = topPerformers
        };
    }

    #endregion

    #region Private Helpers

    private async Task AddHistoryAsync(int taskId, string userId, TaskChangeType changeType, string? fieldName = null, string? oldValue = null, string? newValue = null, string? description = null)
    {
        var history = new TaskHistory
        {
            TaskId = taskId,
            ChangedById = userId,
            ChangeType = changeType,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };

        _context.TaskHistories.Add(history);
    }

    private static string GetStatusName(ProjectTaskStatus status) => status switch
    {
        ProjectTaskStatus.New => "Новая",
        ProjectTaskStatus.InProgress => "В работе",
        ProjectTaskStatus.OnHold => "На паузе",
        ProjectTaskStatus.UnderReview => "На проверке",
        ProjectTaskStatus.Completed => "Завершена",
        ProjectTaskStatus.Cancelled => "Отменена",
        _ => status.ToString()
    };

    #endregion
}
