using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;
using PMMIS.Web.Authorization;

namespace PMMIS.Web.Controllers;

/// <summary>
/// Управление проектами и их компонентами
/// </summary>
[Authorize]
[RequirePermission(MenuKeys.Projects, PermissionType.View)]
public class ProjectsController : Controller
{
    private readonly ApplicationDbContext _context;

    public ProjectsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var projects = await _context.Projects
            .Include(p => p.Components)
                .ThenInclude(c => c.SubComponents)
            .Include(p => p.Contracts)
            .OrderBy(p => p.Code)
            .ToListAsync();

        return View(projects);
    }

    [RequirePermission(MenuKeys.Projects, PermissionType.Create)]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Projects, PermissionType.Create)]
    public async Task<IActionResult> Create(Project project)
    {
        if (await _context.Projects.AnyAsync(p => p.Code == project.Code))
        {
            ModelState.AddModelError("Code", "Проект с таким кодом уже существует");
        }

        if (ModelState.IsValid)
        {
            project.CreatedAt = DateTime.UtcNow;
            _context.Projects.Add(project);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Проект успешно создан";
            return RedirectToAction(nameof(Index));
        }
        return View(project);
    }

    [RequirePermission(MenuKeys.Projects, PermissionType.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var project = await _context.Projects.FindAsync(id);
        if (project == null) return NotFound();
        return View(project);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Projects, PermissionType.Edit)]
    public async Task<IActionResult> Edit(int id, Project project)
    {
        if (id != project.Id) return NotFound();

        if (ModelState.IsValid)
        {
            project.UpdatedAt = DateTime.UtcNow;
            _context.Update(project);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Проект успешно обновлён";
            return RedirectToAction(nameof(Index));
        }
        return View(project);
    }

    #region Components

    public async Task<IActionResult> Components(int projectId)
    {
        var project = await _context.Projects
            .Include(p => p.Components)
                .ThenInclude(c => c.SubComponents)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null) return NotFound();

        ViewBag.Project = project;
        return View(project.Components.OrderBy(c => c.Number).ToList());
    }

    public async Task<IActionResult> CreateComponent(int projectId)
    {
        var project = await _context.Projects.FindAsync(projectId);
        if (project == null) return NotFound();

        ViewBag.Project = project;
        return View(new Component { ProjectId = projectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateComponent(Component component)
    {
        if (ModelState.IsValid)
        {
            component.CreatedAt = DateTime.UtcNow;
            _context.Components.Add(component);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Компонент успешно добавлен";
            return RedirectToAction(nameof(Components), new { projectId = component.ProjectId });
        }

        ViewBag.Project = await _context.Projects.FindAsync(component.ProjectId);
        return View(component);
    }

    public async Task<IActionResult> EditComponent(int id)
    {
        var component = await _context.Components.Include(c => c.Project).FirstOrDefaultAsync(c => c.Id == id);
        if (component == null) return NotFound();

        ViewBag.Project = component.Project;
        return View(component);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditComponent(int id, Component component)
    {
        if (id != component.Id) return NotFound();

        if (ModelState.IsValid)
        {
            component.UpdatedAt = DateTime.UtcNow;
            _context.Update(component);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Компонент успешно обновлён";
            return RedirectToAction(nameof(Components), new { projectId = component.ProjectId });
        }

        ViewBag.Project = await _context.Projects.FindAsync(component.ProjectId);
        return View(component);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Projects, PermissionType.Delete)]
    public async Task<IActionResult> DeleteComponent(int id)
    {
        var component = await _context.Components.FindAsync(id);
        if (component != null)
        {
            var projectId = component.ProjectId;
            _context.Components.Remove(component);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Компонент удалён";
            return RedirectToAction(nameof(Components), new { projectId });
        }
        return RedirectToAction(nameof(Index));
    }

    #endregion

    #region SubComponents

    public async Task<IActionResult> SubComponents(int componentId)
    {
        var component = await _context.Components
            .Include(c => c.Project)
            .Include(c => c.SubComponents)
            .FirstOrDefaultAsync(c => c.Id == componentId);

        if (component == null) return NotFound();

        ViewBag.Component = component;
        ViewBag.Project = component.Project;
        return View(component.SubComponents.OrderBy(sc => sc.Code).ToList());
    }

    public async Task<IActionResult> CreateSubComponent(int componentId)
    {
        var component = await _context.Components.Include(c => c.Project).FirstOrDefaultAsync(c => c.Id == componentId);
        if (component == null) return NotFound();

        ViewBag.Component = component;
        return View(new SubComponent { ComponentId = componentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSubComponent(SubComponent subComponent)
    {
        if (ModelState.IsValid)
        {
            subComponent.CreatedAt = DateTime.UtcNow;
            _context.SubComponents.Add(subComponent);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Подкомпонент успешно добавлен";
            return RedirectToAction(nameof(SubComponents), new { componentId = subComponent.ComponentId });
        }

        ViewBag.Component = await _context.Components.Include(c => c.Project).FirstOrDefaultAsync(c => c.Id == subComponent.ComponentId);
        return View(subComponent);
    }

    public async Task<IActionResult> EditSubComponent(int id)
    {
        var subComponent = await _context.SubComponents
            .Include(sc => sc.Component)
                .ThenInclude(c => c.Project)
            .FirstOrDefaultAsync(sc => sc.Id == id);

        if (subComponent == null) return NotFound();

        ViewBag.Component = subComponent.Component;
        return View(subComponent);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSubComponent(int id, SubComponent subComponent)
    {
        if (id != subComponent.Id) return NotFound();

        if (ModelState.IsValid)
        {
            subComponent.UpdatedAt = DateTime.UtcNow;
            _context.Update(subComponent);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Подкомпонент успешно обновлён";
            return RedirectToAction(nameof(SubComponents), new { componentId = subComponent.ComponentId });
        }

        ViewBag.Component = await _context.Components.Include(c => c.Project).FirstOrDefaultAsync(c => c.Id == subComponent.ComponentId);
        return View(subComponent);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Projects, PermissionType.Delete)]
    public async Task<IActionResult> DeleteSubComponent(int id)
    {
        var subComponent = await _context.SubComponents.FindAsync(id);
        if (subComponent != null)
        {
            var componentId = subComponent.ComponentId;
            _context.SubComponents.Remove(subComponent);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Подкомпонент удалён";
            return RedirectToAction(nameof(SubComponents), new { componentId });
        }
        return RedirectToAction(nameof(Index));
    }

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

    #endregion
}
