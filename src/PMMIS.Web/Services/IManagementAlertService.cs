using PMMIS.Domain.Entities;

namespace PMMIS.Web.Services;

/// <summary>
/// Управленческие сигналы (красные флаги) для мониторинга проектов
/// </summary>
public interface IManagementAlertService
{
    Task<List<ManagementAlert>> GetActiveAlertsAsync();
    Task<DashboardKpiSummary> GetKpiSummaryAsync();
}

/// <summary>
/// Типы управленческих сигналов
/// </summary>
public enum AlertType
{
    ContractAtRisk,      // Низкий прогресс + приближающийся дедлайн
    PaymentDelay,        // Просроченные платежи
    TaskOverdue,         // Просроченные задачи  
    ProcurementDelay,    // Закупка отстаёт от плана
    BudgetExceeded,      // Превышение бюджета
    MilestoneOverdue     // Просроченный этап контракта
}

/// <summary>
/// Уровень критичности сигнала
/// </summary>
public enum AlertSeverity
{
    Warning,   // Жёлтый - требует внимания
    Critical   // Красный - требует немедленного действия
}

/// <summary>
/// Управленческий сигнал (красный флаг)
/// </summary>
public class ManagementAlert
{
    public AlertType Type { get; set; }
    public AlertSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LinkUrl { get; set; } = string.Empty;
    public string LinkText { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    
    // Для группировки
    public int? ContractId { get; set; }
    public int? ProjectId { get; set; }
}

/// <summary>
/// Сводка KPI для дашборда
/// </summary>
public class DashboardKpiSummary
{
    public int TotalContracts { get; set; }
    public int ContractsAtRisk { get; set; }
    public int OverdueTasks { get; set; }
    public int PendingPayments { get; set; }
    public int DelayedProcurements { get; set; }
    public decimal TotalContractValue { get; set; }
    public decimal TotalPaidAmount { get; set; }
    public double OverallProgress { get; set; }
}
