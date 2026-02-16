using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;

namespace PMMIS.Web.Services;

/// <summary>
/// Сервис управленческих сигналов — обнаруживает проблемные ситуации
/// </summary>
public class ManagementAlertService : IManagementAlertService
{
    private readonly ApplicationDbContext _context;
    
    // Пороговые значения для определения рисков
    private const int DaysBeforeDeadlineWarning = 30;  // Предупреждение за 30 дней
    private const int DaysBeforeDeadlineCritical = 14; // Критично за 14 дней
    private const double MinProgressWarning = 0.50;    // 50% минимальный прогресс
    private const double MinProgressCritical = 0.30;   // 30% критический прогресс
    private const int PaymentDelayWarningDays = 5;     // Задержка платежа 5 дней
    private const int PaymentDelayCriticalDays = 14;   // Критическая задержка 14 дней
    
    public ManagementAlertService(ApplicationDbContext context)
    {
        _context = context;
    }
    
    public async Task<List<ManagementAlert>> GetActiveAlertsAsync()
    {
        var alerts = new List<ManagementAlert>();
        
        // 1. Контракты в риске (низкий прогресс + приближающийся дедлайн)
        alerts.AddRange(await GetContractRiskAlertsAsync());
        
        // 2. Просроченные задачи
        alerts.AddRange(await GetOverdueTaskAlertsAsync());
        
        // 3. Просроченные платежи (одобренные, но не оплаченные)
        alerts.AddRange(await GetPaymentDelayAlertsAsync());
        
        // 4. Задержки в закупках
        alerts.AddRange(await GetProcurementDelayAlertsAsync());
        
        // 5. Просроченные этапы контрактов
        alerts.AddRange(await GetOverdueMilestoneAlertsAsync());
        
        return alerts.OrderByDescending(a => a.Severity)
                     .ThenByDescending(a => a.DetectedAt)
                     .ToList();
    }
    
    public async Task<DashboardKpiSummary> GetKpiSummaryAsync()
    {
        var contracts = await _context.Contracts
            .Include(c => c.Payments)
            .ToListAsync();
        
        var tasks = await _context.ProjectTasks.ToListAsync();
        var procurements = await _context.ProcurementPlans.ToListAsync();
        
        var now = DateTime.UtcNow;
        
        // Контракты в риске: прогресс < 50% и до дедлайна < 30 дней
        var contractsAtRisk = contracts.Count(c => 
            c.WorkCompletedPercent < 50 && 
            (c.ContractEndDate - now).TotalDays < (double)DaysBeforeDeadlineWarning &&
            c.ContractEndDate > now);
        
        return new DashboardKpiSummary
        {
            TotalContracts = contracts.Count,
            ContractsAtRisk = contractsAtRisk,
            OverdueTasks = tasks.Count(t => t.DueDate < now && t.Status != ProjectTaskStatus.Completed),
            PendingPayments = contracts.SelectMany(c => c.Payments)
                .Count(p => p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Approved),
            DelayedProcurements = procurements.Count(p => 
                p.PlannedBidOpeningDate.HasValue && 
                p.PlannedBidOpeningDate < now && 
                p.Status != ProcurementStatus.Completed),
            TotalContractValue = contracts.Sum(c => c.ContractAmount + c.AdditionalAmount - c.SavedAmount),
            TotalPaidAmount = contracts.SelectMany(c => c.Payments)
                .Where(p => p.Status == PaymentStatus.Paid).Sum(p => p.Amount),
            OverallProgress = contracts.Any() 
                ? contracts.Average(c => (double)c.WorkCompletedPercent) 
                : 0
        };
    }
    
    #region Private Alert Detection Methods
    
