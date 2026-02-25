using System.ComponentModel.DataAnnotations;

namespace PMMIS.Domain.Entities;

/// <summary>
/// Сессия импорта из Excel — одна запись на каждый загруженный файл
/// </summary>
public class ImportSession : BaseEntity
{
    /// <summary>
    /// Контракт
    /// </summary>
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    
    /// <summary>
    /// Имя загруженного файла
    /// </summary>
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Период (например "Ноябрь 2025", "Август 2025")
    /// </summary>
    [MaxLength(200)]
    public string PeriodName { get; set; } = string.Empty;
    
    /// <summary>
    /// Дата и время импорта
    /// </summary>
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Кто импортировал
    /// </summary>
    [MaxLength(200)]
    public string? ImportedBy { get; set; }
    
    /// <summary>
    /// Режим обработки (ClosedXML / Gemini AI)
    /// </summary>
    [MaxLength(50)]
    public string? Mode { get; set; }
    
    /// <summary>
    /// Всего позиций в файле
    /// </summary>
    public int TotalItems { get; set; }
    
    /// <summary>
    /// Совпавших позиций
    /// </summary>
    public int MatchedItems { get; set; }
    
    /// <summary>
    /// Порядок сортировки (для отображения вкладок)
    /// </summary>
    public int SortOrder { get; set; }
    
    /// <summary>
    /// Записи прогресса
    /// </summary>
    public ICollection<ImportSessionItem> Items { get; set; } = new List<ImportSessionItem>();
}
