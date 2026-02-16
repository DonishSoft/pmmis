namespace PMMIS.Domain.Entities;

/// <summary>
/// Статья бюджета PMU
/// </summary>
public class BudgetItem : LocalizedEntity
{
    public int Number { get; set; }
    public decimal AllocatedAmount { get; set; }
    public string? CalculationNotes { get; set; }
    public BudgetCategory Category { get; set; }
    
    // Foreign Keys
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    
    // Navigation
    public ICollection<BudgetExpense> Expenses { get; set; } = new List<BudgetExpense>();
    
    // Calculated
    public decimal SpentAmount => Expenses.Sum(e => e.Amount);
    public decimal RemainingAmount => AllocatedAmount - SpentAmount;
}

public enum BudgetCategory
{
    Salaries,           // Зарплата персонала
    SocialTaxes,        // Социальные налоги
    Travel,             // Командировки
    Utilities,          // Коммунальные услуги
    Fuel,               // ГСМ
    VehicleMaintenance, // Обслуживание транспорта
    Communications,     // Связь и интернет
    OfficeSupplies,     // Офисные расходы
    OfficeRepairs,      // Ремонт офиса
    Banking,            // Банковское обслуживание
    Insurance,          // Страхование
    Expertise,          // Экспертиза
    Other               // Прочее
}

/// <summary>
/// Расход по статье бюджета
/// </summary>
public class BudgetExpense : BaseEntity
{
    public DateTime ExpenseDate { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string? DocumentReference { get; set; }
    
    // Foreign Keys
    public int BudgetItemId { get; set; }
    public BudgetItem BudgetItem { get; set; } = null!;
}
