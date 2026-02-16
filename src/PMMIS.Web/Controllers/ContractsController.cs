
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
