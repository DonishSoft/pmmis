using Microsoft.AspNetCore.Authorization;

namespace PMMIS.Web.Authorization;

/// <summary>
/// Requirement для проверки доступа к модулю
/// </summary>
public class MenuPermissionRequirement : IAuthorizationRequirement
{
    public string MenuKey { get; }
    public PermissionType PermissionType { get; }

    public MenuPermissionRequirement(string menuKey, PermissionType permissionType = PermissionType.View)
    {
        MenuKey = menuKey;
        PermissionType = permissionType;
    }
}

/// <summary>
/// Типы разрешений
/// </summary>
public enum PermissionType
{
    View,
    Create,
    Edit,
    Delete
}
