using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;
using PMMIS.Web.Authorization;
using PMMIS.Web.Services;
using System.Security.Claims;

namespace PMMIS.Web.Controllers;

/// <summary>
/// Управление подрядчиками
/// </summary>
[Authorize]
[RequirePermission(MenuKeys.Contractors, PermissionType.View)]
public class ContractorsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IFileService _fileService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDataAccessService _dataAccessService;

    public ContractorsController(
        ApplicationDbContext context, 
        IFileService fileService,
        UserManager<ApplicationUser> userManager,
        IDataAccessService dataAccessService)
    {
        _context = context;
        _fileService = fileService;
        _userManager = userManager;
        _dataAccessService = dataAccessService;
    }

    public async Task<IActionResult> Index()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var query = _context.Contractors
            .Include(c => c.Contracts)
            .AsQueryable();

        // Фильтрация по видимости данных
        if (!await _dataAccessService.CanViewAllAsync(currentUser, MenuKeys.Contractors))
        {
            var myContractorIds = await _dataAccessService.GetUserContractIdsAsync(currentUser.Id);
            var contractorIds = await _context.Contracts
                .Where(c => myContractorIds.Contains(c.Id))
                .Select(c => c.ContractorId)
                .Distinct()
                .ToListAsync();
            query = query.Where(c => contractorIds.Contains(c.Id));
        }

        var contractors = await query.OrderBy(c => c.Name).ToListAsync();
        return View(contractors);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Contractor contractor, List<IFormFile>? contractorFiles)
    {
        if (ModelState.IsValid)
        {
            contractor.CreatedAt = DateTime.UtcNow;
            _context.Contractors.Add(contractor);
            await _context.SaveChangesAsync();
            
            // Upload contractor documents
            if (contractorFiles?.Any() == true)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var docs = await _fileService.UploadFilesAsync(
                    contractorFiles, $"contractors/{contractor.Id}", DocumentType.CompanyRegistration, userId);
                foreach (var doc in docs)
                {
                    doc.ContractorId = contractor.Id;
                    _context.Documents.Add(doc);
                }
                await _context.SaveChangesAsync();
            }
            
            TempData["Success"] = "Подрядчик успешно добавлен";
            return RedirectToAction(nameof(Index));
        }
        return View(contractor);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var contractor = await _context.Contractors
            .Include(c => c.Contracts)
            .Include(c => c.Documents)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (contractor == null) return NotFound();
        return View(contractor);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Contractor contractor)
    {
        if (id != contractor.Id) return NotFound();

        if (ModelState.IsValid)
        {
            contractor.UpdatedAt = DateTime.UtcNow;
            _context.Update(contractor);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Подрядчик успешно обновлён";
            return RedirectToAction(nameof(Index));
        }
        return View(contractor);
    }

    public async Task<IActionResult> Details(int id)
    {
        var contractor = await _context.Contractors
            .Include(c => c.Contracts)
                .ThenInclude(ct => ct.Payments)
            .Include(c => c.Contracts)
                .ThenInclude(ct => ct.Project)
            .Include(c => c.Documents)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (contractor == null) return NotFound();
        return View(contractor);
    }
    
    /// <summary>
    /// Загрузить документы подрядчика
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadDocuments(int contractorId, List<IFormFile> files, DocumentType documentType)
    {
        if (files.Any())
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var docs = await _fileService.UploadFilesAsync(
                files, $"contractors/{contractorId}", documentType, userId);
            foreach (var doc in docs)
            {
                doc.ContractorId = contractorId;
                _context.Documents.Add(doc);
            }
            await _context.SaveChangesAsync();
            TempData["Success"] = "Документы загружены";
        }
        return RedirectToAction(nameof(Details), new { id = contractorId });
    }
    
    /// <summary>
    /// Удалить документ подрядчика
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDocument(int documentId, int contractorId)
    {
        await _fileService.DeleteFileAsync(documentId);
        TempData["Success"] = "Документ удалён";
        return RedirectToAction(nameof(Details), new { id = contractorId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = UserRoles.PmuAdmin)]
    public async Task<IActionResult> Delete(int id)
    {
        var contractor = await _context.Contractors
            .Include(c => c.Contracts)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (contractor != null)
        {
            if (contractor.Contracts.Any())
            {
                TempData["Error"] = "Невозможно удалить подрядчика с активными контрактами";
                return RedirectToAction(nameof(Index));
            }

            _context.Contractors.Remove(contractor);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Подрядчик удалён";
        }
        return RedirectToAction(nameof(Index));
    }
}
