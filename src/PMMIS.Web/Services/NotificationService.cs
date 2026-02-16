using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;

namespace PMMIS.Web.Services;

/// <summary>
/// Реализация сервиса уведомлений
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly ITelegramSender _telegramSender;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        ITelegramSender telegramSender,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _userManager = userManager;
        _emailSender = emailSender;
        _telegramSender = telegramSender;
        _logger = logger;
    }

    public async Task SendAsync(Notification notification, CancellationToken ct = default)
    {
        // Сохраняем уведомление в базу
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(ct);

        // Получаем настройки пользователя
        var settings = await GetUserSettingsAsync(notification.UserId);
        
        // Определяем каналы доставки
        var shouldSendEmail = (notification.Channel == NotificationChannel.Email || 
                               notification.Channel == NotificationChannel.All) && 
                               settings.EmailEnabled;
        
        var shouldSendTelegram = (notification.Channel == NotificationChannel.Telegram || 
                                   notification.Channel == NotificationChannel.All) && 
                                   settings.TelegramEnabled && 
                                   !string.IsNullOrEmpty(settings.TelegramChatId);

        // Проверяем тихие часы
        if (settings.QuietHoursEnabled && IsInQuietHours(settings))
        {
            notification.ScheduledAt = GetNextActiveTime(settings);
            _context.Notifications.Update(notification);
            await _context.SaveChangesAsync(ct);
            return;
        }

        // Отправляем Email
        if (shouldSendEmail)
        {
            await SendEmailNotificationAsync(notification, ct);
        }

        // Отправляем Telegram
        if (shouldSendTelegram && !string.IsNullOrEmpty(settings.TelegramChatId))
        {
            await SendTelegramNotificationAsync(notification, settings.TelegramChatId, ct);
        }
    }

    public async Task SendToUserAsync(
        string userId,
        string title,
        string message,
        NotificationType type,
        NotificationPriority priority = NotificationPriority.Normal,
        NotificationChannel channel = NotificationChannel.InApp,
        string? referenceType = null,
        int? referenceId = null,
        string? actionUrl = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            TitleTj = title,
            TitleEn = title,
            Message = message,
            MessageTj = message,
            MessageEn = message,
            Type = type,
            Priority = priority,
            Channel = channel,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            ActionUrl = actionUrl,
            CreatedAt = DateTime.UtcNow
        };

        await SendAsync(notification);
    }

    public async Task SendToRoleAsync(
        string roleName,
        string title,
        string message,
        NotificationType type,
        NotificationPriority priority = NotificationPriority.Normal)
    {
        var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);
        var userIds = usersInRole.Select(u => u.Id);
        
        await SendToUsersAsync(userIds, title, message, type, priority);
    }

    public async Task SendToUsersAsync(
        IEnumerable<string> userIds,
        string title,
        string message,
        NotificationType type,
        NotificationPriority priority = NotificationPriority.Normal)
    {
        var notifications = userIds.Select(userId => new Notification
        {
            UserId = userId,
            Title = title,
            TitleTj = title,
            TitleEn = title,
            Message = message,
            MessageTj = message,
            MessageEn = message,
            Type = type,
            Priority = priority,
            Channel = NotificationChannel.InApp,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        _context.Notifications.AddRange(notifications);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Sent {Count} notifications of type {Type}", notifications.Count, type);
    }

    public async Task MarkAsReadAsync(int notificationId, string userId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
        
        if (notification != null)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        var notifications = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<List<Notification>> GetUnreadAsync(string userId, int take = 20)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task<(List<Notification> Items, int TotalCount)> GetUserNotificationsAsync(
        string userId,
        int page = 1,
        int pageSize = 20,
        bool unreadOnly = false)
    {
        var query = _context.Notifications
            .Where(n => n.UserId == userId);

        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task DeleteAsync(int notificationId, string userId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
        
        if (notification != null)
        {
            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<UserNotificationSettings> GetUserSettingsAsync(string userId)
    {
        var settings = await _context.UserNotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (settings == null)
        {
            settings = new UserNotificationSettings
            {
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            _context.UserNotificationSettings.Add(settings);
            await _context.SaveChangesAsync();
        }

        return settings;
    }

    public async Task SaveUserSettingsAsync(UserNotificationSettings settings)
    {
        var existing = await _context.UserNotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == settings.UserId);

        if (existing != null)
        {
            existing.InAppEnabled = settings.InAppEnabled;
            existing.EmailEnabled = settings.EmailEnabled;
            existing.TelegramEnabled = settings.TelegramEnabled;
            existing.TelegramChatId = settings.TelegramChatId;
            existing.TaskNotifications = settings.TaskNotifications;
            existing.DeadlineNotifications = settings.DeadlineNotifications;
            existing.PaymentNotifications = settings.PaymentNotifications;
            existing.SystemNotifications = settings.SystemNotifications;
            existing.DailyDigest = settings.DailyDigest;
            existing.DigestTime = settings.DigestTime;
            existing.QuietHoursEnabled = settings.QuietHoursEnabled;
            existing.QuietHoursStart = settings.QuietHoursStart;
            existing.QuietHoursEnd = settings.QuietHoursEnd;
            existing.DeadlineWarningDays = settings.DeadlineWarningDays;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            settings.CreatedAt = DateTime.UtcNow;
            _context.UserNotificationSettings.Add(settings);
        }

        await _context.SaveChangesAsync();
    }

    #region Private Methods

    private async Task SendEmailNotificationAsync(Notification notification, CancellationToken ct)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(notification.UserId);
            if (user?.Email == null) return;

            var subject = notification.Title;
            var body = BuildEmailBody(notification);

            var sent = await _emailSender.SendAsync(user.Email, subject, body, true, ct);
            
            if (sent)
            {
                notification.EmailSent = true;
                notification.EmailSentAt = DateTime.UtcNow;
                _context.Notifications.Update(notification);
                await _context.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email notification {Id}", notification.Id);
            notification.RetryCount++;
            notification.LastError = ex.Message;
            _context.Notifications.Update(notification);
            await _context.SaveChangesAsync(ct);
        }
    }

    private async Task SendTelegramNotificationAsync(Notification notification, string chatId, CancellationToken ct)
    {
        try
        {
            var message = $"📢 *{notification.Title}*\n\n{notification.Message}";
            
            var sent = await _telegramSender.SendAsync(chatId, message, ct);
            
            if (sent)
            {
                notification.TelegramSent = true;
                notification.TelegramSentAt = DateTime.UtcNow;
                _context.Notifications.Update(notification);
                await _context.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram notification {Id}", notification.Id);
            notification.RetryCount++;
            notification.LastError = ex.Message;
            _context.Notifications.Update(notification);
            await _context.SaveChangesAsync(ct);
        }
    }

    private string BuildEmailBody(Notification notification)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #1a5f7a; color: white; padding: 20px; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 20px; border: 1px solid #ddd; }}
        .footer {{ font-size: 12px; color: #666; padding: 10px; text-align: center; }}
        .btn {{ display: inline-block; padding: 10px 20px; background: #1a5f7a; color: white; text-decoration: none; border-radius: 4px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>{notification.Title}</h2>
        </div>
        <div class='content'>
            <p>{notification.Message}</p>
            {(notification.ActionUrl != null ? $"<p><a class='btn' href='{notification.ActionUrl}'>Открыть в системе</a></p>" : "")}
        </div>
        <div class='footer'>
            <p>PMMIS — Система управления проектами</p>
        </div>
    </div>
</body>
</html>";
    }

    private bool IsInQuietHours(UserNotificationSettings settings)
    {
        if (!settings.QuietHoursEnabled || 
            settings.QuietHoursStart == null || 
            settings.QuietHoursEnd == null)
            return false;

        var now = DateTime.Now.TimeOfDay;
        var start = settings.QuietHoursStart.Value;
        var end = settings.QuietHoursEnd.Value;

        if (start <= end)
            return now >= start && now <= end;
        else
            return now >= start || now <= end;
    }

    private DateTime GetNextActiveTime(UserNotificationSettings settings)
    {
        var now = DateTime.Now;
        var end = settings.QuietHoursEnd ?? TimeSpan.FromHours(8);
        
        var nextActive = now.Date.Add(end);
        if (nextActive <= now)
            nextActive = nextActive.AddDays(1);
        
        return DateTime.SpecifyKind(nextActive, DateTimeKind.Utc);
    }

    #endregion
}
