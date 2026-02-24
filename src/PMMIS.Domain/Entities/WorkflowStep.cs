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
    /// Вид контракта: Works, Consulting, Goods
    /// </summary>
    public ContractType? ContractType { get; set; }
    
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
    /// Тип назначения: "Role" (по роли), "ContractCurator" (куратор контракта), "ContractPM" (менеджер проекта)
    /// </summary>
    public string AssigneeType { get; set; } = "Role";
    
    /// <summary>
    /// Роль, которая выполняет этот шаг (используется когда AssigneeType = "Role")
    /// </summary>
    public string RoleId { get; set; } = string.Empty;
    public ApplicationRole? Role { get; set; }
    
    /// <summary>
    /// Может ли этот шаг отклонить документ
    /// </summary>
    public bool CanReject { get; set; } = true;
    
    /// <summary>
    /// На какой шаг вернуть при отказе (null = вернуть на первый шаг)
    /// </summary>
    public int? RejectToStepOrder { get; set; }
    
    /// <summary>
    /// Активен ли шаг
    /// </summary>
    public bool IsActive { get; set; } = true;
}
