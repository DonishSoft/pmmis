using PMMIS.Domain.Entities;

namespace PMMIS.Web.ViewModels.Roles;

/// <summary>
/// ViewModel для страницы прав доступа роли
/// </summary>
public class RolePermissionsViewModel
{
    public ApplicationRole Role { get; set; } = null!;
    public Dictionary<string, string> MenuKeys { get; set; } = [];
    public Dictionary<string, RoleMenuPermission> Permissions { get; set; } = [];
}
