using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;
using PMMIS.Web.Authorization;
using PMMIS.Web.Extensions;
using PMMIS.Web.Services;
using PMMIS.Web.ViewModels.WorkProgress;
using System.Security.Claims;

namespace PMMIS.Web.Controllers;

/// <summary>
/// Мониторинг прогресса выполнения работ
/// </summary>
[Authorize]
[RequirePermission(MenuKeys.WorkProgress, PermissionType.View)]
public class WorkProgressController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ITaskService _taskService;
    private readonly IFileService _fileService;
    private readonly IDataAccessService _dataAccess;
    private readonly UserManager<ApplicationUser> _userManager;

    public WorkProgressController(
        ApplicationDbContext context, 
        ITaskService taskService, 
        IFileService fileService,
        IDataAccessService dataAccess,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _taskService = taskService;
        _fileService = fileService;
        _dataAccess = dataAccess;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(int? contractId)
    {
        var user = await _userManager.GetUserAsync(User);
        var canViewAll = user != null && await _dataAccess.CanViewAllAsync(user, MenuKeys.WorkProgress);

        var query = _context.WorkProgresses
            .Include(w => w.Contract)
                .ThenInclude(c => c.Contractor)
            .Include(w => w.Contract.Project)
            .Include(w => w.Documents)
            .AsQueryable();

        // Filter by user's contracts if they don't have CanViewAll
        if (!canViewAll && user != null)
        {
            var userContractIds = await _dataAccess.GetUserContractIdsAsync(user.Id);
            query = query.Where(w => userContractIds.Contains(w.ContractId));
        }

        if (contractId.HasValue)
            query = query.Where(w => w.ContractId == contractId.Value);

        var items = await query
            .OrderByDescending(w => w.ReportDate)
            .ToListAsync();

        // Also filter contracts dropdown
        var contractsQuery = _context.Contracts
            .Include(c => c.Contractor)
            .AsQueryable();
        if (!canViewAll && user != null)
        {
            var userContractIds = await _dataAccess.GetUserContractIdsAsync(user.Id);
            contractsQuery = contractsQuery.Where(c => userContractIds.Contains(c.Id));
        }
        ViewBag.Contracts = await contractsQuery.OrderBy(c => c.ContractNumber).ToListAsync();
        ViewBag.SelectedContractId = contractId;

        return View(items);
    }

    [RequirePermission(MenuKeys.WorkProgress, PermissionType.Create)]
    public async Task<IActionResult> Create(int? contractId)
    {
        var user = await _userManager.GetUserAsync(User);
        var canViewAll = user != null && await _dataAccess.CanViewAllAsync(user, MenuKeys.WorkProgress);

        // Auto-select if user has only 1 contract
        if (!contractId.HasValue && !canViewAll && user != null)
        {
            var userContractIds = await _dataAccess.GetUserContractIdsAsync(user.Id);
            if (userContractIds.Count == 1)
                contractId = userContractIds[0];
        }

        var viewModel = new WorkProgressFormViewModel
        {
            WorkProgress = new Domain.Entities.WorkProgress
            {
                ContractId = contractId ?? 0,
                ReportDate = DateTime.Today
            }
        };
        
        await PopulateViewModel(viewModel, contractId);
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.WorkProgress, PermissionType.Create)]
    public async Task<IActionResult> Create(WorkProgressFormViewModel viewModel)
    {
        // Remove validation errors for navigation properties
        ModelState.Remove("WorkProgress.Contract");
        
        if (ModelState.IsValid)
        {
            var progress = viewModel.WorkProgress;
            progress.CreatedAt = DateTime.UtcNow;
            progress.ReportDate = progress.ReportDate.ToUtc();
            progress.ApprovalStatus = AvrApprovalStatus.Draft;
            
            _context.WorkProgresses.Add(progress);
            await _context.SaveChangesAsync();
            
            // Upload report files (PDF)
            if (viewModel.ReportFiles?.Any() == true)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var docs = await _fileService.UploadFilesAsync(
                    viewModel.ReportFiles, $"workprogress/{progress.Id}/reports", DocumentType.Report, userId);
                foreach (var doc in docs)
                {
                    doc.WorkProgressId = progress.Id;
                    _context.Documents.Add(doc);
                }
                await _context.SaveChangesAsync();
            }
            
            // Upload photo files
            if (viewModel.PhotoFiles?.Any() == true)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var docs = await _fileService.UploadFilesAsync(
                    viewModel.PhotoFiles, $"workprogress/{progress.Id}/photos", DocumentType.Photo, userId);
                foreach (var doc in docs)
                {
                    doc.WorkProgressId = progress.Id;
                    _context.Documents.Add(doc);
                }
                await _context.SaveChangesAsync();
            }
            
            // Save indicator progress values
            if (viewModel.ContractIndicators?.Any() == true)
            {
                foreach (var indicatorInput in viewModel.ContractIndicators.Where(i => i.Value > 0))
                {
                    _context.ContractIndicatorProgresses.Add(new ContractIndicatorProgress
                    {
                        ContractIndicatorId = indicatorInput.ContractIndicatorId,
                        WorkProgressId = progress.Id,
                        Value = indicatorInput.Value,
                        Notes = indicatorInput.Notes,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                await _context.SaveChangesAsync();
                
                // Update achieved values on ContractIndicator
                await UpdateIndicatorAchievedValues(progress.ContractId);
            }

            // Update contract's work completed percent
            await UpdateContractProgress(progress.ContractId);
            
            // Auto-create task for АВР review
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var contract = await _context.Contracts
                .Include(c => c.Contractor)
                .FirstOrDefaultAsync(c => c.Id == progress.ContractId);
            
            if (!string.IsNullOrEmpty(currentUserId) && contract != null)
            {
                // Assign to project manager if set, otherwise to current user
                var reviewerId = contract.ProjectManagerId ?? currentUserId;
                await _taskService.CreateAsync(new ProjectTask
                {
                    Title = $"Проверить АВР по контракту {contract.ContractNumber}",
                    Description = $"Подрядчик: {contract.Contractor.Name}\n" +
                                  $"Дата отчёта: {progress.ReportDate:dd.MM.yyyy}\n" +
                                  $"Прогресс: {progress.CompletedPercent}%\n" +
                                  $"{progress.Description}",
                    Status = ProjectTaskStatus.New,
                    Priority = TaskPriority.High,
                    DueDate = DateTime.UtcNow.AddDays(3),
                    AssigneeId = reviewerId,
                    AssignedById = currentUserId,
                    ContractId = progress.ContractId,
                    WorkProgressId = progress.Id,
                    ProjectId = contract.ProjectId
                }, currentUserId);
            }
            
            TempData["Success"] = "Отчёт о прогрессе создан. Задача на проверку АВР добавлена.";
            return RedirectToAction(nameof(Index), new { contractId = progress.ContractId });
        }

        await PopulateViewModel(viewModel, viewModel.WorkProgress.ContractId);
        return View(viewModel);
    }


    [RequirePermission(MenuKeys.WorkProgress, PermissionType.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var progress = await _context.WorkProgresses
            .Include(w => w.Contract)
            .Include(w => w.Documents)
            .Include(w => w.IndicatorProgresses)
            .FirstOrDefaultAsync(w => w.Id == id);
            
        if (progress == null) return NotFound();

        var viewModel = new WorkProgressFormViewModel
        {
            WorkProgress = progress,
            ExistingDocuments = progress.Documents.ToList()
        };
        
        await PopulateViewModel(viewModel, progress.ContractId);
        
        // Pre-fill indicator values from existing progress
        if (viewModel.ContractIndicators != null)
        {
            foreach (var ci in viewModel.ContractIndicators)
            {
                var existing = progress.IndicatorProgresses
                    .FirstOrDefault(p => p.ContractIndicatorId == ci.ContractIndicatorId);
                if (existing != null)
                {
                    ci.Value = existing.Value;
                    ci.Notes = existing.Notes;
                }
            }
        }
        
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.WorkProgress, PermissionType.Edit)]
    public async Task<IActionResult> Edit(int id, WorkProgressFormViewModel viewModel)
    {
        if (id != viewModel.WorkProgress.Id) return NotFound();

        ModelState.Remove("WorkProgress.Contract");
        
        if (ModelState.IsValid)
        {
            var progress = viewModel.WorkProgress;
            progress.UpdatedAt = DateTime.UtcNow;
            progress.ReportDate = progress.ReportDate.ToUtc();
            
            _context.Update(progress);
            await _context.SaveChangesAsync();
            
            // Upload new report files
            if (viewModel.ReportFiles?.Any() == true)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var docs = await _fileService.UploadFilesAsync(
                    viewModel.ReportFiles, $"workprogress/{progress.Id}/reports", DocumentType.Report, userId);
                foreach (var doc in docs)
                {
                    doc.WorkProgressId = progress.Id;
                    _context.Documents.Add(doc);
                }
                await _context.SaveChangesAsync();
            }
            
            // Upload new photo files
            if (viewModel.PhotoFiles?.Any() == true)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var docs = await _fileService.UploadFilesAsync(
                    viewModel.PhotoFiles, $"workprogress/{progress.Id}/photos", DocumentType.Photo, userId);
                foreach (var doc in docs)
                {
                    doc.WorkProgressId = progress.Id;
                    _context.Documents.Add(doc);
                }
                await _context.SaveChangesAsync();
            }

            // Update indicator progress values
            if (viewModel.ContractIndicators?.Any() == true)
            {
                // Remove existing progress records for this AVR
                var existingProgress = await _context.ContractIndicatorProgresses
                    .Where(cip => cip.WorkProgressId == progress.Id)
                    .ToListAsync();
                _context.ContractIndicatorProgresses.RemoveRange(existingProgress);
                
                // Add new progress records
                foreach (var indicatorInput in viewModel.ContractIndicators.Where(i => i.Value > 0))
                {
                    _context.ContractIndicatorProgresses.Add(new ContractIndicatorProgress
                    {
                        ContractIndicatorId = indicatorInput.ContractIndicatorId,
                        WorkProgressId = progress.Id,
                        Value = indicatorInput.Value,
                        Notes = indicatorInput.Notes,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                await _context.SaveChangesAsync();
                
                // Recalculate achieved values
                await UpdateIndicatorAchievedValues(progress.ContractId);
            }

            // Update contract's work completed percent
            await UpdateContractProgress(progress.ContractId);
            
            TempData["Success"] = "Отчёт обновлён";
            return RedirectToAction(nameof(Index), new { contractId = progress.ContractId });
        }

        await PopulateViewModel(viewModel, viewModel.WorkProgress.ContractId);
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.WorkProgress, PermissionType.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var progress = await _context.WorkProgresses.FindAsync(id);
        if (progress != null)
        {
            var contractId = progress.ContractId;
            _context.WorkProgresses.Remove(progress);
            await _context.SaveChangesAsync();
            
            await UpdateContractProgress(contractId);
            
            TempData["Success"] = "Отчёт удалён";
            return RedirectToAction(nameof(Index), new { contractId });
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Timeline view for a specific contract
    /// </summary>
    public async Task<IActionResult> Timeline(int contractId)
    {
        var contract = await _context.Contracts
            .Include(c => c.Contractor)
            .Include(c => c.WorkProgresses.OrderBy(w => w.ReportDate))
                .ThenInclude(w => w.Documents)
            .FirstOrDefaultAsync(c => c.Id == contractId);

        if (contract == null) return NotFound();

        return View(contract);
    }

    #region Approval Workflow
    
    /// <summary>
    /// Куратор отправляет АВР на проверку Менеджеру
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.WorkProgress, PermissionType.Edit)]
    public async Task<IActionResult> SubmitForReview(int id)
    {
        var progress = await _context.WorkProgresses
            .Include(w => w.Contract)
                .ThenInclude(c => c.Contractor)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (progress == null) return NotFound();
        
        progress.ApprovalStatus = AvrApprovalStatus.SubmittedForReview;
        progress.SubmittedAt = DateTime.UtcNow;
        progress.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        // Create task for Project Manager
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (progress.Contract.ProjectManagerId != null)
        {
            await _taskService.CreateAsync(new ProjectTask
            {
                Title = $"Проверить АВР #{progress.Id} — {progress.Contract.ContractNumber}",
                Description = $"АВР отправлен на проверку.\n" +
                              $"Подрядчик: {progress.Contract.Contractor.Name}\n" +
                              $"Прогресс: {progress.CompletedPercent}%",
                Status = ProjectTaskStatus.New,
                Priority = TaskPriority.High,
                DueDate = DateTime.UtcNow.AddDays(3),
                AssigneeId = progress.Contract.ProjectManagerId,
                AssignedById = userId,
                ContractId = progress.ContractId,
                WorkProgressId = progress.Id,
                ProjectId = progress.Contract.ProjectId
            }, userId!);
        }
        
        TempData["Success"] = "АВР отправлен на проверку Менеджеру проекта.";
        return RedirectToAction(nameof(Index), new { contractId = progress.ContractId });
    }
    
    /// <summary>
    /// Менеджер проекта одобряет АВР
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.WorkProgress, PermissionType.Edit)]
    public async Task<IActionResult> ManagerApprove(int id, string? comment)
    {
        var progress = await _context.WorkProgresses
            .Include(w => w.Contract)
                .ThenInclude(c => c.Contractor)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (progress == null) return NotFound();
        
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        progress.ApprovalStatus = AvrApprovalStatus.ManagerApproved;
        progress.ManagerReviewedAt = DateTime.UtcNow;
        progress.ManagerReviewedById = userId;
        progress.ManagerComment = comment;
        progress.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        // Create task for Director (PMU_ADMIN)
        var directors = await _context.Users
            .Where(u => u.IsActive)
            .ToListAsync();
        // Find users in PMU_ADMIN role
        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == UserRoles.PmuAdmin);
        if (adminRole != null)
        {
            var adminUserIds = await _context.UserRoles
                .Where(ur => ur.RoleId == adminRole.Id)
                .Select(ur => ur.UserId)
                .ToListAsync();
            
            // Create task for first active admin
            var directorId = adminUserIds.FirstOrDefault();
            if (directorId != null)
            {
                await _taskService.CreateAsync(new ProjectTask
                {
                    Title = $"Утвердить АВР #{progress.Id} — {progress.Contract.ContractNumber}",
                    Description = $"АВР одобрен Менеджером проекта.\n" +
                                  $"Подрядчик: {progress.Contract.Contractor.Name}\n" +
                                  $"Прогресс: {progress.CompletedPercent}%\n" +
                                  (string.IsNullOrEmpty(comment) ? "" : $"Комментарий менеджера: {comment}"),
                    Status = ProjectTaskStatus.New,
                    Priority = TaskPriority.High,
                    DueDate = DateTime.UtcNow.AddDays(2),
                    AssigneeId = directorId,
                    AssignedById = userId,
                    ContractId = progress.ContractId,
                    WorkProgressId = progress.Id,
                    ProjectId = progress.Contract.ProjectId
                }, userId!);
            }
        }
        
        TempData["Success"] = "АВР одобрен. Ожидает утверждения Директора.";
        return RedirectToAction(nameof(Index), new { contractId = progress.ContractId });
    }
    
    /// <summary>
    /// Директор утверждает АВР и автоматически создаётся платёжка
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = UserRoles.PmuAdmin)]
    public async Task<IActionResult> DirectorApprove(int id, string? comment)
    {
        var progress = await _context.WorkProgresses
            .Include(w => w.Contract)
                .ThenInclude(c => c.Contractor)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (progress == null) return NotFound();
        
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        progress.ApprovalStatus = AvrApprovalStatus.DirectorApproved;
        progress.DirectorApprovedAt = DateTime.UtcNow;
        progress.DirectorApprovedById = userId;
        progress.DirectorComment = comment;
        progress.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        // --- Auto-create Payment ---
        var contract = progress.Contract;
        var paymentAmount = contract.ContractAmount * progress.CompletedPercent / 100;
        
        var payment = new Payment
        {
            ContractId = contract.Id,
            PaymentDate = DateTime.UtcNow,
            Amount = paymentAmount,
            Type = PaymentType.Interim,
            Status = PaymentStatus.Pending,
            Description = $"Автоплатёж по АВР #{progress.Id} от {progress.ReportDate:dd.MM.yyyy}",
            CreatedAt = DateTime.UtcNow
        };
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();
        
        // Create task for Accountant (PMU_STAFF or first available)
        var staffRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == UserRoles.PmuStaff);
        if (staffRole != null)
        {
            var staffUserId = await _context.UserRoles
                .Where(ur => ur.RoleId == staffRole.Id)
                .Select(ur => ur.UserId)
                .FirstOrDefaultAsync();
            
            if (staffUserId != null)
            {
                await _taskService.CreateAsync(new ProjectTask
                {
                    Title = $"Подготовить платёжку #{payment.Id} — {contract.ContractNumber}",
                    Description = $"АВР утверждён Директором.\nСумма: {paymentAmount:N2}\n" +
                                  $"Подрядчик: {contract.Contractor.Name}\n" +
                                  $"Проверьте и подтвердите платёжку.",
                    Status = ProjectTaskStatus.New,
                    Priority = TaskPriority.High,
                    DueDate = DateTime.UtcNow.AddDays(5),
                    AssigneeId = staffUserId,
                    AssignedById = userId,
                    ContractId = contract.Id,
                    WorkProgressId = progress.Id,
                    ProjectId = contract.ProjectId
                }, userId!);
            }
        }
        
        TempData["Success"] = $"АВР утверждён. Платёжка #{payment.Id} создана для бухгалтерии.";
        return RedirectToAction(nameof(Index), new { contractId = progress.ContractId });
    }
    
    /// <summary>
    /// Отклонить АВР (менеджером или директором)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.WorkProgress, PermissionType.Edit)]
    public async Task<IActionResult> Reject(int id, string reason)
    {
        var progress = await _context.WorkProgresses
            .Include(w => w.Contract)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (progress == null) return NotFound();
        
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        progress.ApprovalStatus = AvrApprovalStatus.Rejected;
        progress.RejectionReason = reason;
        progress.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        // Notify curator
        if (progress.Contract.CuratorId != null)
        {
            await _taskService.CreateAsync(new ProjectTask
            {
                Title = $"АВР #{progress.Id} отклонён — {progress.Contract.ContractNumber}",
                Description = $"Причина: {reason}\nПожалуйста, исправьте и отправьте повторно.",
                Status = ProjectTaskStatus.New,
                Priority = TaskPriority.High,
                DueDate = DateTime.UtcNow.AddDays(3),
                AssigneeId = progress.Contract.CuratorId,
                AssignedById = userId,
                ContractId = progress.ContractId,
                WorkProgressId = progress.Id,
                ProjectId = progress.Contract.ProjectId
            }, userId!);
        }
        
        TempData["Warning"] = "АВР отклонён.";
        return RedirectToAction(nameof(Index), new { contractId = progress.ContractId });
    }
    
    /// <summary>
    /// Удалить прикреплённый документ
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.WorkProgress, PermissionType.Edit)]
    public async Task<IActionResult> DeleteDocument(int documentId, int workProgressId)
    {
        await _fileService.DeleteFileAsync(documentId);
        TempData["Success"] = "Документ удалён";
        return RedirectToAction(nameof(Edit), new { id = workProgressId });
    }
    
    #endregion

    private async Task PopulateViewModel(WorkProgressFormViewModel viewModel, int? contractId)
    {
        // Filter contracts by user access
        var user = await _userManager.GetUserAsync(HttpContext.User);
        var canViewAll = user != null && await _dataAccess.CanViewAllAsync(user, MenuKeys.WorkProgress);

        var contractsQuery = _context.Contracts
            .Include(c => c.Contractor)
            .AsQueryable();

        if (!canViewAll && user != null)
        {
            var userContractIds = await _dataAccess.GetUserContractIdsAsync(user.Id);
            contractsQuery = contractsQuery.Where(c => userContractIds.Contains(c.Id));
        }

        viewModel.Contracts = new SelectList(
            await contractsQuery
                .OrderBy(c => c.ContractNumber)
                .Select(c => new { c.Id, Name = $"{c.ContractNumber} - {c.Contractor.Name}" })
                .ToListAsync(),
            "Id", "Name");
        
        // Load contract indicators if contract is selected
        if (contractId.HasValue && contractId.Value > 0)
        {
            var contractIndicators = await _context.ContractIndicators
                .Include(ci => ci.Indicator)
                .Include(ci => ci.Progresses)
                .Where(ci => ci.ContractId == contractId.Value)
                .ToListAsync();
            
            viewModel.ContractIndicators = contractIndicators.Select(ci => new ContractIndicatorInput
            {
                ContractIndicatorId = ci.Id,
                IndicatorId = ci.IndicatorId,
                IndicatorCode = ci.Indicator.Code,
                IndicatorName = ci.Indicator.NameRu,
                Unit = ci.Indicator.Unit,
                TargetValue = ci.TargetValue,
                PreviousAchieved = ci.Progresses.Sum(p => p.Value),
                Value = 0
            }).ToList();
        }
    }
    
    private async Task LoadDropdowns()
    {
        ViewBag.Contracts = new SelectList(
            await _context.Contracts
                .Include(c => c.Contractor)
                .OrderBy(c => c.ContractNumber)
                .Select(c => new { c.Id, Name = $"{c.ContractNumber} - {c.Contractor.Name}" })
                .ToListAsync(),
            "Id", "Name");
    }

    private async Task UpdateContractProgress(int contractId)
    {
        var contract = await _context.Contracts
            .Include(c => c.WorkProgresses)
            .FirstOrDefaultAsync(c => c.Id == contractId);

        if (contract != null && contract.WorkProgresses.Any())
        {
            // Take the latest progress report
            var latestProgress = contract.WorkProgresses
                .OrderByDescending(w => w.ReportDate)
                .First();
            
            contract.WorkCompletedPercent = latestProgress.CompletedPercent;
            contract.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
    
    private async Task UpdateIndicatorAchievedValues(int contractId)
    {
        var contractIndicators = await _context.ContractIndicators
            .Include(ci => ci.Progresses)
            .Where(ci => ci.ContractId == contractId)
            .ToListAsync();
        
        foreach (var ci in contractIndicators)
        {
            ci.AchievedValue = ci.Progresses.Sum(p => p.Value);
            ci.UpdatedAt = DateTime.UtcNow;
        }
        
        await _context.SaveChangesAsync();
    }

}
