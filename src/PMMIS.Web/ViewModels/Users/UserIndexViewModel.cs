using PMMIS.Domain.Entities;

namespace PMMIS.Web.ViewModels.Users;

/// <summary>
/// ViewModel для списка пользователей
/// </summary>
public class UserIndexViewModel
{
    public IEnumerable<ApplicationUser> Users { get; set; } = [];
    public Dictionary<string, IList<string>> UserRoles { get; set; } = [];
    public string? Search { get; set; }
}
