namespace PortfolioOS.Application.Holdings;

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
    string? Currency,
    decimal CostBasis,
    decimal MarketValue,
    decimal GainLoss,
    decimal GainLossPct,
    decimal DayChange,
    decimal DayChangePct,
    decimal DayGainLoss,
    DateTimeOffset? PriceUpdatedAt);
