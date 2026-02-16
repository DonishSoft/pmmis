using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;
using PMMIS.Web.Authorization;

namespace PMMIS.Web.Services;

/// <summary>
/// Сервис экспорта данных в Excel
/// </summary>
public interface IExportService
{
    Task<byte[]> ExportTasksToExcelAsync(DateTime? from, DateTime? to, ProjectTaskStatus? status, string? userId);
    Task<byte[]> ExportKpiReportAsync(DateTime from, DateTime to);
}

public class ExportService : IExportService
{
    private readonly ApplicationDbContext _context;

    public ExportService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<byte[]> ExportTasksToExcelAsync(DateTime? from, DateTime? to, ProjectTaskStatus? status, string? userId)
    {
        var query = _context.ProjectTasks
            .Include(t => t.Assignee)
            .Include(t => t.AssignedBy)
            .Include(t => t.Project)
            .Include(t => t.Contract)
            .AsQueryable();

        if (from.HasValue)
            query = query.Where(t => t.CreatedAt >= from.Value);
        
        if (to.HasValue)
            query = query.Where(t => t.CreatedAt <= to.Value);
        
        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);
        
        if (!string.IsNullOrEmpty(userId))
            query = query.Where(t => t.AssigneeId == userId);

        var tasks = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Задачи");

        // Header
        worksheet.Cell(1, 1).Value = "ID";
        worksheet.Cell(1, 2).Value = "Заголовок";
        worksheet.Cell(1, 3).Value = "Статус";
        worksheet.Cell(1, 4).Value = "Приоритет";
        worksheet.Cell(1, 5).Value = "Исполнитель";
        worksheet.Cell(1, 6).Value = "Назначил";
        worksheet.Cell(1, 7).Value = "Дедлайн";
        worksheet.Cell(1, 8).Value = "Создана";
        worksheet.Cell(1, 9).Value = "Завершена";
        worksheet.Cell(1, 10).Value = "Проект";
        worksheet.Cell(1, 11).Value = "Контракт";
        
        // Style header
        var headerRange = worksheet.Range(1, 1, 1, 11);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;

        // Data
        var row = 2;
        foreach (var task in tasks)
        {
            worksheet.Cell(row, 1).Value = task.Id;
            worksheet.Cell(row, 2).Value = task.Title;
            worksheet.Cell(row, 3).Value = GetStatusName(task.Status);
            worksheet.Cell(row, 4).Value = GetPriorityName(task.Priority);
            worksheet.Cell(row, 5).Value = task.Assignee?.FullName ?? "";
            worksheet.Cell(row, 6).Value = task.AssignedBy?.FullName ?? "";
            worksheet.Cell(row, 7).Value = task.DueDate;
            worksheet.Cell(row, 8).Value = task.CreatedAt;
            worksheet.Cell(row, 9).Value = task.CompletedAt;
            worksheet.Cell(row, 10).Value = task.Project?.Code ?? "";
            worksheet.Cell(row, 11).Value = task.Contract?.ContractNumber ?? "";

            // Highlight overdue
            if (task.Status != ProjectTaskStatus.Completed && task.DueDate < DateTime.UtcNow)
            {
                worksheet.Row(row).Style.Font.FontColor = XLColor.Red;
            }
            
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportKpiReportAsync(DateTime from, DateTime to)
    {
        var tasks = await _context.ProjectTasks
            .Include(t => t.Assignee)
            .Where(t => t.CreatedAt >= from && t.CreatedAt <= to)
            .ToListAsync();

        var userStats = tasks
            .GroupBy(t => t.Assignee)
            .Select(g => new
            {
                User = g.Key?.FullName ?? "Не назначен",
                Total = g.Count(),
                Completed = g.Count(t => t.Status == ProjectTaskStatus.Completed),
                OnTime = g.Count(t => t.Status == ProjectTaskStatus.Completed && t.CompletedAt <= t.DueDate),
                Late = g.Count(t => t.Status == ProjectTaskStatus.Completed && t.CompletedAt > t.DueDate),
                Overdue = g.Count(t => t.Status != ProjectTaskStatus.Completed && t.DueDate < DateTime.UtcNow)
            })
            .OrderByDescending(x => x.Completed)
            .ToList();

        using var workbook = new XLWorkbook();
        
        // Summary sheet
        var summary = workbook.Worksheets.Add("Сводка");
        summary.Cell(1, 1).Value = "KPI отчёт";
        summary.Cell(1, 1).Style.Font.Bold = true;
        summary.Cell(1, 1).Style.Font.FontSize = 16;
        
        summary.Cell(2, 1).Value = $"Период: {from:dd.MM.yyyy} - {to:dd.MM.yyyy}";
        
        summary.Cell(4, 1).Value = "Всего задач:";
        summary.Cell(4, 2).Value = tasks.Count;
        
        summary.Cell(5, 1).Value = "Завершено:";
        summary.Cell(5, 2).Value = tasks.Count(t => t.Status == ProjectTaskStatus.Completed);
        
        summary.Cell(6, 1).Value = "Просрочено:";
        summary.Cell(6, 2).Value = tasks.Count(t => t.Status != ProjectTaskStatus.Completed && t.DueDate < DateTime.UtcNow);

        // User stats sheet
        var users = workbook.Worksheets.Add("По исполнителям");
        
        users.Cell(1, 1).Value = "Исполнитель";
        users.Cell(1, 2).Value = "Всего";
        users.Cell(1, 3).Value = "Завершено";
        users.Cell(1, 4).Value = "Вовремя";
        users.Cell(1, 5).Value = "С просрочкой";
        users.Cell(1, 6).Value = "Просрочены";
        users.Cell(1, 7).Value = "Эффективность %";
        
        var userHeader = users.Range(1, 1, 1, 7);
        userHeader.Style.Font.Bold = true;
        userHeader.Style.Fill.BackgroundColor = XLColor.LightGreen;

        var uRow = 2;
        foreach (var stat in userStats)
        {
            users.Cell(uRow, 1).Value = stat.User;
            users.Cell(uRow, 2).Value = stat.Total;
            users.Cell(uRow, 3).Value = stat.Completed;
            users.Cell(uRow, 4).Value = stat.OnTime;
            users.Cell(uRow, 5).Value = stat.Late;
            users.Cell(uRow, 6).Value = stat.Overdue;
            users.Cell(uRow, 7).Value = stat.Completed > 0 ? Math.Round((double)stat.OnTime / stat.Completed * 100, 1) : 0;
            uRow++;
        }

        users.Columns().AdjustToContents();
        summary.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string GetStatusName(ProjectTaskStatus status) => status switch
    {
        ProjectTaskStatus.New => "Новая",
        ProjectTaskStatus.InProgress => "В работе",
        ProjectTaskStatus.OnHold => "Приостановлена",
        ProjectTaskStatus.Completed => "Завершена",
        ProjectTaskStatus.Cancelled => "Отменена",
        _ => status.ToString()
    };

    private static string GetPriorityName(TaskPriority priority) => priority switch
    {
        TaskPriority.Low => "Низкий",
        TaskPriority.Normal => "Обычный",
        TaskPriority.High => "Высокий",
        TaskPriority.Critical => "Критический",
        _ => priority.ToString()
    };
}
