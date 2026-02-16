using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;
using PMMIS.Web.Authorization;
using PMMIS.Web.Extensions;
using PMMIS.Web.Services;
using PMMIS.Web.ViewModels.Tasks;

namespace PMMIS.Web.Controllers;

/// <summary>
/// Контроллер управления задачами
/// </summary>
[Authorize]
[RequirePermission(MenuKeys.Tasks)]
public class TasksController : Controller
{
    private readonly ITaskService _taskService;
    private readonly IExportService _exportService;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserHierarchyService _hierarchyService;
    private readonly ILogger<TasksController> _logger;

    public TasksController(
        ITaskService taskService,
        IExportService exportService,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IUserHierarchyService hierarchyService,
        ILogger<TasksController> logger)
    {
        _taskService = taskService;
        _exportService = exportService;
        _context = context;
        _userManager = userManager;
        _hierarchyService = hierarchyService;
        _logger = logger;
    }

    /// <summary>
    /// Список всех задач
    /// </summary>
    public async Task<IActionResult> Index(
        ProjectTaskStatus? status,
        TaskPriority? priority,
        string? assignee,
        int? contract,
        bool? overdue,
        string? search,
        int page = 1)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var filter = new TaskFilterDto
        {
            Status = status,
            Priority = priority,
            AssigneeId = assignee,
            ContractId = contract,
            IsOverdue = overdue,
            SearchTerm = search
        };

        var (tasks, totalCount) = await _taskService.GetTeamTasksAsync(currentUser.Id, filter, page);

        var viewModel = new TaskIndexViewModel
        {
            Tasks = tasks,
            TotalCount = totalCount,
            CurrentPage = page,
            StatusFilter = status,
            PriorityFilter = priority,
            AssigneeFilter = assignee,
            ContractFilter = contract,
            IsOverdueFilter = overdue,
            SearchTerm = search,
            Users = await GetAssignableUsersAsync(currentUser),
            Contracts = await _context.Contracts.ToListAsync(),
            OverdueCount = (await _taskService.GetOverdueTasksAsync(currentUser.Id)).Count,
            UpcomingCount = (await _taskService.GetUpcomingDeadlinesAsync(3, currentUser.Id)).Count
        };

        // For Quick Add modal
        ViewBag.Projects = await _context.Projects.ToListAsync();

