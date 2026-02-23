namespace PMMIS.Domain.Entities;

/// <summary>
/// Шаг цепочки утверждения (Workflow)
/// Определяет порядок действий для АВР и Платежей
/// </summary>
public class WorkflowStep : BaseEntity
{
    /// <summary>
    /// Тип процесса: "AVR" или "Payment"
    /// </summary>
    public string WorkflowType { get; set; } = string.Empty;
    
    /// <summary>
    /// Порядок шага (1, 2, 3...)
    /// </summary>
    public int StepOrder { get; set; }
    
    /// <summary>
    /// Название шага (напр. "Создание АВР", "Проверка менеджером")
    /// </summary>
    public string StepName { get; set; } = string.Empty;
    
    /// <summary>
    /// Тип действия: Create, Review, Approve, FinalApprove
    /// </summary>
    public string ActionType { get; set; } = string.Empty;
    
    /// <summary>
    /// Роль, которая выполняет этот шаг
    /// </summary>
    public string RoleId { get; set; } = string.Empty;
    public ApplicationRole? Role { get; set; }
    
    /// <summary>
    /// Активен ли шаг
    /// </summary>
    public bool IsActive { get; set; } = true;
}
