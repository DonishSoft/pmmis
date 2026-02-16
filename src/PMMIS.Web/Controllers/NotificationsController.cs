using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;
using PMMIS.Web.Authorization;

namespace PMMIS.Web.Controllers;

/// <summary>
/// Контроллер управления уведомлениями
/// </summary>
[Authorize]
[RequirePermission(MenuKeys.Notifications)]
public class NotificationsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<NotificationsController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Список уведомлений текущего пользователя
    /// </summary>
    public async Task<IActionResult> Index(bool? unreadOnly)
    {
        var userId = _userManager.GetUserId(User);
        
        var query = _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt);

        if (unreadOnly == true)
        {
            query = (IOrderedQueryable<Notification>)query.Where(n => !n.IsRead);
        }

        var notifications = await query.Take(100).ToListAsync();
        
        ViewBag.UnreadCount = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .CountAsync();
        ViewBag.UnreadOnly = unreadOnly;
        
        return View(notifications);
    }

    /// <summary>
    /// Отметить уведомление как прочитанное
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = _userManager.GetUserId(User);
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        
        if (notification == null)
            return NotFound();

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Отметить все уведомления как прочитанные
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = _userManager.GetUserId(User);
        var unreadNotifications = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
        }
        
        await _context.SaveChangesAsync();
        
        TempData["Success"] = $"Отмечено как прочитанные: {unreadNotifications.Count}";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Удалить уведомление
    /// </summary>
    [HttpPost]
    [RequirePermission(MenuKeys.Notifications, PermissionType.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _userManager.GetUserId(User);
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        
        if (notification == null)
            return NotFound();

        _context.Notifications.Remove(notification);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Уведомление удалено";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Настройки уведомлений пользователя
    /// </summary>
    public async Task<IActionResult> Settings()
    {
        var userId = _userManager.GetUserId(User);
        var settings = await _context.UserNotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (settings == null)
        {
            settings = new UserNotificationSettings
            {
                UserId = userId!
            };
            _context.UserNotificationSettings.Add(settings);
            await _context.SaveChangesAsync();
        }

        return View(settings);
    }

    /// <summary>
    /// Сохранить настройки уведомлений
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SaveSettings(UserNotificationSettings model)
    {
        var userId = _userManager.GetUserId(User);
        var settings = await _context.UserNotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (settings == null)
            return NotFound();

        settings.EmailEnabled = model.EmailEnabled;
        settings.TelegramEnabled = model.TelegramEnabled;
        settings.InAppEnabled = model.InAppEnabled;
        settings.TelegramChatId = model.TelegramChatId;
        settings.TaskNotifications = model.TaskNotifications;
        settings.DeadlineNotifications = model.DeadlineNotifications;
        settings.PaymentNotifications = model.PaymentNotifications;
        settings.SystemNotifications = model.SystemNotifications;
        settings.DeadlineWarningDays = model.DeadlineWarningDays;

        await _context.SaveChangesAsync();
        
        TempData["Success"] = "Настройки сохранены";
        return RedirectToAction(nameof(Settings));
    }
}
