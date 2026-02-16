using System.ComponentModel.DataAnnotations;

namespace PMMIS.Domain.Entities;

/// <summary>
/// Запрос на продление срока задачи
/// </summary>
public class TaskExtensionRequest : BaseEntity
{
    public int TaskId { get; set; }
    public ProjectTask? Task { get; set; }
    
    [Required]
    public string RequestedById { get; set; } = string.Empty;
    public ApplicationUser? RequestedBy { get; set; }
    
    /// <summary>
    /// Обязательная причина запроса продления
    /// </summary>
    [Required]
    [MinLength(10)]
    public string Reason { get; set; } = string.Empty;
    
    /// <summary>
    /// Оригинальный срок выполнения
    /// </summary>
    public DateTime OriginalDueDate { get; set; }
    
    /// <summary>
    /// Запрашиваемый новый срок
    /// </summary>
    public DateTime NewDueDate { get; set; }
    
    public ExtensionStatus Status { get; set; } = ExtensionStatus.Pending;
    
    // Поля одобрения/отклонения
    public string? ApprovedById { get; set; }
    public ApplicationUser? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    
    public string? RejectionReason { get; set; }
    
    /// <summary>
    /// Количество запрошенных дополнительных дней
    /// </summary>
    public int RequestedDays => (NewDueDate.Date - OriginalDueDate.Date).Days;
}

/// <summary>
/// Статус запроса на продление
/// </summary>
public enum ExtensionStatus
{
    Pending = 0,    // На рассмотрении
    Approved = 1,   // Одобрено
    Rejected = 2    // Отклонено
}
