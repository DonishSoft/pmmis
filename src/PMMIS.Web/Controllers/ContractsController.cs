
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;
using PMMIS.Web.Authorization;
using PMMIS.Web.Extensions;
using PMMIS.Web.Services;
using PMMIS.Web.ViewModels.Contracts;
using System.Security.Claims;
using System.Text.Json;

namespace PMMIS.Web.Controllers;

[Authorize]
[RequirePermission(MenuKeys.Contracts, PermissionType.View)]
public class ContractsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ITaskService _taskService;
    private readonly IFileService _fileService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDataAccessService _dataAccessService;

    public ContractsController(
        ApplicationDbContext context, 
        ITaskService taskService, 
        IFileService fileService,
        UserManager<ApplicationUser> userManager,
        IDataAccessService dataAccessService)
    {
        _context = context;
        _taskService = taskService;
        _fileService = fileService;
        _userManager = userManager;
        _dataAccessService = dataAccessService;
    }

    public async Task<IActionResult> Index()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var query = _context.Contracts
            .Include(c => c.Contractor)
            .Include(c => c.Project)
            .Include(c => c.SubComponent)
                .ThenInclude(sc => sc!.Component)
            .Include(c => c.Payments)
            .AsQueryable();

        // Фильтрация по видимости данных
        if (!await _dataAccessService.CanViewAllAsync(currentUser, MenuKeys.Contracts))
        {
            var roles = await _userManager.GetRolesAsync(currentUser);
            if (roles.Contains(UserRoles.Contractor))
            {
                query = query.Where(c => c.ContractorId == currentUser.ContractorId);
            }
            else
            {
                query = query.Where(c => c.CuratorId == currentUser.Id 
                                      || c.ProjectManagerId == currentUser.Id);
            }
        }

        var contracts = await query.OrderBy(c => c.ContractNumber).ToListAsync();
        return View(contracts);
    }

    /// <summary>
    /// Страница Contract Monitoring Report (6 таблиц)
    /// </summary>
    [RequirePermission(MenuKeys.Contracts, PermissionType.View)]
    public async Task<IActionResult> Report(int? projectId, DateTime? fromDate, DateTime? toDate)
    {
        var vm = new ContractReportViewModel
        {
            Projects = await _context.Projects.OrderBy(p => p.Code).ToListAsync(),
            SelectedProjectId = projectId,
            FromDate = fromDate,
            ToDate = toDate
        };

        var (allContracts, allProcurements) = await LoadReportDataAsync(projectId);

        // Table 1: On-Going Procurement (InProgress/Evaluation, without contract)
        vm.OnGoingProcurements = allProcurements
            .Where(pp => pp.ContractId == null && (pp.Status == ProcurementStatus.InProgress || pp.Status == ProcurementStatus.Evaluation))
            .ToList();

        // Table 2: Contracted Activities (Ongoing or DefectLiability)
        vm.ContractedActivities = allContracts
            .Where(c => c.Status == ContractStatus.Ongoing || c.Status == ContractStatus.DefectLiability)
            .ToList();

        // Table 3: Not Commenced / Pending Readiness (Planned, no contract)
        vm.NotCommencedActivities = allProcurements
            .Where(pp => pp.ContractId == null && pp.Status == ProcurementStatus.Planned)
            .ToList();

        // Table 4: Completed Activities
        vm.CompletedActivities = allContracts
            .Where(c => c.Status == ContractStatus.Completed)
            .ToList();

        // Table 5: Progress Summary
        vm.ProgressSummary = BuildProgressSummary(allContracts);

        // Table 5 — Mission comparison: contracts signed in the date range
        if (fromDate.HasValue && toDate.HasValue)
        {
            var from = fromDate.Value.Date;
            var to = toDate.Value.Date.AddDays(1);

            // Procurement processing started in the period
            var procInPeriod = allProcurements
                .Where(pp => pp.ContractId == null && pp.CreatedAt >= from && pp.CreatedAt < to)
                .Select(pp => new MissionProgressItem
                {
                    Stage = "Procurement Processing",
                    Description = $"{pp.ReferenceNo} — {pp.Description}",
                    Amount = pp.EstimatedAmount,
                    Status = pp.Status.ToString()
                }).ToList();

            // Contracts signed in the period
            var signedInPeriod = allContracts
                .Where(c => c.SigningDate >= from && c.SigningDate < to)
                .Select(c => new MissionProgressItem
                {
                    Stage = "Signed Contracts",
                    Description = $"{c.ContractNumber} — {c.Contractor?.Name}",
                    Amount = c.FinalAmount,
                    Status = c.Status switch
                    {
                        ContractStatus.Ongoing => "On-going",
                        ContractStatus.DefectLiability => "Defect Liability",
                        ContractStatus.Completed => "Completed",
                        ContractStatus.Suspended => "Suspended",
                        ContractStatus.Terminated => "Terminated",
                        _ => c.Status.ToString()
                    }
                }).ToList();

            // Contracts completed in the period
            var completedInPeriod = allContracts
                .Where(c => c.Status == ContractStatus.Completed && c.ActualCompletionDate.HasValue
                    && c.ActualCompletionDate.Value >= from && c.ActualCompletionDate.Value < to)
                .Select(c => new MissionProgressItem
                {
                    Stage = "Completed Contracts",
                    Description = $"{c.ContractNumber} — {c.Contractor?.Name}",
                    Amount = c.PaidAmount,
                    Status = "Completed"
                }).ToList();

            vm.ProgressSinceLastMission = procInPeriod.Concat(signedInPeriod).Concat(completedInPeriod).ToList();
        }

        // Table 6: Summary by Category (by ContractType)
        vm.CategorySummaries = BuildCategorySummaries(allContracts);

        return View(vm);
    }

    /// <summary>
    /// Экспорт отчёта в Excel
    /// </summary>
    [RequirePermission(MenuKeys.Contracts, PermissionType.View)]
    public async Task<IActionResult> ExportExcel(int? projectId, DateTime? fromDate, DateTime? toDate)
    {
        var (allContracts, allProcurements) = await LoadReportDataAsync(projectId);

        using var workbook = new ClosedXML.Excel.XLWorkbook();

        // --- Sheet 1: On-Going Procurement ---
        var onGoingProc = allProcurements
            .Where(pp => pp.ContractId == null && (pp.Status == ProcurementStatus.InProgress || pp.Status == ProcurementStatus.Evaluation))
            .ToList();
        var ws1 = workbook.Worksheets.Add("1. Procurement Processing");
        ws1.Cell(1, 1).Value = "No";
        ws1.Cell(1, 2).Value = "Ref. No";
        ws1.Cell(1, 3).Value = "Description";
        ws1.Cell(1, 4).Value = "Method";
        ws1.Cell(1, 5).Value = "Type";
        ws1.Cell(1, 6).Value = "Estimated Amount (USD)";
        ws1.Cell(1, 7).Value = "Advertisement Date";
        ws1.Cell(1, 8).Value = "Status";
        StyleExcelHeader(ws1, 1, 8);
        for (int i = 0; i < onGoingProc.Count; i++)
        {
            var pp = onGoingProc[i];
            ws1.Cell(i + 2, 1).Value = i + 1;
            ws1.Cell(i + 2, 2).Value = pp.ReferenceNo;
            ws1.Cell(i + 2, 3).Value = pp.Description;
            ws1.Cell(i + 2, 4).Value = pp.Method.ToString();
            ws1.Cell(i + 2, 5).Value = GetProcurementTypeText(pp.Type);
            ws1.Cell(i + 2, 6).Value = (double)pp.EstimatedAmount;
            ws1.Cell(i + 2, 6).Style.NumberFormat.Format = "#,##0.00";
            ws1.Cell(i + 2, 7).Value = pp.AdvertisementDate?.ToString("dd.MM.yyyy") ?? "";
            ws1.Cell(i + 2, 8).Value = GetProcurementStatusText(pp.Status);
        }
        ws1.Columns().AdjustToContents();

        // --- Sheet 2: Contracted Activities ---
        var contracted = allContracts
            .Where(c => c.Status == ContractStatus.Ongoing || c.Status == ContractStatus.DefectLiability)
            .ToList();
        var ws2 = workbook.Worksheets.Add("2. Contracted Activities");
        ws2.Cell(1, 1).Value = "No";
        ws2.Cell(1, 2).Value = "Contract No";
        ws2.Cell(1, 3).Value = "Contractor";
        ws2.Cell(1, 4).Value = "Type";
        ws2.Cell(1, 5).Value = "Contract Sum (USD)";
        ws2.Cell(1, 6).Value = "Paid (USD)";
        ws2.Cell(1, 7).Value = "Signing Date";
        ws2.Cell(1, 8).Value = "Deadline";
        ws2.Cell(1, 9).Value = "Remaining Days";
        ws2.Cell(1, 10).Value = "Work Completed %";
        ws2.Cell(1, 11).Value = "Status";
        StyleExcelHeader(ws2, 1, 11);
        for (int i = 0; i < contracted.Count; i++)
        {
            var c = contracted[i];
            ws2.Cell(i + 2, 1).Value = i + 1;
            ws2.Cell(i + 2, 2).Value = c.ContractNumber;
            ws2.Cell(i + 2, 3).Value = c.Contractor?.Name ?? "";
            ws2.Cell(i + 2, 4).Value = GetContractTypeText(c.Type);
            ws2.Cell(i + 2, 5).Value = (double)c.FinalAmount;
            ws2.Cell(i + 2, 5).Style.NumberFormat.Format = "#,##0.00";
            ws2.Cell(i + 2, 6).Value = (double)c.PaidAmount;
            ws2.Cell(i + 2, 6).Style.NumberFormat.Format = "#,##0.00";
            ws2.Cell(i + 2, 7).Value = c.SigningDate.ToString("dd.MM.yyyy");
            ws2.Cell(i + 2, 8).Value = c.ContractEndDate.ToString("dd.MM.yyyy");
            ws2.Cell(i + 2, 9).Value = c.RemainingDays;
            ws2.Cell(i + 2, 10).Value = (double)c.WorkCompletedPercent;
            ws2.Cell(i + 2, 10).Style.NumberFormat.Format = "0.0\"%\"";
            ws2.Cell(i + 2, 11).Value = GetContractStatusText(c.Status);
        }
        // Totals row
        var trRow = contracted.Count + 2;
        ws2.Cell(trRow, 1).Value = "";
        ws2.Cell(trRow, 4).Value = "TOTAL";
        ws2.Cell(trRow, 4).Style.Font.Bold = true;
        ws2.Cell(trRow, 5).Value = (double)contracted.Sum(c => c.FinalAmount);
        ws2.Cell(trRow, 5).Style.NumberFormat.Format = "#,##0.00";
        ws2.Cell(trRow, 5).Style.Font.Bold = true;
        ws2.Cell(trRow, 6).Value = (double)contracted.Sum(c => c.PaidAmount);
        ws2.Cell(trRow, 6).Style.NumberFormat.Format = "#,##0.00";
        ws2.Cell(trRow, 6).Style.Font.Bold = true;
        ws2.Columns().AdjustToContents();

        // --- Sheet 3: Not Commenced ---
        var notCommenced = allProcurements
            .Where(pp => pp.ContractId == null && pp.Status == ProcurementStatus.Planned)
            .ToList();
        var ws3 = workbook.Worksheets.Add("3. Not Commenced");
        ws3.Cell(1, 1).Value = "No";
        ws3.Cell(1, 2).Value = "Ref. No";
        ws3.Cell(1, 3).Value = "Description";
        ws3.Cell(1, 4).Value = "Type";
        ws3.Cell(1, 5).Value = "Estimated Amount (USD)";
        ws3.Cell(1, 6).Value = "Planned Bid Opening Date";
        ws3.Cell(1, 7).Value = "Comments";
        StyleExcelHeader(ws3, 1, 7);
        for (int i = 0; i < notCommenced.Count; i++)
        {
            var pp = notCommenced[i];
            ws3.Cell(i + 2, 1).Value = i + 1;
            ws3.Cell(i + 2, 2).Value = pp.ReferenceNo;
            ws3.Cell(i + 2, 3).Value = pp.Description;
            ws3.Cell(i + 2, 4).Value = GetProcurementTypeText(pp.Type);
            ws3.Cell(i + 2, 5).Value = (double)pp.EstimatedAmount;
            ws3.Cell(i + 2, 5).Style.NumberFormat.Format = "#,##0.00";
            ws3.Cell(i + 2, 6).Value = pp.PlannedBidOpeningDate?.ToString("dd.MM.yyyy") ?? "";
            ws3.Cell(i + 2, 7).Value = pp.Comments ?? "";
        }
        ws3.Columns().AdjustToContents();

        // --- Sheet 4: Completed ---
        var completed = allContracts.Where(c => c.Status == ContractStatus.Completed).ToList();
        var ws4 = workbook.Worksheets.Add("4. Completed Activities");
        ws4.Cell(1, 1).Value = "No";
        ws4.Cell(1, 2).Value = "Contract No";
        ws4.Cell(1, 3).Value = "Contractor";
        ws4.Cell(1, 4).Value = "Type";
        ws4.Cell(1, 5).Value = "Contract Sum (USD)";
        ws4.Cell(1, 6).Value = "Actual Amount (USD)";
        ws4.Cell(1, 7).Value = "Completion Date";
        ws4.Cell(1, 8).Value = "Performance Rating";
        StyleExcelHeader(ws4, 1, 8);
        for (int i = 0; i < completed.Count; i++)
        {
            var c = completed[i];
            ws4.Cell(i + 2, 1).Value = i + 1;
            ws4.Cell(i + 2, 2).Value = c.ContractNumber;
            ws4.Cell(i + 2, 3).Value = c.Contractor?.Name ?? "";
            ws4.Cell(i + 2, 4).Value = GetContractTypeText(c.Type);
            ws4.Cell(i + 2, 5).Value = (double)c.FinalAmount;
            ws4.Cell(i + 2, 5).Style.NumberFormat.Format = "#,##0.00";
            ws4.Cell(i + 2, 6).Value = (double)c.PaidAmount;
            ws4.Cell(i + 2, 6).Style.NumberFormat.Format = "#,##0.00";
            ws4.Cell(i + 2, 7).Value = c.ActualCompletionDate?.ToString("dd.MM.yyyy") ?? "";
            ws4.Cell(i + 2, 8).Value = GetPerformanceRatingText(c.PerformanceRating);
        }
        ws4.Columns().AdjustToContents();

        // --- Sheet 5: Progress Summary ---
        var summary = BuildProgressSummary(allContracts);
        var ws5 = workbook.Worksheets.Add("5. Progress Summary");
        ws5.Cell(1, 1).Value = "Contract Statistics";
        ws5.Cell(1, 1).Style.Font.Bold = true;
        ws5.Range(1, 1, 1, 2).Merge();
        ws5.Cell(2, 1).Value = "Total Contracts";    ws5.Cell(2, 2).Value = summary.TotalContracts;
        ws5.Cell(3, 1).Value = "Ongoing / Defect Liability";  ws5.Cell(3, 2).Value = summary.OngoingContracts;
        ws5.Cell(4, 1).Value = "Completed";           ws5.Cell(4, 2).Value = summary.CompletedContracts;
        ws5.Cell(5, 1).Value = "Suspended";            ws5.Cell(5, 2).Value = summary.SuspendedContracts;
        ws5.Cell(6, 1).Value = "Terminated";           ws5.Cell(6, 2).Value = summary.TerminatedContracts;
        ws5.Cell(8, 1).Value = "Financial Summary";
        ws5.Cell(8, 1).Style.Font.Bold = true;
        ws5.Range(8, 1, 8, 2).Merge();
        ws5.Cell(9, 1).Value = "Total Contract Amount";   ws5.Cell(9, 2).Value = (double)summary.TotalContractAmount; ws5.Cell(9, 2).Style.NumberFormat.Format = "#,##0.00";
        ws5.Cell(10, 1).Value = "Total Paid Amount";      ws5.Cell(10, 2).Value = (double)summary.TotalPaidAmount; ws5.Cell(10, 2).Style.NumberFormat.Format = "#,##0.00";
        ws5.Cell(11, 1).Value = "Average Work Completed %"; ws5.Cell(11, 2).Value = (double)summary.AverageWorkCompleted; ws5.Cell(11, 2).Style.NumberFormat.Format = "0.0\"%\"";
        ws5.Columns().AdjustToContents();

        // --- Sheet 6: Summary by Category ---
        var categories = BuildCategorySummaries(allContracts);
        var ws6 = workbook.Worksheets.Add("6. Summary by Category");
        ws6.Cell(1, 1).Value = "Category";
        ws6.Cell(1, 2).Value = "Contracts";
        ws6.Cell(1, 3).Value = "Total Amount (USD)";
        ws6.Cell(1, 4).Value = "Paid (USD)";
        ws6.Cell(1, 5).Value = "Disbursement %";
        ws6.Cell(1, 6).Value = "Avg Completion %";
        StyleExcelHeader(ws6, 1, 6);
        for (int i = 0; i < categories.Count; i++)
        {
            var cs = categories[i];
            ws6.Cell(i + 2, 1).Value = cs.CategoryName;
            ws6.Cell(i + 2, 2).Value = cs.Count;
            ws6.Cell(i + 2, 3).Value = (double)cs.TotalAmount;
            ws6.Cell(i + 2, 3).Style.NumberFormat.Format = "#,##0.00";
            ws6.Cell(i + 2, 4).Value = (double)cs.PaidAmount;
            ws6.Cell(i + 2, 4).Style.NumberFormat.Format = "#,##0.00";
            var disbursement = cs.TotalAmount > 0 ? (double)(cs.PaidAmount / cs.TotalAmount * 100) : 0;
            ws6.Cell(i + 2, 5).Value = disbursement;
            ws6.Cell(i + 2, 5).Style.NumberFormat.Format = "0.0\"%\"";
            ws6.Cell(i + 2, 6).Value = (double)cs.AverageCompletion;
            ws6.Cell(i + 2, 6).Style.NumberFormat.Format = "0.0\"%\"";
        }
        ws6.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var fileName = $"Contract_Monitoring_Report_{DateTime.Today:yyyy-MM-dd}.xlsx";
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    /// <summary>
    /// Экспорт отчёта в Word (HTML→DOC)
    /// </summary>
    [RequirePermission(MenuKeys.Contracts, PermissionType.View)]
    public async Task<IActionResult> ExportWord(int? projectId, DateTime? fromDate, DateTime? toDate)
    {
        var (allContracts, allProcurements) = await LoadReportDataAsync(projectId);
        var summary = BuildProgressSummary(allContracts);
        var categories = BuildCategorySummaries(allContracts);

        var onGoingProc = allProcurements
            .Where(pp => pp.ContractId == null && (pp.Status == ProcurementStatus.InProgress || pp.Status == ProcurementStatus.Evaluation)).ToList();
        var contracted = allContracts
            .Where(c => c.Status == ContractStatus.Ongoing || c.Status == ContractStatus.DefectLiability).ToList();
        var notCommenced = allProcurements
            .Where(pp => pp.ContractId == null && pp.Status == ProcurementStatus.Planned).ToList();
        var completed = allContracts.Where(c => c.Status == ContractStatus.Completed).ToList();

        var html = $@"
<html xmlns:o='urn:schemas-microsoft-com:office:office' xmlns:w='urn:schemas-microsoft-com:office:word'>
<head><meta charset='utf-8'><title>Contract Monitoring Report</title>
<style>
    body {{ font-family: 'Times New Roman', serif; font-size: 11pt; }}
    h1 {{ text-align: center; color: #1a3763; font-size: 16pt; text-transform: uppercase; }}
    h2 {{ color: #1a3763; font-size: 13pt; margin-top: 20pt; border-bottom: 1px solid #1a3763; padding-bottom: 4pt; }}
    h3 {{ text-align: center; font-size: 12pt; color: #333; }}
    table {{ border-collapse: collapse; width: 100%; margin-top: 8pt; }}
    th {{ background-color: #d9e2f3; border: 1px solid #999; padding: 4pt 6pt; text-align: left; font-weight: bold; }}
    td {{ border: 1px solid #999; padding: 4pt 6pt; }}
    .num {{ text-align: right; }}
    .bold {{ font-weight: bold; }}
    .total-row td {{ font-weight: bold; background-color: #f2f2f2; }}
</style>
</head><body>
<h1>Contract Monitoring Report</h1>
<p style='text-align:center;'>Report Date: {DateTime.Today:dd.MM.yyyy}</p>

<h2>1. On-Going Procurement Processing</h2>";

        if (onGoingProc.Count > 0)
        {
            html += "<table><tr><th>No</th><th>Ref. No</th><th>Description</th><th>Method</th><th>Type</th><th>Estimated Amount (USD)</th><th>Advertisement Date</th><th>Status</th></tr>";
            for (int i = 0; i < onGoingProc.Count; i++)
            {
                var pp = onGoingProc[i];
                html += $"<tr><td>{i + 1}</td><td>{pp.ReferenceNo}</td><td>{pp.Description}</td><td>{pp.Method}</td><td>{GetProcurementTypeText(pp.Type)}</td><td class='num'>{pp.EstimatedAmount:N2}</td><td>{pp.AdvertisementDate?.ToString("dd.MM.yyyy") ?? "—"}</td><td>{GetProcurementStatusText(pp.Status)}</td></tr>";
            }
            html += "</table>";
        }
        else
            html += "<p><em>No active procurements in process</em></p>";

        html += "<h2>2. Contracted Activities</h2>";
        if (contracted.Count > 0)
        {
            html += "<table><tr><th>No</th><th>Contract No</th><th>Contractor</th><th>Type</th><th>Sum (USD)</th><th>Paid (USD)</th><th>Signing Date</th><th>Deadline</th><th>Work %</th><th>Status</th></tr>";
            for (int i = 0; i < contracted.Count; i++)
            {
                var c = contracted[i];
                html += $"<tr><td>{i + 1}</td><td>{c.ContractNumber}</td><td>{c.Contractor?.Name}</td><td>{GetContractTypeText(c.Type)}</td><td class='num'>{c.FinalAmount:N2}</td><td class='num'>{c.PaidAmount:N2}</td><td>{c.SigningDate:dd.MM.yyyy}</td><td>{c.ContractEndDate:dd.MM.yyyy}</td><td class='num'>{c.WorkCompletedPercent:N1}%</td><td>{GetContractStatusText(c.Status)}</td></tr>";
            }
            html += $"<tr class='total-row'><td colspan='4'>TOTAL</td><td class='num'>{contracted.Sum(c => c.FinalAmount):N2}</td><td class='num'>{contracted.Sum(c => c.PaidAmount):N2}</td><td colspan='4'></td></tr></table>";
        }
        else
            html += "<p><em>No active contracts</em></p>";

        html += "<h2>3. Activities Not Commenced / Pending Readiness</h2>";
        if (notCommenced.Count > 0)
        {
            html += "<table><tr><th>No</th><th>Ref. No</th><th>Description</th><th>Type</th><th>Estimated Amount (USD)</th><th>Planned Bid Opening</th><th>Comments</th></tr>";
            for (int i = 0; i < notCommenced.Count; i++)
            {
                var pp = notCommenced[i];
                html += $"<tr><td>{i + 1}</td><td>{pp.ReferenceNo}</td><td>{pp.Description}</td><td>{GetProcurementTypeText(pp.Type)}</td><td class='num'>{pp.EstimatedAmount:N2}</td><td>{pp.PlannedBidOpeningDate?.ToString("dd.MM.yyyy") ?? "—"}</td><td>{pp.Comments ?? "—"}</td></tr>";
            }
            html += "</table>";
        }
        else
            html += "<p><em>All procurements have commenced</em></p>";

        html += "<h2>4. Completed Activities</h2>";
        if (completed.Count > 0)
        {
            html += "<table><tr><th>No</th><th>Contract No</th><th>Contractor</th><th>Type</th><th>Contract Sum (USD)</th><th>Actual Amount (USD)</th><th>Completion Date</th><th>Rating</th></tr>";
            for (int i = 0; i < completed.Count; i++)
            {
                var c = completed[i];
                html += $"<tr><td>{i + 1}</td><td>{c.ContractNumber}</td><td>{c.Contractor?.Name}</td><td>{GetContractTypeText(c.Type)}</td><td class='num'>{c.FinalAmount:N2}</td><td class='num'>{c.PaidAmount:N2}</td><td>{c.ActualCompletionDate?.ToString("dd.MM.yyyy") ?? "—"}</td><td>{GetPerformanceRatingText(c.PerformanceRating)}</td></tr>";
            }
            html += $"<tr class='total-row'><td colspan='4'>TOTAL</td><td class='num'>{completed.Sum(c => c.FinalAmount):N2}</td><td class='num'>{completed.Sum(c => c.PaidAmount):N2}</td><td colspan='2'></td></tr></table>";
        }
        else
            html += "<p><em>No completed contracts</em></p>";

        html += $@"
<h2>5. Progress Summary</h2>
<table>
    <tr><th colspan='2'>Contract Statistics</th></tr>
    <tr><td>Total Contracts</td><td class='num bold'>{summary.TotalContracts}</td></tr>
    <tr><td>Ongoing / Defect Liability</td><td class='num bold'>{summary.OngoingContracts}</td></tr>
    <tr><td>Completed</td><td class='num bold'>{summary.CompletedContracts}</td></tr>
    <tr><td>Suspended</td><td class='num bold'>{summary.SuspendedContracts}</td></tr>
    <tr><td>Terminated</td><td class='num bold'>{summary.TerminatedContracts}</td></tr>
</table>
<table>
    <tr><th colspan='2'>Financial Summary</th></tr>
    <tr><td>Total Contract Amount</td><td class='num bold'>${summary.TotalContractAmount:N2}</td></tr>
    <tr><td>Total Paid Amount</td><td class='num bold'>${summary.TotalPaidAmount:N2}</td></tr>
    <tr><td>Average Work Completed</td><td class='num bold'>{summary.AverageWorkCompleted:N1}%</td></tr>
</table>

<h2>6. Summary by Category</h2>
<table><tr><th>Category</th><th>Contracts</th><th>Total Amount (USD)</th><th>Paid (USD)</th><th>Disbursement %</th><th>Avg Completion %</th></tr>";

        foreach (var cs in categories)
        {
            var disb = cs.TotalAmount > 0 ? (cs.PaidAmount / cs.TotalAmount * 100) : 0;
            html += $"<tr><td class='bold'>{cs.CategoryName}</td><td class='num'>{cs.Count}</td><td class='num'>{cs.TotalAmount:N2}</td><td class='num'>{cs.PaidAmount:N2}</td><td class='num'>{disb:N1}%</td><td class='num'>{cs.AverageCompletion:N1}%</td></tr>";
        }
        html += $"<tr class='total-row'><td>TOTAL</td><td class='num'>{categories.Sum(c => c.Count)}</td><td class='num'>{categories.Sum(c => c.TotalAmount):N2}</td><td class='num'>{categories.Sum(c => c.PaidAmount):N2}</td><td colspan='2'></td></tr></table>";
        html += "</body></html>";

        var bytes = System.Text.Encoding.UTF8.GetBytes(html);
        var fileName = $"Contract_Monitoring_Report_{DateTime.Today:yyyy-MM-dd}.doc";
        return File(bytes, "application/msword", fileName);
    }

    // ===== Report helper methods =====

    private async Task<(List<Contract> contracts, List<ProcurementPlan> procurements)> LoadReportDataAsync(int? projectId)
    {
        var contractsQuery = _context.Contracts
            .Include(c => c.Contractor)
            .Include(c => c.Project)
            .Include(c => c.SubComponent).ThenInclude(sc => sc!.Component)
            .Include(c => c.Payments)
            .Include(c => c.ProcurementPlan)
            .Include(c => c.Amendments)
            .AsQueryable();

        var procurementsQuery = _context.ProcurementPlans
            .Include(pp => pp.Project)
            .Include(pp => pp.SubComponent).ThenInclude(sc => sc!.Component)
            .AsQueryable();

        if (projectId.HasValue)
        {
            contractsQuery = contractsQuery.Where(c => c.ProjectId == projectId.Value);
            procurementsQuery = procurementsQuery.Where(pp => pp.ProjectId == projectId.Value);
        }

        var contracts = await contractsQuery.OrderBy(c => c.ContractNumber).ToListAsync();
        var procurements = await procurementsQuery.OrderBy(pp => pp.ReferenceNo).ToListAsync();
        return (contracts, procurements);
    }

    private static ReportProgressSummary BuildProgressSummary(List<Contract> allContracts) => new()
    {
        TotalContracts = allContracts.Count,
        OngoingContracts = allContracts.Count(c => c.Status == ContractStatus.Ongoing || c.Status == ContractStatus.DefectLiability),
        CompletedContracts = allContracts.Count(c => c.Status == ContractStatus.Completed),
        SuspendedContracts = allContracts.Count(c => c.Status == ContractStatus.Suspended),
        TerminatedContracts = allContracts.Count(c => c.Status == ContractStatus.Terminated),
        TotalContractAmount = allContracts.Sum(c => c.FinalAmount),
        TotalPaidAmount = allContracts.Sum(c => c.PaidAmount),
        AverageWorkCompleted = allContracts.Count > 0 ? allContracts.Average(c => c.WorkCompletedPercent) : 0
    };

    private static List<CategorySummary> BuildCategorySummaries(List<Contract> allContracts) => allContracts
        .GroupBy(c => c.Type)
        .Select(g => new CategorySummary
        {
            CategoryName = g.Key switch
            {
                ContractType.Works => "Строительные работы",
                ContractType.Consulting => "Консультационные услуги",
                ContractType.Goods => "Товары",
                _ => "Другое"
            },
            Count = g.Count(),
            TotalAmount = g.Sum(c => c.FinalAmount),
            PaidAmount = g.Sum(c => c.PaidAmount),
            AverageCompletion = g.Average(c => c.WorkCompletedPercent)
        })
        .OrderByDescending(cs => cs.TotalAmount)
        .ToList();

    private static void StyleExcelHeader(ClosedXML.Excel.IXLWorksheet ws, int row, int colCount)
    {
        for (int c = 1; c <= colCount; c++)
        {
            ws.Cell(row, c).Style.Font.Bold = true;
            ws.Cell(row, c).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#D9E2F3");
            ws.Cell(row, c).Style.Border.BottomBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
        }
    }

    private static string GetContractTypeText(ContractType type) => type switch
    {
        ContractType.Works => "Works",
        ContractType.Consulting => "Consulting",
        ContractType.Goods => "Goods",
        _ => "Other"
    };

    private static string GetProcurementTypeText(ProcurementType type) => type switch
    {
        ProcurementType.Works => "Works",
        ProcurementType.ConsultingServices => "Consulting",
        ProcurementType.Goods => "Goods",
        ProcurementType.NonConsultingServices => "Non-Consulting Services",
        _ => "Other"
    };

    private static string GetContractStatusText(ContractStatus status) => status switch
    {
        ContractStatus.Ongoing => "On-going",
        ContractStatus.DefectLiability => "Defect Liability",
        ContractStatus.Completed => "Completed",
        ContractStatus.Suspended => "Suspended",
        ContractStatus.Terminated => "Terminated",
        _ => status.ToString()
    };

    private static string GetProcurementStatusText(ProcurementStatus status) => status switch
    {
        ProcurementStatus.InProgress => "In Progress",
        ProcurementStatus.Evaluation => "Evaluation",
        ProcurementStatus.Planned => "Planned",
        _ => status.ToString()
    };

    private static string GetPerformanceRatingText(PerformanceRating? rating) => rating switch
    {
        PerformanceRating.HighlySatisfactory => "Highly Satisfactory",
        PerformanceRating.Satisfactory => "Satisfactory",
        PerformanceRating.Unsatisfactory => "Unsatisfactory",
        _ => "—"
    };

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var contract = await _context.Contracts
            .Include(c => c.Contractor)
            .Include(c => c.Project)
            .Include(c => c.SubComponent)
                .ThenInclude(sc => sc!.Component)
            .Include(c => c.Payments)
            .Include(c => c.WorkProgresses)
            .Include(c => c.Documents)
            .Include(c => c.Amendments.OrderByDescending(a => a.AmendmentDate))
                .ThenInclude(a => a.Documents)
            .Include(c => c.Amendments)
                .ThenInclude(a => a.CreatedBy)
            .Include(c => c.Milestones.OrderBy(m => m.SortOrder))
            .Include(c => c.Curator)
            .Include(c => c.ProjectManager)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (contract == null)
        {
            return NotFound();
        }

        return View(contract);
    }

    [RequirePermission(MenuKeys.Contracts, PermissionType.Create)]
    public async Task<IActionResult> Create()
    {
        var viewModel = new ContractFormViewModel
        {
            Contract = new Contract
            {
                SigningDate = DateTime.Today,
                ContractEndDate = DateTime.Today.AddYears(1)
            },
            Contractors = await _context.Contractors.ToListAsync(),
            Projects = await _context.Projects.ToListAsync(),
            SubComponents = await _context.SubComponents.Include(sc => sc.Component).ToListAsync(),
            ProcurementPlans = await _context.ProcurementPlans
                .Include(pp => pp.SubComponent).ThenInclude(sc => sc!.Component)
                .Where(pp => pp.ContractId == null)
                .OrderBy(pp => pp.ReferenceNo)
                .ToListAsync(),
            Indicators = await _context.Indicators
                .Where(i => i.ParentIndicatorId == null) // Only parent indicators
                .Include(i => i.Category)
                .OrderBy(i => i.Category!.SortOrder)
                .ThenBy(i => i.SortOrder)
                .ToListAsync(),
            Users = await _context.Users.Where(u => u.IsActive).ToListAsync()
        };
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Contracts, PermissionType.Create)]
    public async Task<IActionResult> Create(ContractFormViewModel viewModel)
    {
        // Remove navigation property validation errors — form only sends FK IDs
        ModelState.Remove("Contract.Project");
        ModelState.Remove("Contract.Contractor");
        ModelState.Remove("Contract.ProcurementPlan");
        ModelState.Remove("Contract.Curator");
        ModelState.Remove("Contract.ProjectManager");
        ModelState.Remove("Contract.SubComponent");
        // Optional multilingual fields
        ModelState.Remove("Contract.ScopeOfWorkTj");
        ModelState.Remove("Contract.ScopeOfWorkEn");
        
        // Server-side validation: Completed requires completion fields
        if (viewModel.Contract.Status == ContractStatus.Completed)
        {
            if (!viewModel.Contract.ActualCompletionDate.HasValue)
                ModelState.AddModelError("Contract.ActualCompletionDate", "Дата завершения обязательна при статусе «Завершён»");
            if (!viewModel.Contract.PerformanceRating.HasValue)
                ModelState.AddModelError("Contract.PerformanceRating", "Оценка исполнения обязательна при статусе «Завершён»");
        }
        else
        {
            viewModel.Contract.ActualCompletionDate = null;
            viewModel.Contract.PerformanceRating = null;
        }
        
        // DEBUG: Log all remaining ModelState errors
        foreach (var entry in ModelState.Where(e => e.Value?.Errors.Count > 0))
        {
            foreach (var error in entry.Value!.Errors)
            {
                Console.WriteLine($"[DEBUG CONTRACT CREATE] Key='{entry.Key}' Error='{error.ErrorMessage}' Exception='{error.Exception?.Message}'");
            }
        }
        
        if (ModelState.IsValid)
        {
            viewModel.Contract.CreatedAt = DateTime.UtcNow;
            
            // Server-side fallback: if TJS currency and USD wasn't calculated by JS
            if (viewModel.Contract.Currency == ContractCurrency.TJS 
                && viewModel.Contract.ExchangeRate > 0 
                && viewModel.Contract.AmountTjs > 0 
                && viewModel.Contract.ContractAmount <= 0)
            {
                viewModel.Contract.ContractAmount = Math.Round(viewModel.Contract.AmountTjs.Value / viewModel.Contract.ExchangeRate.Value, 2);
            }
            
            // Convert dates to UTC for PostgreSQL
            viewModel.Contract.SigningDate = viewModel.Contract.SigningDate.ToUtc();
            viewModel.Contract.ContractEndDate = viewModel.Contract.ContractEndDate.ToUtc();
            viewModel.Contract.ExtendedToDate = viewModel.Contract.ExtendedToDate.ToUtc();
            viewModel.Contract.ActualCompletionDate = viewModel.Contract.ActualCompletionDate.ToUtc();
            
            _context.Add(viewModel.Contract);
            await _context.SaveChangesAsync();
            
            // Link procurement plan to this contract
            if (viewModel.Contract.ProcurementPlanId.HasValue)
            {
                var procPlan = await _context.ProcurementPlans.FindAsync(viewModel.Contract.ProcurementPlanId.Value);
                if (procPlan != null)
                {
                    procPlan.ContractId = viewModel.Contract.Id;
                    await _context.SaveChangesAsync();
                }
            }
            
            // Save milestones from JSON
            if (!string.IsNullOrEmpty(viewModel.MilestonesJson))
            {
                await SaveMilestones(viewModel.Contract.Id, viewModel.MilestonesJson);
            }
            
            // Upload categorized documents
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await UploadCategorizedDocuments(viewModel, viewModel.Contract.Id, userId);
            
            // Save selected indicators
            if (viewModel.SelectedIndicators?.Any() == true)
            {
                foreach (var indicatorInput in viewModel.SelectedIndicators.Where(i => i.IndicatorId > 0))
                {
                    var contractIndicator = new ContractIndicator
                    {
                        ContractId = viewModel.Contract.Id,
                        IndicatorId = indicatorInput.IndicatorId,
                        TargetValue = indicatorInput.TargetValue,
                        Notes = indicatorInput.Notes,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.ContractIndicators.Add(contractIndicator);
                    await _context.SaveChangesAsync();
                    
                    // Save village selections for geo-linked indicators
                    if (indicatorInput.VillageIds?.Any() == true)
                    {
                        foreach (var villageId in indicatorInput.VillageIds)
                        {
                            _context.ContractIndicatorVillages.Add(new ContractIndicatorVillage
                            {
                                ContractIndicatorId = contractIndicator.Id,
                                VillageId = villageId
                            });
                        }
                        await _context.SaveChangesAsync();
                    }
                }
            }
            
            // Auto-create task for contract monitoring
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(currentUserId))
            {
                // Assign to curator if set
                var assigneeId = viewModel.Contract.CuratorId ?? currentUserId;
                await _taskService.CreateAsync(new ProjectTask
                {
                    Title = $"Контроль контракта {viewModel.Contract.ContractNumber}",
                    Description = $"Мониторинг исполнения контракта: {viewModel.Contract.ScopeOfWork}",
                    Status = ProjectTaskStatus.New,
                    Priority = TaskPriority.Normal,
                    DueDate = viewModel.Contract.ContractEndDate.AddDays(-7),
                    AssigneeId = assigneeId,
                    AssignedById = currentUserId,
                    ContractId = viewModel.Contract.Id,
                    ProjectId = viewModel.Contract.ProjectId
                }, currentUserId);
            }
            
            TempData["Success"] = "Контракт успешно создан";
            return RedirectToAction(nameof(Index));
        }

        viewModel.Contractors = await _context.Contractors.ToListAsync();
        viewModel.Projects = await _context.Projects.ToListAsync();
        viewModel.SubComponents = await _context.SubComponents.Include(sc => sc.Component).ToListAsync();
        viewModel.ProcurementPlans = await _context.ProcurementPlans
            .Include(pp => pp.SubComponent).ThenInclude(sc => sc!.Component)
            .Where(pp => pp.ContractId == null)
            .OrderBy(pp => pp.ReferenceNo).ToListAsync();
        viewModel.Indicators = await _context.Indicators.Where(i => i.ParentIndicatorId == null).Include(i => i.Category).ToListAsync();
        viewModel.Users = await _context.Users.Where(u => u.IsActive).ToListAsync();
        return View(viewModel);
    }

    [RequirePermission(MenuKeys.Contracts, PermissionType.Edit)]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var contract = await _context.Contracts
            .Include(c => c.ContractIndicators)
                .ThenInclude(ci => ci.Indicator)
            .Include(c => c.ContractIndicators)
                .ThenInclude(ci => ci.Villages)
            .Include(c => c.Milestones.OrderBy(m => m.SortOrder))
            .Include(c => c.Documents)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (contract == null)
        {
            return NotFound();
        }

        var viewModel = new ContractFormViewModel
        {
            Contract = contract,
            Contractors = await _context.Contractors.ToListAsync(),
            Projects = await _context.Projects.ToListAsync(),
            SubComponents = await _context.SubComponents.Include(sc => sc.Component).ToListAsync(),
            ProcurementPlans = await _context.ProcurementPlans
                .Include(pp => pp.SubComponent).ThenInclude(sc => sc!.Component)
                .Where(pp => pp.ContractId == null || pp.ContractId == id)
                .OrderBy(pp => pp.ReferenceNo)
                .ToListAsync(),
            Indicators = await _context.Indicators
                .Where(i => i.ParentIndicatorId == null)
                .Include(i => i.Category)
                .OrderBy(i => i.Category!.SortOrder)
                .ThenBy(i => i.SortOrder)
                .ToListAsync(),
            ExistingContractIndicators = contract.ContractIndicators,
            Users = await _context.Users.Where(u => u.IsActive).ToListAsync(),
            MilestonesJson = JsonSerializer.Serialize(contract.Milestones.Select(m => new
            {
                m.Title, m.TitleTj, m.TitleEn, m.Description,
                DueDate = m.DueDate.ToString("yyyy-MM-dd"),
                Frequency = (int)m.Frequency, m.SortOrder
            }), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
            ExistingDocuments = contract.Documents.ToList()
        };
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Contracts, PermissionType.Edit)]
    public async Task<IActionResult> Edit(int id, ContractFormViewModel viewModel)
    {
        if (id != viewModel.Contract.Id)
        {
            return NotFound();
        }

        // Remove navigation property validation errors — form only sends FK IDs
        ModelState.Remove("Contract.Project");
        ModelState.Remove("Contract.Contractor");
        ModelState.Remove("Contract.ProcurementPlan");
        ModelState.Remove("Contract.Curator");
        ModelState.Remove("Contract.ProjectManager");
        ModelState.Remove("Contract.SubComponent");
        // Optional multilingual fields
        ModelState.Remove("Contract.ScopeOfWorkTj");
        ModelState.Remove("Contract.ScopeOfWorkEn");
        
        // Server-side validation: Completed requires completion fields
        if (viewModel.Contract.Status == ContractStatus.Completed)
        {
            if (!viewModel.Contract.ActualCompletionDate.HasValue)
                ModelState.AddModelError("Contract.ActualCompletionDate", "Дата завершения обязательна при статусе «Завершён»");
            if (!viewModel.Contract.PerformanceRating.HasValue)
                ModelState.AddModelError("Contract.PerformanceRating", "Оценка исполнения обязательна при статусе «Завершён»");
        }
        else
        {
            // Clear completion fields if status is not Completed
            viewModel.Contract.ActualCompletionDate = null;
            viewModel.Contract.PerformanceRating = null;
        }
        
        if (ModelState.IsValid)
        {
            try
            {
                // Preserve original CreatedAt from database (form doesn't send it)
                var existingCreatedAt = await _context.Contracts
                    .Where(c => c.Id == id)
                    .Select(c => c.CreatedAt)
                    .FirstOrDefaultAsync();
                    
                viewModel.Contract.CreatedAt = existingCreatedAt;
                viewModel.Contract.UpdatedAt = DateTime.UtcNow;
                
                // Server-side fallback: if TJS currency and USD wasn't calculated by JS
                if (viewModel.Contract.Currency == ContractCurrency.TJS 
                    && viewModel.Contract.ExchangeRate > 0 
                    && viewModel.Contract.AmountTjs > 0 
                    && viewModel.Contract.ContractAmount <= 0)
                {
                    viewModel.Contract.ContractAmount = Math.Round(viewModel.Contract.AmountTjs.Value / viewModel.Contract.ExchangeRate.Value, 2);
                }
                
                // Convert dates to UTC for PostgreSQL
                viewModel.Contract.SigningDate = viewModel.Contract.SigningDate.ToUtc();
                viewModel.Contract.ContractEndDate = viewModel.Contract.ContractEndDate.ToUtc();
                viewModel.Contract.ExtendedToDate = viewModel.Contract.ExtendedToDate.ToUtc();
                viewModel.Contract.ActualCompletionDate = viewModel.Contract.ActualCompletionDate.ToUtc();
                
                _context.Update(viewModel.Contract);
                
                // Update procurement plan link
                // Unlink old procurement plan if changed
                var oldProcPlan = await _context.ProcurementPlans
                    .FirstOrDefaultAsync(pp => pp.ContractId == id && pp.Id != (viewModel.Contract.ProcurementPlanId ?? 0));
                if (oldProcPlan != null)
                {
                    oldProcPlan.ContractId = null;
                }
                // Link new procurement plan
                if (viewModel.Contract.ProcurementPlanId.HasValue)
                {
                    var newProcPlan = await _context.ProcurementPlans.FindAsync(viewModel.Contract.ProcurementPlanId.Value);
                    if (newProcPlan != null)
                    {
                        newProcPlan.ContractId = id;
                    }
                }
                
                // Update contract indicators
                var existingIndicators = await _context.ContractIndicators
                    .Include(ci => ci.Villages)
                    .Where(ci => ci.ContractId == id)
                    .ToListAsync();
                
                // Remove old indicators that are not in the new list
                var newIndicatorIds = viewModel.SelectedIndicators?
                    .Where(i => i.IndicatorId > 0)
                    .Select(i => i.IndicatorId)
                    .ToHashSet() ?? new HashSet<int>();
                
                foreach (var existing in existingIndicators)
                {
                    if (!newIndicatorIds.Contains(existing.IndicatorId))
                    {
                        _context.ContractIndicators.Remove(existing);
                    }
                }
                
                // Add or update indicators
                if (viewModel.SelectedIndicators?.Any() == true)
                {
                foreach (var indicatorInput in viewModel.SelectedIndicators.Where(i => i.IndicatorId > 0))
                    {
                        var existing = existingIndicators.FirstOrDefault(e => e.IndicatorId == indicatorInput.IndicatorId);
                        if (existing != null)
                        {
                            // Update existing
                            existing.TargetValue = indicatorInput.TargetValue;
                            existing.Notes = indicatorInput.Notes;
                            existing.UpdatedAt = DateTime.UtcNow;
                            
                            // Update villages
                            _context.ContractIndicatorVillages.RemoveRange(existing.Villages);
                            if (indicatorInput.VillageIds?.Any() == true)
                            {
                                foreach (var villageId in indicatorInput.VillageIds)
                                {
                                    _context.ContractIndicatorVillages.Add(new ContractIndicatorVillage
                                    {
                                        ContractIndicatorId = existing.Id,
                                        VillageId = villageId
                                    });
                                }
                            }
                        }
                        else
                        {
                            // Add new
                            var newCi = new ContractIndicator
                            {
                                ContractId = id,
                                IndicatorId = indicatorInput.IndicatorId,
                                TargetValue = indicatorInput.TargetValue,
                                Notes = indicatorInput.Notes,
                                CreatedAt = DateTime.UtcNow
                            };
                            _context.ContractIndicators.Add(newCi);
                            await _context.SaveChangesAsync();
                            
                            // Save villages for new indicator
                            if (indicatorInput.VillageIds?.Any() == true)
                            {
                                foreach (var villageId in indicatorInput.VillageIds)
                                {
                                    _context.ContractIndicatorVillages.Add(new ContractIndicatorVillage
                                    {
                                        ContractIndicatorId = newCi.Id,
                                        VillageId = villageId
                                    });
                                }
                            }
                        }
                    }
                }
                
                // Update milestones (in-place to preserve FK from ProjectTasks)
                if (!string.IsNullOrEmpty(viewModel.MilestonesJson))
                {
                    try
                    {
                        var incoming = JsonSerializer.Deserialize<List<MilestoneInput>>(
                            viewModel.MilestonesJson, 
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        
                        if (incoming != null)
                        {
                            var existing = await _context.ContractMilestones
                                .Where(m => m.ContractId == id)
                                .OrderBy(m => m.SortOrder)
                                .ToListAsync();

                            for (int i = 0; i < incoming.Count; i++)
                            {
                                var m = incoming[i];
                                if (i < existing.Count)
                                {
                                    // Update existing milestone in-place (keeps its Id and FK refs)
                                    existing[i].Title = m.Title ?? "";
                                    existing[i].TitleTj = m.TitleTj;
                                    existing[i].TitleEn = m.TitleEn;
                                    existing[i].Description = m.Description;
                                    existing[i].DueDate = DateTime.Parse(m.DueDate).ToUniversalTime();
                                    existing[i].Frequency = (MilestoneFrequency)m.Frequency;
                                    existing[i].SortOrder = i;
                                    existing[i].UpdatedAt = DateTime.UtcNow;
                                }
                                else
                                {
                                    // Add new milestone
                                    _context.ContractMilestones.Add(new ContractMilestone
                                    {
                                        ContractId = id,
                                        Title = m.Title ?? "",
                                        TitleTj = m.TitleTj,
                                        TitleEn = m.TitleEn,
                                        Description = m.Description,
                                        DueDate = DateTime.Parse(m.DueDate).ToUniversalTime(),
                                        Frequency = (MilestoneFrequency)m.Frequency,
                                        SortOrder = i,
                                        CreatedAt = DateTime.UtcNow
                                    });
                                }
                            }

                            // Remove extras (only those beyond incoming count)
                            if (existing.Count > incoming.Count)
                            {
                                var toRemove = existing.Skip(incoming.Count).ToList();
                                _context.ContractMilestones.RemoveRange(toRemove);
                            }
                        }
                    }
                    catch (JsonException) { /* invalid JSON, skip */ }
                }
                
                // Upload categorized documents
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                await UploadCategorizedDocuments(viewModel, id, currentUserId);
                
                await _context.SaveChangesAsync();
                TempData["Success"] = "Контракт успешно обновлён";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Contracts.AnyAsync(e => e.Id == viewModel.Contract.Id))
                {
                    return NotFound();
                }
                throw;
            }
            return RedirectToAction(nameof(Index));
        }

        viewModel.Contractors = await _context.Contractors.ToListAsync();
        viewModel.Projects = await _context.Projects.ToListAsync();
        viewModel.SubComponents = await _context.SubComponents.Include(sc => sc.Component).ToListAsync();
        viewModel.ProcurementPlans = await _context.ProcurementPlans
            .Include(pp => pp.SubComponent).ThenInclude(sc => sc!.Component)
            .Where(pp => pp.ContractId == null || pp.ContractId == id)
            .OrderBy(pp => pp.ReferenceNo).ToListAsync();
        viewModel.Indicators = await _context.Indicators.Where(i => i.ParentIndicatorId == null).Include(i => i.Category).ToListAsync();
        viewModel.Users = await _context.Users.Where(u => u.IsActive).ToListAsync();
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Contracts, PermissionType.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var contract = await _context.Contracts.FindAsync(id);
        if (contract != null)
        {
            _context.Contracts.Remove(contract);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    // Grid data for Syncfusion DataGrid
    public async Task<IActionResult> GetContractsData()
    {
        var contracts = await _context.Contracts
            .Include(c => c.Contractor)
            .Include(c => c.Payments)
            .Select(c => new
            {
                c.Id,
                c.ContractNumber,
                c.ScopeOfWork,
                ContractorName = c.Contractor.Name,
                c.Type,
                c.SigningDate,
                c.ContractEndDate,
                c.ContractAmount,
                FinalAmount = c.ContractAmount + c.AdditionalAmount - c.SavedAmount,
                PaidAmount = c.Payments.Where(p => p.Status == PaymentStatus.Paid).Sum(p => p.Amount),
                c.WorkCompletedPercent,
                RemainingDays = (c.ExtendedToDate ?? c.ContractEndDate).Subtract(DateTime.Today).Days
            })
            .ToListAsync();

        return Json(contracts);
    }

    /// <summary>
    /// API: Returns allocated target values per indicator across all contracts
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetIndicatorAllocations(int? excludeContractId)
    {
        var query = _context.ContractIndicators.AsQueryable();
        if (excludeContractId.HasValue)
        {
            query = query.Where(ci => ci.ContractId != excludeContractId.Value);
        }

        var allocations = await query
            .GroupBy(ci => ci.IndicatorId)
            .Select(g => new { IndicatorId = g.Key, Allocated = g.Sum(ci => ci.TargetValue) })
            .ToListAsync();

        return Json(allocations);
    }
    
    /// <summary>
    /// API: Returns all villages grouped by district/jamoat with geographic data
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetVillagesData()
    {
        var districts = await _context.Districts
            .Include(d => d.Jamoats)
                .ThenInclude(j => j.Villages)
            .OrderBy(d => d.NameRu)
            .ToListAsync();
        
        // Get school and health facility counts per village
        var schoolCounts = await _context.Schools
            .GroupBy(s => s.VillageId)
            .Select(g => new { VillageId = g.Key, Count = g.Count(), TotalStudents = g.Sum(s => s.TotalStudents) })
            .ToDictionaryAsync(x => x.VillageId, x => new { x.Count, x.TotalStudents });
        
        var healthFacilityCounts = await _context.HealthFacilities
            .GroupBy(h => h.VillageId)
            .Select(g => new { VillageId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.VillageId, x => x.Count);
        
        var result = districts.Select(d => new
        {
            id = d.Id,
            name = d.NameRu,
            jamoats = d.Jamoats.OrderBy(j => j.NameRu).Select(j => new
            {
                id = j.Id,
                name = j.NameRu,
                villages = j.Villages.OrderBy(v => v.NameRu).Select(v => new
                {
                    id = v.Id,
                    name = v.NameRu,
                    population = v.PopulationCurrent,
                    femalePopulation = v.FemalePopulation,
                    households = v.HouseholdsCurrent,
                    schoolCount = schoolCounts.ContainsKey(v.Id) ? schoolCounts[v.Id].Count : 0,
                    healthFacilityCount = healthFacilityCounts.ContainsKey(v.Id) ? healthFacilityCounts[v.Id] : 0,
                    schoolStudents = schoolCounts.ContainsKey(v.Id) ? schoolCounts[v.Id].TotalStudents : 0
                })
            })
        });
        
        return Json(result);
    }
    
    /// <summary>
    /// API: Returns village IDs linked to contract indicators for a specific contract
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetContractIndicatorVillages(int contractId)
    {
        var data = await _context.ContractIndicatorVillages
            .Where(civ => civ.ContractIndicator.ContractId == contractId)
            .Select(civ => new { civ.ContractIndicator.IndicatorId, civ.VillageId })
            .ToListAsync();
        
        var result = data
            .GroupBy(x => x.IndicatorId)
            .Select(g => new { IndicatorId = g.Key, VillageIds = g.Select(x => x.VillageId).ToList() });
        
        return Json(result);
    }
    
    /// <summary>
    /// Удалить документ контракта
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Contracts, PermissionType.Edit)]
    public async Task<IActionResult> DeleteDocument(int documentId, int contractId)
    {
        await _fileService.DeleteFileAsync(documentId);
        TempData["Success"] = "Документ удалён";
        return RedirectToAction(nameof(Edit), new { id = contractId });
    }
    
    #region Private Helpers
    
    private async Task SaveMilestones(int contractId, string json)
    {
        try
        {
            var milestones = JsonSerializer.Deserialize<List<MilestoneInput>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (milestones != null)
            {
                int order = 0;
                foreach (var m in milestones)
                {
                    _context.ContractMilestones.Add(new ContractMilestone
                    {
                        ContractId = contractId,
                        Title = m.Title ?? "",
                        TitleTj = m.TitleTj,
                        TitleEn = m.TitleEn,
                        Description = m.Description,
                        DueDate = DateTime.Parse(m.DueDate).ToUniversalTime(),
                        Frequency = (MilestoneFrequency)(m.Frequency),
                        SortOrder = order++,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                await _context.SaveChangesAsync();
            }
        }
        catch (JsonException)
        {
            // Invalid JSON, skip
        }
    }
    
    private record MilestoneInput(string? Title, string? TitleTj, string? TitleEn, string? Description, string DueDate, int Frequency, int SortOrder);
    
    private async Task UploadCategorizedDocuments(ContractFormViewModel viewModel, int contractId, string? userId)
    {
        await UploadDocumentCategory(viewModel.SignedContractFiles, viewModel.SignedContractNames, 
            contractId, DocumentType.SignedContract, userId);
        await UploadDocumentCategory(viewModel.MandatoryDocumentFiles, viewModel.MandatoryDocumentNames, 
            contractId, DocumentType.MandatoryDocument, userId);
        await UploadDocumentCategory(viewModel.AdditionalDocumentFiles, viewModel.AdditionalDocumentNames, 
            contractId, DocumentType.AdditionalDocument, userId);
        await _context.SaveChangesAsync();
    }
    
    private async Task UploadDocumentCategory(List<IFormFile>? files, List<string>? names, 
        int contractId, DocumentType type, string? userId)
    {
        if (files == null || !files.Any()) return;
        
        for (int i = 0; i < files.Count; i++)
        {
            if (files[i].Length <= 0) continue;
            var doc = await _fileService.UploadFileAsync(files[i], $"contracts/{contractId}", type, userId);
            doc.ContractId = contractId;
            doc.Description = names != null && i < names.Count && !string.IsNullOrWhiteSpace(names[i]) 
                ? names[i] : null;
            _context.Documents.Add(doc);
        }
    }
    
    #endregion
    
    // =============================================
    //  CONTRACT AMENDMENTS (Поправки к контракту)
    // =============================================
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.ContractAmendments, PermissionType.Create)]
    public async Task<IActionResult> CreateAmendment(ContractAmendmentViewModel model)
    {
        var contract = await _context.Contracts
            .Include(c => c.Payments)
            .FirstOrDefaultAsync(c => c.Id == model.ContractId);
        if (contract == null)
            return NotFound();
        
        var userId = _userManager.GetUserId(User);
        var amendmentType = (AmendmentType)model.Type;
        
        // Create amendment record
        var amendment = new ContractAmendment
        {
            ContractId = contract.Id,
            Type = amendmentType,
            AmendmentDate = model.AmendmentDate,
            Description = model.Description,
            CreatedByUserId = userId
        };
        
        // === Apply amendment based on type ===
        switch (amendmentType)
        {
            case AmendmentType.AmountChange:
                amendment.AmountChangeTjs = model.AmountChangeTjs;
                amendment.ExchangeRate = model.ExchangeRate;
                amendment.AmountChangeUsd = model.AmountChangeUsd;
                // Update contract
                if (model.AmountChangeUsd.HasValue)
                    contract.AdditionalAmount += model.AmountChangeUsd.Value;
                break;
                
            case AmendmentType.DeadlineExtension:
                amendment.PreviousEndDate = contract.ExtendedToDate ?? contract.ContractEndDate;
                amendment.NewEndDate = model.NewEndDate;
                // Update contract
                if (model.NewEndDate.HasValue)
                    contract.ExtendedToDate = model.NewEndDate.Value;
                break;
                
            case AmendmentType.ScopeChange:
                // Amount change
                amendment.AmountChangeTjs = model.AmountChangeTjs;
                amendment.ExchangeRate = model.ExchangeRate;
                amendment.AmountChangeUsd = model.AmountChangeUsd;
                if (model.AmountChangeUsd.HasValue)
                    contract.AdditionalAmount += model.AmountChangeUsd.Value;
                    
                // Date change
                amendment.PreviousEndDate = contract.ExtendedToDate ?? contract.ContractEndDate;
                amendment.NewEndDate = model.NewEndDate;
                if (model.NewEndDate.HasValue)
                    contract.ExtendedToDate = model.NewEndDate.Value;
                    
                // Scope text
                amendment.NewScopeOfWork = model.NewScopeOfWork;
                if (!string.IsNullOrWhiteSpace(model.NewScopeOfWork))
                    contract.ScopeOfWork = model.NewScopeOfWork;
                break;
        }
        
        _context.ContractAmendments.Add(amendment);
        await _context.SaveChangesAsync();
        
        // Upload agreement document (required)
        if (model.AgreementFile != null && model.AgreementFile.Length > 0)
        {
            var doc = await _fileService.UploadFileAsync(model.AgreementFile, 
                $"contracts/{contract.Id}/amendments", DocumentType.AmendmentAgreement, userId);
            doc.ContractId = contract.Id;
            doc.ContractAmendmentId = amendment.Id;
            doc.Description = model.AgreementName ?? "Дополнительное соглашение";
            _context.Documents.Add(doc);
        }
        
        // Upload additional documents
        if (model.AdditionalFiles != null)
        {
            for (int i = 0; i < model.AdditionalFiles.Count; i++)
            {
                if (model.AdditionalFiles[i].Length <= 0) continue;
                var doc = await _fileService.UploadFileAsync(model.AdditionalFiles[i],
                    $"contracts/{contract.Id}/amendments", DocumentType.AdditionalDocument, userId);
                doc.ContractId = contract.Id;
                doc.ContractAmendmentId = amendment.Id;
                doc.Description = model.AdditionalNames != null && i < model.AdditionalNames.Count 
                    ? model.AdditionalNames[i] : null;
                _context.Documents.Add(doc);
            }
        }
        
        await _context.SaveChangesAsync();
        
        TempData["SuccessMessage"] = "Поправка к контракту успешно добавлена.";
        return RedirectToAction(nameof(Details), new { id = contract.Id });
    }
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.ContractAmendments, PermissionType.Delete)]
    public async Task<IActionResult> DeleteAmendment(int id)
    {
        var amendment = await _context.ContractAmendments
            .Include(a => a.Contract)
                .ThenInclude(c => c.Payments)
            .Include(a => a.Documents)
            .FirstOrDefaultAsync(a => a.Id == id);
            
        if (amendment == null)
            return NotFound();
            
        var contractId = amendment.ContractId;
        var contract = amendment.Contract;
        
        // Revert amendment effects
        switch (amendment.Type)
        {
            case AmendmentType.AmountChange:
                if (amendment.AmountChangeUsd.HasValue)
                    contract.AdditionalAmount -= amendment.AmountChangeUsd.Value;
                break;
                
            case AmendmentType.DeadlineExtension:
                if (amendment.PreviousEndDate.HasValue)
                    contract.ExtendedToDate = amendment.PreviousEndDate == contract.ContractEndDate 
                        ? null : amendment.PreviousEndDate;
                break;
                
            case AmendmentType.ScopeChange:
                if (amendment.AmountChangeUsd.HasValue)
                    contract.AdditionalAmount -= amendment.AmountChangeUsd.Value;
                if (amendment.PreviousEndDate.HasValue)
                    contract.ExtendedToDate = amendment.PreviousEndDate == contract.ContractEndDate 
                        ? null : amendment.PreviousEndDate;
                break;
        }
        
        _context.ContractAmendments.Remove(amendment);
        await _context.SaveChangesAsync();
        
        TempData["SuccessMessage"] = "Поправка удалена.";
        return RedirectToAction(nameof(Details), new { id = contractId });
    }
}
