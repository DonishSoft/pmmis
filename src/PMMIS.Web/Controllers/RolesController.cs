using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;
using PMMIS.Web.Authorization;
using PMMIS.Web.ViewModels.Roles;

namespace PMMIS.Web.Controllers;

/// <summary>
/// Управление ролями и правами доступа
/// </summary>
[Authorize]
[RequirePermission(MenuKeys.Roles, PermissionType.View)]
public class RolesController : Controller
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public RolesController(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var roles = await _roleManager.Roles.OrderBy(r => r.SortOrder).ToListAsync();
        
        var userCounts = new Dictionary<string, int>();
        var permissionCounts = new Dictionary<string, int>();
        
        foreach (var role in roles)
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
            userCounts[role.Id] = usersInRole.Count;
            
            permissionCounts[role.Id] = await _context.RoleMenuPermissions
                .CountAsync(p => p.RoleId == role.Id && p.CanView);
        }

        var viewModel = new RoleIndexViewModel
        {
            Roles = roles,
            UserCounts = userCounts,
            PermissionCounts = permissionCounts,
            TotalModules = MenuKeys.All.Length
        };
        
        return View(viewModel);
    }

    public IActionResult Create()
    {
        var viewModel = new RoleFormViewModel
        {
            Role = new ApplicationRole(),
            MenuKeys = MenuKeys.Names
        };
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ApplicationRole role)
    {
        if (string.IsNullOrEmpty(role.Name))
        {
            ModelState.AddModelError("Name", "Название роли обязательно");
            var vm = new RoleFormViewModel { Role = role, MenuKeys = MenuKeys.Names };
            return View(vm);
        }

        role.Name = role.Name.ToUpper().Replace(" ", "_");
        role.NormalizedName = role.Name;

        var result = await _roleManager.CreateAsync(role);
        if (result.Succeeded)
        {
            TempData["Success"] = "Роль создана. Настройте права доступа.";
            return RedirectToAction(nameof(Permissions), new { id = role.Id });
        }

        TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
        var viewModel = new RoleFormViewModel { Role = role, MenuKeys = MenuKeys.Names };
        return View(viewModel);
    }

    public async Task<IActionResult> Edit(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role == null) return NotFound();
        
        var viewModel = new RoleFormViewModel
        {
            Role = role,
            UsersInRole = await _userManager.GetUsersInRoleAsync(role.Name!)
        };
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, ApplicationRole model)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role == null) return NotFound();

        role.Description = model.Description;
        role.DescriptionTj = model.DescriptionTj;
        role.DescriptionEn = model.DescriptionEn;
        role.SortOrder = model.SortOrder;
        
        if (!role.IsSystem && !string.IsNullOrEmpty(model.Name))
        {
            role.Name = model.Name.ToUpper().Replace(" ", "_");
            role.NormalizedName = role.Name;
        }

        var result = await _roleManager.UpdateAsync(role);
        if (result.Succeeded)
        {
            TempData["Success"] = "Роль обновлена";
            return RedirectToAction(nameof(Index));
        }

        TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
        var viewModel = new RoleFormViewModel
        {
            Role = role,
            UsersInRole = await _userManager.GetUsersInRoleAsync(role.Name!)
        };
        return View(viewModel);
    }

    /// <summary>
    /// Professional permissions matrix for a role
    /// </summary>
    public async Task<IActionResult> Permissions(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role == null) return NotFound();

        var permissions = await _context.RoleMenuPermissions
            .Where(p => p.RoleId == id)
            .ToListAsync();

        // Create a dictionary for easy lookup
        var permissionDict = permissions.ToDictionary(p => p.MenuKey, p => p);

        var viewModel = new RolePermissionsViewModel
        {
            Role = role,
            MenuKeys = MenuKeys.Names,
            Permissions = permissionDict
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Permissions(string id, Dictionary<string, PermissionModel> permissions)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role == null) return NotFound();

        // Remove existing permissions
        var existing = await _context.RoleMenuPermissions
            .Where(p => p.RoleId == id)
            .ToListAsync();
        _context.RoleMenuPermissions.RemoveRange(existing);

        // Add new permissions
        foreach (var (menuKey, perm) in permissions)
        {
            if (perm.CanView || perm.CanCreate || perm.CanEdit || perm.CanDelete)
            {
                _context.RoleMenuPermissions.Add(new RoleMenuPermission
                {
                    RoleId = id,
                    MenuKey = menuKey,
                    CanView = perm.CanView,
                    CanViewAll = perm.CanViewAll,
                    CanCreate = perm.CanCreate,
                    CanEdit = perm.CanEdit,
                    CanDelete = perm.CanDelete,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "Права доступа сохранены";
        return RedirectToAction(nameof(Permissions), new { id });
    }

    /// <summary>
    /// Quick action: Grant full access to all modules
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GrantFullAccess(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role == null) return NotFound();

        var existing = await _context.RoleMenuPermissions
            .Where(p => p.RoleId == id)
            .ToListAsync();
        _context.RoleMenuPermissions.RemoveRange(existing);

        foreach (var menuKey in MenuKeys.All)
        {
            _context.RoleMenuPermissions.Add(new RoleMenuPermission
            {
                RoleId = id,
                MenuKey = menuKey,
                CanView = true,
                CanViewAll = true,
                CanCreate = true,
                CanEdit = true,
                CanDelete = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "Предоставлен полный доступ ко всем модулям";
        return RedirectToAction(nameof(Permissions), new { id });
    }

    /// <summary>
    /// Quick action: Grant view-only access to all modules
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GrantViewOnly(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role == null) return NotFound();

        var existing = await _context.RoleMenuPermissions
            .Where(p => p.RoleId == id)
            .ToListAsync();
        _context.RoleMenuPermissions.RemoveRange(existing);

        foreach (var menuKey in MenuKeys.All)
        {
            _context.RoleMenuPermissions.Add(new RoleMenuPermission
            {
                RoleId = id,
                MenuKey = menuKey,
                CanView = true,
                CanCreate = false,
                CanEdit = false,
                CanDelete = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "Предоставлен доступ только для просмотра";
        return RedirectToAction(nameof(Permissions), new { id });
    }

    /// <summary>
    /// Quick action: Revoke all access
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeAll(string id)
    {
        var existing = await _context.RoleMenuPermissions
            .Where(p => p.RoleId == id)
            .ToListAsync();
        _context.RoleMenuPermissions.RemoveRange(existing);
        await _context.SaveChangesAsync();
        
        TempData["Success"] = "Все права доступа отозваны";
        return RedirectToAction(nameof(Permissions), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role == null) return NotFound();

        if (role.IsSystem)
        {
            TempData["Error"] = "Нельзя удалить системную роль";
            return RedirectToAction(nameof(Index));
        }

        var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
        if (usersInRole.Count > 0)
        {
            TempData["Error"] = $"Нельзя удалить роль: в ней {usersInRole.Count} пользователей";
            return RedirectToAction(nameof(Index));
        }

        var permissions = await _context.RoleMenuPermissions
            .Where(p => p.RoleId == id)
            .ToListAsync();
        _context.RoleMenuPermissions.RemoveRange(permissions);
        await _context.SaveChangesAsync();

        var result = await _roleManager.DeleteAsync(role);
        if (result.Succeeded)
        {
            TempData["Success"] = "Роль удалена";
        }
        else
        {
            TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
        }

        return RedirectToAction(nameof(Index));
    }
}

/// <summary>
/// Model for binding permission checkboxes
/// </summary>
public class PermissionModel
{
    public bool CanView { get; set; }
    public bool CanViewAll { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}
