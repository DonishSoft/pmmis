using PMMIS.Domain.Entities;

namespace PMMIS.Web.ViewModels.Users;

/// <summary>
/// ViewModel для формы создания пользователя
/// </summary>
public class UserCreateViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public Gender Gender { get; set; } = Gender.Male;
    public DateTime? BirthDate { get; set; }
    public string? SelectedRole { get; set; }
    
    // Dropdown data
    public IEnumerable<ApplicationRole> Roles { get; set; } = [];
}
