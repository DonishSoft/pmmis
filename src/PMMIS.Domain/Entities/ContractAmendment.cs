namespace PMMIS.Domain.Entities;

/// <summary>
/// Поправка к контракту (дополнительное соглашение)
/// </summary>
public class ContractAmendment : BaseEntity
{
    public AmendmentType Type { get; set; }
    public DateTime AmendmentDate { get; set; }
    public string? Description { get; set; }
    
    // === Amount Change fields (Type 1 & 3) ===
    /// <summary>Сумма изменения в сомони (TJS)</summary>
    public decimal? AmountChangeTjs { get; set; }
    /// <summary>Курс USD/TJS на дату поправки</summary>
    public decimal? ExchangeRate { get; set; }
    /// <summary>Сумма изменения в USD (рассчитывается из TJS / Rate)</summary>
    public decimal? AmountChangeUsd { get; set; }
    
    // === Deadline Extension fields (Type 2 & 3) ===
    /// <summary>Предыдущая дата окончания (для отката)</summary>
    public DateTime? PreviousEndDate { get; set; }
    /// <summary>Новая дата окончания контракта</summary>
    public DateTime? NewEndDate { get; set; }
    
    // === Scope Change fields (Type 3) ===
    /// <summary>Новый объём работ (если изменяется)</summary>
    public string? NewScopeOfWork { get; set; }
    
    // Foreign Keys
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    
    // Audit
    public string? CreatedByUserId { get; set; }
    public ApplicationUser? CreatedBy { get; set; }
    
    // Navigation
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}

/// <summary>
/// Тип поправки к контракту
/// </summary>
public enum AmendmentType
{
    /// <summary>Изменение суммы контракта</summary>
    AmountChange = 0,
    /// <summary>Продление срока контракта</summary>
    DeadlineExtension = 1,
    /// <summary>Изменение объёма предоставляемых услуг</summary>
    ScopeChange = 2
}
