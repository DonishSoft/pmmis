using PMMIS.Domain.Entities;

namespace PMMIS.Web.Services;

/// <summary>
/// Сервис уведомлений
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Отправить уведомление
    /// </summary>
    Task SendAsync(Notification notification, CancellationToken ct = default);
    
    /// <summary>
    /// Отправить уведомление пользователю
    /// </summary>
    Task SendToUserAsync(
        string userId, 
        string title, 
        string message,
        NotificationType type,
        NotificationPriority priority = NotificationPriority.Normal,
        NotificationChannel channel = NotificationChannel.InApp,
        string? referenceType = null,
        int? referenceId = null,
        string? actionUrl = null);
    
    /// <summary>
    /// Отправить уведомление всем пользователям с ролью
    /// </summary>
    Task SendToRoleAsync(
        string roleName,
        string title,
        string message,
        NotificationType type,
        NotificationPriority priority = NotificationPriority.Normal);
    
    /// <summary>
    /// Отправить уведомление нескольким пользователям
    /// </summary>
    Task SendToUsersAsync(
        IEnumerable<string> userIds,
        string title,
        string message,
        NotificationType type,
        NotificationPriority priority = NotificationPriority.Normal);
    
    /// <summary>
    /// Пометить уведомление как прочитанное
    /// </summary>
    Task MarkAsReadAsync(int notificationId, string userId);
    
    /// <summary>
    /// Пометить все уведомления пользователя как прочитанные
    /// </summary>
    Task MarkAllAsReadAsync(string userId);
    
    /// <summary>
    /// Получить непрочитанные уведомления пользователя
    /// </summary>
    Task<List<Notification>> GetUnreadAsync(string userId, int take = 20);
    
    /// <summary>
    /// Получить количество непрочитанных уведомлений
    /// </summary>
    Task<int> GetUnreadCountAsync(string userId);
    
    /// <summary>
    /// Получить уведомления пользователя с пагинацией
    /// </summary>
    Task<(List<Notification> Items, int TotalCount)> GetUserNotificationsAsync(
        string userId, 
        int page = 1, 
        int pageSize = 20,
        bool unreadOnly = false);
    
    /// <summary>
    /// Удалить уведомление
    /// </summary>
    Task DeleteAsync(int notificationId, string userId);
    
    /// <summary>
    /// Получить настройки уведомлений пользователя
    /// </summary>
    Task<UserNotificationSettings> GetUserSettingsAsync(string userId);
    
    /// <summary>
    /// Сохранить настройки уведомлений пользователя
    /// </summary>
    Task SaveUserSettingsAsync(UserNotificationSettings settings);
}
