namespace PortfolioOS.Application.Transactions;

public record TransactionDto(
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
