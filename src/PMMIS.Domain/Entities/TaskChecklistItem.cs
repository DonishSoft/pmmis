using System.ComponentModel.DataAnnotations;

namespace PMMIS.Domain.Entities;

/// <summary>
/// Пункт чек-листа задачи
/// </summary>
public class TaskChecklistItem : BaseEntity
{
    [Required]
    [MaxLength(500)]
    public string Text { get; set; } = string.Empty;
    
    public bool IsCompleted { get; set; }
    public bool IsImportant { get; set; }
    public bool IsIndented { get; set; }
    public int SortOrder { get; set; }
    
    // Соисполнитель (опционально)
    public string? CoExecutorId { get; set; }
    public ApplicationUser? CoExecutor { get; set; }
    
    // Связь с чек-листом
    public int TaskChecklistId { get; set; }
    public TaskChecklist? TaskChecklist { get; set; }
}
