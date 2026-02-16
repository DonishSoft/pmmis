using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;
using PMMIS.Web.Services;

namespace PMMIS.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IManagementAlertService _alertService;

    public DashboardController(ApplicationDbContext context, IManagementAlertService alertService)
    {
        _context = context;
        _alertService = alertService;
    }

    public async Task<IActionResult> Index(int? projectId)
    {
        // All projects for selector
        var allProjects = await _context.Projects.OrderBy(p => p.Code).ToListAsync();
        ViewBag.AllProjects = allProjects;

        Project? project = null;
        
        if (projectId.HasValue)
        {
            project = await _context.Projects
                .Include(p => p.Components)
                .Include(p => p.Contracts)
                    .ThenInclude(c => c.Payments)
                .Include(p => p.Contracts)
                    .ThenInclude(c => c.Contractor)
                .FirstOrDefaultAsync(p => p.Id == projectId.Value);
        }
        else if (allProjects.Any())
        {
            // Default to first project
            project = await _context.Projects
                .Include(p => p.Components)
                .Include(p => p.Contracts)
                    .ThenInclude(c => c.Payments)
                .Include(p => p.Contracts)
                    .ThenInclude(c => c.Contractor)
                .FirstOrDefaultAsync(p => p.Id == allProjects.First().Id);
        }

        if (project == null)
        {
            return View("NoProject");
        }

        ViewBag.SelectedProjectId = project.Id;

        // Calculate summary statistics
        var totalBudget = project.TotalBudget;
        var totalContracts = project.Contracts.Count;
        var activeContracts = project.Contracts.Count(c => c.ContractEndDate > DateTime.Today);
        
        var totalContractValue = project.Contracts.Sum(c => c.FinalAmount);
        var totalPaid = project.Contracts.Sum(c => c.Payments.Where(p => p.Status == PaymentStatus.Paid).Sum(p => p.Amount));
        var disbursementRate = totalContractValue > 0 ? (totalPaid / totalContractValue) * 100 : 0;

        var averageProgress = project.Contracts.Any() 
            ? project.Contracts.Average(c => c.WorkCompletedPercent) 
            : 0;

        // Procurement stats
        var procurements = await _context.ProcurementPlans
            .Where(p => p.ProjectId == project.Id)
            .ToListAsync();
        
        var procurementTotal = procurements.Sum(p => p.EstimatedAmount);
        var procurementPlanned = procurements.Count(p => p.Status == ProcurementStatus.Planned);
        var procurementInProgress = procurements.Count(p => p.Status == ProcurementStatus.InProgress || p.Status == ProcurementStatus.Evaluation);
        var procurementCompleted = procurements.Count(p => p.Status == ProcurementStatus.Completed || p.Status == ProcurementStatus.Awarded);

        // Payment stats
        var allPayments = project.Contracts.SelectMany(c => c.Payments).ToList();
        var pendingPayments = allPayments.Where(p => p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Approved).Sum(p => p.Amount);

        // Upcoming deadlines (next 30 days)
        var upcomingDeadlines = project.Contracts
            .Where(c => c.ContractEndDate <= DateTime.Today.AddDays(30) && c.ContractEndDate >= DateTime.Today)
            .OrderBy(c => c.ContractEndDate)
            .Take(5)
            .ToList();

        // Overdue contracts
        var overdueContracts = project.Contracts
            .Where(c => c.ContractEndDate < DateTime.Today && c.WorkCompletedPercent < 100)
            .ToList();

        // Recent contracts
        var recentContracts = project.Contracts
            .OrderByDescending(c => c.CreatedAt)
            .Take(5)
            .ToList();
        
        // === MANAGEMENT ALERTS (KPI / RED FLAGS) ===
        var alerts = await _alertService.GetActiveAlertsAsync();
        var kpiSummary = await _alertService.GetKpiSummaryAsync();
        
        // Filter alerts for current project if needed
        var projectAlerts = alerts.Where(a => a.ProjectId == project.Id || a.ProjectId == null).Take(10).ToList();
        var criticalAlerts = projectAlerts.Where(a => a.Severity == AlertSeverity.Critical).ToList();
        var warningAlerts = projectAlerts.Where(a => a.Severity == AlertSeverity.Warning).ToList();
        
        ViewBag.ManagementAlerts = projectAlerts;
        ViewBag.CriticalAlertsCount = criticalAlerts.Count;
        ViewBag.WarningAlertsCount = warningAlerts.Count;
        ViewBag.KpiSummary = kpiSummary;
        // === END MANAGEMENT ALERTS ===

        ViewBag.Project = project;
        ViewBag.TotalBudget = totalBudget;
        ViewBag.TotalContracts = totalContracts;
        ViewBag.ActiveContracts = activeContracts;
        ViewBag.TotalContractValue = totalContractValue;
        ViewBag.TotalPaid = totalPaid;
        ViewBag.PendingPayments = pendingPayments;
        ViewBag.DisbursementRate = disbursementRate;
        ViewBag.AverageProgress = averageProgress;
        ViewBag.UpcomingDeadlines = upcomingDeadlines;
        ViewBag.OverdueContracts = overdueContracts;
        ViewBag.RecentContracts = recentContracts;
        
        // Procurement
        ViewBag.ProcurementTotal = procurementTotal;
        ViewBag.ProcurementPlanned = procurementPlanned;
        ViewBag.ProcurementInProgress = procurementInProgress;
        ViewBag.ProcurementCompleted = procurementCompleted;
        ViewBag.ProcurementCount = procurements.Count;

        return View();
    }
}

