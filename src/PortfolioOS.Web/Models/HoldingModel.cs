namespace PortfolioOS.Web.Models;

public record HoldingModel(
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
