using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;

namespace PMMIS.Web.Services;

/// <summary>
/// Сервис проверки видимости данных: CanViewAll для текущего пользователя
/// </summary>
public interface IDataAccessService
{
    /// <summary>
    /// Проверяет, может ли пользователь видеть все данные модуля
    /// </summary>
    Task<bool> CanViewAllAsync(ApplicationUser user, string menuKey);
    
    /// <summary>
    /// Возвращает ID контрактов, к которым пользователь имеет отношение (куратор или PM)
    /// </summary>
    Task<List<int>> GetUserContractIdsAsync(string userId);
}

public class DataAccessService : IDataAccessService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DataAccessService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<bool> CanViewAllAsync(ApplicationUser user, string menuKey)
    {
        var roles = await _userManager.GetRolesAsync(user);
        
        // PMU_ADMIN always sees everything
        if (roles.Contains(UserRoles.PmuAdmin))
            return true;

        var roleIds = await _context.Roles
            .Where(r => roles.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync();

        return await _context.RoleMenuPermissions
            .AnyAsync(p => roleIds.Contains(p.RoleId) 
                        && p.MenuKey == menuKey 
                        && p.CanViewAll);
    }

    public async Task<List<int>> GetUserContractIdsAsync(string userId)
    {
        return await _context.Contracts
            .Where(c => c.CuratorId == userId || c.ProjectManagerId == userId)
            .Select(c => c.Id)
            .ToListAsync();
    }
}
