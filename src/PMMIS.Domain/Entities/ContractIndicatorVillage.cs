namespace PMMIS.Domain.Entities;

/// <summary>
/// Связь контракт-индикатора с конкретным селом (для географических индикаторов)
/// </summary>
public class ContractIndicatorVillage : BaseEntity
{
    public int ContractIndicatorId { get; set; }
    public ContractIndicator ContractIndicator { get; set; } = null!;

    public int VillageId { get; set; }
    public Village Village { get; set; } = null!;
}
