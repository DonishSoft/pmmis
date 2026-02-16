namespace PMMIS.Domain.Entities;

/// <summary>
/// Гранулярные права доступа роли к модулям
/// </summary>
public class RoleMenuPermission : BaseEntity
{
    public string RoleId { get; set; } = string.Empty;
    public ApplicationRole Role { get; set; } = null!;
    
    public string MenuKey { get; set; } = string.Empty;  // Ключ модуля (напр. "Contracts", "Projects")
    
    // CRUD permissions
    public bool CanView { get; set; } = false;     // Просмотр
    public bool CanViewAll { get; set; } = false;   // Просмотр всех данных (иначе — только свои)
    public bool CanCreate { get; set; } = false;   // Создание
    public bool CanEdit { get; set; } = false;     // Редактирование  
    public bool CanDelete { get; set; } = false;   // Удаление
}

/// <summary>
/// Константы модулей для прав доступа
/// </summary>
public static class MenuKeys
{
    public const string Home = "Home";
    public const string Contracts = "Contracts";
    public const string Contractors = "Contractors";
    public const string Projects = "Projects";
    public const string Procurement = "Procurement";
    public const string Payments = "Payments";
    public const string WorkProgress = "WorkProgress";
    public const string Geography = "Geography";
    public const string Indicators = "Indicators";
    public const string ReferenceData = "ReferenceData";
    public const string Import = "Import";
    public const string Reports = "Reports";
    public const string Documents = "Documents";
    public const string Users = "Users";
    public const string Roles = "Roles";
    public const string Settings = "Settings";
    public const string Tasks = "Tasks";
    public const string Notifications = "Notifications";
    public const string WorkProgressReports = "WorkProgressReports";
    public const string CurrencyRates = "CurrencyRates";
    
    public static readonly Dictionary<string, string> Names = new()
    {
        { Home, "Главная" },
        { Contracts, "Контракты" },
        { Contractors, "Подрядчики" },
        { Projects, "Проекты" },
        { Procurement, "План закупок" },
        { Payments, "Платежи" },
        { WorkProgress, "Прогресс работ" },
        { Geography, "География" },
        { Indicators, "Индикаторы" },
        { ReferenceData, "Справочники" },
        { Import, "Импорт" },
        { Reports, "Отчёты" },
        { Documents, "Документы" },
        { Users, "Пользователи" },
        { Roles, "Роли" },
        { Settings, "Настройки" },
        { Tasks, "Задачи" },
        { Notifications, "Уведомления" },
        { WorkProgressReports, "АВР" },
        { CurrencyRates, "Курсы валют" }
    };
    
    public static readonly string[] All = { Home, Contracts, Contractors, Projects, Procurement, Payments, WorkProgress, WorkProgressReports, Geography, Indicators, ReferenceData, Import, Reports, Documents, Users, Roles, Settings, Tasks, Notifications, CurrencyRates };
}