    private async Task<List<ManagementAlert>> GetContractRiskAlertsAsync()
    {
        var alerts = new List<ManagementAlert>();
        var now = DateTime.UtcNow;
        
        var contracts = await _context.Contracts
            .Include(c => c.Contractor)
            .Include(c => c.Project)
            .Where(c => c.ContractEndDate > now) // Только активные контракты
            .ToListAsync();
        
        foreach (var contract in contracts)
        {
            var daysRemaining = (contract.ContractEndDate - now).TotalDays;
            var progress = (double)contract.WorkCompletedPercent / 100.0;
            
            // Ожидаемый прогресс на данный момент
            var totalDays = (contract.ContractEndDate - contract.SigningDate).TotalDays;
            var elapsedDays = (now - contract.SigningDate).TotalDays;
            var expectedProgress = totalDays > 0 ? elapsedDays / totalDays : 0;
            
            // Отставание
            var progressGap = expectedProgress - progress;
            
            // Критично: мало времени + сильное отставание
            if (daysRemaining < DaysBeforeDeadlineCritical && progress < MinProgressCritical)
            {
                alerts.Add(new ManagementAlert
                {
                    Type = AlertType.ContractAtRisk,
                    Severity = AlertSeverity.Critical,
                    Title = $"🔴 Контракт {contract.ContractNumber} в критическом состоянии",
                    Description = $"Прогресс: {contract.WorkCompletedPercent}%, до дедлайна: {daysRemaining:N0} дней. " +
                                  $"Подрядчик: {contract.Contractor.Name}",
                    LinkUrl = $"/Contracts/Details/{contract.Id}",
                    LinkText = "Открыть контракт",
                    ContractId = contract.Id,
                    ProjectId = contract.ProjectId
                });
            }
            // Предупреждение: приближается дедлайн + отставание > 20%
            else if (daysRemaining < DaysBeforeDeadlineWarning && progressGap > 0.20)
            {
                alerts.Add(new ManagementAlert
                {
                    Type = AlertType.ContractAtRisk,
                    Severity = AlertSeverity.Warning,
                    Title = $"⚠️ Контракт {contract.ContractNumber} отстаёт от графика",
                    Description = $"Прогресс: {contract.WorkCompletedPercent}% (ожидалось: {expectedProgress * 100:N0}%), " +
                                  $"до дедлайна: {daysRemaining:N0} дней",
                    LinkUrl = $"/Contracts/Details/{contract.Id}",
                    LinkText = "Открыть контракт",
                    ContractId = contract.Id,
                    ProjectId = contract.ProjectId
                });
            }
        }
        
        return alerts;
    }
    
    private async Task<List<ManagementAlert>> GetOverdueTaskAlertsAsync()
    {
        var alerts = new List<ManagementAlert>();
        var now = DateTime.UtcNow;
        
        var overdueTasks = await _context.ProjectTasks
            .Include(t => t.Contract)
            .Include(t => t.Assignee)
            .Where(t => t.DueDate < now && t.Status != ProjectTaskStatus.Completed)
            .OrderBy(t => t.DueDate)
            .Take(10) // Ограничиваем количество
            .ToListAsync();
        
        foreach (var task in overdueTasks)
        {
            var daysOverdue = (now - task.DueDate).TotalDays;
            var severity = daysOverdue > 7 ? AlertSeverity.Critical : AlertSeverity.Warning;
            
            alerts.Add(new ManagementAlert
            {
                Type = AlertType.TaskOverdue,
                Severity = severity,
                Title = severity == AlertSeverity.Critical 
                    ? $"🔴 Задача просрочена: {task.Title}" 
                    : $"⚠️ Просроченная задача: {task.Title}",
                Description = $"Просрочено: {daysOverdue:N0} дней. " +
                             (task.Assignee != null ? $"Исполнитель: {task.Assignee.FullName}" : ""),
                LinkUrl = $"/Tasks/Details/{task.Id}",
                LinkText = "Открыть задачу",
                ContractId = task.ContractId,
                ProjectId = task.ProjectId
            });
        }
        
        return alerts;
    }
    
