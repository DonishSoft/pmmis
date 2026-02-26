using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;
using PMMIS.Web.Authorization;
using System.Security.Claims;

namespace PMMIS.Web.Controllers;

/// <summary>
/// Управление тендерами
/// </summary>
[Authorize]
[RequirePermission(MenuKeys.Tenders, PermissionType.View)]
public class TendersController : Controller
{
    private readonly ApplicationDbContext _context;

    public TendersController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Список тендеров
    /// </summary>
    public async Task<IActionResult> Index(TenderStatus? status, string? search)
    {
        var query = _context.Tenders
            .Include(t => t.ProcurementPlan)
                .ThenInclude(p => p.Project)
            .Include(t => t.Applicants)
            .Include(t => t.Extensions)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.ProcurementPlan.Description.Contains(search) 
                || t.ProcurementPlan.ReferenceNo.Contains(search));

        var tenders = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();

        ViewBag.StatusFilter = status;
        ViewBag.Search = search;
        return View(tenders);
    }

    /// <summary>
    /// Создание тендера — форма
    /// </summary>
    public async Task<IActionResult> Create(int? procurementPlanId)
    {
        await LoadProcurementPlans();
        ViewBag.SelectedProcurementPlanId = procurementPlanId;
        return View(new Tender { StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays(30) });
    }

    /// <summary>
    /// Создание тендера — сохранение + авто-статус
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Tender tender)
    {
        // Clear navigation property validation
        ModelState.Remove("ProcurementPlan");
        ModelState.Remove("Extensions");
        ModelState.Remove("Applicants");
        ModelState.Remove("CreatedBy");

        if (!ModelState.IsValid)
        {
            await LoadProcurementPlans();
            return View(tender);
        }

        // Check if tender already exists for this procurement plan
        var exists = await _context.Tenders.AnyAsync(t => t.ProcurementPlanId == tender.ProcurementPlanId);
        if (exists)
        {
            ModelState.AddModelError("ProcurementPlanId", "Тендер для этой позиции уже существует");
            await LoadProcurementPlans();
            return View(tender);
        }

        tender.CreatedAt = DateTime.UtcNow;
        tender.CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        tender.Status = TenderStatus.Open;

        _context.Tenders.Add(tender);

        // Auto-update procurement plan status to InProgress
        var procPlan = await _context.ProcurementPlans.FindAsync(tender.ProcurementPlanId);
        if (procPlan != null && procPlan.Status == ProcurementStatus.Planned)
        {
            procPlan.Status = ProcurementStatus.InProgress;
            procPlan.AdvertisementDate = tender.StartDate;
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "Тендер успешно объявлен";
        return RedirectToAction(nameof(Details), new { id = tender.Id });
    }

    /// <summary>
    /// Детали тендера (3 вкладки)
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var tender = await _context.Tenders
            .Include(t => t.ProcurementPlan)
                .ThenInclude(p => p.Project)
            .Include(t => t.Extensions.OrderByDescending(e => e.ExtendedAt))
            .Include(t => t.Applicants.OrderByDescending(a => a.AddedAt))
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tender == null) return NotFound();
        return View(tender);
    }

    /// <summary>
    /// Продление срока тендера
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExtendDeadline(int tenderId, DateTime newEndDate, string reason)
    {
        var tender = await _context.Tenders.FindAsync(tenderId);
        if (tender == null) return NotFound();

        if (newEndDate <= tender.EndDate)
            return Json(new { success = false, error = "Новая дата должна быть позже текущей" });

        var extension = new TenderExtension
        {
            TenderId = tenderId,
            PreviousEndDate = tender.EndDate,
            NewEndDate = newEndDate,
            Reason = reason,
            ExtendedAt = DateTime.UtcNow,
            ExtendedBy = User.FindFirstValue(ClaimTypes.NameIdentifier)
        };

        _context.TenderExtensions.Add(extension);
        tender.EndDate = newEndDate;
        tender.Status = TenderStatus.Extended;

        await _context.SaveChangesAsync();
        TempData["Success"] = $"Срок тендера продлён до {newEndDate:dd.MM.yyyy}";
        return Json(new { success = true });
    }

    /// <summary>
    /// Закрытие тендера
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseTender(int tenderId)
    {
        var tender = await _context.Tenders.FindAsync(tenderId);
        if (tender == null) return NotFound();

        tender.Status = TenderStatus.Closed;
        
        // Update procurement plan status to Evaluation
        var procPlan = await _context.ProcurementPlans.FindAsync(tender.ProcurementPlanId);
        if (procPlan != null)
            procPlan.Status = ProcurementStatus.Evaluation;

        await _context.SaveChangesAsync();
        TempData["Success"] = "Тендер закрыт";
        return RedirectToAction(nameof(Details), new { id = tenderId });
    }

    /// <summary>
    /// Удаление тендера
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var tender = await _context.Tenders
            .Include(t => t.Extensions)
            .Include(t => t.Applicants)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (tender == null) return NotFound();

        // Revert procurement plan status
        var procPlan = await _context.ProcurementPlans.FindAsync(tender.ProcurementPlanId);
        if (procPlan != null && (procPlan.Status == ProcurementStatus.InProgress || procPlan.Status == ProcurementStatus.Evaluation))
            procPlan.Status = ProcurementStatus.Planned;

        _context.TenderExtensions.RemoveRange(tender.Extensions);
        _context.TenderApplicants.RemoveRange(tender.Applicants);
        _context.Tenders.Remove(tender);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Тендер удалён";
        return RedirectToAction(nameof(Index));
    }

    // ========== API: Applicants ==========

    /// <summary>
    /// Получить список участников
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetApplicants(int tenderId)
    {
        var applicants = await _context.TenderApplicants
            .Where(a => a.TenderId == tenderId)
            .OrderByDescending(a => a.AddedAt)
            .Select(a => new
            {
                a.Id, a.CompanyName, a.CompanyType, a.Email, a.Phone,
                a.IsForeign, a.Country1, a.Country2, a.AddedAt
            })
            .ToListAsync();
        return Json(applicants);
    }

    /// <summary>
    /// Добавить участника
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddApplicant([FromBody] TenderApplicant applicant)
    {
        if (string.IsNullOrWhiteSpace(applicant.CompanyName))
            return Json(new { success = false, error = "Наименование компании обязательно" });

        applicant.AddedAt = DateTime.UtcNow;
        _context.TenderApplicants.Add(applicant);
        await _context.SaveChangesAsync();

        return Json(new { success = true, id = applicant.Id });
    }

    /// <summary>
    /// Удалить участника
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RemoveApplicant(int id)
    {
        var applicant = await _context.TenderApplicants.FindAsync(id);
        if (applicant == null) return Json(new { success = false });

        _context.TenderApplicants.Remove(applicant);
        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }

    // ========== Helpers ==========

    private async Task LoadProcurementPlans()
    {
        // Get IDs of procurement plans that already have tenders
        var usedPlanIds = await _context.Tenders
            .Select(t => t.ProcurementPlanId)
            .ToListAsync();

        var plans = await _context.ProcurementPlans
            .Where(p => p.Status == ProcurementStatus.Planned && !usedPlanIds.Contains(p.Id))
            .OrderBy(p => p.ReferenceNo)
            .ToListAsync();

        ViewBag.ProcurementPlans = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
            plans.Select(p => new { p.Id, Display = p.ReferenceNo + " — " + p.Description }),
            "Id", "Display");
    }
}
