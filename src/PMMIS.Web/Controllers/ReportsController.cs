using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;
using PMMIS.Web.Authorization;
using PMMIS.Web.ViewModels.Reports;

namespace PMMIS.Web.Controllers;

/// <summary>
/// Отчёты
/// </summary>
[Authorize]
[RequirePermission(MenuKeys.Reports, PermissionType.View)]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _context;

    public ReportsController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Отчёт по платежам (каркас + затем AJAX)
    /// </summary>
    public IActionResult Payments()
    {
        return View();
    }

    /// <summary>
    /// Данные отчёта по платежам (AJAX partial)
    /// </summary>
    public async Task<IActionResult> PaymentsData()
    {
        var contracts = await _context.Contracts
            .Include(c => c.Contractor)
            .Include(c => c.Project)
            .Include(c => c.Payments)
            .OrderBy(c => c.ContractNumber)
            .ToListAsync();

        var viewModel = new PaymentReportViewModel();

        foreach (var contract in contracts)
        {
            var paidPayments = contract.Payments.Where(p => p.Status == PaymentStatus.Paid).ToList();
            var allPayments = contract.Payments.OrderByDescending(p => p.PaymentDate).ToList();
            var isTjs = contract.Currency == ContractCurrency.TJS;
            var contractRate = contract.ExchangeRate;

            var summary = new ContractPaymentSummary
            {
                ContractId = contract.Id,
                ContractNumber = contract.ContractNumber,
                ContractorName = contract.Contractor?.Name ?? "—",
                ProjectName = contract.Project?.Code ?? "—",
                CurrencyLabel = isTjs ? "TJS" : "USD",
                SigningDate = contract.SigningDate,
                PlannedUsd = contract.FinalAmount,
                PlannedTjs = contract.AmountTjs,
                ExchangeRate = contract.ExchangeRate,
                PaidUsd = paidPayments.Sum(p => p.Amount),
                Payments = allPayments.Select(p =>
                {
                    var row = new PaymentRow
                    {
                        Id = p.Id,
                        PaymentDate = p.PaymentDate,
                        TypeLabel = GetTypeName(p.Type),
                        StatusLabel = GetStatusName(p.Status),
                        StatusClass = GetStatusClass(p.Status),
                        AmountUsd = p.Amount,
                        AmountTjs = p.AmountTjs,
                        ExchangeRate = p.ExchangeRate,
                        ContractExchangeRate = contractRate
                    };
                    
                    // Exchange rate gain/loss for TJS payments
                    if (isTjs && p.AmountTjs.HasValue && p.AmountTjs > 0)
                    {
                        if (contractRate.HasValue && contractRate > 0)
                            row.UsdAtContractRate = Math.Round(p.AmountTjs.Value / contractRate.Value, 2);
                        if (p.ExchangeRate.HasValue && p.ExchangeRate > 0)
                            row.UsdAtPaymentRate = Math.Round(p.AmountTjs.Value / p.ExchangeRate.Value, 2);
                        if (row.UsdAtPaymentRate.HasValue && row.UsdAtContractRate.HasValue)
                            row.ExchangeGainLoss = row.UsdAtPaymentRate.Value - row.UsdAtContractRate.Value;
                    }
                    
                    return row;
                }).ToList()
            };
            
            // Aggregate TJS paid and exchange gain/loss
            if (isTjs)
            {
                var paidRows = summary.Payments.Where(p => p.StatusLabel == "Оплачен").ToList();
                summary.PaidTjs = paidRows.Where(p => p.AmountTjs.HasValue).Sum(p => p.AmountTjs!.Value);
                summary.PaidUsdAtContractRate = paidRows.Where(p => p.UsdAtContractRate.HasValue).Sum(p => p.UsdAtContractRate!.Value);
                summary.PaidUsdAtPaymentRate = paidRows.Where(p => p.UsdAtPaymentRate.HasValue).Sum(p => p.UsdAtPaymentRate!.Value);
                if (summary.PaidUsdAtPaymentRate.HasValue && summary.PaidUsdAtContractRate.HasValue)
                    summary.ExchangeGainLoss = summary.PaidUsdAtPaymentRate.Value - summary.PaidUsdAtContractRate.Value;
            }

            viewModel.Contracts.Add(summary);
        }

        viewModel.TotalPlannedUsd = viewModel.Contracts.Sum(c => c.PlannedUsd);
        viewModel.TotalPaidUsd = viewModel.Contracts.Sum(c => c.PaidUsd);
        viewModel.TotalContracts = viewModel.Contracts.Count;
        viewModel.TotalPayments = viewModel.Contracts.Sum(c => c.Payments.Count);

        return PartialView("_PaymentsData", viewModel);
    }

    /// <summary>
    /// Отчёт по индикаторам (каркас + затем AJAX)
    /// </summary>
    public IActionResult Indicators()
    {
        return View();
    }

    /// <summary>
    /// Данные отчёта по индикаторам (AJAX partial)
    /// </summary>
    public async Task<IActionResult> IndicatorsData()
    {
        var indicators = await _context.Indicators
            .Include(i => i.Category)
            .OrderBy(i => i.Code)
            .ToListAsync();

        var contractIndicators = await _context.ContractIndicators
            .Include(ci => ci.Contract)
                .ThenInclude(c => c.Contractor)
            .Include(ci => ci.Progresses)
            .ToListAsync();

        var viewModel = new IndicatorReportViewModel();

        foreach (var indicator in indicators)
        {
            var ciList = contractIndicators.Where(ci => ci.IndicatorId == indicator.Id).ToList();
            
            var row = new IndicatorReportRow
            {
                IndicatorId = indicator.Id,
                Code = indicator.Code,
                Name = indicator.NameRu,
                Unit = indicator.Unit,
                CategoryName = indicator.Category?.Name,
                TotalTarget = ciList.Sum(ci => ci.TargetValue),
                TotalAchieved = ciList.Sum(ci => ci.AchievedValue),
                Contracts = ciList.Select(ci => new IndicatorContractDetail
                {
                    ContractId = ci.ContractId,
                    ContractNumber = ci.Contract?.ContractNumber ?? "—",
                    ContractorName = ci.Contract?.Contractor?.Name ?? "—",
                    TargetValue = ci.TargetValue,
                    AchievedValue = ci.AchievedValue
                }).ToList()
            };

            viewModel.Indicators.Add(row);
        }

        return PartialView("_IndicatorsData", viewModel);
    }

    /// <summary>
    /// Отчёт по KPI сотрудников (каркас + затем AJAX)
    /// </summary>
    public IActionResult EmployeeKpi()
    {
        return View();
    }

    /// <summary>
    /// Данные отчёта по KPI сотрудников (AJAX partial)
    /// </summary>
    public async Task<IActionResult> EmployeeKpiData()
    {
        var users = await _context.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync();

        var tasks = await _context.ProjectTasks.ToListAsync();
        var avrCounts = await _context.WorkProgresses
            .GroupBy(wp => wp.SubmittedByUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        var viewModel = new EmployeeKpiReportViewModel();

        foreach (var user in users)
        {
            var userTasks = tasks.Where(t => t.AssigneeId == user.Id).ToList();
            var avrCount = avrCounts.FirstOrDefault(a => a.UserId == user.Id)?.Count ?? 0;

            viewModel.Employees.Add(new EmployeeKpiRow
            {
                UserId = user.Id,
                FullName = $"{user.LastName} {user.FirstName}",
                Position = user.Position,
                Email = user.Email,
                TotalTasks = userTasks.Count,
                CompletedTasks = userTasks.Count(t => t.Status == ProjectTaskStatus.Completed),
                InProgressTasks = userTasks.Count(t => t.Status == ProjectTaskStatus.InProgress),
                OverdueTasks = userTasks.Count(t => t.IsOverdue),
                AvrCount = avrCount
            });
        }

        return PartialView("_EmployeeKpiData", viewModel);
    }

    #region Helpers

    private static string GetTypeName(PaymentType type) => type switch
    {
        PaymentType.Advance => "Аванс",
        PaymentType.Interim => "Промежуточный",
        PaymentType.Final => "Окончательный",
        PaymentType.Retention => "Удержание",
        _ => type.ToString()
    };

    private static string GetStatusName(PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => "Ожидает",
        PaymentStatus.Approved => "Одобрен",
        PaymentStatus.Paid => "Оплачен",
        PaymentStatus.Rejected => "Отклонён",
        _ => status.ToString()
    };

    private static string GetStatusClass(PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => "bg-warning text-dark",
        PaymentStatus.Approved => "bg-info text-white",
        PaymentStatus.Paid => "bg-success",
        PaymentStatus.Rejected => "bg-danger",
        _ => "bg-secondary"
    };

    #endregion
}
