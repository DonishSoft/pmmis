using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;

namespace PMMIS.Web.Controllers;

/// <summary>
/// Управление справочниками системы
/// </summary>
[Authorize(Roles = $"{UserRoles.PmuAdmin},{UserRoles.PmuStaff}")]
public class ReferenceDataController : Controller
{
    private readonly ApplicationDbContext _context;

    public ReferenceDataController(ApplicationDbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        return View();
    }

    #region Типы учреждений образования

    public async Task<IActionResult> EducationTypes()
    {
        var types = await _context.EducationInstitutionTypes
            .OrderBy(t => t.SortOrder)
            .ToListAsync();
        return View(types);
    }

    public IActionResult CreateEducationType()
    {
        var maxOrder = _context.EducationInstitutionTypes.Any() 
            ? _context.EducationInstitutionTypes.Max(t => t.SortOrder) 
            : 0;
        return View(new EducationInstitutionType { SortOrder = maxOrder + 1 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEducationType(EducationInstitutionType type)
    {
        ModelState.Remove("Schools");
        
        if (ModelState.IsValid)
        {
            type.CreatedAt = DateTime.UtcNow;
            _context.EducationInstitutionTypes.Add(type);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Тип учреждения успешно добавлен";
            return RedirectToAction(nameof(EducationTypes));
        }
        return View(type);
    }

    public async Task<IActionResult> EditEducationType(int id)
    {
        var type = await _context.EducationInstitutionTypes.FindAsync(id);
        if (type == null) return NotFound();
        return View(type);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEducationType(int id, EducationInstitutionType type)
    {
        if (id != type.Id) return NotFound();
        
        ModelState.Remove("Schools");

        if (ModelState.IsValid)
        {
            var existing = await _context.EducationInstitutionTypes.FindAsync(id);
            if (existing == null) return NotFound();
            
            existing.Name = type.Name;
            existing.SortOrder = type.SortOrder;
            existing.IsActive = type.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            TempData["Success"] = "Тип учреждения успешно обновлён";
            return RedirectToAction(nameof(EducationTypes));
        }
        return View(type);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEducationType(int id)
    {
        var type = await _context.EducationInstitutionTypes.FindAsync(id);
        if (type != null)
        {
            // Check if used by schools
            var usedCount = await _context.Schools.CountAsync(s => s.TypeId == id);
            if (usedCount > 0)
            {
                TempData["Error"] = $"Невозможно удалить: тип используется в {usedCount} школах";
                return RedirectToAction(nameof(EducationTypes));
            }
            
            _context.EducationInstitutionTypes.Remove(type);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Тип учреждения удалён";
        }
        return RedirectToAction(nameof(EducationTypes));
    }

    #endregion

    #region Типы медучреждений

    public async Task<IActionResult> HealthFacilityTypes()
    {
        var types = await _context.HealthFacilityTypes
            .OrderBy(t => t.SortOrder)
            .ToListAsync();
        return View(types);
    }

    public IActionResult CreateHealthFacilityType()
    {
        var maxOrder = _context.HealthFacilityTypes.Any() 
            ? _context.HealthFacilityTypes.Max(t => t.SortOrder) 
            : 0;
        return View(new HealthFacilityType { SortOrder = maxOrder + 1 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateHealthFacilityType(HealthFacilityType type)
    {
        ModelState.Remove("HealthFacilities");
        
        if (ModelState.IsValid)
        {
            type.CreatedAt = DateTime.UtcNow;
            _context.HealthFacilityTypes.Add(type);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Тип медучреждения успешно добавлен";
            return RedirectToAction(nameof(HealthFacilityTypes));
        }
        return View(type);
    }

    public async Task<IActionResult> EditHealthFacilityType(int id)
    {
        var type = await _context.HealthFacilityTypes.FindAsync(id);
        if (type == null) return NotFound();
        return View(type);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditHealthFacilityType(int id, HealthFacilityType type)
    {
        if (id != type.Id) return NotFound();
        
        ModelState.Remove("HealthFacilities");

        if (ModelState.IsValid)
        {
            var existing = await _context.HealthFacilityTypes.FindAsync(id);
            if (existing == null) return NotFound();
            
            existing.Name = type.Name;
            existing.SortOrder = type.SortOrder;
            existing.IsActive = type.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            TempData["Success"] = "Тип медучреждения успешно обновлён";
            return RedirectToAction(nameof(HealthFacilityTypes));
        }
        return View(type);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteHealthFacilityType(int id)
    {
        var type = await _context.HealthFacilityTypes.FindAsync(id);
        if (type != null)
        {
            var usedCount = await _context.HealthFacilities.CountAsync(h => h.TypeId == id);
            if (usedCount > 0)
            {
                TempData["Error"] = $"Невозможно удалить: тип используется в {usedCount} медучреждениях";
                return RedirectToAction(nameof(HealthFacilityTypes));
            }
            
            _context.HealthFacilityTypes.Remove(type);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Тип медучреждения удалён";
        }
        return RedirectToAction(nameof(HealthFacilityTypes));
    }

    #endregion

    #region Indicator Categories

    public async Task<IActionResult> IndicatorCategories()
    {
        var categories = await _context.IndicatorCategories
            .OrderBy(c => c.SortOrder)
            .ToListAsync();
        return View(categories);
    }

    public IActionResult CreateIndicatorCategory()
    {
        return View(new IndicatorCategory());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateIndicatorCategory(IndicatorCategory category)
    {
        ModelState.Remove("Indicators");
        
        if (ModelState.IsValid)
        {
            category.CreatedAt = DateTime.UtcNow;
            _context.IndicatorCategories.Add(category);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Категория индикаторов успешно создана";
            return RedirectToAction(nameof(IndicatorCategories));
        }
        return View(category);
    }

    public async Task<IActionResult> EditIndicatorCategory(int id)
    {
        var category = await _context.IndicatorCategories.FindAsync(id);
        if (category == null) return NotFound();
        return View(category);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditIndicatorCategory(int id, IndicatorCategory category)
    {
        if (id != category.Id) return NotFound();
        
        ModelState.Remove("Indicators");

        if (ModelState.IsValid)
        {
            var existing = await _context.IndicatorCategories.FindAsync(id);
            if (existing == null) return NotFound();
            
            existing.Name = category.Name;
            existing.SortOrder = category.SortOrder;
            existing.IsActive = category.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            TempData["Success"] = "Категория индикаторов успешно обновлена";
            return RedirectToAction(nameof(IndicatorCategories));
        }
        return View(category);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteIndicatorCategory(int id)
    {
        var category = await _context.IndicatorCategories.FindAsync(id);
        if (category != null)
        {
            var usedCount = await _context.Indicators.CountAsync(i => i.CategoryId == id);
            if (usedCount > 0)
            {
                TempData["Error"] = $"Невозможно удалить: категория используется в {usedCount} индикаторах";
                return RedirectToAction(nameof(IndicatorCategories));
            }
            
            _context.IndicatorCategories.Remove(category);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Категория индикаторов удалена";
        }
        return RedirectToAction(nameof(IndicatorCategories));
    }

    #endregion
}
