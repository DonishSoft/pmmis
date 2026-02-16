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

    public WorkProgressReportsController(
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

        return View(progress);
    }

    /// <summary>
    /// Форма создания АВР
    /// </summary>
    [RequirePermission(MenuKeys.WorkProgressReports, PermissionType.Create)]
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

            // Auto-create task for АВР review
            var contract = await _context.Contracts
                .Include(c => c.Contractor)
                .FirstOrDefaultAsync(c => c.Id == progress.ContractId);

            if (!string.IsNullOrEmpty(userId) && contract != null)
            {
                await _taskService.CreateAsync(new ProjectTask
                {
                    Title = $"Проверить АВР по контракту {contract.ContractNumber}",
                    Description = $"Подрядчик: {contract.Contractor.Name}\n" +
                                  $"Дата отчёта: {progress.ReportDate:dd.MM.yyyy}\n" +
                                  $"Прогресс: {progress.CompletedPercent}%\n" +
                                  $"{progress.Description}",
                    Status = ProjectTaskStatus.New,
                    Priority = TaskPriority.High,
                    DueDate = DateTime.UtcNow.AddDays(3),
                    AssigneeId = userId,
                    AssignedById = userId,
                    ContractId = progress.ContractId,
                    WorkProgressId = progress.Id,
                    ProjectId = contract.ProjectId
                }, userId);
            }

            TempData["Success"] = "АВР создан. Задача на проверку добавлена.";
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

            await UpdateContractProgress(progress.ContractId);

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

    #endregion
}
