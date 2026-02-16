namespace PMMIS.Web.ViewModels.Reports;

/// <summary>
/// ViewModel для отчёта по платежам
/// </summary>
public class PaymentReportViewModel
{
    public List<ContractPaymentSummary> Contracts { get; set; } = [];
    
    // Агрегаты
    public decimal TotalPlannedUsd { get; set; }
    public decimal TotalPaidUsd { get; set; }
    public decimal TotalDifference => TotalPlannedUsd - TotalPaidUsd;
    public int TotalContracts { get; set; }
    public int TotalPayments { get; set; }
    public decimal OverallPaidPercent => TotalPlannedUsd > 0 ? TotalPaidUsd / TotalPlannedUsd * 100 : 0;
}

public class ContractPaymentSummary
{
    public int ContractId { get; set; }
    public string ContractNumber { get; set; } = "";
    public string ContractorName { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string CurrencyLabel { get; set; } = "USD";
    public DateTime SigningDate { get; set; }
    
    /// <summary>
    /// Плановая сумма контракта (USD)
    /// </summary>
    public decimal PlannedUsd { get; set; }
    
    /// <summary>
    /// Плановая сумма контракта (TJS, если контракт в сомони)
    /// </summary>
    public decimal? PlannedTjs { get; set; }
    
    /// <summary>
    /// Курс на дату подписания
    /// </summary>
    public decimal? ExchangeRate { get; set; }
    
    /// <summary>
    /// Фактически оплачено (USD)
    /// </summary>
    public decimal PaidUsd { get; set; }
    
    /// <summary>
    /// Фактически оплачено (TJS, для сомони-контрактов)
    /// </summary>
    public decimal? PaidTjs { get; set; }
    
    /// <summary>
    /// Сумма оплат в USD по курсу контракта (TJS / contract rate)
    /// </summary>
    public decimal? PaidUsdAtContractRate { get; set; }
    
    /// <summary>
    /// Сумма оплат в USD по курсам на дату оплат (TJS / payment rate)
    /// </summary>
    public decimal? PaidUsdAtPaymentRate { get; set; }
    
    /// <summary>
    /// Курсовая разница = PaidUsdAtPaymentRate - PaidUsdAtContractRate
    /// Положительное = курс выгоднее (больше USD за те же TJS)
    /// </summary>
    public decimal? ExchangeGainLoss { get; set; }
    
    /// <summary>
    /// Разница = Плановая - Оплачено
    /// </summary>
    public decimal Difference => PlannedUsd - PaidUsd;
    
    /// <summary>
    /// Процент оплаты
    /// </summary>
    public decimal PaidPercent => PlannedUsd > 0 ? PaidUsd / PlannedUsd * 100 : 0;
    
    /// <summary>
    /// Платежи контракта
    /// </summary>
    public List<PaymentRow> Payments { get; set; } = [];
}

public class PaymentRow
{
    public int Id { get; set; }
    public DateTime PaymentDate { get; set; }
    public string TypeLabel { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    public string StatusClass { get; set; } = "";
    public decimal AmountUsd { get; set; }
    public decimal? AmountTjs { get; set; }
    public decimal? ExchangeRate { get; set; }
    
    /// <summary>
    /// Курс на дату подписания контракта
    /// </summary>
    public decimal? ContractExchangeRate { get; set; }
    
    /// <summary>
    /// USD по курсу контракта (AmountTjs / ContractExchangeRate)
    /// </summary>
    public decimal? UsdAtContractRate { get; set; }
    
    /// <summary>
    /// USD по курсу на дату оплаты (AmountTjs / ExchangeRate)
    /// </summary>
    public decimal? UsdAtPaymentRate { get; set; }
    
    /// <summary>
    /// Курсовая разница = UsdAtPaymentRate - UsdAtContractRate
    /// </summary>
    public decimal? ExchangeGainLoss { get; set; }
}
