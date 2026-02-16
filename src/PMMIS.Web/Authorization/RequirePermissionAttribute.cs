using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PMMIS.Domain.Entities;

namespace PMMIS.Web.Authorization;

/// <summary>
/// Атрибут для проверки доступа к модулю с указанием типа разрешения
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute : TypeFilterAttribute
{
    public RequirePermissionAttribute(string menuKey, PermissionType permissionType = PermissionType.View)
        : base(typeof(RequirePermissionFilter))
    {
        Arguments = new object[] { menuKey, permissionType };
    }
}

/// <summary>
/// Фильтр для проверки разрешений
/// </summary>
public class RequirePermissionFilter : IAsyncAuthorizationFilter
{
    private readonly string _menuKey;
    private readonly PermissionType _permissionType;
    private readonly IAuthorizationService _authorizationService;

    public RequirePermissionFilter(
        string menuKey, 
        PermissionType permissionType,
        IAuthorizationService authorizationService)
    {
        _menuKey = menuKey;
        _permissionType = permissionType;
        _authorizationService = authorizationService;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        // PMU_ADMIN has full access
        if (user.IsInRole(UserRoles.PmuAdmin))
        {
            return;
        }

        var requirement = new MenuPermissionRequirement(_menuKey, _permissionType);
        var result = await _authorizationService.AuthorizeAsync(user, null, requirement);

        if (!result.Succeeded)
        {
            context.Result = new ForbidResult();
        }
    }
}
