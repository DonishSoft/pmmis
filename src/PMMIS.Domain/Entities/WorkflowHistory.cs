namespace PMMIS.Domain.Entities;

/// <summary>
/// Запись истории Workflow — фиксирует каждое действие в цепочке утверждения
/// </summary>
public class WorkflowHistory : BaseEntity
{
    /// <summary>
    /// АВР, к которому относится запись (0 если для платежа)
    /// </summary>
    public int WorkProgressId { get; set; }
    public WorkProgress? WorkProgress { get; set; }

    /// <summary>
    /// Платёж, к которому относится запись (null если для АВР)
    /// </summary>
    public int? PaymentId { get; set; }
    public Payment? Payment { get; set; }

    /// <summary>
    /// Номер шага Workflow (StepOrder)
    /// </summary>
    public int StepOrder { get; set; }

    /// <summary>
    /// Название шага (для display, чтобы не join'ить)
    /// </summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>
    /// Тип действия: Created, Submitted, Approved, Rejected, Edited, Resubmitted
    /// </summary>
    public WorkflowAction Action { get; set; }

    /// <summary>
    /// Кто выполнил действие
    /// </summary>
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    /// <summary>
    /// Имя пользователя (для быстрого отображения)
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Роль пользователя на этом шаге
    /// </summary>
    public string? RoleName { get; set; }

    /// <summary>
    /// Когда действие должно было быть выполнено (дедлайн)
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Когда действие фактически было выполнено
    /// </summary>
    public DateTime ActionDate { get; set; }

    /// <summary>
    /// Просрочено ли (фактическая дата > дедлайна)
    /// </summary>
    public bool IsOverdue => DueDate.HasValue && ActionDate > DueDate.Value;

    /// <summary>
    /// Комментарий / причина отклонения / описание изменений
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Тип действия в истории Workflow
/// </summary>
public enum WorkflowAction
{
    /// <summary>АВР создан</summary>
    Created = 0,
    
    /// <summary>Отправлен на проверку (передан на следующий шаг)</summary>
    Submitted = 1,
    
    /// <summary>Утверждён на текущем шаге</summary>
    Approved = 2,
    
    /// <summary>Отклонён (возвращён на предыдущий шаг)</summary>
    Rejected = 3,
    
    /// <summary>АВР отредактирован</summary>
    Edited = 4,
    
    /// <summary>Повторно отправлен после исправления</summary>
    Resubmitted = 5,
    
    /// <summary>Финально утверждён (workflow завершён)</summary>
    FinalApproved = 6
}
