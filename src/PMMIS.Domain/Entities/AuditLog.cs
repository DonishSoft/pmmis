namespace PMMIS.Domain.Entities;

/// <summary>
/// Журнал изменений — аудит-лог для отслеживания всех изменений в сущностях
/// </summary>
public class AuditLog
{
    public int Id { get; set; }
    
    /// <summary>Тип сущности: "Contract", "ProcurementPlan"</summary>
    public string EntityType { get; set; } = string.Empty;
    
    /// <summary>ID сущности</summary>
    public int EntityId { get; set; }
    
    /// <summary>Действие: "Создание", "Изменение", "Удаление"</summary>
    public string Action { get; set; } = string.Empty;
    
    /// <summary>JSON массив изменений: [{ "Field": "...", "OldValue": "...", "NewValue": "..." }]</summary>
    public string? Changes { get; set; }
    
    /// <summary>ID пользователя</summary>
    public string UserId { get; set; } = string.Empty;
    
    /// <summary>ФИО пользователя</summary>
    public string UserFullName { get; set; } = string.Empty;
    
    /// <summary>Должность пользователя</summary>
    public string? UserPosition { get; set; }
    
    /// <summary>Время изменения</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
