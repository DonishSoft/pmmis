using System.ComponentModel.DataAnnotations;

namespace PMMIS.Domain.Entities;

/// <summary>
/// Чек-лист задачи (группа пунктов)
/// </summary>
public class TaskChecklist : BaseEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = "Чек-лист";
    
    public int SortOrder { get; set; }
    public bool IsExpanded { get; set; } = true;
    
    // Связь с задачей
    public int ProjectTaskId { get; set; }
    public ProjectTask? ProjectTask { get; set; }
    
    // Пункты чек-листа
    public List<TaskChecklistItem> Items { get; set; } = new();
    
    // Вычисляемые свойства
    public int CompletedCount => Items.Count(i => i.IsCompleted);
    public int TotalCount => Items.Count;
    public int ProgressPercent => TotalCount > 0 ? (int)Math.Round((double)CompletedCount / TotalCount * 100) : 0;
}
