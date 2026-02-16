using Microsoft.AspNetCore.Mvc.Rendering;
using PMMIS.Domain.Entities;

namespace PMMIS.Web.ViewModels.Payments;

/// <summary>
/// ViewModel для формы создания/редактирования платежа
/// </summary>
public class PaymentFormViewModel
{
    public Payment Payment { get; set; } = new();
    public SelectList? Contracts { get; set; }
    public SelectList? Types { get; set; }
    public SelectList? Statuses { get; set; }
}
