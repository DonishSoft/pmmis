using PMMIS.Domain.Entities;

namespace PMMIS.Web.ViewModels.Roles;

/// <summary>
/// ViewModel для формы создания/редактирования роли
/// </summary>
public class RoleFormViewModel
{
    public ApplicationRole Role { get; set; } = null!;
    public Dictionary<string, string> MenuKeys { get; set; } = [];
    public IList<ApplicationUser> UsersInRole { get; set; } = [];
}
