using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;
using PMMIS.Web.Extensions;
using PMMIS.Web.ViewModels.Users;

namespace PMMIS.Web.Controllers;

/// <summary>
/// Управление пользователями
/// </summary>
[Authorize(Roles = UserRoles.PmuAdmin)]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ApplicationDbContext context,
        IWebHostEnvironment environment)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _environment = environment;
    }

    public async Task<IActionResult> Index(string? search)
    {
        var usersQuery = _userManager.Users.AsQueryable();
        
        if (!string.IsNullOrEmpty(search))
        {
            usersQuery = usersQuery.Where(u => 
                u.FirstName.Contains(search) || 
                u.LastName.Contains(search) || 
                u.Email!.Contains(search));
        }
        
        var users = await usersQuery.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToListAsync();
        
        // Get roles for each user
        var userRoles = new Dictionary<string, IList<string>>();
        foreach (var user in users)
        {
            userRoles[user.Id] = await _userManager.GetRolesAsync(user);
        }

        var viewModel = new UserIndexViewModel
        {
            Users = users,
            UserRoles = userRoles,
            Search = search
        };
        
        return View(viewModel);
    }

    public async Task<IActionResult> Create()
    {
        var viewModel = new UserCreateViewModel
        {
            Roles = await _roleManager.Roles.OrderBy(r => r.SortOrder).ToListAsync()
        };
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserCreateViewModel model, IFormFile? photo, IFormFile? contractScan)
    {
        model.Roles = await _roleManager.Roles.OrderBy(r => r.SortOrder).ToListAsync();
        
        if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Password) || 
            string.IsNullOrEmpty(model.FirstName) || string.IsNullOrEmpty(model.LastName))
        {
            TempData["Error"] = "Заполните обязательные поля";
            return View(model);
        }

        // Check if email exists
        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            TempData["Error"] = "Пользователь с таким email уже существует";
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName,
            MiddleName = model.MiddleName,
            Gender = model.Gender,
            BirthDate = model.BirthDate.ToUtc(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Create user first (to get ID)
        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
            return View(model);
        }

        // Now handle file uploads (user has ID)
        bool needsUpdate = false;
        
        if (photo != null && photo.Length > 0)
        {
            user.PhotoPath = await SaveFile(photo, "users/photos", user.Id);
            needsUpdate = true;
        }

        if (contractScan != null && contractScan.Length > 0)
        {
            user.ContractScanPath = await SaveFile(contractScan, "users/contracts", user.Id);
            needsUpdate = true;
        }

        if (needsUpdate)
        {
            await _userManager.UpdateAsync(user);
        }
        
        // Assign role
        if (!string.IsNullOrEmpty(model.SelectedRole))
        {
            await _userManager.AddToRoleAsync(user, model.SelectedRole);
        }
        
        TempData["Success"] = $"Пользователь {user.FullName} успешно создан";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        
        var viewModel = new UserEditViewModel
        {
            User = user,
            Roles = await _roleManager.Roles.OrderBy(r => r.SortOrder).ToListAsync(),
            UserRoles = await _userManager.GetRolesAsync(user),
            AllUsers = await _context.Users
                .Where(u => u.IsActive && u.Id != id)
                .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
                .ToListAsync()
        };
        
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string id, string firstName, string lastName, string? middleName,
        Gender gender, DateTime? birthDate, bool isActive, string? selectedRole,
        string? supervisorId, string? position,
        IFormFile? photo, IFormFile? contractScan)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        user.FirstName = firstName;
        user.LastName = lastName;
        user.MiddleName = middleName;
        user.Gender = gender;
        user.BirthDate = birthDate.ToUtc();
        user.IsActive = isActive;
        user.SupervisorId = string.IsNullOrEmpty(supervisorId) ? null : supervisorId;
        user.Position = position;

        // Handle photo upload
        if (photo != null && photo.Length > 0)
        {
            // Delete old photo if exists
            if (!string.IsNullOrEmpty(user.PhotoPath))
            {
                DeleteFile(user.PhotoPath);
            }
            user.PhotoPath = await SaveFile(photo, "users/photos", user.Id);
        }

        // Handle contract scan upload
        if (contractScan != null && contractScan.Length > 0)
        {
            if (!string.IsNullOrEmpty(user.ContractScanPath))
            {
                DeleteFile(user.ContractScanPath);
            }
            user.ContractScanPath = await SaveFile(contractScan, "users/contracts", user.Id);
        }

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            // Update roles
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            
            if (!string.IsNullOrEmpty(selectedRole))
            {
                await _userManager.AddToRoleAsync(user, selectedRole);
            }
            
            TempData["Success"] = "Пользователь успешно обновлён";
            return RedirectToAction(nameof(Index));
        }
        
        TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
        var viewModel = new UserEditViewModel
        {
            User = user,
            Roles = await _roleManager.Roles.OrderBy(r => r.SortOrder).ToListAsync(),
            UserRoles = await _userManager.GetRolesAsync(user),
            AllUsers = await _context.Users
                .Where(u => u.IsActive && u.Id != id)
                .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
                .ToListAsync()
        };
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        // Don't allow deleting yourself
        if (user.UserName == User.Identity?.Name)
        {
            TempData["Error"] = "Нельзя удалить свой аккаунт";
            return RedirectToAction(nameof(Index));
        }

        // Delete files
        if (!string.IsNullOrEmpty(user.PhotoPath)) DeleteFile(user.PhotoPath);
        if (!string.IsNullOrEmpty(user.ContractScanPath)) DeleteFile(user.ContractScanPath);

        var result = await _userManager.DeleteAsync(user);
        if (result.Succeeded)
        {
            TempData["Success"] = "Пользователь удалён";
        }
        else
        {
            TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
        }
        
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string userId, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        
        if (result.Succeeded)
        {
            TempData["Success"] = "Пароль успешно сброшен";
        }
        else
        {
            TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
        }
        
        return RedirectToAction(nameof(Edit), new { id = userId });
    }

    private async Task<string> SaveFile(IFormFile file, string folder, string userId)
    {
        var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads", folder);
        Directory.CreateDirectory(uploadsDir);
        
        var extension = Path.GetExtension(file.FileName);
        var fileName = $"{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";
        var filePath = Path.Combine(uploadsDir, fileName);
        
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }
        
        return $"/uploads/{folder}/{fileName}";
    }

    private void DeleteFile(string relativePath)
    {
        var filePath = Path.Combine(_environment.WebRootPath, relativePath.TrimStart('/'));
        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }
    }

    // ===========================
    // Иерархия пользователей
    // ===========================

    /// <summary>
    /// Страница иерархии пользователей (TreeView)
    /// </summary>
    public async Task<IActionResult> Hierarchy()
    {
        var users = await _context.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync();

        // Build user-roles dictionary for display
        var userRoles = new Dictionary<string, string>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            userRoles[u.Id] = roles.FirstOrDefault() ?? "";
        }
        ViewBag.UserRoles = userRoles;

        return View(users);
    }

    /// <summary>
    /// Назначить/изменить руководителя (AJAX)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SetSupervisorJson([FromBody] SetSupervisorRequest request)
    {
        var user = await _context.Users.FindAsync(request.UserId);
        if (user == null) return NotFound();

        // Prevent circular reference — user can't be their own supervisor
        if (request.UserId == request.SupervisorId)
            return BadRequest("Пользователь не может быть своим руководителем");

        // Check for circular reference in the chain
        if (!string.IsNullOrEmpty(request.SupervisorId))
        {
            var current = request.SupervisorId;
            while (current != null)
            {
                if (current == request.UserId)
                    return BadRequest("Циклическая зависимость: этот пользователь является руководителем выбранного");
                var parent = await _context.Users.Where(u => u.Id == current).Select(u => u.SupervisorId).FirstOrDefaultAsync();
                current = parent;
            }
        }

        user.SupervisorId = string.IsNullOrEmpty(request.SupervisorId) ? null : request.SupervisorId;
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>
    /// Удалить из иерархии (обнулить SupervisorId) (AJAX)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RemoveFromHierarchyJson([FromBody] RemoveFromHierarchyRequest request)
    {
        var user = await _context.Users.FindAsync(request.UserId);
        if (user == null) return NotFound();

        user.SupervisorId = null;
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>
    /// Обновить должность пользователя (AJAX)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> UpdatePositionJson([FromBody] UpdatePositionRequest request)
    {
        var user = await _context.Users.FindAsync(request.UserId);
        if (user == null) return NotFound();

        user.Position = request.Position;
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>
    /// Импорт сотрудников МИЛ РИМ из списка 2025 года
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SeedStaff()
    {
        var staffList = new[]
        {
            new { First = "Абдусамад", Last = "Саидвализода", Middle = "Раҷабалӣ", Position = "Директор", Phone = "+992988461040", Email = "rwssp@midp.tj", Role = UserRoles.PmuAdmin, Gender = Gender.Male },
            new { First = "Фируз", Last = "Ализода", Middle = "Воҳид", Position = "Главный инженер", Phone = "+992901010003", Email = "f.alizoda@bk.ru", Role = UserRoles.PmuStaff, Gender = Gender.Male },
            new { First = "Фотеҳ", Last = "Асрорзода", Middle = "Асрор", Position = "Менеджер проекта", Phone = "+992985552235", Email = "foteh_2905@mail.ru", Role = UserRoles.PmuStaff, Gender = Gender.Male },
            new { First = "Хабибуло", Last = "Амонов", Middle = "Нурулоевич", Position = "Главный бухгалтер", Phone = "+992980200505", Email = "amonov-66@mail.ru", Role = UserRoles.PmuStaff, Gender = Gender.Male },
            new { First = "Ҷамшед", Last = "Қурбонов", Middle = (string?)null, Position = "Специалист по социальному развитию", Phone = "+992980200808", Email = "jkurbanov72@mail.ru", Role = UserRoles.PmuStaff, Gender = Gender.Male },
            new { First = "Анвар", Last = "Гулов", Middle = "Темирович", Position = "Специалист по финансовому управлению", Phone = "+992989997217", Email = "anvarsho65@mail.ru", Role = UserRoles.PmuStaff, Gender = Gender.Male },
            new { First = "Амирхон", Last = "Кудратуллозода", Middle = (string?)null, Position = "Советник/переводчик", Phone = "+992987805806", Email = "amirkhon66_88@mail.ru", Role = UserRoles.PmuStaff, Gender = Gender.Male },
            new { First = "Шариф", Last = "Сафарзода", Middle = "Сайфулло", Position = "Специалист по социальному развитию", Phone = "+992901005011", Email = "sarifsafarzoda0@gmail.com", Role = UserRoles.PmuStaff, Gender = Gender.Male },
            new { First = "Зулфия", Last = "Курбонова", Middle = (string?)null, Position = "Советник/специалист по мобилизации", Phone = "+992987360065", Email = "qurbonova_67@mail.ru", Role = UserRoles.PmuStaff, Gender = Gender.Female },
            new { First = "Дибар", Last = "Назарова", Middle = "Давроновна", Position = "Старший специалист по закупкам", Phone = "+992918111825", Email = "dilbar.naz@gmail.com", Role = UserRoles.PmuStaff, Gender = Gender.Female },
            new { First = "Беҳруз", Last = "Фируззода", Middle = (string?)null, Position = "Специалист по мониторингу и оценке", Phone = "+992988939999", Email = "firuzzoda.b@gmail.com", Role = UserRoles.PmuStaff, Gender = Gender.Male },
            new { First = "Меҳрубон", Last = "Мерганов", Middle = "Бекназарович", Position = "IT-специалист", Phone = "+992985680196", Email = "merganov.2014@mail.ru", Role = UserRoles.PmuStaff, Gender = Gender.Male },
            new { First = "Гулсара", Last = "Мамадумбархонова", Middle = (string?)null, Position = "Специалист по закупкам", Phone = "+992935556745", Email = "gulsara.payshanbeevna@gmail.com", Role = UserRoles.PmuStaff, Gender = Gender.Female },
            new { First = "Хайрулло", Last = "Мадисоев", Middle = (string?)null, Position = "Специалист по СМИ", Phone = "+992870675555", Email = "khayrulloi.abdullo@mail.ru", Role = UserRoles.PmuStaff, Gender = Gender.Male },
            new { First = "Адиба", Last = "Маҳмадиева", Middle = (string?)null, Position = "Кадровый специалист", Phone = "+992888223981", Email = "mahmadievaa@pmmis.tj", Role = UserRoles.PmuStaff, Gender = Gender.Female },
            new { First = "Искандаршо", Last = "Исматов", Middle = "Ҷурабекович", Position = "Администратор", Phone = "+992987606895", Email = "mmurov54@gmail.com", Role = UserRoles.PmuStaff, Gender = Gender.Male },
            new { First = "Раҷаб", Last = "Холов", Middle = (string?)null, Position = "Инженер/строитель", Phone = "+992904421444", Email = "bahrom5-95@mail.ru", Role = UserRoles.PmuStaff, Gender = Gender.Male },
            new { First = "Манзаршо", Last = "Шарипов", Middle = (string?)null, Position = "Инженер/строитель", Phone = "+992988300333", Email = "manzarsho.sharipov@mail.ru", Role = UserRoles.PmuStaff, Gender = Gender.Male },
            new { First = "Амирхамза", Last = "Хакимов", Middle = "Замонович", Position = "Специалист по закупкам", Phone = "+992918111190", Email = "hakimzoda.a@mail.ru", Role = UserRoles.PmuStaff, Gender = Gender.Male },
            new { First = "Турсунбой", Last = "Акрамзода", Middle = (string?)null, Position = "Специалист/эколог", Phone = "+992988171800", Email = "t.akramzoda@mail.ru", Role = UserRoles.PmuStaff, Gender = Gender.Male },
            new { First = "Азиз", Last = "Саидмуродов", Middle = (string?)null, Position = "Специалист/эколог", Phone = "+992907933333", Email = "aziz19970218@gmail.com", Role = UserRoles.PmuStaff, Gender = Gender.Male },
        };

        // Hierarchy: supervisor email => subordinate emails
        var hierarchy = new Dictionary<string, string[]>
        {
            // Director => direct reports
            ["rwssp@midp.tj"] = new[] { "f.alizoda@bk.ru", "foteh_2905@mail.ru", "amonov-66@mail.ru", "dilbar.naz@gmail.com", "amirkhon66_88@mail.ru", "qurbonova_67@mail.ru", "mahmadievaa@pmmis.tj", "mmurov54@gmail.com" },
            // Chief Engineer => engineers
            ["f.alizoda@bk.ru"] = new[] { "bahrom5-95@mail.ru", "manzarsho.sharipov@mail.ru" },
            // Project Manager => specialists
            ["foteh_2905@mail.ru"] = new[] { "jkurbanov72@mail.ru", "sarifsafarzoda0@gmail.com", "firuzzoda.b@gmail.com", "khayrulloi.abdullo@mail.ru", "merganov.2014@mail.ru", "t.akramzoda@mail.ru", "aziz19970218@gmail.com" },
            // Chief Accountant => finance
            ["amonov-66@mail.ru"] = new[] { "anvarsho65@mail.ru" },
            // Senior Procurement => procurement
            ["dilbar.naz@gmail.com"] = new[] { "gulsara.payshanbeevna@gmail.com", "hakimzoda.a@mail.ru" },
        };

        var created = new List<string>();
        var skipped = new List<string>();
        var emailToUserId = new Dictionary<string, string>();

        foreach (var s in staffList)
        {
            var existing = await _userManager.FindByEmailAsync(s.Email);
            if (existing != null)
            {
                skipped.Add(s.Email);
                emailToUserId[s.Email] = existing.Id;
                // Update position if missing
                if (string.IsNullOrEmpty(existing.Position))
                {
                    existing.Position = s.Position;
                    await _userManager.UpdateAsync(existing);
                }
                continue;
            }

            var user = new ApplicationUser
            {
                UserName = s.Email,
                Email = s.Email,
                EmailConfirmed = true,
                FirstName = s.First,
                LastName = s.Last,
                MiddleName = s.Middle,
                Position = s.Position,
                PhoneNumber = s.Phone,
                Gender = s.Gender,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, "Staff2025!");
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, s.Role);
                created.Add(s.Email);
                emailToUserId[s.Email] = user.Id;
            }
            else
            {
                skipped.Add($"{s.Email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }

        // Set hierarchy
        var hierarchySet = 0;
        foreach (var (supervisorEmail, subordinateEmails) in hierarchy)
        {
            if (!emailToUserId.TryGetValue(supervisorEmail, out var supervisorId)) continue;
            foreach (var subEmail in subordinateEmails)
            {
                if (!emailToUserId.TryGetValue(subEmail, out var subId)) continue;
                var sub = await _context.Users.FindAsync(subId);
                if (sub != null)
                {
                    sub.SupervisorId = supervisorId;
                    hierarchySet++;
                }
            }
        }
        await _context.SaveChangesAsync();

        return Json(new { created = created.Count, skipped = skipped.Count, hierarchySet, details = new { created, skipped } });
    }
}

// DTOs for hierarchy AJAX
public record SetSupervisorRequest(string UserId, string? SupervisorId);
public record RemoveFromHierarchyRequest(string UserId);
public record UpdatePositionRequest(string UserId, string? Position);

