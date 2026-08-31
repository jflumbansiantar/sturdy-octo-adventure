namespace PortfolioOS.Mobile.Models;

public record LoginResponse(string Token, DateTimeOffset ExpiresAt);

public record HoldingModel(
    Guid Id, string Ticker, string Name, string Type, string SubType, string Market,
    decimal Shares, decimal AvgCost, decimal CurrentPrice, decimal PreviousClose,
    string? PriceCurrency, decimal CostBasis, decimal MarketValue,
    decimal GainLoss, decimal GainLossPct,
    decimal DayChange, decimal DayChangePct, decimal DayGainLoss,
    DateTimeOffset? PriceUpdatedAt);

public record TransactionModel(
    Guid Id, DateOnly Date, string Category, string Name, string Type,
    decimal Total, string? Market, decimal? Shares, decimal? Price, DateTimeOffset CreatedAt);

public record PortfolioSummaryModel(
    decimal TotalValue, decimal TotalCostBasis, decimal TotalGainLoss,
    decimal TotalGainLossPct, decimal TodayGainLoss, int HoldingCount,
    List<HoldingModel> Holdings,
    string BaseCurrency, decimal UsdIdrRate, bool IsRateLive);

public record DebtModel(
    Guid Id, string Name, string Type, decimal Balance, decimal InterestRate,
    decimal? MonthlyInterestRate, int? Tenor, decimal MinimumPayment, int DueDay,
    string Currency, string DebtApp, string Notes, string Status,
    decimal TotalPaid, int MonthsPaid, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