    private async Task<List<ManagementAlert>> GetPaymentDelayAlertsAsync()
    {
        var alerts = new List<ManagementAlert>();
        var now = DateTime.UtcNow;
        
        // Платежи одобренные, но не оплаченные (дата платежа прошла)
        var delayedPayments = await _context.Payments
            .Include(p => p.Contract)
                .ThenInclude(c => c.Contractor)
            .Where(p => p.Status == PaymentStatus.Approved && p.PaymentDate < now)
            .ToListAsync();
        
        foreach (var payment in delayedPayments)
        {
            var daysDelay = (now - payment.PaymentDate).TotalDays;
            var severity = daysDelay > PaymentDelayCriticalDays 
                ? AlertSeverity.Critical 
                : AlertSeverity.Warning;
            
            alerts.Add(new ManagementAlert
            {
                Type = AlertType.PaymentDelay,
                Severity = severity,
                Title = severity == AlertSeverity.Critical 
                    ? $"🔴 Критическая задержка платежа" 
                    : $"⚠️ Задержка платежа",
                Description = $"Контракт: {payment.Contract.ContractNumber}, " +
                              $"Сумма: ${payment.Amount:N0}, Задержка: {daysDelay:N0} дней. " +
                              $"Подрядчик: {payment.Contract.Contractor.Name}",
                LinkUrl = $"/Payments?contractId={payment.ContractId}",
                LinkText = "Открыть платежи",
                ContractId = payment.ContractId,
                ProjectId = payment.Contract.ProjectId
            });
        }
        
        return alerts;
    }
    
    private async Task<List<ManagementAlert>> GetProcurementDelayAlertsAsync()
    {
        var alerts = new List<ManagementAlert>();
        var now = DateTime.UtcNow;
        
        // Закупки с просроченной плановой датой, но не завершённые
        var delayedProcurements = await _context.ProcurementPlans
            .Include(p => p.Project)
            .Where(p => p.PlannedBidOpeningDate != null && 
                       p.PlannedBidOpeningDate < now && 
                       p.Status != ProcurementStatus.Completed &&
                       p.Status != ProcurementStatus.Cancelled)
            .Take(10)
            .ToListAsync();
        
        foreach (var procurement in delayedProcurements)
        {
            var daysDelay = (now - procurement.PlannedBidOpeningDate!.Value).TotalDays;
            var severity = daysDelay > 30 ? AlertSeverity.Critical : AlertSeverity.Warning;
            
            alerts.Add(new ManagementAlert
            {
                Type = AlertType.ProcurementDelay,
                Severity = severity,
                Title = severity == AlertSeverity.Critical 
                    ? $"🔴 Критическая задержка закупки" 
                    : $"⚠️ Закупка отстаёт от плана",
                Description = $"{procurement.ReferenceNo}: {procurement.Description?.Substring(0, Math.Min(50, procurement.Description?.Length ?? 0))}... " +
                              $"Задержка: {daysDelay:N0} дней",
                LinkUrl = $"/Procurement/Details/{procurement.Id}",
                LinkText = "Открыть закупку",
                ProjectId = procurement.ProjectId
            });
        }
        
        return alerts;
    }
    
    /// <summary>
    /// Просроченные этапы контрактов (milestones)
    /// </summary>
    private async Task<List<ManagementAlert>> GetOverdueMilestoneAlertsAsync()
    {
        var alerts = new List<ManagementAlert>();
        var now = DateTime.UtcNow;

        var overdueMilestones = await _context.Set<ContractMilestone>()
            .Include(m => m.Contract)
                .ThenInclude(c => c.Contractor)
            .Where(m => m.DueDate < now &&
                        m.Status != MilestoneStatus.Completed)
            .OrderBy(m => m.DueDate)
            .Take(10)
            .ToListAsync();

        foreach (var milestone in overdueMilestones)
        {
            var overdueDays = (int)(now.Date - milestone.DueDate.Date).TotalDays;
            var severity = overdueDays > 30 ? AlertSeverity.Critical : AlertSeverity.Warning;

            alerts.Add(new ManagementAlert
            {
                Type = AlertType.MilestoneOverdue,
                Severity = severity,
                Title = severity == AlertSeverity.Critical
                    ? $"\ud83d\udd34 Этап \"{milestone.Title}\" просрочен на {overdueDays} дн."
                    : $"\u26a0\ufe0f Этап \"{milestone.Title}\" просрочен на {overdueDays} дн.",
                Description = $"Контракт {milestone.Contract.ContractNumber}, " +
                              $"подрядчик: {milestone.Contract.Contractor?.Name}. " +
                              $"Срок сдачи: {milestone.DueDate:dd.MM.yyyy}",
                LinkUrl = $"/Contracts/Details/{milestone.ContractId}",
                LinkText = "Открыть контракт",
                ContractId = milestone.ContractId,
                ProjectId = milestone.Contract.ProjectId
            });
        }

        return alerts;
    }
    
    #endregion
}
