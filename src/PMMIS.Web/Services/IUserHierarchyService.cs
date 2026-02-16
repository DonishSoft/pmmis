using PMMIS.Domain.Entities;

namespace PMMIS.Web.Services;

/// <summary>
/// Сервис для работы с иерархией пользователей
/// </summary>
public interface IUserHierarchyService
{
    /// <summary>
    /// Получить всех подчинённых (рекурсивно)
    /// </summary>
    Task<List<ApplicationUser>> GetAllSubordinatesAsync(string userId);
    
    /// <summary>
    /// Получить прямых подчинённых
    /// </summary>
    Task<List<ApplicationUser>> GetDirectSubordinatesAsync(string userId);
    
    /// <summary>
    /// Проверить: subordinateId подчиняется supervisorId?
    /// </summary>
    Task<bool> IsSubordinateAsync(string supervisorId, string subordinateId);
    
    /// <summary>
    /// Получить цепочку руководства (вверх от пользователя до корня)
    /// </summary>
    Task<List<ApplicationUser>> GetManagementChainAsync(string userId);
    
    /// <summary>
    /// Получить ID всех подчинённых (рекурсивно) — для фильтрации
    /// </summary>
    Task<List<string>> GetAllSubordinateIdsAsync(string userId);
}
