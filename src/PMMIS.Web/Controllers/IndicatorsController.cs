using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;
using PMMIS.Web.Authorization;

namespace PMMIS.Web.Controllers;

/// <summary>
/// Управление индикаторами/KPI проекта
/// </summary>
[Authorize]
[RequirePermission(MenuKeys.Indicators, PermissionType.View)]
public class IndicatorsController : Controller
{
    private readonly ApplicationDbContext _context;

    public IndicatorsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var indicators = await _context.Indicators
            .Include(i => i.Category)
            .Include(i => i.ParentIndicator)
            .Include(i => i.SubIndicators)
            .Include(i => i.Values)
            .Where(i => i.ParentIndicatorId == null) // Only top-level indicators
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Code)
            .ToListAsync();

        return View(indicators);
    }

    [RequirePermission(MenuKeys.Indicators, PermissionType.Create)]
    public async Task<IActionResult> Create(int? parentId = null)
    {
        ViewBag.Categories = await _context.IndicatorCategories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync();
        ViewBag.ParentIndicators = await _context.Indicators.Where(i => i.ParentIndicatorId == null).OrderBy(i => i.SortOrder).ToListAsync();
        
        var indicator = new Indicator();
        if (parentId.HasValue)
        {
            indicator.ParentIndicatorId = parentId.Value;
        }
        return View(indicator);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Indicators, PermissionType.Create)]
    public async Task<IActionResult> Create(Indicator indicator)
    {
        if (await _context.Indicators.AnyAsync(i => i.Code == indicator.Code))
        {
            ModelState.AddModelError("Code", "Индикатор с таким кодом уже существует");
        }

        ModelState.Remove("Category");
        ModelState.Remove("ParentIndicator");
        ModelState.Remove("SubIndicators");
        ModelState.Remove("Values");
        ModelState.Remove("NameTj");
        ModelState.Remove("NameEn");
        ModelState.Remove("Unit");

        if (ModelState.IsValid)
        {
            indicator.NameTj ??= string.Empty;
            indicator.NameEn ??= string.Empty;
            indicator.Unit ??= string.Empty;
            indicator.CreatedAt = DateTime.UtcNow;
            _context.Indicators.Add(indicator);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Индикатор успешно создан";
            return RedirectToAction(nameof(Index));
        }
        
        ViewBag.Categories = await _context.IndicatorCategories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync();
        ViewBag.ParentIndicators = await _context.Indicators.Where(i => i.ParentIndicatorId == null).OrderBy(i => i.SortOrder).ToListAsync();
        return View(indicator);
    }

    [RequirePermission(MenuKeys.Indicators, PermissionType.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var indicator = await _context.Indicators.FindAsync(id);
        if (indicator == null) return NotFound();
        
        ViewBag.Categories = await _context.IndicatorCategories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync();
        ViewBag.ParentIndicators = await _context.Indicators.Where(i => i.ParentIndicatorId == null && i.Id != id).OrderBy(i => i.SortOrder).ToListAsync();
        return View(indicator);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Indicators, PermissionType.Edit)]
    public async Task<IActionResult> Edit(int id, Indicator indicator)
    {
        if (id != indicator.Id) return NotFound();

        if (await _context.Indicators.AnyAsync(i => i.Code == indicator.Code && i.Id != id))
        {
            ModelState.AddModelError("Code", "Индикатор с таким кодом уже существует");
        }

        ModelState.Remove("Category");
        ModelState.Remove("ParentIndicator");
        ModelState.Remove("SubIndicators");
        ModelState.Remove("Values");
        ModelState.Remove("NameTj");
        ModelState.Remove("NameEn");
        ModelState.Remove("Unit");

        if (ModelState.IsValid)
        {
            var existing = await _context.Indicators.FindAsync(id);
            if (existing == null) return NotFound();
            
            existing.Code = indicator.Code;
            existing.NameRu = indicator.NameRu;
            existing.NameTj = indicator.NameTj ?? string.Empty;
            existing.NameEn = indicator.NameEn ?? string.Empty;
            existing.Unit = indicator.Unit ?? string.Empty;
            existing.TargetValue = indicator.TargetValue;
            existing.SortOrder = indicator.SortOrder;
            existing.MeasurementType = indicator.MeasurementType;
            existing.GeoDataSource = indicator.GeoDataSource;
            existing.CategoryId = indicator.CategoryId;
            existing.ParentIndicatorId = indicator.ParentIndicatorId;
            existing.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            TempData["Success"] = "Индикатор успешно обновлён";
            return RedirectToAction(nameof(Index));
        }
        ViewBag.Categories = await _context.IndicatorCategories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync();
        ViewBag.ParentIndicators = await _context.Indicators.Where(i => i.ParentIndicatorId == null && i.Id != id).OrderBy(i => i.SortOrder).ToListAsync();
        return View(indicator);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Indicators, PermissionType.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var indicator = await _context.Indicators.FindAsync(id);
        if (indicator != null)
        {
            _context.Indicators.Remove(indicator);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Индикатор удалён";
        }
        return RedirectToAction(nameof(Index));
    }

    // Indicator Values management
    public async Task<IActionResult> Values(int indicatorId)
    {
        var indicator = await _context.Indicators
            .Include(i => i.Values)
                .ThenInclude(iv => iv.Village)
                    .ThenInclude(v => v!.Jamoat)
                        .ThenInclude(j => j.District)
            .Include(i => i.Values)
                .ThenInclude(iv => iv.District)
            .FirstOrDefaultAsync(i => i.Id == indicatorId);

        if (indicator == null) return NotFound();

        ViewBag.Indicator = indicator;
        ViewBag.Districts = await _context.Districts.ToListAsync();
        ViewBag.Villages = await _context.Villages
            .Include(v => v.Jamoat)
                .ThenInclude(j => j.District)
            .ToListAsync();

        // Load geographic data if indicator is linked to a geo source
        if (indicator.GeoDataSource != GeoDataSource.None)
        {
            var geoVillages = await _context.Villages
                .Include(v => v.Jamoat)
                    .ThenInclude(j => j.District)
                .Include(v => v.Schools)
                .Include(v => v.HealthFacilities)
                .OrderBy(v => v.Jamoat.District.NameRu)
                    .ThenBy(v => v.Jamoat.NameRu)
                        .ThenBy(v => v.NameRu)
                .ToListAsync();
            ViewBag.GeoVillages = geoVillages;
        }

        return View(indicator.Values.OrderByDescending(v => v.MeasurementDate).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddValue(IndicatorValue value)
    {
        ModelState.Remove("Indicator");
        ModelState.Remove("Village");
        ModelState.Remove("District");
        
        if (ModelState.IsValid)
        {
            value.CreatedAt = DateTime.UtcNow;
            _context.IndicatorValues.Add(value);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Значение индикатора добавлено";
        }
        return RedirectToAction(nameof(Values), new { indicatorId = value.IndicatorId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteValue(int id, int indicatorId)
    {
        var value = await _context.IndicatorValues.FindAsync(id);
        if (value != null)
        {
            _context.IndicatorValues.Remove(value);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Значение удалено";
        }
        return RedirectToAction(nameof(Values), new { indicatorId });
    }
}
