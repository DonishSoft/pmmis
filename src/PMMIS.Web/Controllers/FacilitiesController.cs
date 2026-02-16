using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;
using PMMIS.Web.Authorization;

namespace PMMIS.Web.Controllers;

/// <summary>
/// Управление учреждениями: Школы и Медучреждения
/// </summary>
[Authorize]
[RequirePermission(MenuKeys.Geography, PermissionType.View)]
public class FacilitiesController : Controller
{
    private readonly ApplicationDbContext _context;

    public FacilitiesController(ApplicationDbContext context)
    {
        _context = context;
    }

    #region Schools

    [RequirePermission(MenuKeys.Geography, PermissionType.Create)]
    public async Task<IActionResult> CreateSchool(int villageId)
    {
        var village = await _context.Villages.FindAsync(villageId);
        if (village == null) return NotFound();

        ViewBag.Village = village;
        ViewBag.EducationTypes = await _context.EducationInstitutionTypes.OrderBy(t => t.SortOrder).ToListAsync();
        return View(new School { VillageId = villageId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Geography, PermissionType.Create)]
    public async Task<IActionResult> CreateSchool(School school)
    {
        ModelState.Remove("Village");
        
        if (school.Number == 0)
        {
            var count = await _context.Schools.CountAsync(s => s.VillageId == school.VillageId);
            school.Number = count + 1;
        }

        if (ModelState.IsValid)
        {
            school.CreatedAt = DateTime.UtcNow;
            _context.Schools.Add(school);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Школа успешно добавлена";
            return RedirectToAction("VillageDetails", "Geography", new { id = school.VillageId });
        }

        ViewBag.Village = await _context.Villages.FindAsync(school.VillageId);
        return View(school);
    }

    [RequirePermission(MenuKeys.Geography, PermissionType.Edit)]
    public async Task<IActionResult> EditSchool(int id)
    {
        var school = await _context.Schools
            .Include(s => s.Village)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (school == null) return NotFound();

        ViewBag.Village = school.Village;
        ViewBag.EducationTypes = await _context.EducationInstitutionTypes.OrderBy(t => t.SortOrder).ToListAsync();
        return View(school);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Geography, PermissionType.Edit)]
    public async Task<IActionResult> EditSchool(int id, School school)
    {
        if (id != school.Id) return NotFound();

        ModelState.Remove("Village");
        ModelState.Remove("Type");

        if (ModelState.IsValid)
        {
            var existing = await _context.Schools.FindAsync(id);
            if (existing == null) return NotFound();
            
            existing.Number = school.Number;
            existing.Name = school.Name;
            existing.TypeId = school.TypeId;
            existing.TotalStudents = school.TotalStudents;
            existing.FemaleStudents = school.FemaleStudents;
            existing.TeachersCount = school.TeachersCount;
            existing.FemaleTeachersCount = school.FemaleTeachersCount;
            existing.HasWaterSupply = school.HasWaterSupply;
            existing.HasSanitation = school.HasSanitation;
            existing.Notes = school.Notes;
            existing.SortOrder = school.SortOrder;
            existing.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            TempData["Success"] = "Школа успешно обновлена";
            return RedirectToAction("VillageDetails", "Geography", new { id = existing.VillageId });
        }

        ViewBag.Village = await _context.Villages.FindAsync(school.VillageId);
        ViewBag.EducationTypes = await _context.EducationInstitutionTypes.OrderBy(t => t.SortOrder).ToListAsync();
        return View(school);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Geography, PermissionType.Delete)]
    public async Task<IActionResult> DeleteSchool(int id)
    {
        var school = await _context.Schools.FindAsync(id);
        if (school != null)
        {
            var villageId = school.VillageId;
            _context.Schools.Remove(school);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Школа удалена";
            return RedirectToAction("VillageDetails", "Geography", new { id = villageId });
        }
        return RedirectToAction("Index", "Geography");
    }

    #endregion

    #region Health Facilities

    [RequirePermission(MenuKeys.Geography, PermissionType.Create)]
    public async Task<IActionResult> CreateHealthFacility(int villageId)
    {
        var village = await _context.Villages.FindAsync(villageId);
        if (village == null) return NotFound();

        ViewBag.Village = village;
        ViewBag.HealthFacilityTypes = await _context.HealthFacilityTypes.OrderBy(t => t.SortOrder).ToListAsync();
        return View(new HealthFacility { VillageId = villageId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Geography, PermissionType.Create)]
    public async Task<IActionResult> CreateHealthFacility(HealthFacility facility)
    {
        ModelState.Remove("Village");
        ModelState.Remove("Type");
        
        if (ModelState.IsValid)
        {
            facility.CreatedAt = DateTime.UtcNow;
            _context.HealthFacilities.Add(facility);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Медучреждение успешно добавлено";
            return RedirectToAction("VillageDetails", "Geography", new { id = facility.VillageId });
        }

        ViewBag.Village = await _context.Villages.FindAsync(facility.VillageId);
        ViewBag.HealthFacilityTypes = await _context.HealthFacilityTypes.OrderBy(t => t.SortOrder).ToListAsync();
        return View(facility);
    }

    [RequirePermission(MenuKeys.Geography, PermissionType.Edit)]
    public async Task<IActionResult> EditHealthFacility(int id)
    {
        var facility = await _context.HealthFacilities
            .Include(h => h.Village)
            .FirstOrDefaultAsync(h => h.Id == id);

        if (facility == null) return NotFound();

        ViewBag.Village = facility.Village;
        ViewBag.HealthFacilityTypes = await _context.HealthFacilityTypes.OrderBy(t => t.SortOrder).ToListAsync();
        return View(facility);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Geography, PermissionType.Edit)]
    public async Task<IActionResult> EditHealthFacility(int id, HealthFacility facility)
    {
        if (id != facility.Id) return NotFound();

        ModelState.Remove("Village");
        ModelState.Remove("Type");

        if (ModelState.IsValid)
        {
            var existing = await _context.HealthFacilities.FindAsync(id);
            if (existing == null) return NotFound();
            
            existing.TypeId = facility.TypeId;
            existing.Name = facility.Name;
            existing.TotalStaff = facility.TotalStaff;
            existing.FemaleStaff = facility.FemaleStaff;
            existing.PatientsPerDay = facility.PatientsPerDay;
            existing.HasWaterSupply = facility.HasWaterSupply;
            existing.HasSanitation = facility.HasSanitation;
            existing.Notes = facility.Notes;
            existing.SortOrder = facility.SortOrder;
            existing.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            TempData["Success"] = "Медучреждение успешно обновлено";
            return RedirectToAction("VillageDetails", "Geography", new { id = existing.VillageId });
        }

        ViewBag.Village = await _context.Villages.FindAsync(facility.VillageId);
        ViewBag.HealthFacilityTypes = await _context.HealthFacilityTypes.OrderBy(t => t.SortOrder).ToListAsync();
        return View(facility);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Geography, PermissionType.Delete)]
    public async Task<IActionResult> DeleteHealthFacility(int id)
    {
        var facility = await _context.HealthFacilities.FindAsync(id);
        if (facility != null)
        {
            var villageId = facility.VillageId;
            _context.HealthFacilities.Remove(facility);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Медучреждение удалено";
            return RedirectToAction("VillageDetails", "Geography", new { id = villageId });
        }
        return RedirectToAction("Index", "Geography");
    }

    #endregion

    #region JSON API for TreeView

    // Schools
    [HttpGet]
    public async Task<IActionResult> GetSchoolJson(int id)
    {
        var s = await _context.Schools.FindAsync(id);
        if (s == null) return NotFound();
        return Json(new { s.Id, s.VillageId, s.TypeId, s.Number, s.SortOrder, s.Name,
            s.TotalStudents, s.FemaleStudents, s.TeachersCount, s.FemaleTeachersCount, s.HasWaterSupply, s.HasSanitation, s.Notes });
    }

    [HttpPost]
    [RequirePermission(MenuKeys.Geography, PermissionType.Edit)]
    public async Task<IActionResult> SaveSchoolJson([FromBody] School model)
    {
        if (model.Id == 0)
        {
            if (model.Number == 0)
            {
                var count = await _context.Schools.CountAsync(s => s.VillageId == model.VillageId);
                model.Number = count + 1;
            }
            model.CreatedAt = DateTime.UtcNow;
            _context.Schools.Add(model);
        }
        else
        {
            var existing = await _context.Schools.FindAsync(model.Id);
            if (existing == null) return NotFound();
            existing.TypeId = model.TypeId;
            existing.Number = model.Number;
            existing.SortOrder = model.SortOrder;
            existing.Name = model.Name;
            existing.TotalStudents = model.TotalStudents;
            existing.FemaleStudents = model.FemaleStudents;
            existing.TeachersCount = model.TeachersCount;
            existing.FemaleTeachersCount = model.FemaleTeachersCount;
            existing.HasWaterSupply = model.HasWaterSupply;
            existing.HasSanitation = model.HasSanitation;
            existing.Notes = model.Notes;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
        return Json(new { success = true, id = model.Id > 0 ? model.Id : _context.Schools.OrderByDescending(x => x.Id).First().Id });
    }

    [HttpDelete]
    [RequirePermission(MenuKeys.Geography, PermissionType.Delete)]
    public async Task<IActionResult> DeleteSchoolJson(int id)
    {
        var s = await _context.Schools.FindAsync(id);
        if (s == null) return NotFound();
        _context.Schools.Remove(s);
        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }

    // HealthFacilities
    [HttpGet]
    public async Task<IActionResult> GetHealthFacilityJson(int id)
    {
        var h = await _context.HealthFacilities.FindAsync(id);
        if (h == null) return NotFound();
        return Json(new { h.Id, h.VillageId, h.TypeId, h.SortOrder, h.Name,
            h.TotalStaff, h.FemaleStaff, h.PatientsPerDay, h.HasWaterSupply, h.HasSanitation, h.Notes });
    }

    [HttpPost]
    [RequirePermission(MenuKeys.Geography, PermissionType.Edit)]
    public async Task<IActionResult> SaveHealthFacilityJson([FromBody] HealthFacility model)
    {
        if (model.Id == 0)
        {
            model.CreatedAt = DateTime.UtcNow;
            _context.HealthFacilities.Add(model);
        }
        else
        {
            var existing = await _context.HealthFacilities.FindAsync(model.Id);
            if (existing == null) return NotFound();
            existing.TypeId = model.TypeId;
            existing.SortOrder = model.SortOrder;
            existing.Name = model.Name;
            existing.TotalStaff = model.TotalStaff;
            existing.FemaleStaff = model.FemaleStaff;
            existing.PatientsPerDay = model.PatientsPerDay;
            existing.HasWaterSupply = model.HasWaterSupply;
            existing.HasSanitation = model.HasSanitation;
            existing.Notes = model.Notes;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
        return Json(new { success = true, id = model.Id > 0 ? model.Id : _context.HealthFacilities.OrderByDescending(x => x.Id).First().Id });
    }

    [HttpDelete]
    [RequirePermission(MenuKeys.Geography, PermissionType.Delete)]
    public async Task<IActionResult> DeleteHealthFacilityJson(int id)
    {
        var h = await _context.HealthFacilities.FindAsync(id);
        if (h == null) return NotFound();
        _context.HealthFacilities.Remove(h);
        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }

    #endregion

    #region Reference Data

    [HttpGet]
    public async Task<IActionResult> GetEducationTypesJson()
    {
        var types = await _context.EducationInstitutionTypes
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync();
        return Json(types);
    }

    [HttpGet]
    public async Task<IActionResult> GetHealthFacilityTypesJson()
    {
        var types = await _context.HealthFacilityTypes
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync();
        return Json(types);
    }

    #endregion
}
