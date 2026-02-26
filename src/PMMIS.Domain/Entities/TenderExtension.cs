using System.ComponentModel.DataAnnotations;

namespace PMMIS.Domain.Entities;

/// <summary>
/// Продление срока тендера (история)
/// </summary>
public class TenderExtension : BaseEntity
{
    /// <summary>
    /// Тендер
    /// </summary>
    public int TenderId { get; set; }
    public Tender Tender { get; set; } = null!;
    
    /// <summary>
    /// Предыдущая дата окончания
    /// </summary>
    public DateTime PreviousEndDate { get; set; }
    
    /// <summary>
    /// Новая дата окончания
    /// </summary>
    public DateTime NewEndDate { get; set; }
    
    /// <summary>
    /// Причина продления
    /// </summary>
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
    
    /// <summary>
    /// Дата продления
    /// </summary>
    public DateTime ExtendedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Кто продлил
    /// </summary>
    [MaxLength(200)]
    public string? ExtendedBy { get; set; }
}
