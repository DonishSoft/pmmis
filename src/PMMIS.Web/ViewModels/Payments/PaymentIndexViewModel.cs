using PMMIS.Domain.Entities;

namespace PMMIS.Web.ViewModels.Payments;

/// <summary>
/// ViewModel для списка платежей с фильтрацией и суммами
/// </summary>
public class PaymentIndexViewModel
{
    public IEnumerable<Payment> Payments { get; set; } = [];
    public IEnumerable<Contract> Contracts { get; set; } = [];
    public int? SelectedContractId { get; set; }
    public PaymentStatus? SelectedStatus { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal PendingAmount { get; set; }
}
