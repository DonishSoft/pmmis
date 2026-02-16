namespace PMMIS.Domain.Entities;

/// <summary>
/// Уведомление
/// </summary>
public class Notification : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string TitleTj { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    
    public string Message { get; set; } = string.Empty;
    public string MessageTj { get; set; } = string.Empty;
    public string MessageEn { get; set; } = string.Empty;
    
    public NotificationType Type { get; set; }
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;
    
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    
    // Target user
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    
    // Reference to related entity
    public string? ReferenceType { get; set; } // "Contract", "Payment", "Task", etc.
    public int? ReferenceId { get; set; }
    public string? ActionUrl { get; set; }  // URL для перехода при клике
    
    // Multi-channel delivery status
    public bool EmailSent { get; set; }
    public DateTime? EmailSentAt { get; set; }
    public bool TelegramSent { get; set; }
    public DateTime? TelegramSentAt { get; set; }
    
    // Scheduling
    public DateTime? ScheduledAt { get; set; }  // Для отложенных уведомлений
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    
    public string GetTitle(string language) => language switch
    {
        "tj" => string.IsNullOrEmpty(TitleTj) ? Title : TitleTj,
        "en" => string.IsNullOrEmpty(TitleEn) ? Title : TitleEn,
        _ => Title
    };
    
    public string GetMessage(string language) => language switch
    {
        "tj" => string.IsNullOrEmpty(MessageTj) ? Message : MessageTj,
        "en" => string.IsNullOrEmpty(MessageEn) ? Message : MessageEn,
        _ => Message
    };
}

/// <summary>
/// Тип уведомления
/// </summary>
public enum NotificationType
{
    // Задачи
    TaskAssigned,           // Назначена задача
    TaskStatusChanged,      // Изменён статус задачи
    TaskCommented,          // Новый комментарий
    TaskExtensionRequested, // Запрошено продление срока
    TaskExtensionApproved,  // Продление одобрено
    TaskExtensionRejected,  // Продление отклонено
    
    // Дедлайны
    DeadlineApproaching,    // Приближающийся дедлайн (24/48/72ч)
    DeadlineOverdue,        // Просроченный дедлайн
    
    // Контракты и платежи
    NewWorkAct,             // Новый акт от подрядчика
    PaymentPending,         // Ожидающий платёж
    PaymentApproved,        // Платёж одобрен
    ContractExpiring,       // Контракт истекает
    
    // Прочее
    ProgressUpdate,         // Обновление прогресса
    SystemMessage,          // Системное сообщение
    Mention                 // Упоминание в комментарии
}

/// <summary>
/// Приоритет уведомления
/// </summary>
public enum NotificationPriority
{
    Low,
    Normal,
    High,
    Urgent
}

/// <summary>
/// Канал доставки уведомления
/// </summary>
public enum NotificationChannel
{
    InApp,      // Только внутри системы
    Email,      // Email
    Telegram,   // Telegram
    All         // Все каналы
}
