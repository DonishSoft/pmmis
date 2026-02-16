namespace PMMIS.Domain.Entities;

/// <summary>
/// Тип образовательного учреждения (справочник)
/// </summary>
public class EducationInstitutionType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    
    public ICollection<School> Schools { get; set; } = new List<School>();
}

/// <summary>
/// Тип медицинского учреждения (справочник)
/// </summary>
public class HealthFacilityType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    
    public ICollection<HealthFacility> HealthFacilities { get; set; } = new List<HealthFacility>();
}

/// <summary>
/// Школа/образовательное учреждение в селе
/// </summary>
public class School : BaseEntity
{
    public int SortOrder { get; set; } = 0;
    public int Number { get; set; }
    public string? Name { get; set; }
    
    public int? TypeId { get; set; }
    public EducationInstitutionType? Type { get; set; }
    
    public int TotalStudents { get; set; }
    public int FemaleStudents { get; set; }
    public int TeachersCount { get; set; }
    public int FemaleTeachersCount { get; set; }
    
    public bool HasWaterSupply { get; set; }
    public bool HasSanitation { get; set; }
    public string? Notes { get; set; }
    
    public int VillageId { get; set; }
    public Village Village { get; set; } = null!;
}

/// <summary>
/// Медицинское учреждение
/// </summary>
public class HealthFacility : BaseEntity
{
    public int SortOrder { get; set; } = 0;
    public string? Name { get; set; }
    
    public int? TypeId { get; set; }
    public HealthFacilityType? Type { get; set; }
    
    /// <summary>
    /// Общее количество персонала
    /// </summary>
    public int TotalStaff { get; set; }
    
    /// <summary>
    /// Из них женщин
    /// </summary>
    public int FemaleStaff { get; set; }
    
    /// <summary>
    /// Количество пациентов в день
    /// </summary>
    public int PatientsPerDay { get; set; }
    
    public bool HasWaterSupply { get; set; }
    public bool HasSanitation { get; set; }
    public string? Notes { get; set; }
    
    public int VillageId { get; set; }
    public Village Village { get; set; } = null!;
}
