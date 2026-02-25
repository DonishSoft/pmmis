using Microsoft.AspNetCore.Identity;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;
using System.Security.Claims;
using System.Text.Json;

namespace PMMIS.Web.Services;

/// <summary>
/// Сервис аудита — отслеживание изменений в сущностях
/// </summary>
public interface IAuditService
{
    Task LogCreateAsync(string entityType, int entityId, ClaimsPrincipal user);
    Task LogChangesAsync<T>(string entityType, int entityId, T oldEntity, T newEntity, ClaimsPrincipal user) where T : class;
    Task LogDeleteAsync(string entityType, int entityId, ClaimsPrincipal user);
}

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    // Словарь человекочитаемых названий полей
    private static readonly Dictionary<string, string> FieldLabels = new()
    {
        // ProcurementPlan
        ["ReferenceNo"] = "Референс номер",
        ["Description"] = "Описание",
        ["DescriptionTj"] = "Описание (тадж.)",
        ["DescriptionEn"] = "Описание (англ.)",
        ["EstimatedAmount"] = "Оценочная стоимость",
        ["Method"] = "Метод закупки",
        ["Type"] = "Тип",
        ["Status"] = "Статус",
        ["AdvertisementDate"] = "Дата объявления",
        ["PlannedBidOpeningDate"] = "Плановая дата открытия тендера",
        ["PlannedContractSigningDate"] = "Плановая дата подписания контракта",
        ["PlannedCompletionDate"] = "Плановая дата завершения",
        ["ActualBidOpeningDate"] = "Фактическая дата открытия тендера",
        ["ActualContractSigningDate"] = "Фактическая дата подписания контракта",
        ["ActualCompletionDate"] = "Фактическая дата завершения",
        ["ProjectId"] = "Проект",
        ["ComponentId"] = "Компонент",
        ["SubComponentId"] = "Субкомпонент",
        ["ContractId"] = "Контракт",
        
        // Contract
        ["ContractNumber"] = "Номер контракта",
        ["ScopeOfWork"] = "Объём работ",
        ["ScopeOfWorkTj"] = "Объём работ (тадж.)",
        ["ScopeOfWorkEn"] = "Объём работ (англ.)",
        ["ContractAmount"] = "Сумма контракта",
        ["ContractorId"] = "Подрядчик",
        ["SigningDate"] = "Дата подписания",
        ["ContractEndDate"] = "Дата завершения",
        ["ExtendedEndDate"] = "Продлён до",
        ["Currency"] = "Валюта",
        ["ExchangeRate"] = "Курс подписания",
        ["AmountInTJS"] = "Сумма (TJS)",
        ["AdditionalAmount"] = "Дополнительная сумма",
        ["SavedAmount"] = "Экономия",
        ["WorkCompletedPercent"] = "Выполнено (%)",
        ["ProcurementPlanId"] = "План закупок",
        ["CuratorId"] = "Куратор",
        ["ProjectManagerId"] = "Менеджер проекта",
        ["PerformanceRating"] = "Оценка исполнения",
    };

    // Поля, которые не нужно отслеживать
    private static readonly HashSet<string> IgnoredFields = new()
    {
        "Id", "CreatedAt", "UpdatedAt",
        // Navigation properties and collections
        "Project", "Component", "SubComponent", "Contract",
        "Contractor", "ProcurementPlan", "Curator", "ProjectManager",
        "Payments", "Documents", "Amendments", "Milestones",
        "Contracts", "Indicators", "WorkItems", "Progresses",
        "ContractIndicators", "ContractWorkItems"
    };

    public AuditService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task LogCreateAsync(string entityType, int entityId, ClaimsPrincipal user)
    {
        var appUser = await _userManager.GetUserAsync(user);
        _context.AuditLogs.Add(new AuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = "Создание",
            UserId = appUser?.Id ?? "",
            UserFullName = appUser?.FullName ?? "Система",
            UserPosition = appUser?.Position,
            Timestamp = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    public async Task LogChangesAsync<T>(string entityType, int entityId, T oldEntity, T newEntity, ClaimsPrincipal user) where T : class
    {
        var changes = CompareEntities(oldEntity, newEntity);
        if (changes.Count == 0) return;

        var appUser = await _userManager.GetUserAsync(user);
        _context.AuditLogs.Add(new AuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = "Изменение",
            Changes = JsonSerializer.Serialize(changes),
            UserId = appUser?.Id ?? "",
            UserFullName = appUser?.FullName ?? "Система",
            UserPosition = appUser?.Position,
            Timestamp = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    public async Task LogDeleteAsync(string entityType, int entityId, ClaimsPrincipal user)
    {
        var appUser = await _userManager.GetUserAsync(user);
        _context.AuditLogs.Add(new AuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = "Удаление",
            UserId = appUser?.Id ?? "",
            UserFullName = appUser?.FullName ?? "Система",
            UserPosition = appUser?.Position,
            Timestamp = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    private List<ChangeEntry> CompareEntities<T>(T oldEntity, T newEntity) where T : class
    {
        var changes = new List<ChangeEntry>();
        var properties = typeof(T).GetProperties()
            .Where(p => p.CanRead && !IgnoredFields.Contains(p.Name));

        foreach (var prop in properties)
        {
            var oldVal = prop.GetValue(oldEntity);
            var newVal = prop.GetValue(newEntity);

            // Skip collection/complex navigation properties
            if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string) 
                && !prop.PropertyType.IsEnum && !Nullable.GetUnderlyingType(prop.PropertyType)?.IsEnum == true)
                continue;

            var oldStr = FormatValue(oldVal);
            var newStr = FormatValue(newVal);

            if (oldStr != newStr)
            {
                var label = FieldLabels.GetValueOrDefault(prop.Name, prop.Name);
                changes.Add(new ChangeEntry
                {
                    Field = label,
                    OldValue = oldStr ?? "—",
                    NewValue = newStr ?? "—"
                });
            }
        }

        return changes;
    }

    private static string? FormatValue(object? value)
    {
        if (value == null) return null;
        
        return value switch
        {
            DateTime dt => dt.ToString("dd.MM.yyyy"),
            decimal d => d.ToString("N2"),
            double d => d.ToString("N2"),
            Enum e => e.ToString(),
            _ => value.ToString()
        };
    }

    private class ChangeEntry
    {
        public string Field { get; set; } = "";
        public string OldValue { get; set; } = "";
        public string NewValue { get; set; } = "";
    }
}
