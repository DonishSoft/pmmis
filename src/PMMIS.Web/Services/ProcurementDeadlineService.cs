using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;

namespace PMMIS.Web.Services;

/// <summary>
/// Фоновый сервис: проверяет сроки плана закупок и создаёт задачи
/// для роли Procurement по пунктам, у которых приближается дата заключения контракта.
/// Запускается ежедневно в 08:00.
/// </summary>
public class ProcurementDeadlineService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProcurementDeadlineService> _logger;

    public ProcurementDeadlineService(
        IServiceScopeFactory scopeFactory,
        ILogger<ProcurementDeadlineService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Calculate delay until next 08:00
            var now = DateTime.UtcNow.AddHours(5); // Tajikistan UTC+5
            var nextRun = now.Date.AddHours(8);
            if (now >= nextRun) nextRun = nextRun.AddDays(1);
            var delay = nextRun - now;

            _logger.LogInformation("ProcurementDeadlineService: next run at {NextRun} (in {Delay})", nextRun, delay);
            await Task.Delay(delay, stoppingToken);

            try
            {
                await CheckDeadlinesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProcurementDeadlineService: error checking deadlines");
            }
        }
    }

    private async Task CheckDeadlinesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var taskService = scope.ServiceProvider.GetRequiredService<ITaskService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var today = DateTime.UtcNow.Date;

        // Find procurement plans without contracts where PlannedContractSigningDate is within 30 days
        var upcomingPlans = await context.ProcurementPlans
            .Include(pp => pp.Project)
            .Where(pp => pp.ContractId == null
                      && pp.Status != ProcurementStatus.Cancelled
                      && pp.Status != ProcurementStatus.Completed
                      && pp.PlannedContractSigningDate != null
                      && pp.PlannedContractSigningDate.Value >= today
                      && pp.PlannedContractSigningDate.Value <= today.AddDays(30))
            .ToListAsync(ct);

        if (!upcomingPlans.Any())
        {
            _logger.LogInformation("ProcurementDeadlineService: no upcoming procurement deadlines");
            return;
        }

        // Get existing procurement tasks so we don't duplicate
        var planIds = upcomingPlans.Select(pp => pp.Id).ToList();
        var existingTasks = await context.ProjectTasks
            .Where(t => t.ProcurementPlanId != null
                     && planIds.Contains(t.ProcurementPlanId.Value)
                     && t.Status != ProjectTaskStatus.Completed
                     && t.Status != ProjectTaskStatus.Cancelled)
            .Select(t => t.ProcurementPlanId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var plansToNotify = upcomingPlans
            .Where(pp => !existingTasks.Contains(pp.Id))
            .ToList();

        if (!plansToNotify.Any())
        {
            _logger.LogInformation("ProcurementDeadlineService: all deadlines already have active tasks");
            return;
        }

        // Get users with Procurement role
        var procurementUsers = await userManager.GetUsersInRoleAsync("PROCUREMENT");
        if (!procurementUsers.Any())
        {
            _logger.LogWarning("ProcurementDeadlineService: no users with PROCUREMENT role");
            return;
        }

        // Find a system user for "assigned by" (first procurement user or first admin)
        var systemUserId = procurementUsers.First().Id;

        foreach (var plan in plansToNotify)
        {
            var daysUntil = (plan.PlannedContractSigningDate!.Value - today).Days;
            var dueDate = plan.PlannedContractSigningDate.Value;

            var title = $"📋 Заключить контракт: {plan.ReferenceNo}";
            var description = $"Позиция плана закупок: {plan.ReferenceNo}\n" +
                              $"Описание: {plan.Description}\n" +
                              $"Оценочная стоимость: ${plan.EstimatedAmount:N0}\n" +
                              $"Метод: {plan.Method}\n" +
                              $"Плановая дата подписания: {dueDate:dd.MM.yyyy}\n" +
                              $"Осталось дней: {daysUntil}";

            var priority = daysUntil <= 7 ? TaskPriority.Critical
                         : daysUntil <= 14 ? TaskPriority.High
                         : TaskPriority.Normal;

            foreach (var user in procurementUsers)
            {
                try
                {
                    await taskService.CreateAsync(new ProjectTask
                    {
                        Title = title,
                        Description = description,
                        Status = ProjectTaskStatus.New,
                        Priority = priority,
                        DueDate = dueDate,
                        AssigneeId = user.Id,
                        AssignedById = systemUserId,
                        ProcurementPlanId = plan.Id,
                        ProjectId = plan.ProjectId
                    }, systemUserId);

                    _logger.LogInformation(
                        "Created procurement deadline task for user {UserId}, plan {PlanRef}, due {DueDate}",
                        user.Id, plan.ReferenceNo, dueDate);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create task for user {UserId}, plan {PlanId}",
                        user.Id, plan.Id);
                }
            }

            // Also send notification
            try
            {
                var urgency = daysUntil <= 7 ? "⚠️ СРОЧНО! " : "";
                await notificationService.SendToRoleAsync(
                    "PROCUREMENT",
                    $"{urgency}📋 Срок контракта: {plan.ReferenceNo}",
                    $"Контракт по позиции {plan.ReferenceNo} ({plan.Description}) должен быть заключён до {dueDate:dd.MM.yyyy} (осталось {daysUntil} дн.)",
                    NotificationType.DeadlineApproaching,
                    daysUntil <= 7 ? NotificationPriority.Urgent : NotificationPriority.High);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification for plan {PlanId}", plan.Id);
            }
        }

        _logger.LogInformation("ProcurementDeadlineService: created tasks for {Count} procurement plans", plansToNotify.Count);
    }
}
