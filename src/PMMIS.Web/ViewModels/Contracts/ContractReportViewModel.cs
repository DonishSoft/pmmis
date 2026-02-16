using PMMIS.Domain.Entities;

namespace PMMIS.Web.ViewModels.Contracts;

/// <summary>
/// ViewModel для страницы Contract Monitoring Report
/// </summary>
public class ContractReportViewModel
{
    /// <summary>
    /// Список проектов для фильтра
    /// </summary>
    public IEnumerable<Project> Projects { get; set; } = [];
    
    /// <summary>
    /// Выбранный проект (null = все)
    /// </summary>
    public int? SelectedProjectId { get; set; }
    
    /// <summary>
    /// Дата отчёта
    /// </summary>
    public DateTime ReportDate { get; set; } = DateTime.Today;
    
    // === Table 1: On-Going Procurement ===
    public List<ProcurementPlan> OnGoingProcurements { get; set; } = [];
    
    // === Table 2: Contracted Activities ===
    public List<Contract> ContractedActivities { get; set; } = [];
    
    // === Table 3: Not Commenced / Pending Readiness ===
    public List<ProcurementPlan> NotCommencedActivities { get; set; } = [];
    
    // === Table 4: Completed Activities ===
    public List<Contract> CompletedActivities { get; set; } = [];
    
    // === Table 5: Progress Summary ===
    public ReportProgressSummary ProgressSummary { get; set; } = new();
    
    // === Table 6: Summary by Category ===
    public List<CategorySummary> CategorySummaries { get; set; } = [];
}

/// <summary>
/// Сводка прогресса
/// </summary>
public class ReportProgressSummary
{
    public int TotalContracts { get; set; }
    public int OngoingContracts { get; set; }
    public int CompletedContracts { get; set; }
    public int SuspendedContracts { get; set; }
    public int TerminatedContracts { get; set; }
    public decimal TotalContractAmount { get; set; }
    public decimal TotalPaidAmount { get; set; }
    public decimal AverageWorkCompleted { get; set; }
}

/// <summary>
/// Сводка по категории контрактов
/// </summary>
public class CategorySummary
{
    public string CategoryName { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal AverageCompletion { get; set; }
}
