namespace PortfolioOS.Application.Common.Interfaces;

public record QuoteResult(
    string Ticker,
    decimal CurrentPrice,
    decimal PreviousClose,
    string Currency);

public interface IMarketDataService
{
    Task<IReadOnlyList<QuoteResult>> GetQuotesAsync(IEnumerable<string> tickers, CancellationToken ct = default);
}
