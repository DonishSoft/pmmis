using Microsoft.AspNetCore.Identity;

namespace PMMIS.Domain.Entities;

/// <summary>
/// Пол пользователя
/// </summary>
public enum Gender
{
    Male,       // Мужской
    Female      // Женский
}

/// <summary>
/// Пользователь системы
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string PreferredLanguage { get; set; } = "ru";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    
    // Extended profile
    public string? PhotoPath { get; set; }           // Путь к фото профиля
    public Gender Gender { get; set; } = Gender.Male;
    public DateTime? BirthDate { get; set; }         // Дата рождения
    public string? ContractScanPath { get; set; }    // Скан трудового контракта (для PMU)
    
    // For contractors linked to a contractor record
    public int? ContractorId { get; set; }
    
    // Иерархия — кому подчиняется
    public string? SupervisorId { get; set; }
    public ApplicationUser? Supervisor { get; set; }
    public ICollection<ApplicationUser> Subordinates { get; set; } = new List<ApplicationUser>();
    
    // Должность
    public string? Position { get; set; }  // "Директор", "Глава бухгалтерии" и т.д.
    
    public string FullName => string.IsNullOrEmpty(MiddleName) 
        ? $"{LastName} {FirstName}".Trim()
        : $"{LastName} {FirstName} {MiddleName}".Trim();
}

/// <summary>
/// Роль пользователя
/// </summary>
public class ApplicationRole : IdentityRole
{
    public string? Description { get; set; }
    public string? DescriptionTj { get; set; }
    public string? DescriptionEn { get; set; }
    public int SortOrder { get; set; } = 0;
    public bool IsSystem { get; set; } = false;  // Системные роли нельзя удалять
}

/// <summary>
/// Константы ролей
/// </summary>
public static class UserRoles
{
    public const string PmuAdmin = "PMU_ADMIN";
    public const string PmuStaff = "PMU_STAFF";
    public const string Accountant = "ACCOUNTANT";
    public const string WorldBank = "WORLD_BANK";
    public const string Contractor = "CONTRACTOR";
}
