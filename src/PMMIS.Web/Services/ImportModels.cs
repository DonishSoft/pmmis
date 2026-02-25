namespace PMMIS.Web.Services;

/// <summary>
/// Результат импорта Excel (общий для ClosedXML и AI)
/// </summary>
public class ImportResult
{
    /// <summary>
    /// Все спарсенные позиции из Excel
    /// </summary>
    public List<ImportedWorkItem> Items { get; set; } = new();
    
    /// <summary>
    /// Новые позиции (есть в файле, нет в базе)
    /// </summary>
    public List<ImportedWorkItem> NewItems { get; set; } = new();
    
    /// <summary>
    /// Отсутствующие (есть в базе, нет в файле)
    /// </summary>
    public List<MissingItem> MissingItems { get; set; } = new();
    
    /// <summary>
    /// Изменения в существующих записях
    /// </summary>
    public List<ImportDifference> Changes { get; set; } = new();
    
    /// <summary>
    /// Сопоставленные позиции (без изменений)
    /// </summary>
    public List<MatchedItem> Matched { get; set; } = new();
    
    /// <summary>
    /// Ошибки парсинга
    /// </summary>
    public List<string> Errors { get; set; } = new();
    
    /// <summary>
    /// Режим импорта
    /// </summary>
    public string Mode { get; set; } = "ClosedXML";
    
    /// <summary>
    /// Первый импорт?
    /// </summary>
    public bool IsFirstImport { get; set; }
    
    /// <summary>
    /// ID контракта
    /// </summary>
    public int ContractId { get; set; }
}

/// <summary>
/// Позиция из Excel
/// </summary>
public class ImportedWorkItem
{
    public string? ItemNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Category { get; set; }
    public int SortOrder { get; set; }
    
    // Progress columns
    public decimal PreviousQuantity { get; set; }
    public decimal ThisPeriodQuantity { get; set; }
    public decimal CumulativeQuantity { get; set; }
    public decimal PreviousAmount { get; set; }
    public decimal ThisPeriodAmount { get; set; }
    public decimal CumulativeAmount { get; set; }
    
    /// <summary>
    /// ID существующей записи (если сопоставлено)
    /// </summary>
    public int? ExistingId { get; set; }
}

/// <summary>
/// Отсутствующая позиция
/// </summary>
public class MissingItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string? ItemNumber { get; set; }
}

/// <summary>
/// Различие между существующей и импортированной записью
/// </summary>
public class ImportDifference
{
    public int ExistingId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
}

/// <summary>
/// Сопоставленная позиция
/// </summary>
public class MatchedItem
{
    public int ExistingId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal ThisPeriodQuantity { get; set; }
    public decimal ThisPeriodAmount { get; set; }
    public decimal PreviousQuantity { get; set; }
    public decimal PreviousAmount { get; set; }
}
