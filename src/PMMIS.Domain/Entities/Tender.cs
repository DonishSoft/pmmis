using System.ComponentModel.DataAnnotations;

namespace PMMIS.Domain.Entities;

/// <summary>
/// Статус тендера
/// </summary>
public enum TenderStatus
{
    Open,       // Открыт
    Extended,   // Продлён
    Closed,     // Закрыт
    Cancelled   // Отменён
}

/// <summary>
/// Тендер — объявление о закупке
/// </summary>
public class Tender : BaseEntity
{
    /// <summary>
    /// Позиция плана закупок
    /// </summary>
    public int ProcurementPlanId { get; set; }
    public ProcurementPlan ProcurementPlan { get; set; } = null!;
    
    /// <summary>
    /// Дата начала тендера
    /// </summary>
    public DateTime StartDate { get; set; }
    
    /// <summary>
    /// Дата окончания тендера (текущая, с учётом продлений)
    /// </summary>
    public DateTime EndDate { get; set; }
    
    /// <summary>
    /// Статус тендера
    /// </summary>
    public TenderStatus Status { get; set; } = TenderStatus.Open;
    
    /// <summary>
    /// Примечания
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// Дата создания записи
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Кто создал
    /// </summary>
    [MaxLength(200)]
    public string? CreatedBy { get; set; }
    
    // Navigation properties
    public ICollection<TenderExtension> Extensions { get; set; } = new List<TenderExtension>();
    public ICollection<TenderApplicant> Applicants { get; set; } = new List<TenderApplicant>();
}