        return View(viewModel);
    }

    /// <summary>
    /// Быстрое создание задачи (AJAX)
    /// </summary>
    [HttpPost]
    [RequirePermission(MenuKeys.Tasks, PermissionType.Create)]
    public async Task<IActionResult> QuickCreate(string Title, string? Description, string AssigneeId, DateTime DueDate)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Json(new { success = false, error = "Не авторизован" });

        try
        {
            if (string.IsNullOrEmpty(Title))
            {
                return Json(new { success = false, error = "Название обязательно" });
            }

            if (string.IsNullOrEmpty(AssigneeId))
            {
                return Json(new { success = false, error = "Выберите исполнителя" });
            }

            var task = new ProjectTask
            {
                Title = Title,
                Description = Description,
                Status = ProjectTaskStatus.New,
                Priority = TaskPriority.Normal,
                DueDate = DueDate.ToUtc(),
                AssigneeId = AssigneeId
            };

            await _taskService.CreateAsync(task, currentUser.Id);
            
            return Json(new { success = true, taskId = task.Id });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }



    /// <summary>
    /// Мои задачи
    /// </summary>
    public async Task<IActionResult> MyTasks(ProjectTaskStatus? status, int page = 1)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var filter = new TaskFilterDto { Status = status };
        var (tasks, totalCount) = await _taskService.GetUserTasksAsync(currentUser.Id, filter, page);

        var viewModel = new TaskIndexViewModel
        {
            Tasks = tasks,
            TotalCount = totalCount,
            CurrentPage = page,
            StatusFilter = status
        };

        return View("Index", viewModel);
    }

    /// <summary>
    /// Детали задачи
    /// </summary>
    public async Task<IActionResult> Details(int id)
    {
        var task = await _taskService.GetByIdAsync(id, includeRelations: true);
        if (task == null) return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        var currentUserRoles = currentUser != null ? await _userManager.GetRolesAsync(currentUser) : new List<string>();
        var isAdmin = currentUserRoles.Contains(UserRoles.PmuAdmin);
        var isAssignee = task.AssigneeId == currentUser?.Id;
        var isCreator = task.AssignedById == currentUser?.Id;

        // Load checklists with items
        var checklists = await _context.TaskChecklists
            .Where(c => c.ProjectTaskId == id)
            .Include(c => c.Items.OrderBy(i => i.SortOrder))
            .ThenInclude(i => i.CoExecutor)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();

        var viewModel = new TaskDetailsViewModel
        {
            Task = task,
            Comments = task.Comments.OrderByDescending(c => c.CreatedAt).ToList(),
            Attachments = task.Attachments.ToList(),
            History = task.History.OrderByDescending(h => h.CreatedAt).ToList(),
            ExtensionRequests = task.ExtensionRequests.OrderByDescending(r => r.CreatedAt).ToList(),
            SubTasks = task.SubTasks.ToList(),
            Checklists = checklists,
            CanEdit = isAdmin || isCreator,
            CanChangeStatus = isAdmin || isAssignee || isCreator,
            CanApproveExtension = isAdmin || isCreator,
            CanRequestExtension = isAssignee,
            CanAddComment = true,
            CanAddAttachment = isAssignee || isCreator || isAdmin,
            LinkedContract = task.Contract,
            LinkedProcurement = task.ProcurementPlan
        };

        return View(viewModel);
    }

    /// <summary>
    /// Форма создания задачи
    /// </summary>
    [RequirePermission(MenuKeys.Tasks, PermissionType.Create)]
    public async Task<IActionResult> Create(int? contractId = null, int? procurementId = null, int? parentTaskId = null)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var viewModel = new TaskFormViewModel
        {
            ContractId = contractId,
            ProcurementPlanId = procurementId,
            ParentTaskId = parentTaskId,
            AvailableAssignees = await GetAssignableUsersAsync(currentUser),
            Contracts = await _context.Contracts.ToListAsync(),
            ProcurementPlans = await _context.ProcurementPlans.ToListAsync(),
            Projects = await _context.Projects.ToListAsync(),
            ParentTasks = await _context.ProjectTasks
                .Where(t => t.ParentTaskId == null)
                .ToListAsync()
        };

        return View(viewModel);
    }

    /// <summary>
    /// Создать задачу
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Tasks, PermissionType.Create)]
    public async Task<IActionResult> Create(TaskFormViewModel model)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        if (string.IsNullOrEmpty(model.AssigneeId))
        {
            ModelState.AddModelError("AssigneeId", "Выберите исполнителя");
        }

        if (!ModelState.IsValid)
        {
            model.AvailableAssignees = await GetAssignableUsersAsync(currentUser);
            model.Contracts = await _context.Contracts.ToListAsync();
            model.ProcurementPlans = await _context.ProcurementPlans.ToListAsync();
            model.Projects = await _context.Projects.ToListAsync();
            return View(model);
        }

        // Проверка иерархии ролей
        if (!await _taskService.CanAssignToAsync(currentUser.Id, model.AssigneeId!))
        {
            ModelState.AddModelError("AssigneeId", "Вы не можете назначить задачу этому пользователю");
            model.AvailableAssignees = await GetAssignableUsersAsync(currentUser);
            return View(model);
        }

        var task = new ProjectTask
        {
            Title = model.Title,
            TitleTj = model.TitleTj,
            TitleEn = model.TitleEn,
            Description = model.Description,
            DescriptionTj = model.DescriptionTj,
            DescriptionEn = model.DescriptionEn,
            Status = ProjectTaskStatus.New,
            Priority = model.Priority,
            StartDate = model.StartDate?.ToUtc(),
            DueDate = model.DueDate.ToUtc(),
            AssigneeId = model.AssigneeId!,
            ParentTaskId = model.ParentTaskId,
            ContractId = model.ContractId,
            ProcurementPlanId = model.ProcurementPlanId,
            ProjectId = model.ProjectId,
            EstimatedHours = model.EstimatedHours
        };

        await _taskService.CreateAsync(task, currentUser.Id);

        // Save checklists if provided
        if (!string.IsNullOrEmpty(model.ChecklistsJson))
        {
            await SaveChecklistsAsync(task.Id, model.ChecklistsJson);
        }
        
        TempData["Success"] = "Задача успешно создана";
        return RedirectToAction(nameof(Details), new { id = task.Id });
    }

    /// <summary>
    /// Форма редактирования задачи
    /// </summary>
    [RequirePermission(MenuKeys.Tasks, PermissionType.Edit)]
    public async Task<IActionResult> Edit(int id)
    {
        var task = await _taskService.GetByIdAsync(id);
        if (task == null) return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var viewModel = new TaskFormViewModel
        {
            Id = task.Id,
            Title = task.Title,
            TitleTj = task.TitleTj,
            TitleEn = task.TitleEn,
            Description = task.Description,
            DescriptionTj = task.DescriptionTj,
            DescriptionEn = task.DescriptionEn,
            Status = task.Status,
            Priority = task.Priority,
            StartDate = task.StartDate,
            DueDate = task.DueDate,
            AssigneeId = task.AssigneeId,
            ParentTaskId = task.ParentTaskId,
            ContractId = task.ContractId,
            ProcurementPlanId = task.ProcurementPlanId,
            ProjectId = task.ProjectId,
            EstimatedHours = task.EstimatedHours,
            AvailableAssignees = await GetAssignableUsersAsync(currentUser),
            Contracts = await _context.Contracts.ToListAsync(),
            ProcurementPlans = await _context.ProcurementPlans.ToListAsync(),
            Projects = await _context.Projects.ToListAsync(),
            ParentTasks = await _context.ProjectTasks
                .Where(t => t.Id != id && t.ParentTaskId == null)
                .ToListAsync(),
            Checklists = await LoadChecklistDtosAsync(id)
        };

        return View(viewModel);
    }

    /// <summary>
    /// Сохранить изменения задачи
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Tasks, PermissionType.Edit)]
    public async Task<IActionResult> Edit(TaskFormViewModel model)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        if (!ModelState.IsValid)
        {
            model.AvailableAssignees = await GetAssignableUsersAsync(currentUser);
            model.Contracts = await _context.Contracts.ToListAsync();
            model.ProcurementPlans = await _context.ProcurementPlans.ToListAsync();
            model.Projects = await _context.Projects.ToListAsync();
            return View(model);
        }

        var task = await _taskService.GetByIdAsync(model.Id);
        if (task == null) return NotFound();

        task.Title = model.Title;
        task.TitleTj = model.TitleTj;
        task.TitleEn = model.TitleEn;
        task.Description = model.Description;
        task.DescriptionTj = model.DescriptionTj;
        task.DescriptionEn = model.DescriptionEn;
        task.Priority = model.Priority;
        task.StartDate = model.StartDate?.ToUtc();
        task.DueDate = model.DueDate.ToUtc();
        task.ParentTaskId = model.ParentTaskId;
        task.ContractId = model.ContractId;
        task.ProcurementPlanId = model.ProcurementPlanId;
        task.ProjectId = model.ProjectId;
        task.EstimatedHours = model.EstimatedHours;

        await _taskService.UpdateAsync(task, currentUser.Id);

        // Update checklists if provided
        if (!string.IsNullOrEmpty(model.ChecklistsJson))
        {
            await SaveChecklistsAsync(task.Id, model.ChecklistsJson);
        }
        
        TempData["Success"] = "Задача успешно обновлена";
        return RedirectToAction(nameof(Details), new { id = task.Id });
    }

    /// <summary>
    /// Изменить статус задачи (AJAX)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ChangeStatus(int id, ProjectTaskStatus status)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        try
        {
            await _taskService.ChangeStatusAsync(id, status, currentUser.Id);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Удалить задачу
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MenuKeys.Tasks, PermissionType.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        await _taskService.DeleteAsync(id, currentUser.Id);
        
        TempData["Success"] = "Задача удалена";
        return RedirectToAction(nameof(Index));
    }

    #region Extension Requests

    /// <summary>
    /// Форма запроса продления
    /// </summary>
    public async Task<IActionResult> RequestExtension(int taskId)
    {
        var task = await _taskService.GetByIdAsync(taskId);
        if (task == null) return NotFound();

        var viewModel = new TaskExtensionFormViewModel
        {
            TaskId = taskId,
            TaskTitle = task.Title,
            CurrentDueDate = task.DueDate,
            NewDueDate = task.DueDate.AddDays(7)
        };

        return View(viewModel);
    }

    /// <summary>
    /// Отправить запрос на продление
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestExtension(TaskExtensionFormViewModel model)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        if (string.IsNullOrWhiteSpace(model.Reason) || model.Reason.Length < 10)
        {
            ModelState.AddModelError("Reason", "Укажите причину продления (минимум 10 символов)");
            return View(model);
        }

        if (model.NewDueDate <= model.CurrentDueDate)
        {
            ModelState.AddModelError("NewDueDate", "Новая дата должна быть позже текущей");
            return View(model);
        }

        await _taskService.RequestExtensionAsync(
            model.TaskId,
            model.Reason,
            model.NewDueDate.ToUtc(),
            currentUser.Id);

        TempData["Success"] = "Запрос на продление отправлен";
        return RedirectToAction(nameof(Details), new { id = model.TaskId });
    }

    /// <summary>
    /// Список запросов на продление
    /// </summary>
    public async Task<IActionResult> Extensions()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var roles = await _userManager.GetRolesAsync(currentUser);
        var isAdmin = roles.Contains(UserRoles.PmuAdmin);

        var requests = await _taskService.GetPendingExtensionsAsync(isAdmin ? null : currentUser.Id);
        return View(requests);
    }

    /// <summary>
    /// Одобрить продление
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveExtension(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        await _taskService.ApproveExtensionAsync(id, currentUser.Id);
        
        TempData["Success"] = "Продление одобрено";
        return RedirectToAction(nameof(Extensions));
    }

    /// <summary>
    /// Отклонить продление
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectExtension(int id, string reason)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        await _taskService.RejectExtensionAsync(id, reason, currentUser.Id);
        
        TempData["Success"] = "Продление отклонено";
        return RedirectToAction(nameof(Extensions));
    }

    #endregion

    #region Comments & Attachments

    /// <summary>
    /// Добавить комментарий
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int taskId, string content)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(content))
        {
            return BadRequest("Комментарий не может быть пустым");
        }

        await _taskService.AddCommentAsync(taskId, content, currentUser.Id);
        return RedirectToAction(nameof(Details), new { id = taskId });
    }

    /// <summary>
    /// Загрузить вложение
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAttachment(int taskId, IFormFile file)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        if (file == null || file.Length == 0)
        {
            return BadRequest("Файл не выбран");
        }

        // Save file
        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "tasks");
        Directory.CreateDirectory(uploadsFolder);
        
        var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var relativePath = $"/uploads/tasks/{uniqueFileName}";
        await _taskService.AddAttachmentAsync(
            taskId,
            file.FileName,
            relativePath,
            file.Length,
            file.ContentType,
            currentUser.Id);

        return RedirectToAction(nameof(Details), new { id = taskId });
    }

    /// <summary>
    /// Удалить вложение
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAttachment(int attachmentId, int taskId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();

        await _taskService.DeleteAttachmentAsync(attachmentId, currentUser.Id);
        return RedirectToAction(nameof(Details), new { id = taskId });
    }

    #endregion

    #region KPI

    /// <summary>
    /// KPI дашборд
    /// </summary>
    public async Task<IActionResult> Kpi(DateTime? from, DateTime? to)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var fromDate = from ?? DateTime.UtcNow.AddMonths(-1);
        var toDate = to ?? DateTime.UtcNow;

        var userKpi = await _taskService.CalculateKpiAsync(currentUser.Id, fromDate, toDate);
        var teamKpi = await _taskService.CalculateTeamKpiAsync(fromDate, toDate);

        var viewModel = new TaskKpiViewModel
        {
            UserKpi = userKpi,
            TeamKpi = teamKpi,
            FromDate = fromDate,
            ToDate = toDate
        };

        return View(viewModel);
    }

    #endregion

    #region Private Helpers

    private async Task<List<ApplicationUser>> GetAssignableUsersAsync(ApplicationUser currentUser)
    {
        var currentRoles = await _userManager.GetRolesAsync(currentUser);
        
        // Админ может назначать задачи всем активным пользователям
        if (currentRoles.Contains(UserRoles.PmuAdmin))
        {
            return await _context.Users.Where(u => u.IsActive).ToListAsync();
        }
        
        // Остальные — только себе и своим подчинённым (рекурсивно по иерархии)
        var subordinates = await _hierarchyService.GetAllSubordinatesAsync(currentUser.Id);
        var result = new List<ApplicationUser> { currentUser };
        result.AddRange(subordinates);
        return result.Where(u => u.IsActive).ToList();
    }

    private async Task<List<ChecklistFormDto>> LoadChecklistDtosAsync(int taskId)
    {
        var checklists = await _context.TaskChecklists
            .Where(c => c.ProjectTaskId == taskId)
            .Include(c => c.Items.OrderBy(i => i.SortOrder))
            .ThenInclude(i => i.CoExecutor)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();

        return checklists.Select(c => new ChecklistFormDto
        {
            Id = c.Id,
            Name = c.Name,
            Items = c.Items.Select(i => new ChecklistItemDto
            {
                Id = i.Id,
                Text = i.Text,
                IsCompleted = i.IsCompleted,
                IsImportant = i.IsImportant,
                IsIndented = i.IsIndented,
                CoExecutorId = i.CoExecutorId,
                CoExecutorName = i.CoExecutor?.FullName
            }).ToList()
        }).ToList();
    }

    private async Task SaveChecklistsAsync(int taskId, string checklistsJson)
    {
        try
        {
            var checklistDtos = JsonSerializer.Deserialize<List<ChecklistFormDto>>(checklistsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (checklistDtos == null) return;

            // Remove existing checklists
            var existingChecklists = await _context.TaskChecklists
                .Where(c => c.ProjectTaskId == taskId)
                .Include(c => c.Items)
                .ToListAsync();

            _context.TaskChecklistItems.RemoveRange(existingChecklists.SelectMany(c => c.Items));
            _context.TaskChecklists.RemoveRange(existingChecklists);

            // Add new checklists
            var sortOrder = 0;
            foreach (var dto in checklistDtos)
            {
                var checklist = new TaskChecklist
                {
                    Name = dto.Name,
                    SortOrder = sortOrder++,
                    ProjectTaskId = taskId,
                    Items = dto.Items.Select((item, idx) => new TaskChecklistItem
                    {
                        Text = item.Text,
                        IsCompleted = item.IsCompleted,
                        IsImportant = item.IsImportant,
                        IsIndented = item.IsIndented,
                        CoExecutorId = item.CoExecutorId,
                        SortOrder = idx
                    }).ToList()
                };
                _context.TaskChecklists.Add(checklist);
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving checklists for task {TaskId}", taskId);
        }
    }

    #endregion

    #region Export

    /// <summary>
    /// Экспорт задач в Excel
    /// </summary>
    public async Task<IActionResult> ExportTasks(
        DateTime? from, 
        DateTime? to, 
        ProjectTaskStatus? status,
        string? userId)
    {
        try
        {
            var bytes = await _exportService.ExportTasksToExcelAsync(from, to, status, userId);
            var fileName = $"tasks_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting tasks to Excel");
            TempData["Error"] = "Ошибка при экспорте";
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Экспорт KPI отчёта
    /// </summary>
    public async Task<IActionResult> ExportKpi(DateTime? from, DateTime? to)
    {
        try
        {
            var fromDate = from ?? DateTime.Today.AddMonths(-1);
            var toDate = to ?? DateTime.Today;
            
            var bytes = await _exportService.ExportKpiReportAsync(fromDate, toDate);
            var fileName = $"kpi_report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting KPI report");
            TempData["Error"] = "Ошибка при экспорте KPI";
            return RedirectToAction(nameof(Kpi));
        }
    }

    #endregion
}
