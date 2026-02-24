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
            .ThenBy(s => s.ContractType)
            .ThenBy(s => s.StepOrder)
            .ToListAsync();

        var roles = await _roleManager.Roles
            .OrderBy(r => r.Name)
            .ToListAsync();

        var rolesJson = roles.Select(r => new { id = r.Id, name = r.Name }).ToList();

        // Helper to build step JSON
        object StepToJson(WorkflowStep s) => new
        {
            id = s.Id,
            stepOrder = s.StepOrder,
            stepName = s.StepName,
            actionType = s.ActionType,
            assigneeType = s.AssigneeType,
            roleId = s.RoleId,
            roleName = s.Role?.Name ?? "",
            canReject = s.CanReject,
            rejectToStepOrder = s.RejectToStepOrder,
            isActive = s.IsActive
        };

        // 6 workflow sets: AVR×3 + Payment×3
        var contractTypes = new[] { ContractType.Works, ContractType.Consulting, ContractType.Goods };
        var workflowData = new Dictionary<string, object>();

        foreach (var ct in contractTypes)
        {
            var ctName = ct.ToString();
            workflowData[$"avr{ctName}"] = steps
                .Where(s => s.WorkflowType == "AVR" && s.ContractType == ct)
                .Select(StepToJson).ToList();
            workflowData[$"payment{ctName}"] = steps
                .Where(s => s.WorkflowType == "Payment" && s.ContractType == ct)
                .Select(StepToJson).ToList();
        }

        var jsonOpts = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };

        ViewBag.RolesJson = System.Text.Json.JsonSerializer.Serialize(rolesJson, jsonOpts);
        ViewBag.WorkflowDataJson = System.Text.Json.JsonSerializer.Serialize(workflowData, jsonOpts);

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

        if (!Enum.TryParse<ContractType>(model.ContractTypeName, out var contractType))
            return BadRequest(new { success = false, message = "Неизвестный тип контракта" });

        // Remove existing steps for this workflow type + contract type
        var existing = await _context.WorkflowSteps
            .Where(s => s.WorkflowType == model.WorkflowType && s.ContractType == contractType)
            .ToListAsync();
        _context.WorkflowSteps.RemoveRange(existing);

        // Add new steps
        var order = 1;
        foreach (var step in model.Steps)
        {
            _context.WorkflowSteps.Add(new WorkflowStep
            {
                WorkflowType = model.WorkflowType,
                ContractType = contractType,
                StepOrder = order++,
                StepName = step.StepName,
                ActionType = step.ActionType,
                AssigneeType = step.AssigneeType ?? "Role",
                RoleId = step.RoleId ?? "",
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
    public string ContractTypeName { get; set; } = string.Empty;
    public List<WorkflowStepInput> Steps { get; set; } = new();
}

public class WorkflowStepInput
{
    public string StepName { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string? AssigneeType { get; set; } = "Role";
    public string? RoleId { get; set; } = string.Empty;
    public bool CanReject { get; set; } = true;
    public int? RejectToStepOrder { get; set; }
}
