using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;

namespace PMMIS.Web.Services;

/// <summary>
/// Реализация сервиса иерархии пользователей
/// Использует self-referencing FK (SupervisorId) для построения дерева
/// </summary>
public class UserHierarchyService : IUserHierarchyService
{
    private readonly ApplicationDbContext _context;

    public UserHierarchyService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Получить всех подчинённых рекурсивно (BFS обход дерева)
    /// </summary>
    public async Task<List<ApplicationUser>> GetAllSubordinatesAsync(string userId)
    {
        var allUsers = await _context.Users
            .Where(u => u.IsActive && u.SupervisorId != null)
            .ToListAsync();
        
        var result = new List<ApplicationUser>();
        var queue = new Queue<string>();
        queue.Enqueue(userId);
        
        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            var directSubs = allUsers.Where(u => u.SupervisorId == currentId).ToList();
            
            foreach (var sub in directSubs)
            {
                result.Add(sub);
                queue.Enqueue(sub.Id);
            }
        }
        
        return result;
    }

    /// <summary>
    /// Получить прямых подчинённых (только 1 уровень)
    /// </summary>
    public async Task<List<ApplicationUser>> GetDirectSubordinatesAsync(string userId)
    {
        return await _context.Users
            .Where(u => u.SupervisorId == userId && u.IsActive)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync();
    }

    /// <summary>
    /// Проверить: subordinateId подчиняется supervisorId (прямо или косвенно)?
    /// </summary>
    public async Task<bool> IsSubordinateAsync(string supervisorId, string subordinateId)
    {
        if (supervisorId == subordinateId) return false;
        
        var subordinateIds = await GetAllSubordinateIdsAsync(supervisorId);
        return subordinateIds.Contains(subordinateId);
    }

    /// <summary>
    /// Получить цепочку руководства (от пользователя вверх до корня)
    /// </summary>
    public async Task<List<ApplicationUser>> GetManagementChainAsync(string userId)
    {
        var chain = new List<ApplicationUser>();
        var visited = new HashSet<string>();
        var currentId = userId;
        
        while (currentId != null)
        {
            if (!visited.Add(currentId)) break; // защита от циклов
            
            var user = await _context.Users.FindAsync(currentId);
            if (user == null) break;
            
            if (user.Id != userId) // не добавляем самого себя
                chain.Add(user);
            
            currentId = user.SupervisorId;
        }
        
        return chain;
    }

    /// <summary>
    /// Получить ID всех подчинённых (для фильтрации в запросах)
    /// </summary>
    public async Task<List<string>> GetAllSubordinateIdsAsync(string userId)
    {
        var subordinates = await GetAllSubordinatesAsync(userId);
        return subordinates.Select(u => u.Id).ToList();
    }
}
