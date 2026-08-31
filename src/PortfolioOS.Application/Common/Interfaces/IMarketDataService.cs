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

    /// <summary>
    /// Spot price of one USD in IDR. Null when the provider cannot be reached, which callers
    /// must handle rather than defaulting to 1 - a rate of 1 would silently understate every
    /// dollar holding by four orders of magnitude.
    /// </summary>
    Task<decimal?> GetUsdIdrRateAsync(CancellationToken ct = default);
}
