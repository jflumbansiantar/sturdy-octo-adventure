namespace PortfolioOS.Application.Common.Interfaces;

/// <summary>
/// How many rupiah one US dollar buys, and how much that figure can be trusted.
/// </summary>
/// <param name="UsdIdr">Rupiah per USD.</param>
/// <param name="AsOf">When the rate was last fetched from the provider.</param>
/// <param name="IsLive">
/// False when the provider could not be reached and a cached or fallback rate is being used.
/// Callers surface this so a portfolio total is never silently priced off a stale rate.
/// </param>
public record ExchangeRate(decimal UsdIdr, DateTimeOffset AsOf, bool IsLive);

/// <summary>
/// Supplies the USD-IDR rate used to express the whole portfolio in one currency.
/// Holdings are priced in the currency of their exchange - rupiah for IDX, dollars for US
/// listings and crypto - so any total that spans both markets is meaningless without this.
/// </summary>
public interface IExchangeRateService
{
    Task<ExchangeRate> GetUsdIdrAsync(CancellationToken ct = default);
}
