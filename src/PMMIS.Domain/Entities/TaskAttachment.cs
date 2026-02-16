using System.ComponentModel.DataAnnotations;

namespace PMMIS.Domain.Entities;

/// <summary>
/// Вложение к задаче
/// </summary>
public class TaskAttachment : BaseEntity
{
    public int TaskId { get; set; }
    public ProjectTask? Task { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;
    
    [MaxLength(255)]
    public string? OriginalFileName { get; set; }  // Исходное имя файла
    
    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;
    
    public long FileSize { get; set; }
    
    [MaxLength(100)]
    public string? ContentType { get; set; }
    
    [Required]
    public string UploadedById { get; set; } = string.Empty;
    public ApplicationUser? UploadedBy { get; set; }
    
    public string? Description { get; set; }
    
    // Вычисляемые свойства
    public string FileSizeFormatted => FileSize switch
    {
        < 1024 => $"{FileSize} B",
        < 1024 * 1024 => $"{FileSize / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{FileSize / (1024.0 * 1024):F1} MB",
        _ => $"{FileSize / (1024.0 * 1024 * 1024):F1} GB"
    };
    
    public bool IsImage => ContentType?.StartsWith("image/") == true;
    public bool IsPdf => ContentType == "application/pdf";
}
