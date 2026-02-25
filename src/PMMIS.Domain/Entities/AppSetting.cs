namespace PMMIS.Domain.Entities;

/// <summary>
/// Системные настройки приложения (ключ-значение)
/// </summary>
public class AppSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
}
