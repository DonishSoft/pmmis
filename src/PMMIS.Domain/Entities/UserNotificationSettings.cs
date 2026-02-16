namespace PMMIS.Domain.Entities;

/// <summary>
/// Настройки уведомлений пользователя
/// </summary>
public class UserNotificationSettings : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    
    // Каналы доставки
    public bool InAppEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; } = true;
    public bool TelegramEnabled { get; set; } = false;
    public string? TelegramChatId { get; set; }
    
    // Типы уведомлений
    public bool TaskNotifications { get; set; } = true;
    public bool DeadlineNotifications { get; set; } = true;
    public bool PaymentNotifications { get; set; } = true;
    public bool SystemNotifications { get; set; } = true;
    
    // Дайджест
    public bool DailyDigest { get; set; } = false;
    public TimeSpan? DigestTime { get; set; }  // Время отправки дайджеста
    
    // Тихие часы
    public bool QuietHoursEnabled { get; set; } = false;
    public TimeSpan? QuietHoursStart { get; set; }
    public TimeSpan? QuietHoursEnd { get; set; }
    
    // Настройки дедлайнов
    public int DeadlineWarningDays { get; set; } = 3;  // За сколько дней предупреждать
}
