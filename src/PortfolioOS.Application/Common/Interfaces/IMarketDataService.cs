using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Common.Interfaces;

/// <summary>What to look up. Market and Type decide the provider's symbol suffix.</summary>
public record QuoteRequest(string Ticker, Market Market, HoldingType Type);

public record QuoteResult(
    string Ticker,
    decimal CurrentPrice,
    decimal PreviousClose,
    string Currency);

public interface IMarketDataService
{
    Task<IReadOnlyList<QuoteResult>> GetQuotesAsync(IEnumerable<QuoteRequest> requests, CancellationToken ct = default);
}
