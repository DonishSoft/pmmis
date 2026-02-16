namespace PMMIS.Domain.Entities;

/// <summary>
/// Контракт
/// </summary>
public class Contract : BaseEntity
{
    public string ContractNumber { get; set; } = string.Empty;
    public string ScopeOfWork { get; set; } = string.Empty;
    public string? ScopeOfWorkTj { get; set; }
    public string? ScopeOfWorkEn { get; set; }
    
    public ContractType Type { get; set; }
    
    // Dates
    public DateTime SigningDate { get; set; }
    public DateTime ContractEndDate { get; set; }
    public DateTime? ExtendedToDate { get; set; }
    
    // Currency
    public ContractCurrency Currency { get; set; } = ContractCurrency.USD;
    
    // Financials (in USD)
    public decimal ContractAmount { get; set; }
    public decimal AdditionalAmount { get; set; }
    public decimal SavedAmount { get; set; }
    public decimal FinalAmount => ContractAmount + AdditionalAmount - SavedAmount;
    
    // TJS fields (when Currency = TJS)
    public decimal? AmountTjs { get; set; }
    public decimal? ExchangeRate { get; set; }
    
    // Progress
    public decimal WorkCompletedPercent { get; set; }
    public int RemainingDays => (ExtendedToDate ?? ContractEndDate).Subtract(DateTime.Today).Days;
    
    // Foreign Keys
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    
    public int? SubComponentId { get; set; }
    public SubComponent? SubComponent { get; set; }
    
    public int ContractorId { get; set; }
    public Contractor Contractor { get; set; } = null!;
    
    /// <summary>
    /// Связь с позицией плана закупок
    /// </summary>
    public int? ProcurementPlanId { get; set; }
    public ProcurementPlan? ProcurementPlan { get; set; }
    
    // Ответственные лица
    public string? CuratorId { get; set; }
    public ApplicationUser? Curator { get; set; }
    
    public string? ProjectManagerId { get; set; }
    public ApplicationUser? ProjectManager { get; set; }
    
    // Navigation
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<WorkProgress> WorkProgresses { get; set; } = new List<WorkProgress>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<ContractIndicator> ContractIndicators { get; set; } = new List<ContractIndicator>();
    public ICollection<ContractMilestone> Milestones { get; set; } = new List<ContractMilestone>();
    
    // Calculated properties
    public decimal PaidAmount => Payments.Where(p => p.Status == PaymentStatus.Paid).Sum(p => p.Amount);
    public decimal PaidPercent => FinalAmount > 0 ? PaidAmount / FinalAmount * 100 : 0;
    public decimal RemainingAmount => FinalAmount - PaidAmount;
}

public enum ContractType
{
    Works,           // Строительные работы
    Consulting,      // Консультационные услуги
    Goods            // Товары
}

public enum ContractCurrency
{
    USD,             // Доллар США
    TJS              // Сомони
}

/// <summary>
/// Подрядчик / Исполнитель
/// </summary>
public class Contractor : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    
    // Navigation
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    
    // User account for contractor portal
    public string? UserId { get; set; }
}
