namespace PMMIS.Web.ViewModels.Reports;

/// <summary>
/// Отчёт по KPI сотрудников
/// </summary>
public class EmployeeKpiReportViewModel
{
    public List<EmployeeKpiRow> Employees { get; set; } = new();
    public int TotalEmployees => Employees.Count;
    public int ActiveEmployees => Employees.Count(e => e.TotalTasks > 0);
    public decimal AverageCompletionRate => Employees.Any(e => e.TotalTasks > 0)
        ? Math.Round(Employees.Where(e => e.TotalTasks > 0).Average(e => e.CompletionPercent), 1)
        : 0;
    public int TotalTasks => Employees.Sum(e => e.TotalTasks);
    public int TotalCompletedTasks => Employees.Sum(e => e.CompletedTasks);
    public int TotalOverdueTasks => Employees.Sum(e => e.OverdueTasks);
}

public class EmployeeKpiRow
{
    public string UserId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Position { get; set; }
    public string? Email { get; set; }
    
    /// <summary>Всего задач назначено</summary>
    public int TotalTasks { get; set; }
    
    /// <summary>Завершённые задачи</summary>
    public int CompletedTasks { get; set; }
    
    /// <summary>В работе</summary>
    public int InProgressTasks { get; set; }
    
    /// <summary>Просроченные задачи</summary>
    public int OverdueTasks { get; set; }
    
    /// <summary>Процент выполнения</summary>
    public decimal CompletionPercent => TotalTasks > 0 
        ? Math.Round((decimal)CompletedTasks / TotalTasks * 100, 1) 
        : 0;
    
    /// <summary>Средняя оценка</summary>
    public decimal? AverageRating { get; set; }
    
    /// <summary>Кол-во АВР</summary>
    public int AvrCount { get; set; }
}
