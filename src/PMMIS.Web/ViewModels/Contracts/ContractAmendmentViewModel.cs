using Microsoft.AspNetCore.Http;

namespace PMMIS.Web.ViewModels.Contracts;

/// <summary>
/// ViewModel для создания поправки к контракту
/// </summary>
public class ContractAmendmentViewModel
{
    public int ContractId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    
    /// <summary>Тип поправки (0=Сумма, 1=Срок, 2=Объём)</summary>
    public int Type { get; set; }
    
    public DateTime AmendmentDate { get; set; } = DateTime.Today;
    public string? Description { get; set; }
    
    // === Amount Change (Type 0 & 2) ===
    public decimal? AmountChangeTjs { get; set; }
    public decimal? ExchangeRate { get; set; }
    public decimal? AmountChangeUsd { get; set; }
    
    // === Deadline Extension (Type 1 & 2) ===
    public DateTime? NewEndDate { get; set; }
    
    // === Scope Change (Type 2) ===
    public string? NewScopeOfWork { get; set; }
    
    // === Documents ===
    /// <summary>Дополнительное соглашение (обязательно)</summary>
    public IFormFile? AgreementFile { get; set; }
    public string? AgreementName { get; set; }
    
    /// <summary>Дополнительные документы</summary>
    public List<IFormFile>? AdditionalFiles { get; set; }
    public List<string>? AdditionalNames { get; set; }
}
