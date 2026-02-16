namespace PMMIS.Web.ViewModels.Reports;

/// <summary>
/// Отчёт по индикаторам
/// </summary>
public class IndicatorReportViewModel
{
    public List<IndicatorReportRow> Indicators { get; set; } = new();
    public int TotalIndicators => Indicators.Count;
    public int CompletedIndicators => Indicators.Count(i => i.AchievedPercent >= 100);
    public int InProgressIndicators => Indicators.Count(i => i.AchievedPercent > 0 && i.AchievedPercent < 100);
    public int NotStartedIndicators => Indicators.Count(i => i.AchievedPercent == 0);
    public decimal OverallProgress => TotalIndicators > 0
        ? Indicators.Average(i => Math.Min(i.AchievedPercent, 100))
        : 0;
}

public class IndicatorReportRow
{
    public int IndicatorId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public string? CategoryName { get; set; }
    
    /// <summary>Суммарный целевой показатель по всем контрактам</summary>
    public decimal TotalTarget { get; set; }
    
    /// <summary>Суммарный достигнутый показатель по всем контрактам</summary>
    public decimal TotalAchieved { get; set; }
    
    public decimal AchievedPercent => TotalTarget > 0 
        ? Math.Round(TotalAchieved / TotalTarget * 100, 1) 
        : 0;
    
    public decimal Remaining => Math.Max(0, TotalTarget - TotalAchieved);
    
    /// <summary>По каким контрактам привязан</summary>
    public List<IndicatorContractDetail> Contracts { get; set; } = new();
}

public class IndicatorContractDetail
{
    public int ContractId { get; set; }
    public string ContractNumber { get; set; } = "";
    public string ContractorName { get; set; } = "";
    public decimal TargetValue { get; set; }
    public decimal AchievedValue { get; set; }
    public decimal Percent => TargetValue > 0 
        ? Math.Round(AchievedValue / TargetValue * 100, 1) 
        : 0;
}
