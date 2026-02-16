namespace PMMIS.Domain.Entities;

/// <summary>
/// Район
/// </summary>
public class District : LocalizedEntity
{
    public string Code { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
    
    // Navigation
    public ICollection<Jamoat> Jamoats { get; set; } = new List<Jamoat>();
}

/// <summary>
/// Джамоат (административная единица)
/// </summary>
public class Jamoat : LocalizedEntity
{
    public string Code { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
    
    // Foreign Keys
    public int DistrictId { get; set; }
    public District District { get; set; } = null!;
    
    // Navigation
    public ICollection<Village> Villages { get; set; } = new List<Village>();
}

/// <summary>
/// Населённый пункт / Село
/// </summary>
public class Village : LocalizedEntity
{
    public string Zone { get; set; } = string.Empty; // e.g., "2D", "3D"
    public int Number { get; set; }
    public int SortOrder { get; set; } = 0;
    
    // Demographics - Baseline (2020)
    public int Households2020 { get; set; }
    public int Population2020 { get; set; }
    
    // Demographics - Current (2025)
    public int HouseholdsCurrent { get; set; }
    public int PopulationCurrent { get; set; }
    public int FemalePopulation { get; set; }
    
    // Coverage
    public bool IsCoveredByProject { get; set; }
    
    // Foreign Keys
    public int JamoatId { get; set; }
    public Jamoat Jamoat { get; set; } = null!;
    
    // Navigation
    public ICollection<IndicatorValue> IndicatorValues { get; set; } = new List<IndicatorValue>();
    public ICollection<School> Schools { get; set; } = new List<School>();
    public ICollection<HealthFacility> HealthFacilities { get; set; } = new List<HealthFacility>();
}
