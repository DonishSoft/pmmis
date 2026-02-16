namespace PMMIS.Domain.Entities;

/// <summary>
/// Проект (WSIP-1, AF#2 и т.д.)
/// </summary>
public class Project : LocalizedEntity
{
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal TotalBudget { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    
    // Navigation
    public ICollection<Component> Components { get; set; } = new List<Component>();
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}

public enum ProjectStatus
{
    Planning,
    Active,
    OnHold,
    Completed,
    Cancelled
}

/// <summary>
/// Компонент проекта (Component 1, 2, 3)
/// </summary>
public class Component : LocalizedEntity
{
    public int Number { get; set; }
    public string? Description { get; set; }
    public decimal AllocatedBudget { get; set; }
    
    // Foreign Keys
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    
    // Navigation
    public ICollection<SubComponent> SubComponents { get; set; } = new List<SubComponent>();
}

/// <summary>
/// Подкомпонент (1.1, 1.2, 2.1, 2.2)
/// </summary>
public class SubComponent : LocalizedEntity
{
    public string Code { get; set; } = string.Empty; // e.g., "1.1", "2.2"
    public string? Description { get; set; }
    public decimal AllocatedBudget { get; set; }
    
    // Foreign Keys
    public int ComponentId { get; set; }
    public Component Component { get; set; } = null!;
    
    // Navigation
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}
