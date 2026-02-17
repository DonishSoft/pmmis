using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using PMMIS.Domain.Entities;

namespace PMMIS.Web.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment env)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _env = env;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, bool rememberMe, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ModelState.AddModelError(string.Empty, "Введите email и пароль");
            return View();
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Неверные учетные данные или пользователь деактивирован");
            return View();
        }

        var result = await _signInManager.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            // Update last login
            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            // Set preferred language
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(user.PreferredLanguage)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
            );

            return LocalRedirect(returnUrl ?? "/");
        }

        if (result.IsLockedOut)
        {
            return View("Lockout");
        }

        ModelState.AddModelError(string.Empty, "Неверные учетные данные");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }

    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpPost]
    public IActionResult SetLanguage(string culture, string returnUrl)
    {
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
        );

        return LocalRedirect(returnUrl);
    }

    // ==================== PROFILE ====================

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login");

        var roles = await _userManager.GetRolesAsync(user);
        ViewBag.Roles = roles;
        return View(user);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(string firstName, string lastName, string? middleName,
        Gender gender, string? position, DateTime? birthDate, string preferredLanguage, IFormFile? photo)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login");

        user.FirstName = firstName;
        user.LastName = lastName;
        user.MiddleName = middleName;
        user.Gender = gender;
        user.Position = position;
        user.BirthDate = birthDate.HasValue ? DateTime.SpecifyKind(birthDate.Value, DateTimeKind.Utc) : null;
        user.PreferredLanguage = preferredLanguage ?? "ru";

        // Handle photo upload
        if (photo != null && photo.Length > 0)
        {
            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "photos");
            Directory.CreateDirectory(uploadsDir);

            var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
            var fileName = $"profile_{user.Id}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await photo.CopyToAsync(stream);

            user.PhotoPath = $"/uploads/photos/{fileName}";
        }

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            // Update language cookie
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(user.PreferredLanguage)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
            );

            TempData["Success"] = "Профиль успешно обновлён";
        }
        else
        {
            TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
        }

        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login");

        if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword))
        {
            TempData["PasswordError"] = "Все поля обязательны для заполнения";
            return RedirectToAction(nameof(Profile));
        }

        if (newPassword != confirmPassword)
        {
            TempData["PasswordError"] = "Новый пароль и подтверждение не совпадают";
            return RedirectToAction(nameof(Profile));
        }

        if (newPassword.Length < 6)
        {
            TempData["PasswordError"] = "Пароль должен содержать не менее 6 символов";
            return RedirectToAction(nameof(Profile));
        }

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (result.Succeeded)
        {
            await _signInManager.RefreshSignInAsync(user);
            TempData["PasswordSuccess"] = "Пароль успешно изменён";
        }
        else
        {
            TempData["PasswordError"] = string.Join(", ", result.Errors.Select(e => e.Description));
        }

        return RedirectToAction(nameof(Profile));
    }
}
