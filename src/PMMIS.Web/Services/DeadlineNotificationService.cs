using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;

namespace PMMIS.Web.Services;

/// <summary>
/// Background service that checks for approaching task deadlines and sends notifications.
/// Runs every hour.
/// </summary>
public class DeadlineNotificationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DeadlineNotificationService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

    public DeadlineNotificationService(
        IServiceProvider serviceProvider,
        ILogger<DeadlineNotificationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DeadlineNotificationService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckDeadlinesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking task deadlines");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CheckDeadlinesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTime.UtcNow;
        var warningThreshold = now.AddDays(3); // 3 days before deadline

        // Find tasks with approaching deadlines that haven't been notified
        var tasksNeedingNotification = await context.ProjectTasks
            .Include(t => t.Assignee)
            .Where(t => t.Status != ProjectTaskStatus.Completed &&
                        t.Status != ProjectTaskStatus.Cancelled &&
                        t.DueDate <= warningThreshold &&
                        t.DueDate >= now)
            .ToListAsync(cancellationToken);

        foreach (var task in tasksNeedingNotification)
        {
            // Check if we already sent a notification today
            var alreadyNotified = await context.Notifications
                .AnyAsync(n => n.UserId == task.AssigneeId &&
                              n.ReferenceType == "ProjectTask" &&
                              n.ReferenceId == task.Id &&
                              n.Type == NotificationType.DeadlineApproaching &&
                              n.CreatedAt.Date == now.Date,
                    cancellationToken);

            if (alreadyNotified)
                continue;

            var daysUntilDue = (task.DueDate.Date - now.Date).Days;
            var message = daysUntilDue switch
            {
                0 => $"Задача \"{task.Title}\" должна быть выполнена сегодня!",
                1 => $"Задача \"{task.Title}\" должна быть выполнена завтра.",
                _ => $"До дедлайна задачи \"{task.Title}\" осталось {daysUntilDue} дн."
            };

            var notification = new Notification
            {
                UserId = task.AssigneeId,
                Title = daysUntilDue == 0 ? "⚠️ Дедлайн сегодня!" : "⏰ Напоминание о дедлайне",
                Message = message,
                Type = NotificationType.DeadlineApproaching,
                Priority = daysUntilDue == 0 ? NotificationPriority.High : NotificationPriority.Normal,
                Channel = NotificationChannel.All,
                ReferenceType = "ProjectTask",
                ReferenceId = task.Id,
                ActionUrl = $"/Tasks/Details/{task.Id}",
                CreatedAt = DateTime.UtcNow
            };

            context.Notifications.Add(notification);
            _logger.LogInformation("Created deadline notification for task {TaskId} to user {UserId}", task.Id, task.AssigneeId);
        }

        // Check for overdue tasks
        var overdueTasks = await context.ProjectTasks
            .Include(t => t.Assignee)
            .Include(t => t.AssignedBy)
            .Where(t => t.Status != ProjectTaskStatus.Completed &&
                        t.Status != ProjectTaskStatus.Cancelled &&
                        t.DueDate < now)
            .ToListAsync(cancellationToken);

        foreach (var task in overdueTasks)
        {
            // Notify assignee about overdue
            var assigneeNotified = await context.Notifications
                .AnyAsync(n => n.UserId == task.AssigneeId &&
                              n.ReferenceType == "ProjectTask" &&
                              n.ReferenceId == task.Id &&
                              n.Type == NotificationType.DeadlineOverdue &&
                              n.CreatedAt.Date == now.Date,
                    cancellationToken);

            if (!assigneeNotified)
            {
                context.Notifications.Add(new Notification
                {
                    UserId = task.AssigneeId,
                    Title = "🚨 Задача просрочена!",
                    Message = $"Задача \"{task.Title}\" просрочена на {(now.Date - task.DueDate.Date).Days} дн.",
                    Type = NotificationType.DeadlineOverdue,
                    Priority = NotificationPriority.Urgent,
                    Channel = NotificationChannel.All,
                    ReferenceType = "ProjectTask",
                    ReferenceId = task.Id,
                    ActionUrl = $"/Tasks/Details/{task.Id}",
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Notify manager (AssignedBy) about overdue
            if (task.AssignedById != task.AssigneeId)
            {
                var managerNotified = await context.Notifications
                    .AnyAsync(n => n.UserId == task.AssignedById &&
                                  n.ReferenceType == "ProjectTask" &&
                                  n.ReferenceId == task.Id &&
                                  n.Type == NotificationType.DeadlineOverdue &&
                                  n.CreatedAt.Date == now.Date,
                        cancellationToken);

                if (!managerNotified)
                {
                    context.Notifications.Add(new Notification
                    {
                        UserId = task.AssignedById,
                        Title = "⚠️ Задача под вашим контролем просрочена",
                        Message = $"Задача \"{task.Title}\" (исп. {task.Assignee?.FullName}) просрочена.",
                        Type = NotificationType.DeadlineOverdue,
                        Priority = NotificationPriority.High,
                        Channel = NotificationChannel.Email,
                        ReferenceType = "ProjectTask",
                        ReferenceId = task.Id,
                        ActionUrl = $"/Tasks/Details/{task.Id}",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        // === CHECK OVERDUE MILESTONES AND CREATE TASKS ===
        var milestoneTasksCreated = await CheckMilestoneDeadlinesAsync(context, now, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deadline check completed. {UpcomingCount} upcoming, {OverdueCount} overdue tasks, {MilestoneCount} milestone tasks created.",
            tasksNeedingNotification.Count, overdueTasks.Count, milestoneTasksCreated);
    }

    /// <summary>
    /// Проверяет просроченные этапы (milestones) и создаёт задачи для Куратора и Менеджера проекта
    /// </summary>
    private async Task<int> CheckMilestoneDeadlinesAsync(ApplicationDbContext context, DateTime now, CancellationToken cancellationToken)
    {
        var tasksCreated = 0;

        // Find milestones that are past due and not completed
        var overdueMilestones = await context.Set<ContractMilestone>()
            .Include(m => m.Contract)
                .ThenInclude(c => c.Curator)
            .Include(m => m.Contract)
                .ThenInclude(c => c.ProjectManager)
            .Where(m => m.DueDate < now &&
                        m.Status != MilestoneStatus.Completed)
            .ToListAsync(cancellationToken);

        foreach (var milestone in overdueMilestones)
        {
            // Update milestone status to Overdue if still Pending/InProgress
            if (milestone.Status != MilestoneStatus.Overdue)
            {
                milestone.Status = MilestoneStatus.Overdue;
            }

            // Check if tasks already exist for this milestone (via MilestoneId FK)
            var existingTasks = await context.ProjectTasks
                .Where(t => t.MilestoneId == milestone.Id)
                .ToListAsync(cancellationToken);

            if (existingTasks.Any())
                continue; // Tasks already created, skip

            var contract = milestone.Contract;
            var overdueDays = (now.Date - milestone.DueDate.Date).Days;
            var taskTitle = $"Просрочен этап: {milestone.Title} (контракт {contract.ContractNumber})";
            var taskDescription = $"Промежуточный этап \"{milestone.Title}\" по контракту {contract.ContractNumber} " +
                                  $"просрочен на {overdueDays} дн. Срок был: {milestone.DueDate:dd.MM.yyyy}";

            // Create task for Curator
            if (!string.IsNullOrEmpty(contract.CuratorId))
            {
                context.ProjectTasks.Add(new ProjectTask
                {
                    Title = taskTitle,
                    Description = taskDescription,
                    Status = ProjectTaskStatus.New,
                    Priority = TaskPriority.Critical,
                    DueDate = milestone.DueDate,
                    AssigneeId = contract.CuratorId,
                    AssignedById = contract.CuratorId, // Auto-assigned by system
                    ContractId = contract.Id,
                    MilestoneId = milestone.Id,
                    ProjectId = contract.ProjectId,
                    CreatedAt = DateTime.UtcNow
                });
                tasksCreated++;

                _logger.LogInformation(
                    "Created milestone overdue task for Curator {CuratorId}, milestone '{MilestoneTitle}', contract {ContractNumber}",
                    contract.CuratorId, milestone.Title, contract.ContractNumber);
            }

            // Create task for Project Manager
            if (!string.IsNullOrEmpty(contract.ProjectManagerId) && contract.ProjectManagerId != contract.CuratorId)
            {
                context.ProjectTasks.Add(new ProjectTask
                {
                    Title = taskTitle,
                    Description = taskDescription,
                    Status = ProjectTaskStatus.New,
                    Priority = TaskPriority.Critical,
                    DueDate = milestone.DueDate,
                    AssigneeId = contract.ProjectManagerId,
                    AssignedById = contract.ProjectManagerId, // Auto-assigned by system
                    ContractId = contract.Id,
                    MilestoneId = milestone.Id,
                    ProjectId = contract.ProjectId,
                    CreatedAt = DateTime.UtcNow
                });
                tasksCreated++;

                _logger.LogInformation(
                    "Created milestone overdue task for PM {PmId}, milestone '{MilestoneTitle}', contract {ContractNumber}",
                    contract.ProjectManagerId, milestone.Title, contract.ContractNumber);
            }
        }

        return tasksCreated;
    }
}
