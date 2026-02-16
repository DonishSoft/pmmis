using Microsoft.AspNetCore.Mvc.Rendering;
using PMMIS.Domain.Entities;

namespace PMMIS.Web.ViewModels.WorkProgress;

/// <summary>
/// ViewModel for WorkProgress Create/Edit forms
/// </summary>
public class WorkProgressFormViewModel
{
    public Domain.Entities.WorkProgress WorkProgress { get; set; } = new();
    
    /// <summary>
    /// Dropdown for contracts
    /// </summary>
    public SelectList? Contracts { get; set; }
    
    /// <summary>
    /// Indicators associated with the selected contract
    /// </summary>
    public List<ContractIndicatorInput> ContractIndicators { get; set; } = new();
    
    /// <summary>
    /// PDF report files to upload
    /// </summary>
    public List<IFormFile>? ReportFiles { get; set; }
    
    /// <summary>
    /// Photo evidence files to upload
    /// </summary>
    public List<IFormFile>? PhotoFiles { get; set; }
    
    /// <summary>
    /// Existing documents (for Edit view)
    /// </summary>
    public List<Document>? ExistingDocuments { get; set; }
}

/// <summary>
/// Input model for indicator progress in AVR
/// </summary>
public class ContractIndicatorInput
{
    public int ContractIndicatorId { get; set; }
    
    /// <summary>
    /// Indicator details for display
    /// </summary>
    public int IndicatorId { get; set; }
    public string IndicatorCode { get; set; } = "";
    public string IndicatorName { get; set; } = "";
    public string? Unit { get; set; }
    
    /// <summary>
    /// Target value for the indicator in this contract
    /// </summary>
    public decimal TargetValue { get; set; }
    
    /// <summary>
    /// Previous achieved value (sum of all previous progress)
    /// </summary>
    public decimal PreviousAchieved { get; set; }
    
    /// <summary>
    /// Value to add in this AVR
    /// </summary>
    public decimal Value { get; set; }
    
    /// <summary>
    /// Notes for this progress entry
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// Geographic data source type (determines checklist content)
    /// </summary>
    public GeoDataSource GeoDataSource { get; set; } = GeoDataSource.None;
    
    /// <summary>
    /// Geographic checklist items (villages, schools, health facilities)
    /// </summary>
    public List<GeoChecklistItem> GeoItems { get; set; } = new();
    
    /// <summary>
    /// Calculate new total after this entry
    /// </summary>
    public decimal NewTotal => PreviousAchieved + Value;
    
    /// <summary>
    /// Progress percentage after this entry
    /// </summary>
    public decimal ProgressPercent => TargetValue > 0 ? Math.Min(100, (NewTotal / TargetValue) * 100) : 0;
    
    /// <summary>
    /// Whether this indicator uses geo checklist instead of numeric input
    /// </summary>
    public bool HasGeoChecklist => GeoDataSource != GeoDataSource.None && GeoItems.Any();
}

/// <summary>
/// Item in geographic checklist for indicator progress
/// </summary>
public class GeoChecklistItem
{
    /// <summary>
    /// Unique item ID (VillageId, SchoolId, or HealthFacilityId)
    /// </summary>
    public int ItemId { get; set; }
    
    /// <summary>
    /// Type of item
    /// </summary>
    public GeoItemType ItemType { get; set; }
    
    /// <summary>
    /// Display name
    /// </summary>
    public string Name { get; set; } = "";
    
    /// <summary>
    /// Additional info (population, student count, etc.)
    /// </summary>
    public string Description { get; set; } = "";
    
    /// <summary>
    /// Whether this item was checked
    /// </summary>
    public bool IsChecked { get; set; }
    
    /// <summary>
    /// Numeric value for this item (e.g. population)
    /// </summary>
    public decimal NumericValue { get; set; }
}

