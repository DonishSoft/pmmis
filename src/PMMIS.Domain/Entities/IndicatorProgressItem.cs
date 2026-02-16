namespace PMMIS.Domain.Entities;

/// <summary>
/// Элемент прогресса индикатора — отметка о завершении для конкретного объекта (село, школа, медучреждение)
/// </summary>
public class IndicatorProgressItem : BaseEntity
{
    /// <summary>
    /// Связь с записью прогресса индикатора в АВР
    /// </summary>
    public int ContractIndicatorProgressId { get; set; }
    public ContractIndicatorProgress ContractIndicatorProgress { get; set; } = null!;
    
    /// <summary>
    /// Тип объекта
    /// </summary>
    public GeoItemType ItemType { get; set; }
    
    /// <summary>
    /// Село (если тип = Village)
    /// </summary>
    public int? VillageId { get; set; }
    public Village? Village { get; set; }
    
    /// <summary>
    /// Школа (если тип = School)
    /// </summary>
    public int? SchoolId { get; set; }
    public School? School { get; set; }
    
    /// <summary>
    /// Медицинское учреждение (если тип = HealthFacility)
    /// </summary>
    public int? HealthFacilityId { get; set; }
    public HealthFacility? HealthFacility { get; set; }
    
    /// <summary>
    /// Отмечено как выполнено
    /// </summary>
    public bool IsCompleted { get; set; }
    
    /// <summary>
    /// Числовое значение (население, кол-во учеников и т.д.)
    /// </summary>
    public decimal NumericValue { get; set; }
}

/// <summary>
/// Тип географического объекта в чеклисте
/// </summary>
public enum GeoItemType
{
    Village,
    School,
    HealthFacility
}
