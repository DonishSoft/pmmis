namespace PMMIS.Domain.Entities;

/// <summary>
/// Базовый класс для всех сущностей
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Базовый класс для сущностей с переводами
/// </summary>
public abstract class LocalizedEntity : BaseEntity
{
    public string NameRu { get; set; } = string.Empty;
    public string NameTj { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    
    public string GetName(string language) => language switch
    {
        "tj" => NameTj,
        "en" => NameEn,
        _ => NameRu
    };
}
