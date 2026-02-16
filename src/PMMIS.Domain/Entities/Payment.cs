namespace PMMIS.Domain.Entities;

/// <summary>
/// Платёж по контракту
/// </summary>
public class Payment : BaseEntity
{
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    
    // TJS fields (when contract is in TJS)
    public decimal? AmountTjs { get; set; }
    public decimal? ExchangeRate { get; set; }
    
    public PaymentType Type { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? Description { get; set; }
    public string? InvoiceNumber { get; set; }
    
    // Approval workflow
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedById { get; set; }
    public ApplicationUser? ApprovedBy { get; set; }
    
    // Rejection
    public string? RejectionReason { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectedById { get; set; }
    public ApplicationUser? RejectedBy { get; set; }
    
    // Foreign Keys
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    
    // Link to AVR (Work Progress)
    public int? WorkProgressId { get; set; }
    public WorkProgress? WorkProgress { get; set; }
    
    // Document reference
    public int? DocumentId { get; set; }
    public Document? Document { get; set; }
}

public enum PaymentType
{
    Advance,         // Аванс
    Interim,         // Промежуточный      
    Final,           // Окончательный
    Retention        // Удержание
}

public enum PaymentStatus
{
    Pending,         // Ожидает
    Approved,        // Одобрен
    Paid,            // Оплачен
    Rejected         // Отклонён
}

/// <summary>
/// Прогресс выполнения работ
/// </summary>
public class WorkProgress : BaseEntity
{
    public DateTime ReportDate { get; set; }
    public decimal CompletedPercent { get; set; }
    public string? Description { get; set; }
    public string? Issues { get; set; }
    
    // Foreign Keys
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    
    // Submitted by contractor
    public string? SubmittedByUserId { get; set; }
    
    // Approval Workflow
    public AvrApprovalStatus ApprovalStatus { get; set; } = AvrApprovalStatus.Draft;
    
    // Curator submission
    public DateTime? SubmittedAt { get; set; }
    
    // Manager review
    public DateTime? ManagerReviewedAt { get; set; }
    public string? ManagerReviewedById { get; set; }
    public ApplicationUser? ManagerReviewedBy { get; set; }
    public string? ManagerComment { get; set; }
    
    // Director approval
    public DateTime? DirectorApprovedAt { get; set; }
    public string? DirectorApprovedById { get; set; }
    public ApplicationUser? DirectorApprovedBy { get; set; }
    public string? DirectorComment { get; set; }
    
    // Rejection
    public string? RejectionReason { get; set; }
    
    // Navigation
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<ContractIndicatorProgress> IndicatorProgresses { get; set; } = new List<ContractIndicatorProgress>();
}

/// <summary>
/// Статус утверждения АВР
/// </summary>
public enum AvrApprovalStatus
{
    Draft = 0,              // Черновик (Куратор редактирует)
    SubmittedForReview = 1, // Отправлен на проверку Менеджеру
    ManagerApproved = 2,    // Одобрен Менеджером, ждёт Директора
    DirectorApproved = 3,   // Утверждён Директором
    Rejected = 4            // Отклонён (с комментарием)
}

