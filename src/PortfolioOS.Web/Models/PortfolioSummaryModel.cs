namespace PortfolioOS.Web.Models;

public record PortfolioSummaryModel(
    decimal TotalValue,
    decimal TotalCostBasis,
    decimal TotalGainLoss,
    decimal TotalGainLossPct,
    decimal TodayGainLoss,
    int HoldingCount,
    IReadOnlyList<HoldingModel> Holdings,
    string BaseCurrency,
    decimal UsdIdrRate,
    bool IsRateLive);
