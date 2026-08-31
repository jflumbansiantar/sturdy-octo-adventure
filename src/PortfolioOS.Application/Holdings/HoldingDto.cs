namespace PortfolioOS.Application.Holdings;

/// <summary>
/// A holding enriched with its latest price.
///
/// Two currencies meet in this record, so the split matters. Per-share figures
/// (<see cref="AvgCost"/>, <see cref="CurrentPrice"/>, <see cref="PreviousClose"/>,
/// <see cref="DayChange"/>) are quoted in <see cref="PriceCurrency"/> - the currency of the
/// exchange the instrument trades on. Everything that can be summed across holdings
/// (<see cref="CostBasis"/>, <see cref="MarketValue"/>, <see cref="GainLoss"/>,
/// <see cref="DayGainLoss"/>) is converted to the portfolio's base currency, IDR.
///
/// That conversion is the point: adding an IDX position in rupiah to a US position in dollars
/// produces a number that is neither, which is exactly the bug this split exists to prevent.
/// Percentages are ratios and need no conversion.
/// </summary>
public record HoldingDto(
    Guid Id,
    string Ticker,
    string Name,
    string Type,
    string SubType,
    string Market,
    decimal Shares,
    decimal AvgCost,
    decimal CurrentPrice,
    decimal PreviousClose,
    string? PriceCurrency,
    decimal CostBasis,
    decimal MarketValue,
    decimal GainLoss,
    decimal GainLossPct,
    decimal DayChange,
    decimal DayChangePct,
    decimal DayGainLoss,
    DateTimeOffset? PriceUpdatedAt);
