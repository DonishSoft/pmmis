using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;

namespace PMMIS.Web.Services;

/// <summary>
/// Сервис маршрутизации Workflow — автоматическое создание задач и уведомлений
/// по цепочке утверждения АВР и Платежей
/// </summary>
public interface IWorkflowRoutingService
{
    /// <summary>
    /// Запустить workflow после создания АВР (шаг Create → автозавершение → задача для следующей роли)
    /// </summary>
    Task StartWorkflowAsync(int workProgressId, string creatorUserId);

    /// <summary>
    /// Продвинуть workflow на следующий шаг (после утверждения текущего шага)
    /// </summary>
    Task AdvanceAsync(int workProgressId, string approverUserId);

    /// <summary>
    /// Отклонить АВР — вернуть на указанный шаг (RejectToStepOrder) или на шаг 1
    /// </summary>
    Task RejectAsync(int workProgressId, string rejectorUserId, string reason);

    /// <summary>
    /// Проверить, может ли пользователь утверждать/отклонять данный АВР
    /// </summary>
    Task<bool> CanUserActOnCurrentStepAsync(int workProgressId, string userId);

    /// <summary>
    /// Получить информацию о текущем шаге workflow для АВР
    /// </summary>
    Task<WorkflowStepInfo?> GetCurrentStepInfoAsync(int workProgressId);
}

/// <summary>
/// Информация о текущем шаге для отображения в UI
/// </summary>
public class WorkflowStepInfo
{
    public int StepOrder { get; set; }
    public string StepName { get; set; } = "";
    public string ActionType { get; set; } = "";
    public string RoleName { get; set; } = "";
    public int TotalSteps { get; set; }
    public bool IsCompleted { get; set; }
    public bool CanReject { get; set; }
}

public class WorkflowRoutingService : IWorkflowRoutingService
{
    private readonly ApplicationDbContext _context;
    private readonly ITaskService _taskService;
    private readonly INotificationService _notificationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<WorkflowRoutingService> _logger;

