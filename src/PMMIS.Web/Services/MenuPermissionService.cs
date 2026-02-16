using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;

namespace PMMIS.Web.Services;

/// <summary>
/// Сервис проверки доступа к меню на основе прав роли
/// </summary>
public interface IMenuPermissionService
{
    Task<HashSet<string>> GetAllowedMenuKeysForUserAsync(string userId);
    bool CanViewMenu(HashSet<string> allowedKeys, string menuKey);
}

public class MenuPermissionService : IMenuPermissionService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public MenuPermissionService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<HashSet<string>> GetAllowedMenuKeysForUserAsync(string userId)
    {
        var result = new HashSet<string>();
        
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return result;

        var roles = await _userManager.GetRolesAsync(user);
        
        // PMU_ADMIN has access to everything
        if (roles.Contains(UserRoles.PmuAdmin))
        {
            return MenuKeys.All.ToHashSet();
        }

        // Get role IDs
        var roleIds = await _context.Roles
            .Where(r => roles.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync();

        // Get allowed menu keys for these roles
        var allowedKeys = await _context.RoleMenuPermissions
            .Where(p => roleIds.Contains(p.RoleId) && p.CanView)
            .Select(p => p.MenuKey)
            .Distinct()
            .ToListAsync();

        foreach (var key in allowedKeys)
        {
            result.Add(key);
        }

        // Always allow Home
        result.Add(MenuKeys.Home);

        return result;
    }

    public bool CanViewMenu(HashSet<string> allowedKeys, string menuKey)
    {
        return allowedKeys.Contains(menuKey);
    }
}
