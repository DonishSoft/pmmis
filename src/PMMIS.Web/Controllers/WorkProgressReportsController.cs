using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;
using PMMIS.Web.Authorization;
using PMMIS.Web.Extensions;
using PMMIS.Web.Services;
using PMMIS.Web.ViewModels.WorkProgress;
using System.Security.Claims;

namespace PMMIS.Web.Controllers;

/// <summary>
/// Реестр АВР (Актов выполненных работ)
/// </summary>
[Authorize]
[RequirePermission(MenuKeys.WorkProgressReports, PermissionType.View)]
public class WorkProgressReportsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ITaskService _taskService;
    private readonly IFileService _fileService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDataAccessService _dataAccessService;
    private readonly IWorkflowRoutingService _workflowRouting;

    public WorkProgressReportsController(
        ApplicationDbContext context, 
        ITaskService taskService, 
        IFileService fileService,
        UserManager<ApplicationUser> userManager,
        IDataAccessService dataAccessService,
        IWorkflowRoutingService workflowRouting)
    {
        _context = context;
        _taskService = taskService;
        _fileService = fileService;
        _userManager = userManager;
        _dataAccessService = dataAccessService;
        _workflowRouting = workflowRouting;
    }

    /// <summary>
    /// Реестр всех АВР с фильтрами
    /// </summary>
    public async Task<IActionResult> Index(int? contractId, int? projectId, DateTime? dateFrom, DateTime? dateTo)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var query = _context.WorkProgresses
            .Include(w => w.Contract)
                .ThenInclude(c => c.Contractor)
            .Include(w => w.Contract.Project)
            .Include(w => w.Documents)
            .AsQueryable();

        // Фильтрация по видимости данных
        if (!await _dataAccessService.CanViewAllAsync(currentUser, MenuKeys.WorkProgressReports))
        {
            var myContractIds = await _dataAccessService.GetUserContractIdsAsync(currentUser.Id);
            query = query.Where(w => myContractIds.Contains(w.ContractId) 
                                  || w.SubmittedByUserId == currentUser.Id);
        }

        // Filters
        if (contractId.HasValue)
            query = query.Where(w => w.ContractId == contractId.Value);
        
        if (projectId.HasValue)
            query = query.Where(w => w.Contract.ProjectId == projectId.Value);
        
        if (dateFrom.HasValue)
            query = query.Where(w => w.ReportDate >= dateFrom.Value.ToUtc());
        
        if (dateTo.HasValue)
            query = query.Where(w => w.ReportDate <= dateTo.Value.ToUtc());

        var items = await query
            .OrderByDescending(w => w.ReportDate)
            .ToListAsync();

        // Load dropdowns for filters
        ViewBag.Projects = new SelectList(
            await _context.Projects.OrderBy(p => p.Code).ToListAsync(),
            "Id", "Code");
        
        ViewBag.Contracts = new SelectList(
            await _context.Contracts
                .Include(c => c.Contractor)
                .OrderBy(c => c.ContractNumber)
                .Select(c => new { c.Id, Name = $"{c.ContractNumber} - {c.Contractor.Name}" })
                .ToListAsync(),
            "Id", "Name");

        ViewBag.SelectedContractId = contractId;
        ViewBag.SelectedProjectId = projectId;
        ViewBag.DateFrom = dateFrom;
        ViewBag.DateTo = dateTo;

        // Summary statistics
        ViewBag.TotalReports = items.Count;
        ViewBag.TotalContracts = items.Select(i => i.ContractId).Distinct().Count();
        ViewBag.AverageProgress = items.Any() ? items.Average(i => i.CompletedPercent) : 0;

        return View(items);
    }

    /// <summary>
    /// Детали АВР
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var progress = await _context.WorkProgresses
            .Include(w => w.Contract)
                .ThenInclude(c => c.Contractor)
            .Include(w => w.Contract.Project)
            .Include(w => w.Documents)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (progress == null) return NotFound();

        // Workflow info for approval buttons
        var stepInfo = await _workflowRouting.GetCurrentStepInfoAsync(id);
        var currentUser = await _userManager.GetUserAsync(User);
        var canApprove = currentUser != null && await _workflowRouting.CanUserActOnCurrentStepAsync(id, currentUser.Id);

        // Workflow history
        var history = await _context.WorkflowHistories
            .Where(h => h.WorkProgressId == id)
            .OrderBy(h => h.ActionDate)
            .ToListAsync();

        ViewBag.WorkflowStep = stepInfo;
        ViewBag.CanApprove = canApprove;
        ViewBag.WorkflowHistory = history;

        return View(progress);
    }

    /// <summary>
    /// Утвердить АВР (продвинуть workflow на следующий шаг)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        var canAct = await _workflowRouting.CanUserActOnCurrentStepAsync(id, userId);
        if (!canAct)
        {
            TempData["Error"] = "У вас нет прав для утверждения на данном шаге.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await _workflowRouting.AdvanceAsync(id, userId);

        TempData["Success"] = "АВР утверждён. Задача передана на следующий шаг.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Отклонить АВР (вернуть на указанный шаг)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string reason)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        var canAct = await _workflowRouting.CanUserActOnCurrentStepAsync(id, userId);
        if (!canAct)
        {
            TempData["Error"] = "У вас нет прав для отклонения на данном шаге.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["Error"] = "Укажите причину отклонения.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await _workflowRouting.RejectAsync(id, userId, reason);

        TempData["Success"] = "АВР отклонён. Задача на исправление назначена.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// API: Получить статус Workflow для АВР (AJAX)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetWorkflowStatus(int id)
    {
        var stepInfo = await _workflowRouting.GetCurrentStepInfoAsync(id);
        if (stepInfo == null)
            return Json(new { hasWorkflow = false });

        var currentUser = await _userManager.GetUserAsync(User);
        var canApprove = currentUser != null && await _workflowRouting.CanUserActOnCurrentStepAsync(id, currentUser.Id);

        return Json(new
        {
            hasWorkflow = true,
            stepOrder = stepInfo.StepOrder,
            stepName = stepInfo.StepName,
            actionType = stepInfo.ActionType,
            roleName = stepInfo.RoleName,
            totalSteps = stepInfo.TotalSteps,
            isCompleted = stepInfo.IsCompleted,
            canReject = stepInfo.CanReject,
            canApprove
        });
    }

    /// <summary>
    /// Форма создания АВР
    /// </summary>
    public async Task<IActionResult> Create(int? contractId)
    {
        var user = await _userManager.GetUserAsync(User);
        var canViewAll = user != null && await _dataAccessService.CanViewAllAsync(user, MenuKeys.WorkProgressReports);

        // If no contract specified and user has limited access, auto-select if they have only 1 contract
        if (!contractId.HasValue && !canViewAll && user != null)
        {
            var userContractIds = await _dataAccessService.GetUserContractIdsAsync(user.Id);
            if (userContractIds.Count == 1)
                contractId = userContractIds[0];
        }

        var viewModel = new WorkProgressFormViewModel
        {
            WorkProgress = new WorkProgress
            {
                ContractId = contractId ?? 0,
                ReportDate = DateTime.Today
            }
        };

        await PopulateViewModel(viewModel, contractId);
        return View(viewModel);
    }

    /// <summary>
    /// Сохранение АВР
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.WorkProgressReports, PermissionType.Create)]
    public async Task<IActionResult> Create(WorkProgressFormViewModel viewModel, List<IFormFile>? ReportFiles, List<IFormFile>? PhotoFiles)
    {
        ModelState.Remove("WorkProgress.Contract");
        ModelState.Remove("WorkProgress.Contractor");

        if (ModelState.IsValid)
        {
            var progress = viewModel.WorkProgress;
            progress.CreatedAt = DateTime.UtcNow;
            progress.ReportDate = progress.ReportDate.ToUtc();

            _context.WorkProgresses.Add(progress);
            await _context.SaveChangesAsync();

            // Save indicator progress
            if (viewModel.ContractIndicators != null)
            {
                foreach (var ci in viewModel.ContractIndicators.Where(i => i.Value > 0 || (i.GeoItems != null && i.GeoItems.Any(g => g.IsChecked))))
                {
                    // For geo-checklist indicators, calculate value from checked items
                    var value = ci.Value;
                    if (ci.GeoItems != null && ci.GeoItems.Any(g => g.IsChecked))
                    {
                        value = ci.GeoItems.Where(g => g.IsChecked).Sum(g => g.NumericValue);
                    }
                    
                    if (value <= 0) continue;

                    var indicatorProgress = new ContractIndicatorProgress
                    {
                        ContractIndicatorId = ci.ContractIndicatorId,
                        WorkProgressId = progress.Id,
                        Value = value,
                        Notes = ci.Notes,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.ContractIndicatorProgresses.Add(indicatorProgress);
                    await _context.SaveChangesAsync();

                    // Save geo checklist items
                    if (ci.GeoItems != null)
                    {
                        foreach (var geo in ci.GeoItems.Where(g => g.IsChecked))
                        {
                            _context.IndicatorProgressItems.Add(new IndicatorProgressItem
                            {
                                ContractIndicatorProgressId = indicatorProgress.Id,
                                ItemType = geo.ItemType,
                                VillageId = geo.ItemType == GeoItemType.Village ? geo.ItemId : null,
                                SchoolId = geo.ItemType == GeoItemType.School ? geo.ItemId : null,
                                HealthFacilityId = geo.ItemType == GeoItemType.HealthFacility ? geo.ItemId : null,
                                IsCompleted = true,
                                NumericValue = geo.NumericValue,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }
                }
                await _context.SaveChangesAsync();
                await UpdateIndicatorAchievedValues(progress.ContractId);
            }

            // Save work item progress
            if (viewModel.WorkItems?.Any() == true)
            {
                foreach (var wi in viewModel.WorkItems)
                {
                    if (wi.Value <= 0) continue;
                    _context.WorkItemProgresses.Add(new WorkItemProgress
                    {
                        ContractWorkItemId = wi.ContractWorkItemId,
                        WorkProgressId = progress.Id,
                        Value = wi.Value,
                        Notes = wi.Notes,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                await _context.SaveChangesAsync();
                await UpdateWorkItemAchievedValues(progress.ContractId);
            }

            // Save uploaded files
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var allFiles = new List<(IFormFile file, DocumentType type)>();
            if (ReportFiles != null)
                allFiles.AddRange(ReportFiles.Select(f => (f, DocumentType.Report)));
            if (PhotoFiles != null)
                allFiles.AddRange(PhotoFiles.Select(f => (f, DocumentType.Photo)));

            foreach (var (file, docType) in allFiles)
            {
                var doc = await _fileService.UploadFileAsync(file, "work-progress", docType, userId);
                doc.WorkProgressId = progress.Id;
                _context.Documents.Add(doc);
            }
            if (allFiles.Any())
                await _context.SaveChangesAsync();

            // Update contract's work completed percent
            await UpdateContractProgress(progress.ContractId);

            // Start workflow — automatically creates tasks/notifications for the next step
            progress.SubmittedByUserId = userId;
            await _context.SaveChangesAsync();
            await _workflowRouting.StartWorkflowAsync(progress.Id, userId ?? "");

            TempData["Success"] = "АВР создан. Задачи по цепочке утверждения назначены.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateViewModel(viewModel, viewModel.WorkProgress.ContractId);
        return View(viewModel);
    }

    /// <summary>
    /// Форма редактирования АВР
    /// </summary>
    [RequirePermission(MenuKeys.WorkProgressReports, PermissionType.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var progress = await _context.WorkProgresses
            .Include(w => w.Contract)
            .Include(w => w.Documents)
            .Include(w => w.IndicatorProgresses)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (progress == null) return NotFound();

        var viewModel = new WorkProgressFormViewModel
        {
            WorkProgress = progress,
            ExistingDocuments = progress.Documents.ToList()
        };

        await PopulateViewModel(viewModel, progress.ContractId);

        // Pre-fill indicator values from existing progress
        if (viewModel.ContractIndicators != null)
        {
            foreach (var ci in viewModel.ContractIndicators)
            {
                var existing = progress.IndicatorProgresses
                    .FirstOrDefault(p => p.ContractIndicatorId == ci.ContractIndicatorId);
                if (existing != null)
                {
                    ci.Value = existing.Value;
                    ci.Notes = existing.Notes;
                }
            }
        }

        return View(viewModel);
    }

    /// <summary>
    /// Обновление АВР
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.WorkProgressReports, PermissionType.Edit)]
    public async Task<IActionResult> Edit(int id, WorkProgressFormViewModel viewModel)
    {
        if (id != viewModel.WorkProgress.Id) return NotFound();

        ModelState.Remove("WorkProgress.Contract");
        ModelState.Remove("WorkProgress.Contractor");

        if (ModelState.IsValid)
        {
            var progress = viewModel.WorkProgress;
            progress.CreatedAt = DateTime.SpecifyKind(progress.CreatedAt, DateTimeKind.Utc);
            progress.UpdatedAt = DateTime.UtcNow;
            progress.ReportDate = progress.ReportDate.ToUtc();

            _context.Update(progress);
            await _context.SaveChangesAsync();

            // Update indicator progress
            var existingProgress = await _context.ContractIndicatorProgresses
                .Where(p => p.WorkProgressId == progress.Id)
                .ToListAsync();
            _context.ContractIndicatorProgresses.RemoveRange(existingProgress);

            if (viewModel.ContractIndicators != null)
            {
                foreach (var ci in viewModel.ContractIndicators.Where(i => i.Value > 0))
                {
                    _context.ContractIndicatorProgresses.Add(new ContractIndicatorProgress
                    {
                        ContractIndicatorId = ci.ContractIndicatorId,
                        WorkProgressId = progress.Id,
                        Value = ci.Value,
                        Notes = ci.Notes,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                await _context.SaveChangesAsync();
                await UpdateIndicatorAchievedValues(progress.ContractId);
            }

            // Update work item progress
            var existingWiProgress = await _context.WorkItemProgresses
                .Where(p => p.WorkProgressId == progress.Id)
                .ToListAsync();
            _context.WorkItemProgresses.RemoveRange(existingWiProgress);

            if (viewModel.WorkItems != null)
            {
                foreach (var wi in viewModel.WorkItems.Where(i => i.Value > 0))
                {
                    _context.WorkItemProgresses.Add(new WorkItemProgress
                    {
                        ContractWorkItemId = wi.ContractWorkItemId,
                        WorkProgressId = progress.Id,
                        Value = wi.Value,
                        Notes = wi.Notes,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                await _context.SaveChangesAsync();
                await UpdateWorkItemAchievedValues(progress.ContractId);
            }

            await UpdateContractProgress(progress.ContractId);

            // Log edit history
            var editUser = await _userManager.GetUserAsync(User);
            var editUserId = editUser?.Id ?? "";
            var currentStep = progress.CurrentStepOrder ?? 0;
            _context.WorkflowHistories.Add(new WorkflowHistory
            {
                WorkProgressId = progress.Id,
                StepOrder = currentStep,
                StepName = "Редактирование",
                Action = WorkflowAction.Edited,
                UserId = editUserId,
                UserName = editUser != null ? $"{editUser.LastName} {editUser.FirstName}" : "—",
                ActionDate = DateTime.UtcNow,
                Notes = "АВР отредактирован",
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "АВР обновлён";
            return RedirectToAction(nameof(Index));
        }

        await PopulateViewModel(viewModel, viewModel.WorkProgress.ContractId);
        return View(viewModel);
    }

    /// <summary>
    /// Удаление АВР
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.WorkProgressReports, PermissionType.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var progress = await _context.WorkProgresses.FindAsync(id);
        if (progress != null)
        {
            var contractId = progress.ContractId;
            _context.WorkProgresses.Remove(progress);
            await _context.SaveChangesAsync();

            await UpdateContractProgress(contractId);

            TempData["Success"] = "АВР удалён";
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// API: Получить финансовые данные контракта (для AJAX)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetContractFinancials(int contractId)
    {
        var contract = await _context.Contracts
            .Include(c => c.Payments)
            .FirstOrDefaultAsync(c => c.Id == contractId);

        if (contract == null)
            return Json(new { success = false });

        return Json(new
        {
            success = true,
            contractAmount = contract.FinalAmount,
            paidAmount = contract.PaidAmount,
            remainingAmount = contract.RemainingAmount,
            currency = contract.Currency.ToString(),
            paidPercent = contract.PaidPercent
        });
    }

    #region Helpers

    private async Task PopulateViewModel(WorkProgressFormViewModel viewModel, int? contractId)
    {
        // Filter contracts by user access
        var user = await _userManager.GetUserAsync(HttpContext.User);
        var canViewAll = user != null && await _dataAccessService.CanViewAllAsync(user, MenuKeys.WorkProgressReports);

        var contractsQuery = _context.Contracts
            .Include(c => c.Contractor)
            .AsQueryable();

        if (!canViewAll && user != null)
        {
            var userContractIds = await _dataAccessService.GetUserContractIdsAsync(user.Id);
            contractsQuery = contractsQuery.Where(c => userContractIds.Contains(c.Id));
        }

        viewModel.Contracts = new SelectList(
            await contractsQuery
                .OrderBy(c => c.ContractNumber)
                .Select(c => new { c.Id, Name = $"{c.ContractNumber} - {c.Contractor.Name}" })
                .ToListAsync(),
            "Id", "Name");

        // Load contract indicators if contract is selected
        if (contractId.HasValue && contractId.Value > 0)
        {
            var contractIndicators = await _context.ContractIndicators
                .Include(ci => ci.Indicator)
                .Include(ci => ci.Progresses)
                .Include(ci => ci.Villages)
                    .ThenInclude(civ => civ.Village)
                        .ThenInclude(v => v.Schools)
                .Include(ci => ci.Villages)
                    .ThenInclude(civ => civ.Village)
                        .ThenInclude(v => v.HealthFacilities)
                .Include(ci => ci.Villages)
                    .ThenInclude(civ => civ.Village)
                        .ThenInclude(v => v.Jamoat)
                            .ThenInclude(j => j.District)
                .Where(ci => ci.ContractId == contractId.Value)
                .ToListAsync();

            viewModel.ContractIndicators = contractIndicators.Select(ci =>
            {
                var input = new ContractIndicatorInput
                {
                    ContractIndicatorId = ci.Id,
                    IndicatorId = ci.IndicatorId,
                    IndicatorCode = ci.Indicator.Code,
                    IndicatorName = ci.Indicator.NameRu,
                    Unit = ci.Indicator.Unit,
                    TargetValue = ci.TargetValue,
                    PreviousAchieved = ci.Progresses.Sum(p => p.Value),
                    Value = 0,
                    GeoDataSource = ci.Indicator.GeoDataSource
                };

                // Build geo checklist if indicator has geographic data source
                if (ci.Indicator.GeoDataSource != GeoDataSource.None && ci.Villages.Any())
                {
                    var villages = ci.Villages.Select(civ => civ.Village).Where(v => v != null).OrderBy(v => v!.NameRu).ToList();
                    
                    switch (ci.Indicator.GeoDataSource)
                    {
                        case GeoDataSource.Population:
                        case GeoDataSource.FemalePopulation:
                        case GeoDataSource.Households:
                            input.GeoItems = villages.Select(v => new GeoChecklistItem
                            {
                                ItemId = v!.Id,
                                ItemType = GeoItemType.Village,
                                Name = v.NameRu,
                                Description = (ci.Indicator.GeoDataSource switch
                                {
                                    GeoDataSource.Population => $"{v.PopulationCurrent:N0} чел.",
                                    GeoDataSource.FemalePopulation => $"{v.FemalePopulation:N0} женщин",
                                    GeoDataSource.Households => $"{v.HouseholdsCurrent:N0} домохозяйств",
                                    _ => ""
                                }) + $" — {v.Jamoat?.District?.NameRu}, {v.Jamoat?.NameRu}",
                                NumericValue = ci.Indicator.GeoDataSource switch
                                {
                                    GeoDataSource.Population => v.PopulationCurrent,
                                    GeoDataSource.FemalePopulation => v.FemalePopulation,
                                    GeoDataSource.Households => v.HouseholdsCurrent,
                                    _ => 0
                                }
                            }).ToList();
                            break;

                        case GeoDataSource.SchoolCount:
                        case GeoDataSource.SchoolStudents:
                            input.GeoItems = villages
                                .SelectMany(v => v!.Schools.Select(s => new GeoChecklistItem
                                {
                                    ItemId = s.Id,
                                    ItemType = GeoItemType.School,
                                    Name = $"Школа №{s.Number}" + (string.IsNullOrEmpty(s.Name) ? "" : $" «{s.Name}»"),
                                    Description = ci.Indicator.GeoDataSource == GeoDataSource.SchoolStudents
                                        ? $"{s.TotalStudents:N0} учеников — {v.NameRu}, {v.Jamoat?.NameRu}, {v.Jamoat?.District?.NameRu}"
                                        : $"{v.NameRu}, {v.Jamoat?.NameRu}, {v.Jamoat?.District?.NameRu}",
                                    NumericValue = ci.Indicator.GeoDataSource == GeoDataSource.SchoolStudents
                                        ? s.TotalStudents
                                        : 1
                                })).ToList();
                            break;

                        case GeoDataSource.HealthFacilityCount:
                            input.GeoItems = villages
                                .SelectMany(v => v!.HealthFacilities.Select(h => new GeoChecklistItem
                                {
                                    ItemId = h.Id,
                                    ItemType = GeoItemType.HealthFacility,
                                    Name = h.Name ?? "Медучреждение",
                                    Description = $"{h.TotalStaff} персонала — {v.NameRu}, {v.Jamoat?.NameRu}, {v.Jamoat?.District?.NameRu}",
                                    NumericValue = 1
                                })).ToList();
                            break;
                    }
                }

                return input;
            }).ToList();
        }
        
        // Load work items (Объём работ)
        if (contractId.HasValue && contractId.Value > 0)
        {
            var workItems = await _context.ContractWorkItems
                .Include(w => w.Progresses)
                .Where(w => w.ContractId == contractId.Value)
                .OrderBy(w => w.SortOrder)
                .ToListAsync();
            
            viewModel.WorkItems = workItems.Select(w => new WorkItemInput
            {
                ContractWorkItemId = w.Id,
                Name = w.Name,
                Unit = w.Unit,
                TargetQuantity = w.TargetQuantity,
                PreviousAchieved = w.Progresses
                    .Where(p => viewModel.WorkProgress.Id == 0 || p.WorkProgressId != viewModel.WorkProgress.Id)
                    .Sum(p => p.Value),
                Value = viewModel.WorkProgress.Id > 0 
                    ? w.Progresses.Where(p => p.WorkProgressId == viewModel.WorkProgress.Id).Sum(p => p.Value) 
                    : 0,
                Notes = viewModel.WorkProgress.Id > 0 
                    ? w.Progresses.FirstOrDefault(p => p.WorkProgressId == viewModel.WorkProgress.Id)?.Notes 
                    : null
            }).ToList();
        }
    }

    private async Task UpdateContractProgress(int contractId)
    {
        var contract = await _context.Contracts
            .Include(c => c.WorkProgresses)
            .FirstOrDefaultAsync(c => c.Id == contractId);

        if (contract != null && contract.WorkProgresses.Any())
        {
            var latestProgress = contract.WorkProgresses
                .OrderByDescending(w => w.ReportDate)
                .First();

            contract.WorkCompletedPercent = latestProgress.CompletedPercent;
            contract.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    private async Task UpdateIndicatorAchievedValues(int contractId)
    {
        var contractIndicators = await _context.ContractIndicators
            .Include(ci => ci.Progresses)
            .Where(ci => ci.ContractId == contractId)
            .ToListAsync();

        foreach (var ci in contractIndicators)
        {
            ci.AchievedValue = ci.Progresses.Sum(p => p.Value);
            ci.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    private async Task UpdateWorkItemAchievedValues(int contractId)
    {
        var workItems = await _context.ContractWorkItems
            .Include(w => w.Progresses)
            .Where(w => w.ContractId == contractId)
            .ToListAsync();

        foreach (var wi in workItems)
        {
            wi.AchievedQuantity = wi.Progresses.Sum(p => p.Value);
            wi.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    #endregion
}
