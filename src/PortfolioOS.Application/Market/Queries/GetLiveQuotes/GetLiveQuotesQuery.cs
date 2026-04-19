using MediatR;

namespace PortfolioOS.Application.MarketData.Queries.GetLiveQuotes;

public record QuoteDto(
    string Ticker,
    decimal CurrentPrice,
    decimal PreviousClose,
    decimal DayChange,
    decimal DayChangePct,
    string Currency,
    DateTimeOffset UpdatedAt);

public record GetLiveQuotesQuery : IRequest<List<QuoteDto>>;
