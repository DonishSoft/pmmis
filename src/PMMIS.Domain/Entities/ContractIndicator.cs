namespace PMMIS.Domain.Entities;

/// <summary>
/// Связь контракта с индикатором и целевым значением
/// </summary>
public class ContractIndicator : BaseEntity
{
    /// <summary>
    /// Контракт
    /// </summary>
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    
    /// <summary>
    /// Индикатор
    /// </summary>
    public int IndicatorId { get; set; }
    public Indicator Indicator { get; set; } = null!;
    
    /// <summary>
    /// Целевое значение - сколько подрядчик должен достичь по этому контракту
    /// </summary>
    public decimal TargetValue { get; set; }
    
    /// <summary>
    /// Достигнутое значение - сумма из всех АВР прогрессов
    /// </summary>
    public decimal AchievedValue { get; set; }
    
    /// <summary>
    /// Процент выполнения
    /// </summary>
    public decimal ProgressPercent => TargetValue > 0 ? AchievedValue / TargetValue * 100 : 0;
    
    /// <summary>
    /// Примечания
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// Записи прогресса по АВР
    /// </summary>
    public ICollection<ContractIndicatorProgress> Progresses { get; set; } = new List<ContractIndicatorProgress>();
    
    /// <summary>
    /// Привязанные сёла (для географических индикаторов)
    /// </summary>
    public ICollection<ContractIndicatorVillage> Villages { get; set; } = new List<ContractIndicatorVillage>();
}
