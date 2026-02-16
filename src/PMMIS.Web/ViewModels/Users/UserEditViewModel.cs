using PMMIS.Domain.Entities;

namespace PMMIS.Web.ViewModels.Users;

/// <summary>
/// ViewModel для формы редактирования пользователя
/// </summary>
public class UserEditViewModel
{
    public ApplicationUser User { get; set; } = null!;
    public IEnumerable<ApplicationRole> Roles { get; set; } = [];
    public IList<string> UserRoles { get; set; } = [];
    
    /// <summary>
    /// Все активные пользователи (для выпадающего списка «Руководитель»)
    /// </summary>
    public List<ApplicationUser> AllUsers { get; set; } = [];
}
