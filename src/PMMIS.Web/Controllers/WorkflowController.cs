using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;
using PMMIS.Web.Authorization;

namespace PMMIS.Web.Controllers;

/// <summary>
/// Настройка цепочки утверждений (Workflow)
/// </summary>
[Authorize]
[RequirePermission(MenuKeys.Workflow, PermissionType.View)]
public class WorkflowController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public WorkflowController(ApplicationDbContext context, RoleManager<ApplicationRole> roleManager)
    {
        _context = context;
        _roleManager = roleManager;
    }

    /// <summary>
    /// Страница настройки цепочки утверждений
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var steps = await _context.WorkflowSteps
            .Include(s => s.Role)
            .OrderBy(s => s.WorkflowType)
            .ThenBy(s => s.StepOrder)
            .ToListAsync();

        var roles = await _roleManager.Roles
            .OrderBy(r => r.Name)
            .ToListAsync();

        // JSON data for Syncfusion components
        var rolesJson = roles.Select(r => new { id = r.Id, name = r.Name }).ToList();
        var avrStepsJson = steps.Where(s => s.WorkflowType == "AVR").Select(s => new
        {
            id = s.Id,
            stepOrder = s.StepOrder,
            stepName = s.StepName,
            actionType = s.ActionType,
            roleId = s.RoleId,
            roleName = s.Role?.Name ?? "",
            canReject = s.CanReject,
            rejectToStepOrder = s.RejectToStepOrder,
            isActive = s.IsActive
        }).ToList();
        var paymentStepsJson = steps.Where(s => s.WorkflowType == "Payment").Select(s => new
        {
            id = s.Id,
            stepOrder = s.StepOrder,
            stepName = s.StepName,
            actionType = s.ActionType,
            roleId = s.RoleId,
            roleName = s.Role?.Name ?? "",
            canReject = s.CanReject,
            rejectToStepOrder = s.RejectToStepOrder,
            isActive = s.IsActive
        }).ToList();

        ViewBag.RolesJson = System.Text.Json.JsonSerializer.Serialize(rolesJson);
        ViewBag.AvrStepsJson = System.Text.Json.JsonSerializer.Serialize(avrStepsJson);
        ViewBag.PaymentStepsJson = System.Text.Json.JsonSerializer.Serialize(paymentStepsJson);

        return View();
    }

    /// <summary>
    /// Сохранение конфигурации workflow (AJAX)
    /// </summary>
    [HttpPost]
    [RequirePermission(MenuKeys.Workflow, PermissionType.Edit)]
    public async Task<IActionResult> Save([FromBody] WorkflowSaveModel model)
    {
        if (model?.Steps == null)
            return BadRequest(new { success = false, message = "Нет данных" });

        // Remove existing steps for this workflow type
        var existing = await _context.WorkflowSteps
            .Where(s => s.WorkflowType == model.WorkflowType)
            .ToListAsync();
        _context.WorkflowSteps.RemoveRange(existing);

        // Add new steps
        var order = 1;
        foreach (var step in model.Steps)
        {
            _context.WorkflowSteps.Add(new WorkflowStep
            {
                WorkflowType = model.WorkflowType,
                StepOrder = order++,
                StepName = step.StepName,
                ActionType = step.ActionType,
                RoleId = step.RoleId,
                CanReject = step.CanReject,
                RejectToStepOrder = step.RejectToStepOrder,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        return Json(new { success = true, message = "Цепочка сохранена" });
    }
}

// ─── Request Models ───
public class WorkflowSaveModel
{
    public string WorkflowType { get; set; } = string.Empty;
    public List<WorkflowStepInput> Steps { get; set; } = new();
}

public class WorkflowStepInput
{
    public string StepName { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public bool CanReject { get; set; } = true;
    public int? RejectToStepOrder { get; set; }
}
