using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;

namespace PMMIS.Web.Services;

/// <summary>
/// Реализация проверки прав доступа на основе RoleMenuPermission
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public PermissionService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<bool> HasPermissionAsync(ClaimsPrincipal user, string menuKey, string permissionType)
    {
        if (!user.Identity?.IsAuthenticated ?? true)
            return false;

        // PMU_ADMIN has full access
        if (user.IsInRole(UserRoles.PmuAdmin))
            return true;

        var userId = _userManager.GetUserId(user);
        if (string.IsNullOrEmpty(userId))
            return false;

        var appUser = await _userManager.FindByIdAsync(userId);
        if (appUser == null)
            return false;

        var roles = await _userManager.GetRolesAsync(appUser);
        if (!roles.Any())
            return false;

        var roleIds = await _context.Roles
            .Where(r => roles.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync();

        var permissions = await _context.RoleMenuPermissions
            .Where(p => roleIds.Contains(p.RoleId) && p.MenuKey == menuKey)
            .ToListAsync();

        return permissions.Any(p => permissionType.ToLower() switch
        {
            "view" => p.CanView,
            "create" => p.CanCreate,
            "edit" => p.CanEdit,
            "delete" => p.CanDelete,
            "viewall" => p.CanViewAll,
            _ => p.CanView
        });
    }
}
