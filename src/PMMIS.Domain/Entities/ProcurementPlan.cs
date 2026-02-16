namespace PMMIS.Domain.Entities;

/// <summary>
/// Метод закупки
/// </summary>
public enum ProcurementMethod
{
    NCB,                // National Competitive Bidding
    ICB,                // International Competitive Bidding  
    Shopping,           // Shopping (до $100K)
    DirectContracting,  // Прямой контракт
    CQS,                // Consultant Qualification Selection
    QCBS,               // Quality and Cost Based Selection
    LCS,                // Least Cost Selection
    FBS                 // Fixed Budget Selection
}

/// <summary>
/// Тип закупки
/// </summary>
public enum ProcurementType
{
    Goods,                  // Товары
    Works,                  // Работы
    ConsultingServices,     // Консультационные услуги
    NonConsultingServices   // Некон. услуги (Training, etc.)
}

/// <summary>
/// Статус закупки
/// </summary>
public enum ProcurementStatus
{
    Planned,            // Запланировано
    InProgress,         // В процессе (тендер открыт)
    Evaluation,         // Оценка предложений
    Awarded,            // Контракт присуждён
    Completed,          // Завершено
    Cancelled           // Отменено
}

/// <summary>
/// Позиция плана закупок
/// </summary>
public class ProcurementPlan : BaseEntity
{
    /// <summary>
    /// Референс номер IDA-WSIP-AF3/2024/001
    /// </summary>
    public string ReferenceNo { get; set; } = string.Empty;
    
    /// <summary>
    /// Описание закупки
    /// </summary>
    public string Description { get; set; } = string.Empty;
    public string? DescriptionTj { get; set; }
    public string? DescriptionEn { get; set; }
    
    /// <summary>
    /// Метод закупки (NCB, ICB, Shopping, etc.)
    /// </summary>
    public ProcurementMethod Method { get; set; } = ProcurementMethod.NCB;
    
    /// <summary>
    /// Тип закупки (Goods, Works, Consulting)
    /// </summary>
    public ProcurementType Type { get; set; } = ProcurementType.Works;
    
    /// <summary>
    /// Оценочная стоимость (USD)
    /// </summary>
    public decimal EstimatedAmount { get; set; }
    
    // Плановые даты
    public DateTime? PlannedBidOpeningDate { get; set; }
    public DateTime? PlannedContractSigningDate { get; set; }
    public DateTime? PlannedCompletionDate { get; set; }
    
    /// <summary>
    /// Дата публикации объявления о тендере
    /// </summary>
    public DateTime? AdvertisementDate { get; set; }
    
    // Фактические даты
    public DateTime? ActualBidOpeningDate { get; set; }
    public DateTime? ActualContractSigningDate { get; set; }
    public DateTime? ActualCompletionDate { get; set; }
    
    /// <summary>
    /// Статус
    /// </summary>
    public ProcurementStatus Status { get; set; } = ProcurementStatus.Planned;
    
    /// <summary>
    /// Комментарии / примечания
    /// </summary>
    public string? Comments { get; set; }
    
    // Foreign Keys
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    
    public int? ComponentId { get; set; }
    public Component? Component { get; set; }
    
    public int? SubComponentId { get; set; }
    public SubComponent? SubComponent { get; set; }
    
    /// <summary>
    /// Связь с контрактом после подписания
    /// </summary>
    public int? ContractId { get; set; }
    public Contract? Contract { get; set; }
}