    public WorkflowRoutingService(
        ApplicationDbContext context,
        ITaskService taskService,
        INotificationService notificationService,
        UserManager<ApplicationUser> userManager,
        ILogger<WorkflowRoutingService> logger)
    {
        _context = context;
        _taskService = taskService;
        _notificationService = notificationService;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task StartWorkflowAsync(int workProgressId, string creatorUserId)
    {
        var wp = await _context.WorkProgresses
            .Include(w => w.Contract)
                .ThenInclude(c => c.Contractor)
            .FirstOrDefaultAsync(w => w.Id == workProgressId);

        if (wp == null) return;

        var steps = await _context.WorkflowSteps
            .Where(s => s.WorkflowType == "AVR" && s.IsActive)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();

        if (!steps.Any())
        {
            _logger.LogWarning("No active workflow steps for AVR. Skipping workflow routing.");
            return;
        }

        // Step 1 is "Create" — auto-complete it since the AVR was just created
        var firstStep = steps.First();
        _logger.LogInformation("Starting AVR workflow for WP#{WorkProgressId}, step 1: {StepName}", workProgressId, firstStep.StepName);

        // Find the next step (step 2 = usually Review)
        var nextStep = steps.FirstOrDefault(s => s.StepOrder > firstStep.StepOrder);

        if (nextStep != null)
        {
            // Move to step 2 and create task + notification
            wp.CurrentStepOrder = nextStep.StepOrder;
            wp.ApprovalStatus = AvrApprovalStatus.SubmittedForReview;
            wp.SubmittedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await CreateTaskForStepAsync(wp, nextStep, creatorUserId);
            await SendNotificationForStepAsync(wp, nextStep);
        }
        else
        {
            // Only 1 step — auto-approve
            wp.CurrentStepOrder = firstStep.StepOrder;
            wp.ApprovalStatus = AvrApprovalStatus.DirectorApproved;
            await _context.SaveChangesAsync();
        }
    }

    public async Task AdvanceAsync(int workProgressId, string approverUserId)
    {
        var wp = await _context.WorkProgresses
            .Include(w => w.Contract)
                .ThenInclude(c => c.Contractor)
            .FirstOrDefaultAsync(w => w.Id == workProgressId);

        if (wp == null || wp.CurrentStepOrder == null) return;

        var steps = await _context.WorkflowSteps
            .Where(s => s.WorkflowType == "AVR" && s.IsActive)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();

        var currentStep = steps.FirstOrDefault(s => s.StepOrder == wp.CurrentStepOrder);
        if (currentStep == null) return;

        _logger.LogInformation("Advancing AVR workflow WP#{Id}: step {From} ({Name}) approved by {User}",
            workProgressId, currentStep.StepOrder, currentStep.StepName, approverUserId);

        // Complete existing tasks for this AVR
        await _taskService.CompleteRelatedTasksAsync(wp.ContractId, workProgressId, approverUserId);

        // Find next step
        var nextStep = steps.FirstOrDefault(s => s.StepOrder > currentStep.StepOrder);

        if (nextStep != null)
        {
            // Move to next step
            wp.CurrentStepOrder = nextStep.StepOrder;

            // Update approval status based on action type
            if (nextStep.ActionType == "Approve" || nextStep.ActionType == "FinalApprove")
                wp.ApprovalStatus = AvrApprovalStatus.ManagerApproved;

            await _context.SaveChangesAsync();

            await CreateTaskForStepAsync(wp, nextStep, approverUserId);
            await SendNotificationForStepAsync(wp, nextStep);

            // Notify creator that step was approved
            if (!string.IsNullOrEmpty(wp.SubmittedByUserId))
            {
                await _notificationService.SendToUserAsync(
                    wp.SubmittedByUserId,
                    $"АВР утверждён на шаге «{currentStep.StepName}»",
                    $"АВР по контракту {wp.Contract.ContractNumber} прошёл шаг «{currentStep.StepName}» и перешёл к «{nextStep.StepName}»",
                    NotificationType.ProgressUpdate,
                    NotificationPriority.Normal,
                    NotificationChannel.InApp,
                    "WorkProgress", workProgressId,
                    $"/WorkProgressReports/Details/{workProgressId}");
            }
        }
        else
        {
            // No more steps — workflow complete!
            wp.CurrentStepOrder = currentStep.StepOrder;
            wp.ApprovalStatus = AvrApprovalStatus.DirectorApproved;
            wp.DirectorApprovedAt = DateTime.UtcNow;
            wp.DirectorApprovedById = approverUserId;
            await _context.SaveChangesAsync();

            _logger.LogInformation("AVR workflow completed for WP#{Id}. Final approval.", workProgressId);

            // Notify creator that AVR is fully approved
            if (!string.IsNullOrEmpty(wp.SubmittedByUserId))
            {
                await _notificationService.SendToUserAsync(
                    wp.SubmittedByUserId,
                    "✅ АВР полностью утверждён!",
                    $"АВР по контракту {wp.Contract.ContractNumber} ({wp.Contract.Contractor.Name}) прошёл все этапы утверждения.",
                    NotificationType.ProgressUpdate,
                    NotificationPriority.High,
                    NotificationChannel.InApp,
                    "WorkProgress", workProgressId,
                    $"/WorkProgressReports/Details/{workProgressId}");
            }
        }
    }

    public async Task RejectAsync(int workProgressId, string rejectorUserId, string reason)
    {
        var wp = await _context.WorkProgresses
            .Include(w => w.Contract)
                .ThenInclude(c => c.Contractor)
            .FirstOrDefaultAsync(w => w.Id == workProgressId);

        if (wp == null || wp.CurrentStepOrder == null) return;

        var steps = await _context.WorkflowSteps
            .Where(s => s.WorkflowType == "AVR" && s.IsActive)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();

        var currentStep = steps.FirstOrDefault(s => s.StepOrder == wp.CurrentStepOrder);
        if (currentStep == null) return;

        var rejectToOrder = currentStep.RejectToStepOrder ?? 1;
        var rejectToStep = steps.FirstOrDefault(s => s.StepOrder == rejectToOrder) ?? steps.First();

        _logger.LogInformation("Rejecting AVR WP#{Id}: step {From} → step {To} ({Name}), reason: {Reason}",
            workProgressId, currentStep.StepOrder, rejectToStep.StepOrder, rejectToStep.StepName, reason);

        // Complete existing tasks
        await _taskService.CompleteRelatedTasksAsync(wp.ContractId, workProgressId, rejectorUserId);

        // Set rejection info
        wp.ApprovalStatus = AvrApprovalStatus.Rejected;
        wp.CurrentStepOrder = rejectToStep.StepOrder;
        wp.RejectionReason = reason;
        await _context.SaveChangesAsync();

        // Create task for the rejection target role
        await CreateTaskForStepAsync(wp, rejectToStep, rejectorUserId, 
            $"Исправить АВР (отклонён на этапе «{currentStep.StepName}»): {reason}");

        // Notify creator
        if (!string.IsNullOrEmpty(wp.SubmittedByUserId))
        {
            await _notificationService.SendToUserAsync(
                wp.SubmittedByUserId,
                $"❌ АВР отклонён на шаге «{currentStep.StepName}»",
                $"АВР по контракту {wp.Contract.ContractNumber} отклонён.\nПричина: {reason}\nВозвращён на шаг «{rejectToStep.StepName}»",
                NotificationType.ProgressUpdate,
                NotificationPriority.High,
                NotificationChannel.InApp,
                "WorkProgress", workProgressId,
                $"/WorkProgressReports/Details/{workProgressId}");
        }
    }

    public async Task<bool> CanUserActOnCurrentStepAsync(int workProgressId, string userId)
    {
        var wp = await _context.WorkProgresses.FindAsync(workProgressId);
        if (wp?.CurrentStepOrder == null) return false;

        var step = await _context.WorkflowSteps
            .FirstOrDefaultAsync(s => s.WorkflowType == "AVR" 
                                   && s.StepOrder == wp.CurrentStepOrder 
                                   && s.IsActive);
        if (step == null) return false;

        // Check if user has the required role
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return false;

        var userRoles = await _userManager.GetRolesAsync(user);
        return userRoles.Any(r => r.Equals(step.RoleId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<WorkflowStepInfo?> GetCurrentStepInfoAsync(int workProgressId)
    {
        var wp = await _context.WorkProgresses.FindAsync(workProgressId);
        if (wp == null) return null;

        var steps = await _context.WorkflowSteps
            .Where(s => s.WorkflowType == "AVR" && s.IsActive)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();

        if (!steps.Any()) return null;

        var isCompleted = wp.ApprovalStatus == AvrApprovalStatus.DirectorApproved;
        var currentStep = steps.FirstOrDefault(s => s.StepOrder == wp.CurrentStepOrder);

        return new WorkflowStepInfo
        {
            StepOrder = currentStep?.StepOrder ?? 0,
            StepName = currentStep?.StepName ?? (isCompleted ? "Завершён" : "Не начат"),
            ActionType = currentStep?.ActionType ?? "",
            RoleName = currentStep?.RoleId ?? "",
            TotalSteps = steps.Count,
            IsCompleted = isCompleted,
            CanReject = currentStep?.CanReject ?? false
        };
    }

    #region Private Helpers

    private async Task CreateTaskForStepAsync(WorkProgress wp, WorkflowStep step, string creatorUserId, string? customDescription = null)
    {
        // Find users with the required role
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == step.RoleId || r.Id == step.RoleId);
        if (role == null)
        {
            _logger.LogWarning("Role '{Role}' not found for workflow step {StepName}", step.RoleId, step.StepName);
            return;
        }

        var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name ?? step.RoleId);
        if (!usersInRole.Any())
        {
            _logger.LogWarning("No users in role '{Role}' for workflow step {StepName}", step.RoleId, step.StepName);
            return;
        }

        var actionLabel = step.ActionType switch
        {
            "Create" => "Создать",
            "Review" => "Проверить",
            "Approve" => "Утвердить",
            "FinalApprove" => "Финально утвердить",
            _ => step.StepName
        };

        var description = customDescription ?? 
            $"Контракт: {wp.Contract.ContractNumber} — {wp.Contract.Contractor.Name}\n" +
            $"Дата отчёта: {wp.ReportDate:dd.MM.yyyy}\n" +
            $"Шаг: {step.StepName} (этап {step.StepOrder})";

        // Create task for each user in the role
        foreach (var user in usersInRole)
        {
            try
            {
                await _taskService.CreateAsync(new ProjectTask
                {
                    Title = $"{actionLabel} АВР — {wp.Contract.ContractNumber}",
                    Description = description,
                    Status = ProjectTaskStatus.New,
                    Priority = TaskPriority.High,
                    DueDate = DateTime.UtcNow.AddDays(3),
                    AssigneeId = user.Id,
                    AssignedById = creatorUserId,
                    ContractId = wp.ContractId,
                    WorkProgressId = wp.Id,
                    ProjectId = wp.Contract.ProjectId
                }, creatorUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create task for user {UserId} on step {StepName}", user.Id, step.StepName);
            }
        }
    }

    private async Task SendNotificationForStepAsync(WorkProgress wp, WorkflowStep step)
    {
        var actionLabel = step.ActionType switch
        {
            "Review" => "Требуется проверка",
            "Approve" => "Требуется утверждение",
            "FinalApprove" => "Требуется финальное утверждение",
            _ => step.StepName
        };

        try
        {
            var roleName = step.RoleId;
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == step.RoleId);
            if (role?.Name != null) roleName = role.Name;

            await _notificationService.SendToRoleAsync(
                roleName,
                $"📋 {actionLabel}: АВР {wp.Contract.ContractNumber}",
                $"АВР по контракту {wp.Contract.ContractNumber} ({wp.Contract.Contractor.Name}) ожидает действия на шаге «{step.StepName}»",
                NotificationType.NewWorkAct,
                NotificationPriority.High);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification for step {StepName}", step.StepName);
        }
    }

    #endregion
}
