using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;
using PMMIS.Web.Authorization;

namespace PMMIS.Web.Controllers;

/// <summary>
/// Настройки системы
/// </summary>
[Authorize]
[RequirePermission(MenuKeys.Settings, PermissionType.View)]
public class SettingsController : Controller
{
    private readonly ApplicationDbContext _context;

    public SettingsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var settings = await _context.AppSettings.OrderBy(s => s.Key).ToListAsync();
        return View(settings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Settings, PermissionType.Edit)]
    public async Task<IActionResult> SaveSetting(string key, string value, string? description)
    {
        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting == null)
        {
            setting = new AppSetting { Key = key, Value = value, Description = description };
            _context.AppSettings.Add(setting);
        }
        else
        {
            setting.Value = value;
            if (description != null) setting.Description = description;
        }
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Настройка \"{key}\" сохранена";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Settings, PermissionType.Delete)]
    public async Task<IActionResult> DeleteSetting(int id)
    {
        var setting = await _context.AppSettings.FindAsync(id);
        if (setting != null)
        {
            _context.AppSettings.Remove(setting);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Настройка удалена";
        }
        return RedirectToAction(nameof(Index));
    }
}
