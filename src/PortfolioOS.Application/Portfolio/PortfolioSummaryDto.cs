using PortfolioOS.Application.Holdings;

namespace PortfolioOS.Application.Portfolio;

public record PortfolioSummaryDto(
    decimal TotalValue,
    decimal TotalCostBasis,
    decimal TotalGainLoss,
    decimal TotalGainLossPct,
    decimal TodayGainLoss,
    int HoldingCount,
    IReadOnlyList<HoldingDto> Holdings);
