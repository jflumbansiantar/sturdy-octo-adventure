namespace PortfolioOS.Web.Models;

public record QuoteModel(
    string Ticker,
    decimal CurrentPrice,
    decimal PreviousClose,
    decimal DayChange,
    decimal DayChangePct,
    string Currency,
    DateTimeOffset UpdatedAt);
