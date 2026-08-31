using PortfolioOS.Application.Holdings;

namespace PortfolioOS.Application.Portfolio;

/// <summary>
/// Portfolio totals. Every monetary field is in <see cref="BaseCurrency"/>, converted from
/// the holdings' native currencies at <see cref="UsdIdrRate"/>.
/// </summary>
/// <param name="UsdIdrRate">Rupiah per USD used for the conversion.</param>
/// <param name="IsRateLive">
/// False when the rate came from cache or a fallback because the provider was unreachable -
/// the totals are then approximate, and the UI should say so.
/// </param>
public record PortfolioSummaryDto(
    decimal TotalValue,
    decimal TotalCostBasis,
    decimal TotalGainLoss,
    decimal TotalGainLossPct,
    decimal TodayGainLoss,
    int HoldingCount,
    IReadOnlyList<HoldingDto> Holdings,
    string BaseCurrency,
    decimal UsdIdrRate,
    bool IsRateLive);
