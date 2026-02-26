using System.ComponentModel.DataAnnotations;

namespace PMMIS.Domain.Entities;

/// <summary>
/// Потенциальный участник тендера (компания, запросившая тендерную документацию)
/// </summary>
public class TenderApplicant : BaseEntity
{
    /// <summary>
    /// Тендер
    /// </summary>
    public int TenderId { get; set; }
    public Tender Tender { get; set; } = null!;
    
    /// <summary>
    /// Наименование компании
    /// </summary>
    [MaxLength(500)]
    public string CompanyName { get; set; } = string.Empty;
    
    /// <summary>
    /// Тип компании (ООО, ГУП, Joint Venture, Consortium и т.д.)
    /// </summary>
    [MaxLength(200)]
    public string? CompanyType { get; set; }
    
    /// <summary>
    /// Электронная почта
    /// </summary>
    [MaxLength(300)]
    public string? Email { get; set; }
    
    /// <summary>
    /// Номер телефона
    /// </summary>
    [MaxLength(100)]
    public string? Phone { get; set; }
    
    /// <summary>
    /// Иностранная компания?
    /// </summary>
    public bool IsForeign { get; set; }
    
    /// <summary>
    /// Страна 1 (для иностранных компаний)
    /// </summary>
    [MaxLength(200)]
    public string? Country1 { get; set; }
    
    /// <summary>
    /// Страна 2 (для консорциума)
    /// </summary>
    [MaxLength(200)]
    public string? Country2 { get; set; }
    
    /// <summary>
    /// Подал документы → участник тендера
    /// </summary>
    public bool IsParticipant { get; set; }
    
    /// <summary>
    /// Победитель тендера
    /// </summary>
    public bool IsWinner { get; set; }
    
    /// <summary>
    /// Путь к оценочному документу (для победителя)
    /// </summary>
    [MaxLength(500)]
    public string? EvaluationDocPath { get; set; }
    
    /// <summary>
    /// Дата добавления
    /// </summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
