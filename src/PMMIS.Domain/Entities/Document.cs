namespace PMMIS.Domain.Entities;

/// <summary>
/// Документ (контракт, акт, фото и т.д.)
/// </summary>
public class Document : BaseEntity
{
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public DocumentType Type { get; set; }
    public string? Description { get; set; }
    
    // Foreign Keys (polymorphic - can belong to Contract or WorkProgress)
    public int? ContractId { get; set; }
    public Contract? Contract { get; set; }
    
    public int? WorkProgressId { get; set; }
    public WorkProgress? WorkProgress { get; set; }
    
    public int? PaymentId { get; set; }
    public Payment? Payment { get; set; }
    
    public int? ContractorId { get; set; }
    public Contractor? Contractor { get; set; }
    
    // Uploaded by
    public string? UploadedByUserId { get; set; }
}

public enum DocumentType
{
    Contract,            // Контракт
    Amendment,           // Дополнение к контракту
    WorkAct,             // Акт выполненных работ
    Invoice,             // Счёт-фактура
    Photo,               // Фото прогресса
    Report,              // Отчёт
    Other,               // Прочее
    TenderDocument,      // Тендерная документация
    CompanyRegistration, // Свидетельство регистрации
    CompanyLicense,      // Лицензия
    TaxCertificate,      // Налоговый сертификат
    BankDetails,         // Банковские реквизиты
    Insurance,           // Страховка
    SignedContract,      // Подписанный контракт
    MandatoryDocument,   // Обязательные документы
    AdditionalDocument   // Дополнительные документы
}
