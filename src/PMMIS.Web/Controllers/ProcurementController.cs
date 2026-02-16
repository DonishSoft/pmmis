using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;
using PMMIS.Web.Authorization;
using PMMIS.Web.Extensions;
using PMMIS.Web.Services;
using System.Security.Claims;

namespace PMMIS.Web.Controllers;

/// <summary>
/// Управление планом закупок
/// </summary>
[Authorize]
[RequirePermission(MenuKeys.Procurement, PermissionType.View)]
public class ProcurementController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ITaskService _taskService;

    public ProcurementController(ApplicationDbContext context, ITaskService taskService)
    {
        _context = context;
        _taskService = taskService;
    }

    public async Task<IActionResult> Index(int? projectId, ProcurementStatus? status, ProcurementType? type)
    {
        var query = _context.ProcurementPlans
            .Include(p => p.Project)
            .Include(p => p.Component)
            .Include(p => p.Contract)
            .AsQueryable();

        if (projectId.HasValue)
            query = query.Where(p => p.ProjectId == projectId.Value);

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        if (type.HasValue)
            query = query.Where(p => p.Type == type.Value);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        ViewBag.Projects = await _context.Projects.OrderBy(p => p.Code).ToListAsync();
        ViewBag.SelectedProjectId = projectId;
        ViewBag.SelectedStatus = status;
        ViewBag.SelectedType = type;

        return View(items);
    }

    public async Task<IActionResult> Create(int? projectId)
    {
        await LoadDropdowns();
        
        var model = new ProcurementPlan
        {
            ProjectId = projectId ?? 0,
            Status = ProcurementStatus.Planned
        };
        
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProcurementPlan plan)
    {
        if (await _context.ProcurementPlans.AnyAsync(p => p.ReferenceNo == plan.ReferenceNo))
        {
            ModelState.AddModelError("ReferenceNo", "Позиция с таким номером уже существует");
        }

        if (ModelState.IsValid)
        {
            plan.CreatedAt = DateTime.UtcNow;
            
            // Convert dates to UTC
            plan.PlannedBidOpeningDate = plan.PlannedBidOpeningDate.ToUtc();
            plan.PlannedContractSigningDate = plan.PlannedContractSigningDate.ToUtc();
            plan.PlannedCompletionDate = plan.PlannedCompletionDate.ToUtc();
            plan.ActualBidOpeningDate = plan.ActualBidOpeningDate.ToUtc();
            plan.ActualContractSigningDate = plan.ActualContractSigningDate.ToUtc();
            plan.ActualCompletionDate = plan.ActualCompletionDate.ToUtc();
            
            _context.ProcurementPlans.Add(plan);
            await _context.SaveChangesAsync();
            
            // Auto-create task for procurement monitoring
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                await _taskService.CreateAsync(new ProjectTask
                {
                    Title = $"Закупка: {plan.Description}",
                    Description = $"Ref: {plan.ReferenceNo}. Контроль закупочной процедуры.",
                    Status = ProjectTaskStatus.New,
                    Priority = TaskPriority.Normal,
                    DueDate = plan.PlannedCompletionDate ?? DateTime.UtcNow.AddMonths(1),
                    AssigneeId = userId,
                    AssignedById = userId,
                    ProcurementPlanId = plan.Id,
                    ProjectId = plan.ProjectId
                }, userId);
            }
            
            TempData["Success"] = "Позиция плана закупок успешно создана";
            return RedirectToAction(nameof(Index), new { projectId = plan.ProjectId });
        }

        await LoadDropdowns();
        return View(plan);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var plan = await _context.ProcurementPlans
            .Include(p => p.Project)
            .Include(p => p.Component)
            .FirstOrDefaultAsync(p => p.Id == id);
            
        if (plan == null) return NotFound();

        await LoadDropdowns(plan.ProjectId);
        ViewBag.Contracts = await GetContractsForProject(plan.ProjectId);
        
        return View(plan);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProcurementPlan plan)
    {
        if (id != plan.Id) return NotFound();

        if (ModelState.IsValid)
        {
            plan.UpdatedAt = DateTime.UtcNow;
            
            // Convert dates to UTC
            plan.PlannedBidOpeningDate = plan.PlannedBidOpeningDate.ToUtc();
            plan.PlannedContractSigningDate = plan.PlannedContractSigningDate.ToUtc();
            plan.PlannedCompletionDate = plan.PlannedCompletionDate.ToUtc();
            plan.ActualBidOpeningDate = plan.ActualBidOpeningDate.ToUtc();
            plan.ActualContractSigningDate = plan.ActualContractSigningDate.ToUtc();
            plan.ActualCompletionDate = plan.ActualCompletionDate.ToUtc();
            
            _context.Update(plan);
            await _context.SaveChangesAsync();
            
            TempData["Success"] = "Позиция плана закупок обновлена";
            return RedirectToAction(nameof(Index), new { projectId = plan.ProjectId });
        }

        await LoadDropdowns(plan.ProjectId);
        ViewBag.Contracts = await GetContractsForProject(plan.ProjectId);
        return View(plan);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = UserRoles.PmuAdmin)]
    public async Task<IActionResult> Delete(int id)
    {
        var plan = await _context.ProcurementPlans.FindAsync(id);
        if (plan != null)
        {
            var projectId = plan.ProjectId;
            _context.ProcurementPlans.Remove(plan);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Позиция удалена";
            return RedirectToAction(nameof(Index), new { projectId });
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, ProcurementStatus status)
    {
        var plan = await _context.ProcurementPlans.FindAsync(id);
        if (plan != null)
        {
            plan.Status = status;
            plan.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            
            // Auto-complete linked task when procurement is completed
            if (status == ProcurementStatus.Completed)
            {
                var linkedTask = await _context.ProjectTasks
                    .FirstOrDefaultAsync(t => t.ProcurementPlanId == id && 
                                              t.Status != ProjectTaskStatus.Completed &&
                                              t.Status != ProjectTaskStatus.Cancelled);
                
                if (linkedTask != null)
                {
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (!string.IsNullOrEmpty(userId))
                    {
                        await _taskService.ChangeStatusAsync(linkedTask.Id, ProjectTaskStatus.Completed, userId);
                    }
                }
            }
            
            TempData["Success"] = "Статус обновлён";
        }
        return RedirectToAction(nameof(Index), new { projectId = plan?.ProjectId });
    }

    #region Helpers

    private async Task LoadDropdowns(int? selectedProjectId = null)
    {
        ViewBag.Projects = new SelectList(
            await _context.Projects.OrderBy(p => p.Code).ToListAsync(),
            "Id", "Code", selectedProjectId);

        ViewBag.Components = selectedProjectId.HasValue
            ? new SelectList(
                await _context.Components.Where(c => c.ProjectId == selectedProjectId).OrderBy(c => c.Number).ToListAsync(),
                "Id", "NameRu")
            : new SelectList(Enumerable.Empty<SelectListItem>());

        ViewBag.Methods = Enum.GetValues<ProcurementMethod>()
            .Select(m => new SelectListItem { Value = ((int)m).ToString(), Text = m.ToString() });

        ViewBag.Types = Enum.GetValues<ProcurementType>()
            .Select(t => new SelectListItem { Value = ((int)t).ToString(), Text = GetTypeName(t) });

        ViewBag.Statuses = Enum.GetValues<ProcurementStatus>()
            .Select(s => new SelectListItem { Value = ((int)s).ToString(), Text = GetStatusName(s) });
    }

    private async Task<SelectList> GetContractsForProject(int projectId)
    {
        var contracts = await _context.Contracts
            .Where(c => c.ProjectId == projectId)
            .Select(c => new { c.Id, Name = $"{c.ContractNumber}: {c.ScopeOfWork}" })
            .ToListAsync();
        return new SelectList(contracts, "Id", "Name");
    }

    private static string GetTypeName(ProcurementType type) => type switch
    {
        ProcurementType.Goods => "Товары",
        ProcurementType.Works => "Работы",
        ProcurementType.ConsultingServices => "Консалтинг",
        ProcurementType.NonConsultingServices => "Некон. услуги",
        _ => type.ToString()
    };

    private static string GetStatusName(ProcurementStatus status) => status switch
    {
        ProcurementStatus.Planned => "Запланировано",
        ProcurementStatus.InProgress => "В процессе",
        ProcurementStatus.Evaluation => "Оценка",
        ProcurementStatus.Awarded => "Присуждено",
        ProcurementStatus.Completed => "Завершено",
        ProcurementStatus.Cancelled => "Отменено",
        _ => status.ToString()
    };



    #endregion

    #region API

    [HttpGet]
    public async Task<IActionResult> GetComponentsByProject(int projectId)
    {
        var components = await _context.Components
            .Where(c => c.ProjectId == projectId)
            .Select(c => new { c.Id, Name = $"Компонент {c.Number}: {c.NameRu}" })
            .ToListAsync();
        return Json(components);
    }

    [HttpGet]
    public async Task<IActionResult> GetSubComponentsByComponent(int componentId)
    {
        var subComponents = await _context.SubComponents
            .Where(sc => sc.ComponentId == componentId)
            .Select(sc => new { sc.Id, Name = $"{sc.Code}: {sc.NameRu}" })
            .ToListAsync();
        return Json(subComponents);
    }

    /// <summary>
    /// Get subcomponent budget info (allocated and remaining)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSubComponentBudget(int subComponentId)
    {
        var subComponent = await _context.SubComponents
            .FirstOrDefaultAsync(sc => sc.Id == subComponentId);
        
        if (subComponent == null)
            return Json(new { allocated = 0m, used = 0m, remaining = 0m });

        // Sum of all procurement plans for this subcomponent
        var usedAmount = await _context.ProcurementPlans
            .Where(p => p.SubComponentId == subComponentId)
            .SumAsync(p => p.EstimatedAmount);

        var remaining = subComponent.AllocatedBudget - usedAmount;

        return Json(new { 
            allocated = subComponent.AllocatedBudget, 
            used = usedAmount, 
            remaining = remaining 
        });
    }

    /// <summary>
    /// Get component budget info (allocated and remaining)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetComponentBudget(int componentId)
    {
        var component = await _context.Components
            .FirstOrDefaultAsync(c => c.Id == componentId);
        
        if (component == null)
            return Json(new { allocated = 0m, used = 0m, remaining = 0m });

        // Sum of all procurement plans for this component
        var usedAmount = await _context.ProcurementPlans
            .Where(p => p.ComponentId == componentId)
            .SumAsync(p => p.EstimatedAmount);

        var remaining = component.AllocatedBudget - usedAmount;

        return Json(new { 
            allocated = component.AllocatedBudget, 
            used = usedAmount, 
            remaining = remaining 
        });
    }

    #endregion
}
