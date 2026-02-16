using PMMIS.Domain.Entities;

namespace PMMIS.Web.ViewModels.Roles;

/// <summary>
/// ViewModel для списка ролей
/// </summary>
public class RoleIndexViewModel
{
    public IEnumerable<ApplicationRole> Roles { get; set; } = [];
    public Dictionary<string, int> UserCounts { get; set; } = [];
    public Dictionary<string, int> PermissionCounts { get; set; } = [];
    public int TotalModules { get; set; }
}
