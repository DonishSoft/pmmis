using System.ComponentModel.DataAnnotations;

namespace PMMIS.Domain.Entities;

/// <summary>
/// Комментарий к задаче
/// </summary>
public class TaskComment : BaseEntity
{
    public int TaskId { get; set; }
    public ProjectTask? Task { get; set; }
    
    [Required]
    public string AuthorId { get; set; } = string.Empty;
    public ApplicationUser? Author { get; set; }
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// Системные комментарии генерируются автоматически при изменениях
    /// </summary>
    public bool IsSystemGenerated { get; set; }
    
    /// <summary>
    /// ID родительского комментария для вложенных ответов
    /// </summary>
    public int? ParentCommentId { get; set; }
    public TaskComment? ParentComment { get; set; }
    public List<TaskComment> Replies { get; set; } = new();
}
