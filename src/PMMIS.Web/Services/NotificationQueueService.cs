using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;

namespace PMMIS.Web.Services;

/// <summary>
/// Background service that processes pending Email and Telegram notifications.
/// Runs every 5 minutes.
/// </summary>
public class NotificationQueueService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationQueueService> _logger;
    private readonly TimeSpan _processInterval = TimeSpan.FromMinutes(5);

    public NotificationQueueService(
        IServiceProvider serviceProvider,
        ILogger<NotificationQueueService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NotificationQueueService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueueAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing notification queue");
            }

            await Task.Delay(_processInterval, stoppingToken);
        }
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var telegramSender = scope.ServiceProvider.GetRequiredService<ITelegramSender>();

        // Get pending notifications (not yet sent via Email or Telegram)
        var pendingNotifications = await context.Notifications
            .Include(n => n.User)
            .Where(n => 
                ((n.Channel == NotificationChannel.Email || n.Channel == NotificationChannel.All) && !n.EmailSent) ||
                ((n.Channel == NotificationChannel.Telegram || n.Channel == NotificationChannel.All) && !n.TelegramSent))
            .Where(n => n.ScheduledAt == null || n.ScheduledAt <= DateTime.UtcNow) // Only process scheduled or immediate
            .Take(50) // Process in batches
            .ToListAsync(cancellationToken);

        if (!pendingNotifications.Any())
            return;

        _logger.LogInformation("Processing {Count} pending notifications", pendingNotifications.Count);

        foreach (var notification in pendingNotifications)
        {
            // Process Email
            if ((notification.Channel == NotificationChannel.Email || notification.Channel == NotificationChannel.All) &&
                !notification.EmailSent)
            {
                await ProcessEmailAsync(notification, emailSender, context);
            }

            // Process Telegram
            if ((notification.Channel == NotificationChannel.Telegram || notification.Channel == NotificationChannel.All) &&
                !notification.TelegramSent)
            {
                await ProcessTelegramAsync(notification, telegramSender, context);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Notification queue processing completed");
    }

    private async Task ProcessEmailAsync(Notification notification, IEmailSender emailSender, ApplicationDbContext context)
    {
        if (string.IsNullOrEmpty(notification.User?.Email))
        {
            notification.LastError = "No email address";
            return;
        }

        try
        {
            // Check user notification settings
            var settings = await context.UserNotificationSettings
                .FirstOrDefaultAsync(s => s.UserId == notification.UserId);

            if (settings?.EmailEnabled != true)
            {
                // Skip email but mark as "sent" to avoid re-processing
                notification.EmailSent = true;
                return;
            }

            await emailSender.SendAsync(
                notification.User.Email,
                notification.Title,
                BuildEmailBody(notification),
                true);

            notification.EmailSent = true;
            notification.EmailSentAt = DateTime.UtcNow;
            _logger.LogInformation("Email sent for notification {NotificationId}", notification.Id);
        }
        catch (Exception ex)
        {
            notification.LastError = ex.Message;
            notification.RetryCount++;
            _logger.LogWarning(ex, "Failed to send email for notification {NotificationId}", notification.Id);
        }
    }

    private async Task ProcessTelegramAsync(Notification notification, ITelegramSender telegramSender, ApplicationDbContext context)
    {
        // Get user's Telegram chat ID from settings
        var settings = await context.UserNotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == notification.UserId);

        if (string.IsNullOrEmpty(settings?.TelegramChatId))
        {
            // No Telegram ID, skip
            notification.TelegramSent = true;
            return;
        }

        if (!settings.TelegramEnabled)
        {
            notification.TelegramSent = true;
            return;
        }

        try
        {
            var message = $"*{notification.Title}*\n\n{notification.Message}";
            if (!string.IsNullOrEmpty(notification.ActionUrl))
            {
                message += $"\n\n[Перейти]({notification.ActionUrl})";
            }

            await telegramSender.SendAsync(settings.TelegramChatId, message);

            notification.TelegramSent = true;
            notification.TelegramSentAt = DateTime.UtcNow;
            _logger.LogInformation("Telegram message sent for notification {NotificationId}", notification.Id);
        }
        catch (Exception ex)
        {
            notification.LastError = ex.Message;
            notification.RetryCount++;
            _logger.LogWarning(ex, "Failed to send Telegram message for notification {NotificationId}", notification.Id);
        }
    }

    private static string BuildEmailBody(Notification notification)
    {
        var body = $"<h2>{notification.Title}</h2><p>{notification.Message}</p>";
        if (!string.IsNullOrEmpty(notification.ActionUrl))
        {
            body += $"<p><a href=\"{notification.ActionUrl}\">Перейти к задаче</a></p>";
        }
        return body;
    }
}
