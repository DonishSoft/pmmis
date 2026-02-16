using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;
using PMMIS.Web.Authorization;

namespace PMMIS.Web.Controllers;

/// <summary>
/// Управление географическими справочниками: Районы, Джамоаты, Сёла
/// </summary>
[Authorize]
[RequirePermission(MenuKeys.Geography, PermissionType.View)]
public class GeographyController : Controller
{
    private readonly ApplicationDbContext _context;

    public GeographyController(ApplicationDbContext context)
    {
        _context = context;
    }

    #region Districts

    public async Task<IActionResult> Index(string? search)
    {
        var query = _context.Districts
            .Include(d => d.Jamoats)
                .ThenInclude(j => j.Villages)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(d => d.NameRu.Contains(search) || d.NameTj.Contains(search) || d.Code.Contains(search));
            ViewBag.Search = search;
        }

        var districts = await query
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.NameRu)
            .ToListAsync();

        return View(districts);
    }

    public IActionResult CreateDistrict()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDistrict(District district)
    {
        if (ModelState.IsValid)
        {
            district.CreatedAt = DateTime.UtcNow;
            _context.Districts.Add(district);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Район успешно добавлен";
            return RedirectToAction(nameof(Index));
        }
        return View(district);
    }

    public async Task<IActionResult> EditDistrict(int id)
    {
        var district = await _context.Districts.FindAsync(id);
        if (district == null) return NotFound();
        return View(district);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditDistrict(int id, District district)
    {
        if (id != district.Id) return NotFound();

        // Clear navigation property validation errors
        ModelState.Remove("Jamoats");

        if (ModelState.IsValid)
        {
            var existing = await _context.Districts.FindAsync(id);
            if (existing == null) return NotFound();
            
            // Update only editable fields
            existing.Code = district.Code;
            existing.NameRu = district.NameRu;
            existing.NameTj = district.NameTj;
            existing.NameEn = district.NameEn;
            existing.SortOrder = district.SortOrder;
            existing.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            TempData["Success"] = "Район успешно обновлён";
            return RedirectToAction(nameof(Index));
        }
        return View(district);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = UserRoles.PmuAdmin)]
    public async Task<IActionResult> DeleteDistrict(int id)
    {
        var district = await _context.Districts.FindAsync(id);
        if (district != null)
        {
            _context.Districts.Remove(district);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Район удалён";
        }
        return RedirectToAction(nameof(Index));
    }

    #endregion

    #region Jamoats

    public async Task<IActionResult> Jamoats(int districtId, string? search)
    {
        var district = await _context.Districts
            .Include(d => d.Jamoats)
                .ThenInclude(j => j.Villages)
            .FirstOrDefaultAsync(d => d.Id == districtId);

        if (district == null) return NotFound();

        var jamoats = district.Jamoats.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            jamoats = jamoats.Where(j => j.NameRu.Contains(search) || j.NameTj.Contains(search) || j.Code.Contains(search));
            ViewBag.Search = search;
        }

        ViewBag.District = district;
        return View(jamoats.OrderBy(j => j.SortOrder).ThenBy(j => j.NameRu).ToList());
    }

    public async Task<IActionResult> CreateJamoat(int districtId)
    {
        var district = await _context.Districts.FindAsync(districtId);
        if (district == null) return NotFound();

        ViewBag.District = district;
        return View(new Jamoat { DistrictId = districtId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateJamoat(Jamoat jamoat)
    {
        // Clear navigation property validation errors
        ModelState.Remove("District");
        
        // Auto-generate code if empty
        if (string.IsNullOrWhiteSpace(jamoat.Code))
        {
            var count = await _context.Jamoats.CountAsync(j => j.DistrictId == jamoat.DistrictId);
            jamoat.Code = $"JAM{count + 1:D2}";
        }

        if (ModelState.IsValid)
        {
            jamoat.CreatedAt = DateTime.UtcNow;
            _context.Jamoats.Add(jamoat);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Джамоат успешно добавлен";
            return RedirectToAction(nameof(Jamoats), new { districtId = jamoat.DistrictId });
        }

        ViewBag.District = await _context.Districts.FindAsync(jamoat.DistrictId);
        return View(jamoat);
    }

    public async Task<IActionResult> EditJamoat(int id)
    {
        var jamoat = await _context.Jamoats.Include(j => j.District).FirstOrDefaultAsync(j => j.Id == id);
        if (jamoat == null) return NotFound();

        ViewBag.District = jamoat.District;
        return View(jamoat);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditJamoat(int id, Jamoat jamoat)
    {
        if (id != jamoat.Id) return NotFound();

        // Clear navigation property validation errors
        ModelState.Remove("District");
        ModelState.Remove("Villages");

        if (ModelState.IsValid)
        {
            var existing = await _context.Jamoats.FindAsync(id);
            if (existing == null) return NotFound();
            
            // Update only editable fields
            existing.Code = jamoat.Code;
            existing.NameRu = jamoat.NameRu;
            existing.NameTj = jamoat.NameTj;
            existing.NameEn = jamoat.NameEn;
            existing.SortOrder = jamoat.SortOrder;
            existing.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            TempData["Success"] = "Джамоат успешно обновлён";
            return RedirectToAction(nameof(Jamoats), new { districtId = existing.DistrictId });
        }

        ViewBag.District = await _context.Districts.FindAsync(jamoat.DistrictId);
        return View(jamoat);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = UserRoles.PmuAdmin)]
    public async Task<IActionResult> DeleteJamoat(int id)
    {
        var jamoat = await _context.Jamoats.FindAsync(id);
        if (jamoat != null)
        {
            var districtId = jamoat.DistrictId;
            _context.Jamoats.Remove(jamoat);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Джамоат удалён";
            return RedirectToAction(nameof(Jamoats), new { districtId });
        }
        return RedirectToAction(nameof(Index));
    }

    #endregion

    #region Villages

    public async Task<IActionResult> Villages(int jamoatId, string? search)
    {
        var jamoat = await _context.Jamoats
            .Include(j => j.District)
            .Include(j => j.Villages)
            .FirstOrDefaultAsync(j => j.Id == jamoatId);

        if (jamoat == null) return NotFound();

        var villages = jamoat.Villages.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            villages = villages.Where(v => v.NameRu.Contains(search) || v.NameTj.Contains(search) || v.Zone.Contains(search));
            ViewBag.Search = search;
        }

        ViewBag.Jamoat = jamoat;
        ViewBag.District = jamoat.District;
        return View(villages.OrderBy(v => v.SortOrder).ThenBy(v => v.Number).ToList());
    }

    public async Task<IActionResult> CreateVillage(int jamoatId)
    {
        var jamoat = await _context.Jamoats.Include(j => j.District).FirstOrDefaultAsync(j => j.Id == jamoatId);
        if (jamoat == null) return NotFound();

        ViewBag.Jamoat = jamoat;
        return View(new Village { JamoatId = jamoatId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateVillage(Village village)
    {
        // Clear navigation property validation errors
        ModelState.Remove("Jamoat");
        ModelState.Remove("Schools");
        ModelState.Remove("HealthFacilities");
        ModelState.Remove("IndicatorValues");
        
        // Auto-generate number if zero
        if (village.Number == 0)
        {
            var count = await _context.Villages.CountAsync(v => v.JamoatId == village.JamoatId);
            village.Number = count + 1;
        }

        if (ModelState.IsValid)
        {
            village.CreatedAt = DateTime.UtcNow;
            _context.Villages.Add(village);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Населённый пункт успешно добавлен";
            return RedirectToAction(nameof(Villages), new { jamoatId = village.JamoatId });
        }

        ViewBag.Jamoat = await _context.Jamoats.Include(j => j.District).FirstOrDefaultAsync(j => j.Id == village.JamoatId);
        return View(village);
    }

    public async Task<IActionResult> EditVillage(int id)
    {
        var village = await _context.Villages
            .Include(v => v.Jamoat)
                .ThenInclude(j => j.District)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (village == null) return NotFound();

        ViewBag.Jamoat = village.Jamoat;
        return View(village);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditVillage(int id, Village village)
    {
        if (id != village.Id) return NotFound();

        // Clear navigation property validation errors
        ModelState.Remove("Jamoat");
        ModelState.Remove("Schools");
        ModelState.Remove("HealthFacilities");
        ModelState.Remove("IndicatorValues");

        if (ModelState.IsValid)
        {
            var existing = await _context.Villages.FindAsync(id);
            if (existing == null) return NotFound();
            
            // Update only editable fields
            existing.Number = village.Number;
            existing.Zone = village.Zone;
            existing.NameRu = village.NameRu;
            existing.NameTj = village.NameTj;
            existing.NameEn = village.NameEn;
            existing.Population2020 = village.Population2020;
            existing.PopulationCurrent = village.PopulationCurrent;
            existing.FemalePopulation = village.FemalePopulation;
            existing.Households2020 = village.Households2020;
            existing.HouseholdsCurrent = village.HouseholdsCurrent;
            existing.IsCoveredByProject = village.IsCoveredByProject;
            existing.SortOrder = village.SortOrder;
            existing.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            TempData["Success"] = "Населённый пункт успешно обновлён";
            return RedirectToAction(nameof(Villages), new { jamoatId = existing.JamoatId });
        }

        ViewBag.Jamoat = await _context.Jamoats.Include(j => j.District).FirstOrDefaultAsync(j => j.Id == village.JamoatId);
        return View(village);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = UserRoles.PmuAdmin)]
    public async Task<IActionResult> DeleteVillage(int id)
    {
        var village = await _context.Villages.FindAsync(id);
        if (village != null)
        {
            var jamoatId = village.JamoatId;
            _context.Villages.Remove(village);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Населённый пункт удалён";
            return RedirectToAction(nameof(Villages), new { jamoatId });
        }
        return RedirectToAction(nameof(Index));
    }

    #endregion

    #region Village Details

    public async Task<IActionResult> VillageDetails(int id)
    {
        var village = await _context.Villages
            .Include(v => v.Jamoat)
                .ThenInclude(j => j.District)
            .Include(v => v.Schools)
            .Include(v => v.HealthFacilities)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (village == null) return NotFound();

        return View(village);
    }

    #endregion

    #region API for Dropdowns

    [HttpGet]
    public async Task<IActionResult> GetJamoatsByDistrict(int districtId)
    {
        var jamoats = await _context.Jamoats
            .Where(j => j.DistrictId == districtId)
            .Select(j => new { j.Id, Name = j.NameRu })
            .ToListAsync();
        return Json(jamoats);
    }

    [HttpGet]
    public async Task<IActionResult> GetVillagesByJamoat(int jamoatId)
    {
        var villages = await _context.Villages
            .Where(v => v.JamoatId == jamoatId)
            .Select(v => new { v.Id, Name = v.NameRu })
            .ToListAsync();
        return Json(villages);
    }

    #endregion

    #region TreeView Visualization

    /// <summary>
    /// Display full hierarchical structure: Districts → Jamoats → Villages → Schools/HealthFacilities
    /// </summary>
    public async Task<IActionResult> TreeView()
    {
        var districts = await _context.Districts
            .Include(d => d.Jamoats)
                .ThenInclude(j => j.Villages)
                    .ThenInclude(v => v.Schools)
            .Include(d => d.Jamoats)
                .ThenInclude(j => j.Villages)
                    .ThenInclude(v => v.HealthFacilities)
            .OrderBy(d => d.NameRu)
            .ToListAsync();

        return View(districts);
    }

    #endregion

    #region TreeView API (JSON for Modal CRUD)

    // Districts
    [HttpGet]
    public async Task<IActionResult> GetDistrictJson(int id)
    {
        var d = await _context.Districts.FindAsync(id);
        if (d == null) return NotFound();
        return Json(new { d.Id, d.Code, d.SortOrder, d.NameRu, d.NameTj, d.NameEn });
    }

    [HttpPost]
    public async Task<IActionResult> SaveDistrictJson([FromBody] District model)
    {
        if (string.IsNullOrWhiteSpace(model.NameRu)) return BadRequest("NameRu required");
        
        if (model.Id == 0)
        {
            model.CreatedAt = DateTime.UtcNow;
            _context.Districts.Add(model);
        }
        else
        {
            var existing = await _context.Districts.FindAsync(model.Id);
            if (existing == null) return NotFound();
            existing.Code = model.Code;
            existing.SortOrder = model.SortOrder;
            existing.NameRu = model.NameRu;
            existing.NameTj = model.NameTj;
            existing.NameEn = model.NameEn;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
        return Json(new { success = true, id = model.Id > 0 ? model.Id : _context.Districts.OrderByDescending(x => x.Id).First().Id });
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteDistrictJson(int id)
    {
        var d = await _context.Districts.FindAsync(id);
        if (d == null) return NotFound();
        _context.Districts.Remove(d);
        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }

    // Jamoats
    [HttpGet]
    public async Task<IActionResult> GetJamoatJson(int id)
    {
        var j = await _context.Jamoats.FindAsync(id);
        if (j == null) return NotFound();
        return Json(new { j.Id, j.DistrictId, j.Code, j.SortOrder, j.NameRu, j.NameTj, j.NameEn });
    }

    [HttpPost]
    public async Task<IActionResult> SaveJamoatJson([FromBody] Jamoat model)
    {
        if (string.IsNullOrWhiteSpace(model.NameRu)) return BadRequest("NameRu required");
        
        if (model.Id == 0)
        {
            if (string.IsNullOrEmpty(model.Code))
            {
                var count = await _context.Jamoats.CountAsync(j => j.DistrictId == model.DistrictId);
                model.Code = $"JAM{(count + 1).ToString("D2")}";
            }
            model.CreatedAt = DateTime.UtcNow;
            _context.Jamoats.Add(model);
        }
        else
        {
            var existing = await _context.Jamoats.FindAsync(model.Id);
            if (existing == null) return NotFound();
            existing.Code = model.Code;
            existing.SortOrder = model.SortOrder;
            existing.NameRu = model.NameRu;
            existing.NameTj = model.NameTj;
            existing.NameEn = model.NameEn;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
        return Json(new { success = true, id = model.Id > 0 ? model.Id : _context.Jamoats.OrderByDescending(x => x.Id).First().Id });
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteJamoatJson(int id)
    {
        var j = await _context.Jamoats.FindAsync(id);
        if (j == null) return NotFound();
        _context.Jamoats.Remove(j);
        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }

    // Villages
    [HttpGet]
    public async Task<IActionResult> GetVillageJson(int id)
    {
        var v = await _context.Villages.FindAsync(id);
        if (v == null) return NotFound();
        return Json(new { v.Id, v.JamoatId, v.Zone, v.Number, v.SortOrder, v.NameRu, v.NameTj, v.NameEn,
            v.Population2020, v.PopulationCurrent, v.FemalePopulation, v.Households2020, v.HouseholdsCurrent, v.IsCoveredByProject });
    }

    [HttpPost]
    public async Task<IActionResult> SaveVillageJson([FromBody] Village model)
    {
        if (string.IsNullOrWhiteSpace(model.NameRu)) return BadRequest("NameRu required");
        
        if (model.Id == 0)
        {
            if (model.Number == 0)
            {
                var count = await _context.Villages.CountAsync(v => v.JamoatId == model.JamoatId);
                model.Number = count + 1;
            }
            model.CreatedAt = DateTime.UtcNow;
            _context.Villages.Add(model);
        }
        else
        {
            var existing = await _context.Villages.FindAsync(model.Id);
            if (existing == null) return NotFound();
            existing.Zone = model.Zone;
            existing.Number = model.Number;
            existing.SortOrder = model.SortOrder;
            existing.NameRu = model.NameRu;
            existing.NameTj = model.NameTj;
            existing.NameEn = model.NameEn;
            existing.Population2020 = model.Population2020;
            existing.PopulationCurrent = model.PopulationCurrent;
            existing.FemalePopulation = model.FemalePopulation;
            existing.Households2020 = model.Households2020;
            existing.HouseholdsCurrent = model.HouseholdsCurrent;
            existing.IsCoveredByProject = model.IsCoveredByProject;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
        return Json(new { success = true, id = model.Id > 0 ? model.Id : _context.Villages.OrderByDescending(x => x.Id).First().Id });
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteVillageJson(int id)
    {
        var v = await _context.Villages.FindAsync(id);
        if (v == null) return NotFound();
        _context.Villages.Remove(v);
        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }

    #endregion
}
