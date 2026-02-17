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
using PMMIS.Web.ViewModels.Payments;
using System.Security.Claims;

namespace PMMIS.Web.Controllers;

/// <summary>
/// Управление платежами по контрактам
/// </summary>
[Authorize]
[RequirePermission(MenuKeys.Payments, PermissionType.View)]
public class PaymentsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ITaskService _taskService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDataAccessService _dataAccessService;

    public PaymentsController(
        ApplicationDbContext context, 
        ITaskService taskService,
        UserManager<ApplicationUser> userManager,
        IDataAccessService dataAccessService)
    {
        _context = context;
        _taskService = taskService;
        _userManager = userManager;
        _dataAccessService = dataAccessService;
    }

    public async Task<IActionResult> Index(int? contractId, PaymentStatus? status)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var query = _context.Payments
            .Include(p => p.Contract)
                .ThenInclude(c => c.Contractor)
            .Include(p => p.Contract.Project)
            .AsQueryable();

        // Фильтрация по видимости данных
        if (!await _dataAccessService.CanViewAllAsync(currentUser, MenuKeys.Payments))
        {
            var myContractIds = await _dataAccessService.GetUserContractIdsAsync(currentUser.Id);
            query = query.Where(p => myContractIds.Contains(p.ContractId));
        }

        if (contractId.HasValue)
            query = query.Where(p => p.ContractId == contractId.Value);

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        var payments = await query
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();

        var viewModel = new PaymentIndexViewModel
        {
            Payments = payments,
            Contracts = await _context.Contracts
                .Include(c => c.Contractor)
                .OrderBy(c => c.ContractNumber)
                .ToListAsync(),
            SelectedContractId = contractId,
            SelectedStatus = status,
            TotalAmount = payments.Sum(p => p.Amount),
            PaidAmount = payments.Where(p => p.Status == PaymentStatus.Paid).Sum(p => p.Amount),
            PendingAmount = payments.Where(p => p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Approved).Sum(p => p.Amount)
        };

        return View(viewModel);
    }

    [RequirePermission(MenuKeys.Payments, PermissionType.Create)]
    public async Task<IActionResult> Create(int? contractId)
    {
        var viewModel = new PaymentFormViewModel
        {
            Payment = new Payment
            {
                ContractId = contractId ?? 0,
                PaymentDate = DateTime.Today,
                Status = PaymentStatus.Pending
            }
        };
        await PopulateDropdowns(viewModel);
        await PopulateContractCurrencies();
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Payments, PermissionType.Create)]
    public async Task<IActionResult> Create(PaymentFormViewModel viewModel)
    {
        // Remove validation errors for navigation properties that are not bound from form
        ModelState.Remove("Payment.Contract");
        ModelState.Remove("Payment.Document");
        ModelState.Remove("Payment.ApprovedBy");
        ModelState.Remove("Payment.RejectedBy");
        ModelState.Remove("Payment.WorkProgress");
        
        if (ModelState.IsValid)
        {
            var contract = await _context.Contracts
                .Include(c => c.Payments)
                .Include(c => c.WorkProgresses)
                .Include(c => c.Contractor)
                .FirstOrDefaultAsync(c => c.Id == viewModel.Payment.ContractId);
            
            if (contract == null)
            {
                ModelState.AddModelError("Payment.ContractId", "Контракт не найден");
                await PopulateDropdowns(viewModel);
                return View(viewModel);
            }
            
            // === ВАЛИДАЦИЯ: Проверка наличия АВР ===
            var hasWorkProgress = contract.WorkProgresses.Any();
            if (!hasWorkProgress && viewModel.Payment.Type != PaymentType.Advance)
            {
                // Предупреждение (не блокирует, но показывает уведомление)
                TempData["Warning"] = "⚠️ Внимание: Отсутствует АВР (акт выполненных работ) для этого контракта. " +
                                      "Рекомендуется добавить АВР перед созданием промежуточных/финальных платежей.";
            }
            
            // === ВАЛИДАЦИЯ: Проверка лимита контракта ===
            var paidAmount = contract.Payments.Where(p => p.Status == PaymentStatus.Paid).Sum(p => p.Amount);
            var pendingAmount = contract.Payments.Where(p => p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Approved).Sum(p => p.Amount);
            var finalAmount = contract.ContractAmount + contract.AdditionalAmount - contract.SavedAmount;
            var availableLimit = finalAmount - paidAmount - pendingAmount;
            
            if (viewModel.Payment.Amount > availableLimit)
            {
                ModelState.AddModelError("Payment.Amount", 
                    $"Сумма платежа ({viewModel.Payment.Amount:N2} USD) превышает доступный остаток по контракту ({availableLimit:N2} USD). " +
                    $"Общая сумма контракта: {finalAmount:N2} USD, оплачено: {paidAmount:N2} USD, в ожидании: {pendingAmount:N2} USD.");
                await PopulateDropdowns(viewModel);
                return View(viewModel);
            }
            
            // Предупреждение при превышении 90% лимита
            if (viewModel.Payment.Amount > availableLimit * 0.9m && viewModel.Payment.Amount <= availableLimit)
            {
                TempData["Warning"] = (TempData["Warning"]?.ToString() ?? "") + 
                    $" ⚠️ Этот платёж использует более 90% оставшегося бюджета контракта.";
            }
            
            // Server-side TJS → USD fallback for TJS contracts
            if (contract.Currency == ContractCurrency.TJS && viewModel.Payment.AmountTjs.HasValue && viewModel.Payment.ExchangeRate.HasValue && viewModel.Payment.ExchangeRate.Value > 0)
            {
                viewModel.Payment.Amount = viewModel.Payment.AmountTjs.Value / viewModel.Payment.ExchangeRate.Value;
            }
            
            viewModel.Payment.CreatedAt = DateTime.UtcNow;
            viewModel.Payment.PaymentDate = viewModel.Payment.PaymentDate.ToUtc();
            
            _context.Payments.Add(viewModel.Payment);
            await _context.SaveChangesAsync();
            
            // === АВТОСОЗДАНИЕ ЗАДАЧИ: Подготовить документы к оплате ===
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                await _taskService.CreateAsync(new ProjectTask
                {
                    Title = $"Подготовить документы к оплате #{viewModel.Payment.Id}",
                    Description = $"Контракт: {contract.ContractNumber}\n" +
                                  $"Подрядчик: {contract.Contractor.Name}\n" +
                                  $"Сумма: {viewModel.Payment.Amount:N2} USD\n" +
                                  $"Тип: {GetTypeName(viewModel.Payment.Type)}\n" +
                                  $"Дата платежа: {viewModel.Payment.PaymentDate:dd.MM.yyyy}",
                    Status = ProjectTaskStatus.New,
                    Priority = viewModel.Payment.Type == PaymentType.Final ? TaskPriority.High : TaskPriority.Normal,
                    DueDate = viewModel.Payment.PaymentDate.AddDays(-2), // 2 days before payment date
                    AssigneeId = userId,
                    AssignedById = userId,
                    ContractId = contract.Id,
                    ProjectId = contract.ProjectId
                }, userId);
            }
            
            var successMessage = "Платёж успешно создан. Задача на подготовку документов добавлена.";
            if (TempData["Warning"] != null)
            {
                TempData["Success"] = successMessage;
            }
            else
            {
                TempData["Success"] = successMessage;
            }
            
            return RedirectToAction(nameof(Index), new { contractId = viewModel.Payment.ContractId });
        }

        await PopulateDropdowns(viewModel);
        await PopulateContractCurrencies();
        return View(viewModel);
    }


    [RequirePermission(MenuKeys.Payments, PermissionType.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var payment = await _context.Payments
            .Include(p => p.Contract)
            .FirstOrDefaultAsync(p => p.Id == id);
            
        if (payment == null) return NotFound();

        var viewModel = new PaymentFormViewModel { Payment = payment };
        await PopulateDropdowns(viewModel);
        await PopulateContractCurrencies();
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Payments, PermissionType.Edit)]
    public async Task<IActionResult> Edit(int id, PaymentFormViewModel viewModel)
    {
        if (id != viewModel.Payment.Id) return NotFound();
        
        // Remove validation for navigation properties
        ModelState.Remove("Payment.Contract");
        ModelState.Remove("Payment.Document");
        ModelState.Remove("Payment.ApprovedBy");
        ModelState.Remove("Payment.RejectedBy");

        if (ModelState.IsValid)
        {
            // Server-side TJS → USD fallback for TJS contracts
            var contract = await _context.Contracts.FindAsync(viewModel.Payment.ContractId);
            if (contract?.Currency == ContractCurrency.TJS && viewModel.Payment.AmountTjs.HasValue && viewModel.Payment.ExchangeRate.HasValue && viewModel.Payment.ExchangeRate.Value > 0)
            {
                viewModel.Payment.Amount = viewModel.Payment.AmountTjs.Value / viewModel.Payment.ExchangeRate.Value;
            }
            
            viewModel.Payment.CreatedAt = DateTime.SpecifyKind(viewModel.Payment.CreatedAt, DateTimeKind.Utc);
            viewModel.Payment.UpdatedAt = DateTime.UtcNow;
            viewModel.Payment.PaymentDate = viewModel.Payment.PaymentDate.ToUtc();
            
            _context.Update(viewModel.Payment);
            await _context.SaveChangesAsync();
            
            TempData["Success"] = "Платёж обновлён";
            return RedirectToAction(nameof(Index), new { contractId = viewModel.Payment.ContractId });
        }

        await PopulateDropdowns(viewModel);
        await PopulateContractCurrencies();
        return View(viewModel);
    }

    /// <summary>
    /// Одобрить платёж
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Payments, PermissionType.Edit)]
    public async Task<IActionResult> Approve(int id)
    {
        var payment = await _context.Payments
            .Include(p => p.Contract)
                .ThenInclude(c => c.Contractor)
            .FirstOrDefaultAsync(p => p.Id == id);
            
        if (payment == null) return NotFound();
        
        if (payment.Status != PaymentStatus.Pending)
        {
            TempData["Error"] = "Можно одобрить только платежи в статусе 'Ожидает'";
            return RedirectToAction(nameof(Index), new { contractId = payment.ContractId });
        }
        
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        payment.Status = PaymentStatus.Approved;
        payment.ApprovedAt = DateTime.UtcNow;
        payment.ApprovedById = userId;
        payment.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        TempData["Success"] = $"Платёж #{id} на сумму {payment.Amount:N2} USD одобрен";
        return RedirectToAction(nameof(Index), new { contractId = payment.ContractId });
    }

    /// <summary>
    /// Отклонить платёж с указанием причины
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Payments, PermissionType.Edit)]
    public async Task<IActionResult> Reject(int id, string rejectionReason)
    {
        var payment = await _context.Payments
            .Include(p => p.Contract)
            .FirstOrDefaultAsync(p => p.Id == id);
            
        if (payment == null) return NotFound();
        
        if (payment.Status == PaymentStatus.Paid)
        {
            TempData["Error"] = "Нельзя отклонить уже оплаченный платёж";
            return RedirectToAction(nameof(Index), new { contractId = payment.ContractId });
        }
        
        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            TempData["Error"] = "Необходимо указать причину отклонения";
            return RedirectToAction(nameof(Index), new { contractId = payment.ContractId });
        }
        
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        payment.Status = PaymentStatus.Rejected;
        payment.RejectionReason = rejectionReason;
        payment.RejectedAt = DateTime.UtcNow;
        payment.RejectedById = userId;
        payment.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        TempData["Success"] = $"Платёж #{id} отклонён. Причина: {rejectionReason}";
        return RedirectToAction(nameof(Index), new { contractId = payment.ContractId });
    }

    /// <summary>
    /// Отметить платёж как оплаченный
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Payments, PermissionType.Edit)]
    public async Task<IActionResult> MarkAsPaid(int id)
    {
        var payment = await _context.Payments.FindAsync(id);
        if (payment == null) return NotFound();
        
        if (payment.Status != PaymentStatus.Approved)
        {
            TempData["Error"] = "Можно отметить как оплаченный только одобренные платежи";
            return RedirectToAction(nameof(Index), new { contractId = payment.ContractId });
        }
        
        payment.Status = PaymentStatus.Paid;
        payment.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        // Auto-complete related payment preparation tasks
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
        {
            await _taskService.CompleteRelatedTasksAsync(payment.ContractId, null, userId);
        }
        
        TempData["Success"] = $"Платёж #{id} отмечен как оплаченный";
        return RedirectToAction(nameof(Index), new { contractId = payment?.ContractId });
    }

    /// <summary>
    /// Изменить статус платежа (legacy)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, PaymentStatus status)
    {
        var payment = await _context.Payments.FindAsync(id);
        if (payment != null)
        {
            payment.Status = status;
            payment.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Статус изменён на: {GetStatusName(status)}";
        }
        return RedirectToAction(nameof(Index), new { contractId = payment?.ContractId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Payments, PermissionType.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var payment = await _context.Payments.FindAsync(id);
        if (payment != null)
        {
            var contractId = payment.ContractId;
            _context.Payments.Remove(payment);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Платёж удалён";
            return RedirectToAction(nameof(Index), new { contractId });
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// API: Returns approved (DirectorApproved) AVRs for a contract that are not yet linked to a payment
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetApprovedAvrs(int contractId)
    {
        // Get IDs of work progresses already linked to payments
        var linkedWpIds = await _context.Payments
            .Where(p => p.ContractId == contractId && p.WorkProgressId.HasValue)
            .Select(p => p.WorkProgressId!.Value)
            .ToListAsync();

        var avrs = await _context.WorkProgresses
            .Where(wp => wp.ContractId == contractId 
                      && wp.ApprovalStatus == AvrApprovalStatus.DirectorApproved
                      && !linkedWpIds.Contains(wp.Id))
            .OrderByDescending(wp => wp.ReportDate)
            .Select(wp => new {
                wp.Id,
                Text = $"АВР от {wp.ReportDate:dd.MM.yyyy} — {wp.CompletedPercent}%",
                wp.ReportDate,
                wp.CompletedPercent
            })
            .ToListAsync();

        return Json(avrs);
    }

    #region Helpers

    private async Task PopulateDropdowns(PaymentFormViewModel viewModel)
    {
        viewModel.Contracts = new SelectList(
            await _context.Contracts
                .Include(c => c.Contractor)
                .OrderBy(c => c.ContractNumber)
                .Select(c => new { c.Id, Name = $"{c.ContractNumber} - {c.Contractor.Name}" })
                .ToListAsync(),
            "Id", "Name");

        viewModel.Types = new SelectList(
            Enum.GetValues<PaymentType>().Select(t => new { 
                Value = (int)t, 
                Text = GetTypeName(t) 
            }), "Value", "Text");

        viewModel.Statuses = new SelectList(
            Enum.GetValues<PaymentStatus>().Select(s => new { 
                Value = (int)s, 
                Text = GetStatusName(s) 
            }), "Value", "Text");
    }

    private async Task PopulateContractCurrencies()
    {
        ViewBag.ContractCurrencies = await _context.Contracts
            .Select(c => new { c.Id, Currency = (int)c.Currency, Rate = c.ExchangeRate })
            .ToListAsync();
    }

    private static string GetTypeName(PaymentType type) => type switch
    {
        PaymentType.Advance => "Аванс",
        PaymentType.Interim => "Промежуточный",
        PaymentType.Final => "Окончательный",
        PaymentType.Retention => "Удержание",
        _ => type.ToString()
    };

    private static string GetStatusName(PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => "Ожидает",
        PaymentStatus.Approved => "Одобрен",
        PaymentStatus.Paid => "Оплачен",
        PaymentStatus.Rejected => "Отклонён",
        _ => status.ToString()
    };

    #endregion
}
