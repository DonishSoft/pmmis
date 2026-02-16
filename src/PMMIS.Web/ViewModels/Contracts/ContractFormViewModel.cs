using PMMIS.Domain.Entities;

namespace PMMIS.Web.ViewModels.Contracts;

/// <summary>
/// ViewModel для формы создания/редактирования контракта
/// </summary>
public class ContractFormViewModel
{
    public Contract Contract { get; set; } = new();
    public IEnumerable<Contractor> Contractors { get; set; } = [];
    public IEnumerable<Project> Projects { get; set; } = [];
    public IEnumerable<SubComponent> SubComponents { get; set; } = [];
    
    /// <summary>
    /// Доступные позиции плана закупок
    /// </summary>
    public IEnumerable<ProcurementPlan> ProcurementPlans { get; set; } = [];
    
    /// <summary>
    /// Доступные индикаторы для выбора
    /// </summary>
    public IEnumerable<Indicator> Indicators { get; set; } = [];
    
    /// <summary>
    /// Выбранные индикаторы с целевыми значениями
    /// </summary>
    public List<ContractIndicatorInput> SelectedIndicators { get; set; } = [];
    
    /// <summary>
    /// Существующие связи контракта с индикаторами (для редактирования)
    /// </summary>
    public IEnumerable<ContractIndicator> ExistingContractIndicators { get; set; } = [];
    
    /// <summary>
    /// Пользователи для выбора Куратора и Менеджера проекта
    /// </summary>
    public IEnumerable<ApplicationUser> Users { get; set; } = [];
    
    /// <summary>
    /// JSON строка milestones для динамичного списка
    /// </summary>
    public string? MilestonesJson { get; set; }
    
    /// <summary>
    /// Документы для загрузки (тендерные, контрактные)
    /// </summary>
    public List<IFormFile>? Documents { get; set; }
    
    /// <summary>
    /// Существующие документы
    /// </summary>
    public List<Document>? ExistingDocuments { get; set; }
}

/// <summary>
/// Input model для привязки данных индикатора контракта из формы
/// </summary>
public class ContractIndicatorInput
{
    public int IndicatorId { get; set; }
    public decimal TargetValue { get; set; }
    public string? Notes { get; set; }
    
    /// <summary>
    /// Выбранные сёла (для географических индикаторов)
    /// </summary>
    public List<int> VillageIds { get; set; } = [];
}
