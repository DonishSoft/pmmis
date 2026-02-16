using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;

namespace PMMIS.Web.Authorization;

/// <summary>
/// Обработчик для проверки прав доступа на основе RoleMenuPermission
/// </summary>
public class MenuPermissionHandler : AuthorizationHandler<MenuPermissionRequirement>
{
    private readonly IServiceProvider _serviceProvider;

    public MenuPermissionHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, 
        MenuPermissionRequirement requirement)
    {
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            return;
        }

        // PMU_ADMIN has full access to everything
        if (context.User.IsInRole(UserRoles.PmuAdmin))
        {
            context.Succeed(requirement);
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var userId = userManager.GetUserId(context.User);
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return;
        }

        var roles = await userManager.GetRolesAsync(user);
        if (!roles.Any())
        {
            return;
        }

        // Get role IDs
        var roleIds = await dbContext.Roles
            .Where(r => roles.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync();

        // Get permissions for user's roles and requested menu key
        var permissions = await dbContext.RoleMenuPermissions
            .Where(p => roleIds.Contains(p.RoleId) && p.MenuKey == requirement.MenuKey)
            .ToListAsync();

        // Check if any permission matches the required type
        var hasPermission = permissions.Any(p => requirement.PermissionType switch
        {
            PermissionType.View => p.CanView,
            PermissionType.Create => p.CanCreate,
            PermissionType.Edit => p.CanEdit,
            PermissionType.Delete => p.CanDelete,
            _ => p.CanView
        });

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}
