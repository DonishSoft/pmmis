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
    // ─── AVR Workflow ───
    Task StartWorkflowAsync(int workProgressId, string creatorUserId);
    Task AdvanceAsync(int workProgressId, string approverUserId);
    Task RejectAsync(int workProgressId, string rejectorUserId, string reason);
    Task<bool> CanUserActOnCurrentStepAsync(int workProgressId, string userId);
    Task<WorkflowStepInfo?> GetCurrentStepInfoAsync(int workProgressId);

    // ─── Payment Workflow ───
    Task StartPaymentWorkflowAsync(int paymentId, string creatorUserId);
    Task AdvancePaymentAsync(int paymentId, string approverUserId);
    Task RejectPaymentAsync(int paymentId, string rejectorUserId, string reason);
    Task<bool> CanUserActOnPaymentStepAsync(int paymentId, string userId);
    Task<WorkflowStepInfo?> GetPaymentStepInfoAsync(int paymentId);
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

        var ct = wp.Contract.Type;
        var steps = await _context.WorkflowSteps
            .Where(s => s.WorkflowType == "AVR" && s.ContractType == ct && s.IsActive)
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

        // Log history: AVR created
        await LogHistoryAsync(wp.Id, firstStep, WorkflowAction.Created, creatorUserId, "АВР создан");

        // Find the next step (step 2 = usually Review)
        var nextStep = steps.FirstOrDefault(s => s.StepOrder > firstStep.StepOrder);

        if (nextStep != null)
        {
            // Move to step 2 and create task + notification
            wp.CurrentStepOrder = nextStep.StepOrder;
            wp.ApprovalStatus = AvrApprovalStatus.SubmittedForReview;
            wp.SubmittedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Log history: submitted for next step
            await LogHistoryAsync(wp.Id, nextStep, WorkflowAction.Submitted, creatorUserId,
                $"Передан на шаг «{nextStep.StepName}»");

            await CreateTaskForStepAsync(wp, nextStep, creatorUserId);
            await SendNotificationForStepAsync(wp, nextStep);
        }
        else
        {
            // Only 1 step — auto-approve
            wp.CurrentStepOrder = firstStep.StepOrder;
            wp.ApprovalStatus = AvrApprovalStatus.DirectorApproved;
            await _context.SaveChangesAsync();

            await LogHistoryAsync(wp.Id, firstStep, WorkflowAction.FinalApproved, creatorUserId, "Автоматически утверждён (единственный шаг)");
        }
    }

    public async Task AdvanceAsync(int workProgressId, string approverUserId)
    {
        var wp = await _context.WorkProgresses
            .Include(w => w.Contract)
                .ThenInclude(c => c.Contractor)
            .FirstOrDefaultAsync(w => w.Id == workProgressId);

        if (wp == null || wp.CurrentStepOrder == null) return;

        var ct = wp.Contract.Type;
        var steps = await _context.WorkflowSteps
            .Where(s => s.WorkflowType == "AVR" && s.ContractType == ct && s.IsActive)
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
            // Log history: current step approved
            await LogHistoryAsync(wp.Id, currentStep, WorkflowAction.Approved, approverUserId,
                $"Утверждён на шаге «{currentStep.StepName}»");

            // Move to next step
            wp.CurrentStepOrder = nextStep.StepOrder;

            // Update approval status based on action type
            if (nextStep.ActionType == "Approve" || nextStep.ActionType == "FinalApprove")
                wp.ApprovalStatus = AvrApprovalStatus.ManagerApproved;

            await _context.SaveChangesAsync();

            // Log: submitted to next step
            await LogHistoryAsync(wp.Id, nextStep, WorkflowAction.Submitted, approverUserId,
                $"Передан на шаг «{nextStep.StepName}»");

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

            // Log history: final approval
            await LogHistoryAsync(wp.Id, currentStep, WorkflowAction.FinalApproved, approverUserId,
                "АВР полностью утверждён");

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

            // ──── CHAIN: AVR approved → trigger Payment workflow ────
            // Notify payment role to create a payment for this AVR
            var paymentSteps = await _context.WorkflowSteps
                .Where(s => s.WorkflowType == "Payment" && s.ContractType == ct && s.IsActive)
                .OrderBy(s => s.StepOrder)
                .ToListAsync();

            if (paymentSteps.Any())
            {
                var firstPaymentStep = paymentSteps.First();
                var paymentRoleName = firstPaymentStep.RoleId;
                var paymentRole = await _context.Roles.FirstOrDefaultAsync(r => r.Id == firstPaymentStep.RoleId);
                if (paymentRole?.Name != null) paymentRoleName = paymentRole.Name;

                // Create task for payment creation
                var paymentUsers = await _userManager.GetUsersInRoleAsync(paymentRoleName);
                foreach (var u in paymentUsers)
                {
                    try
                    {
                        await _taskService.CreateAsync(new ProjectTask
                        {
                            Title = $"Создать платёж — утверждённый АВР {wp.Contract.ContractNumber}",
                            Description = $"АВР #{wp.Id} по контракту {wp.Contract.ContractNumber} полностью утверждён.\n" +
                                          $"Подрядчик: {wp.Contract.Contractor.Name}\n" +
                                          (wp.EstimatedPaymentAmount.HasValue ? $"Ориентировочная сумма: {wp.EstimatedPaymentAmount:N2}\n" : "") +
                                          "Необходимо создать платёж.",
                            Status = ProjectTaskStatus.New,
                            Priority = TaskPriority.High,
                            DueDate = DateTime.UtcNow.AddDays(3),
                            AssigneeId = u.Id,
                            AssignedById = approverUserId,
                            ContractId = wp.ContractId,
                            WorkProgressId = wp.Id,
                            ProjectId = wp.Contract.ProjectId
                        }, approverUserId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to create payment task for user {UserId}", u.Id);
                    }
                }

                await _notificationService.SendToRoleAsync(
                    paymentRoleName,
                    $"💰 Создайте платёж: АВР {wp.Contract.ContractNumber} утверждён",
                    $"АВР #{wp.Id} по контракту {wp.Contract.ContractNumber} полностью утверждён. Необходимо создать платёж.",
                    NotificationType.NewWorkAct,
                    NotificationPriority.High);

                _logger.LogInformation("Payment creation task sent to role {Role} after AVR #{Id} approval", paymentRoleName, wp.Id);
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

        var ct = wp.Contract.Type;
        var steps = await _context.WorkflowSteps
            .Where(s => s.WorkflowType == "AVR" && s.ContractType == ct && s.IsActive)
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

        // Log history: rejected
        await LogHistoryAsync(wp.Id, currentStep, WorkflowAction.Rejected, rejectorUserId,
            $"Отклонён на шаге «{currentStep.StepName}». Причина: {reason}. Возвращён на шаг «{rejectToStep.StepName}»");

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
        var wp = await _context.WorkProgresses
            .Include(w => w.Contract)
            .FirstOrDefaultAsync(w => w.Id == workProgressId);
        if (wp?.CurrentStepOrder == null) return false;

        var step = await _context.WorkflowSteps
            .FirstOrDefaultAsync(s => s.WorkflowType == "AVR" 
                                   && s.ContractType == wp.Contract.Type
                                   && s.StepOrder == wp.CurrentStepOrder 
                                   && s.IsActive);
        if (step == null) return false;

        return await IsUserMatchingStepAsync(step, userId, wp.Contract);
    }

    public async Task<WorkflowStepInfo?> GetCurrentStepInfoAsync(int workProgressId)
    {
        var wp = await _context.WorkProgresses
            .Include(w => w.Contract)
            .FirstOrDefaultAsync(w => w.Id == workProgressId);
        if (wp == null) return null;

        var steps = await _context.WorkflowSteps
            .Where(s => s.WorkflowType == "AVR" && s.ContractType == wp.Contract.Type && s.IsActive)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();

        if (!steps.Any()) return null;

        var isCompleted = wp.ApprovalStatus == AvrApprovalStatus.DirectorApproved;
        var currentStep = steps.FirstOrDefault(s => s.StepOrder == wp.CurrentStepOrder);

        // Resolve display name for role
        var roleName = currentStep != null ? GetStepAssigneeLabel(currentStep) : "";
        if (currentStep?.AssigneeType == "Role" && !string.IsNullOrEmpty(currentStep.RoleId))
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == currentStep.RoleId || r.Name == currentStep.RoleId);
            if (role?.Name != null) roleName = role.Name;
        }

        return new WorkflowStepInfo
        {
            StepOrder = currentStep?.StepOrder ?? 0,
            StepName = currentStep?.StepName ?? (isCompleted ? "Завершён" : "Не начат"),
            ActionType = currentStep?.ActionType ?? "",
            RoleName = roleName,
            TotalSteps = steps.Count,
            IsCompleted = isCompleted,
            CanReject = currentStep?.CanReject ?? false
        };
    }

    #region Private Helpers

    private async Task CreateTaskForStepAsync(WorkProgress wp, WorkflowStep step, string creatorUserId, string? customDescription = null)
    {
        var users = await ResolveUsersForStepAsync(step, wp.Contract);
        if (!users.Any())
        {
            _logger.LogWarning("No users found for workflow step {StepName} (AssigneeType={AssigneeType})", step.StepName, step.AssigneeType);
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

        foreach (var user in users)
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
            if (step.AssigneeType == "ContractCurator" || step.AssigneeType == "ContractPM")
            {
                // Send directly to the contract person
                var userId = step.AssigneeType == "ContractCurator" ? wp.Contract.CuratorId : wp.Contract.ProjectManagerId;
                if (!string.IsNullOrEmpty(userId))
                {
                    var label = step.AssigneeType == "ContractCurator" ? "Куратор" : "Менеджер проекта";
                    await _notificationService.SendToUserAsync(
                        userId,
                        $"📋 {actionLabel}: АВР {wp.Contract.ContractNumber}",
                        $"АВР по контракту {wp.Contract.ContractNumber} ({wp.Contract.Contractor.Name}) ожидает действия ({label}) на шаге «{step.StepName}»",
                        NotificationType.NewWorkAct,
                        NotificationPriority.High,
                        NotificationChannel.InApp,
                        "WorkProgress", wp.Id,
                        $"/WorkProgressReports/Details/{wp.Id}");
                }
            }
            else
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification for step {StepName}", step.StepName);
        }
    }

    /// <summary>
    /// Записать событие в историю Workflow
    /// </summary>
    private async Task LogHistoryAsync(int workProgressId, WorkflowStep step, WorkflowAction action, string userId, string? notes = null, int? paymentId = null)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            var roleName = GetStepAssigneeLabel(step);
            if (step.AssigneeType == "Role")
            {
                var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == step.RoleId || r.Name == step.RoleId);
                if (role?.Name != null) roleName = role.Name;
            }

            var history = new WorkflowHistory
            {
                WorkProgressId = workProgressId,
                PaymentId = paymentId,
                StepOrder = step.StepOrder,
                StepName = step.StepName,
                Action = action,
                UserId = userId,
                UserName = user != null ? $"{user.LastName} {user.FirstName}" : "Системный пользователь",
                RoleName = roleName,
                DueDate = DateTime.UtcNow.AddDays(3),
                ActionDate = DateTime.UtcNow,
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            };

            _context.WorkflowHistories.Add(history);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log workflow history for WP#{WpId}/Payment#{PaymentId}, action: {Action}", workProgressId, paymentId, action);
        }
    }

    /// <summary>
    /// Resolve users for a workflow step based on AssigneeType
    /// </summary>
    private async Task<IList<ApplicationUser>> ResolveUsersForStepAsync(WorkflowStep step, Contract contract)
    {
        if (step.AssigneeType == "ContractCurator")
        {
            if (!string.IsNullOrEmpty(contract.CuratorId))
            {
                var curator = await _userManager.FindByIdAsync(contract.CuratorId);
                return curator != null ? new List<ApplicationUser> { curator } : new List<ApplicationUser>();
            }
            return new List<ApplicationUser>();
        }
        
        if (step.AssigneeType == "ContractPM")
        {
            if (!string.IsNullOrEmpty(contract.ProjectManagerId))
            {
                var pm = await _userManager.FindByIdAsync(contract.ProjectManagerId);
                return pm != null ? new List<ApplicationUser> { pm } : new List<ApplicationUser>();
            }
            return new List<ApplicationUser>();
        }

        // Default: Role-based
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == step.RoleId || r.Id == step.RoleId);
        if (role?.Name == null) return new List<ApplicationUser>();
        return (await _userManager.GetUsersInRoleAsync(role.Name)).ToList();
    }

    /// <summary>
    /// Check if a user matches the step's assignee (by role or contract person)
    /// </summary>
    private async Task<bool> IsUserMatchingStepAsync(WorkflowStep step, string userId, Contract contract)
    {
        if (step.AssigneeType == "ContractCurator")
            return contract.CuratorId == userId;
        if (step.AssigneeType == "ContractPM")
            return contract.ProjectManagerId == userId;

        // Role-based
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return false;
        var userRoles = await _userManager.GetRolesAsync(user);
        return userRoles.Any(r => r.Equals(step.RoleId, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetStepAssigneeLabel(WorkflowStep step)
    {
        return step.AssigneeType switch
        {
            "ContractCurator" => "Куратор контракта",
            "ContractPM" => "Менеджер проекта",
            _ => step.RoleId
        };
    }

    #endregion

    #region Payment Workflow

    public async Task StartPaymentWorkflowAsync(int paymentId, string creatorUserId)
    {
        var payment = await _context.Payments
            .Include(p => p.Contract)
                .ThenInclude(c => c.Contractor)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment == null) return;

        var steps = await _context.WorkflowSteps
            .Where(s => s.WorkflowType == "Payment" && s.ContractType == payment.Contract.Type && s.IsActive)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();

        if (!steps.Any())
        {
            _logger.LogWarning("No active Payment workflow steps. Skipping.");
            return;
        }

        var firstStep = steps.First();
        _logger.LogInformation("Starting Payment workflow for Payment#{Id}, step 1: {StepName}", paymentId, firstStep.StepName);

        // Log: payment created
        await LogHistoryAsync(0, firstStep, WorkflowAction.Created, creatorUserId, 
            $"Платёж #{paymentId} создан на сумму {payment.Amount:N2}", paymentId);

        var nextStep = steps.FirstOrDefault(s => s.StepOrder > firstStep.StepOrder);

        if (nextStep != null)
        {
            payment.CurrentStepOrder = nextStep.StepOrder;
            payment.CreatedByUserId = creatorUserId;
            await _context.SaveChangesAsync();

            await LogHistoryAsync(0, nextStep, WorkflowAction.Submitted, creatorUserId,
                $"Передан на шаг «{nextStep.StepName}»", paymentId);

            await CreatePaymentTaskForStepAsync(payment, nextStep, creatorUserId);
            await SendPaymentNotificationAsync(payment, nextStep);
        }
        else
        {
            // Only 1 step → auto-approve
            payment.CurrentStepOrder = firstStep.StepOrder;
            payment.Status = PaymentStatus.Approved;
            payment.ApprovedAt = DateTime.UtcNow;
            payment.ApprovedById = creatorUserId;
            await _context.SaveChangesAsync();

            await LogHistoryAsync(0, firstStep, WorkflowAction.FinalApproved, creatorUserId,
                "Автоматически утверждён (единственный шаг)", paymentId);
        }
    }

    public async Task AdvancePaymentAsync(int paymentId, string approverUserId)
    {
        var payment = await _context.Payments
            .Include(p => p.Contract)
                .ThenInclude(c => c.Contractor)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment == null || payment.CurrentStepOrder == null) return;

        var steps = await _context.WorkflowSteps
            .Where(s => s.WorkflowType == "Payment" && s.ContractType == payment.Contract.Type && s.IsActive)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();

        var currentStep = steps.FirstOrDefault(s => s.StepOrder == payment.CurrentStepOrder);
        if (currentStep == null) return;

        _logger.LogInformation("Advancing Payment workflow #{Id}: step {From} ({Name}) approved by {User}",
            paymentId, currentStep.StepOrder, currentStep.StepName, approverUserId);

        // Complete existing tasks
        await _taskService.CompleteRelatedTasksAsync(payment.ContractId, null, approverUserId);

        var nextStep = steps.FirstOrDefault(s => s.StepOrder > currentStep.StepOrder);

        if (nextStep != null)
        {
            await LogHistoryAsync(0, currentStep, WorkflowAction.Approved, approverUserId,
                $"Утверждён на шаге «{currentStep.StepName}»", paymentId);

            payment.CurrentStepOrder = nextStep.StepOrder;
            await _context.SaveChangesAsync();

            await LogHistoryAsync(0, nextStep, WorkflowAction.Submitted, approverUserId,
                $"Передан на шаг «{nextStep.StepName}»", paymentId);

            await CreatePaymentTaskForStepAsync(payment, nextStep, approverUserId);
            await SendPaymentNotificationAsync(payment, nextStep);

            // Notify creator
            if (!string.IsNullOrEmpty(payment.CreatedByUserId))
            {
                await _notificationService.SendToUserAsync(
                    payment.CreatedByUserId,
                    $"Платёж #{paymentId} утверждён на шаге «{currentStep.StepName}»",
                    $"Платёж по контракту {payment.Contract.ContractNumber} перешёл к «{nextStep.StepName}»",
                    NotificationType.ProgressUpdate,
                    NotificationPriority.Normal,
                    NotificationChannel.InApp,
                    "Payment", paymentId,
                    $"/Payments/Index?contractId={payment.ContractId}");
            }
        }
        else
        {
            // No more steps → Payment approved!
            payment.CurrentStepOrder = currentStep.StepOrder;
            payment.Status = PaymentStatus.Approved;
            payment.ApprovedAt = DateTime.UtcNow;
            payment.ApprovedById = approverUserId;
            await _context.SaveChangesAsync();

            await LogHistoryAsync(0, currentStep, WorkflowAction.FinalApproved, approverUserId,
                "Платёж полностью утверждён", paymentId);

            _logger.LogInformation("Payment workflow completed for Payment#{Id}.", paymentId);

            if (!string.IsNullOrEmpty(payment.CreatedByUserId))
            {
                await _notificationService.SendToUserAsync(
                    payment.CreatedByUserId,
                    "✅ Платёж полностью утверждён!",
                    $"Платёж #{paymentId} по контракту {payment.Contract.ContractNumber} прошёл все этапы утверждения.",
                    NotificationType.ProgressUpdate,
                    NotificationPriority.High,
                    NotificationChannel.InApp,
                    "Payment", paymentId,
                    $"/Payments/Index?contractId={payment.ContractId}");
            }
        }
    }

    public async Task RejectPaymentAsync(int paymentId, string rejectorUserId, string reason)
    {
        var payment = await _context.Payments
            .Include(p => p.Contract)
                .ThenInclude(c => c.Contractor)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment == null || payment.CurrentStepOrder == null) return;

        var steps = await _context.WorkflowSteps
            .Where(s => s.WorkflowType == "Payment" && s.ContractType == payment.Contract.Type && s.IsActive)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();

        var currentStep = steps.FirstOrDefault(s => s.StepOrder == payment.CurrentStepOrder);
        if (currentStep == null) return;

        var rejectToOrder = currentStep.RejectToStepOrder ?? 1;
        var rejectToStep = steps.FirstOrDefault(s => s.StepOrder == rejectToOrder) ?? steps.First();

        _logger.LogInformation("Rejecting Payment #{Id}: step {From} → step {To}, reason: {Reason}",
            paymentId, currentStep.StepOrder, rejectToStep.StepOrder, reason);

        await _taskService.CompleteRelatedTasksAsync(payment.ContractId, null, rejectorUserId);

        payment.Status = PaymentStatus.Rejected;
        payment.CurrentStepOrder = rejectToStep.StepOrder;
        payment.RejectionReason = reason;
        payment.RejectedAt = DateTime.UtcNow;
        payment.RejectedById = rejectorUserId;
        await _context.SaveChangesAsync();

        await LogHistoryAsync(0, currentStep, WorkflowAction.Rejected, rejectorUserId,
            $"Отклонён. Причина: {reason}. Возвращён на шаг «{rejectToStep.StepName}»", paymentId);

        await CreatePaymentTaskForStepAsync(payment, rejectToStep, rejectorUserId,
            $"Исправить платёж (отклонён на «{currentStep.StepName}»): {reason}");

        if (!string.IsNullOrEmpty(payment.CreatedByUserId))
        {
            await _notificationService.SendToUserAsync(
                payment.CreatedByUserId,
                $"❌ Платёж #{paymentId} отклонён",
                $"Платёж по контракту {payment.Contract.ContractNumber} отклонён.\nПричина: {reason}",
                NotificationType.ProgressUpdate,
                NotificationPriority.High,
                NotificationChannel.InApp,
                "Payment", paymentId,
                $"/Payments/Index?contractId={payment.ContractId}");
        }
    }

    public async Task<bool> CanUserActOnPaymentStepAsync(int paymentId, string userId)
    {
        var payment = await _context.Payments
            .Include(p => p.Contract)
            .FirstOrDefaultAsync(p => p.Id == paymentId);
        if (payment?.CurrentStepOrder == null) return false;

        var step = await _context.WorkflowSteps
            .FirstOrDefaultAsync(s => s.WorkflowType == "Payment" 
                                   && s.ContractType == payment.Contract.Type
                                   && s.StepOrder == payment.CurrentStepOrder 
                                   && s.IsActive);
        if (step == null) return false;

        return await IsUserMatchingStepAsync(step, userId, payment.Contract);
    }

    public async Task<WorkflowStepInfo?> GetPaymentStepInfoAsync(int paymentId)
    {
        var payment = await _context.Payments
            .Include(p => p.Contract)
            .FirstOrDefaultAsync(p => p.Id == paymentId);
        if (payment == null) return null;

        var steps = await _context.WorkflowSteps
            .Where(s => s.WorkflowType == "Payment" && s.ContractType == payment.Contract.Type && s.IsActive)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();

        if (!steps.Any()) return null;

        var isCompleted = payment.Status == PaymentStatus.Approved || payment.Status == PaymentStatus.Paid;
        var currentStep = steps.FirstOrDefault(s => s.StepOrder == payment.CurrentStepOrder);

        // Resolve display name for role
        var roleName = currentStep != null ? GetStepAssigneeLabel(currentStep) : "";
        if (currentStep?.AssigneeType == "Role" && !string.IsNullOrEmpty(currentStep.RoleId))
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == currentStep.RoleId || r.Name == currentStep.RoleId);
            if (role?.Name != null) roleName = role.Name;
        }

        return new WorkflowStepInfo
        {
            StepOrder = currentStep?.StepOrder ?? 0,
            StepName = currentStep?.StepName ?? (isCompleted ? "Завершён" : "Не начат"),
            ActionType = currentStep?.ActionType ?? "",
            RoleName = roleName,
            TotalSteps = steps.Count,
            IsCompleted = isCompleted,
            CanReject = currentStep?.CanReject ?? false
        };
    }

    private async Task CreatePaymentTaskForStepAsync(Payment payment, WorkflowStep step, string creatorUserId, string? customDescription = null)
    {
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == step.RoleId || r.Id == step.RoleId);
        if (role == null) return;

        var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name ?? step.RoleId);
        if (!usersInRole.Any()) return;

        var actionLabel = step.ActionType switch
        {
            "Create" => "Создать",
            "Review" => "Проверить",
            "Approve" => "Утвердить",
            "FinalApprove" => "Финально утвердить",
            _ => step.StepName
        };

        var description = customDescription ??
            $"Контракт: {payment.Contract.ContractNumber} — {payment.Contract.Contractor.Name}\n" +
            $"Сумма: {payment.Amount:N2}\n" +
            $"Шаг: {step.StepName} (этап {step.StepOrder})";

        foreach (var user in usersInRole)
        {
            try
            {
                await _taskService.CreateAsync(new ProjectTask
                {
                    Title = $"{actionLabel} платёж — {payment.Contract.ContractNumber}",
                    Description = description,
                    Status = ProjectTaskStatus.New,
                    Priority = TaskPriority.High,
                    DueDate = DateTime.UtcNow.AddDays(3),
                    AssigneeId = user.Id,
                    AssignedById = creatorUserId,
                    ContractId = payment.ContractId,
                    ProjectId = payment.Contract.ProjectId
                }, creatorUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create payment task for user {UserId}", user.Id);
            }
        }
    }

    private async Task SendPaymentNotificationAsync(Payment payment, WorkflowStep step)
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
                $"💰 {actionLabel}: Платёж {payment.Contract.ContractNumber}",
                $"Платёж по контракту {payment.Contract.ContractNumber} ({payment.Amount:N2}) ожидает действия на шаге «{step.StepName}»",
                NotificationType.NewWorkAct,
                NotificationPriority.High);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment notification for step {StepName}", step.StepName);
        }
    }

    #endregion
}
