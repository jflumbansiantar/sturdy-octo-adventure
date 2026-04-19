namespace PortfolioOS.Web.Models;

public record TransactionModel(
    Guid Id,
    DateOnly Date,
    string Category,
    string Name,
    string Type,
    decimal Total,
    string? Market,
    decimal? Shares,
    decimal? Price,
    DateTimeOffset CreatedAt);
