namespace PMMIS.Domain.Entities;

/// <summary>
/// Тип измерения индикатора
/// </summary>
public enum MeasurementType
{
    Number,      // Число
    Percentage,  // Процент
    YesNo        // Да/Нет
}

/// <summary>
/// Источник географических данных для индикатора
/// </summary>
public enum GeoDataSource
{
    None = 0,              // Не связан с географией
    Population,            // Село: Текущее население (PopulationCurrent)
    FemalePopulation,      // Село: Женское население (FemalePopulation)
    Households,            // Село: Количество домохозяйств (HouseholdsCurrent)
    SchoolCount,           // Количество школ в сёлах
    HealthFacilityCount,   // Количество медучреждений в сёлах
    SchoolStudents,        // Сумма учеников в школах (TotalStudents)
}

/// <summary>
/// Категория индикатора (справочник)
/// </summary>
public class IndicatorCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    
    // Navigation
    public ICollection<Indicator> Indicators { get; set; } = new List<Indicator>();
}

/// <summary>
/// Индикатор / KPI
/// </summary>
public class Indicator : LocalizedEntity
{
    public string Code { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty; // %, количество, чел., и т.д.
    public decimal? TargetValue { get; set; }
    public int SortOrder { get; set; } = 0;
    public MeasurementType MeasurementType { get; set; } = MeasurementType.Number;
    
    /// <summary>
    /// Связь с географическим источником данных
    /// </summary>
    public GeoDataSource GeoDataSource { get; set; } = GeoDataSource.None;
    
    // Category (reference table)
    public int? CategoryId { get; set; }
    public IndicatorCategory? Category { get; set; }
    
    // Parent indicator for sub-indicators
    public int? ParentIndicatorId { get; set; }
    public Indicator? ParentIndicator { get; set; }
    public ICollection<Indicator> SubIndicators { get; set; } = new List<Indicator>();
    
    // Values
    public ICollection<IndicatorValue> Values { get; set; } = new List<IndicatorValue>();
}

/// <summary>
/// Значение индикатора для конкретного села
/// </summary>
public class IndicatorValue : BaseEntity
{
    public decimal Value { get; set; }
    public bool? BoolValue { get; set; } // For YesNo type
    public DateTime MeasurementDate { get; set; }
    public string? Notes { get; set; }
    
    // Foreign Keys
    public int IndicatorId { get; set; }
    public Indicator Indicator { get; set; } = null!;
    
    public int? VillageId { get; set; }
    public Village? Village { get; set; }
    
    public int? DistrictId { get; set; }
    public District? District { get; set; }
}
