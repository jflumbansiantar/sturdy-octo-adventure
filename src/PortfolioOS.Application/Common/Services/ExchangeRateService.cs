using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Domain.Entities;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Common.Services;

/// <summary>
/// Reads the USD-IDR rate from Yahoo and keeps it in <c>price_caches</c> alongside the
/// instrument prices, so it survives restarts and stays available when the network does not.
/// </summary>
public class ExchangeRateService : IExchangeRateService
{
    /// <summary>
    /// The rate is stored as an ordinary price row. The "=X" suffix is Yahoo's own FX
    /// convention and cannot collide with a holding ticker.
    /// </summary>
    public const string CacheTicker = "USDIDR=X";

    /// <summary>
    /// FX moves slowly enough that refetching on every request would only add latency, and
    /// slowly enough that a rate from this morning is still a fair basis for a total.
    /// </summary>
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(12);

    /// <summary>
    /// Last resort for a first run with no network and no cached row. Deliberately a round
    /// number: it is reported with IsLive = false so the UI can say the total is approximate.
    /// </summary>
    private const decimal FallbackRate = 16_000m;

    private readonly IApplicationDbContext _context;
    private readonly IMarketDataService _marketData;

    public ExchangeRateService(IApplicationDbContext context, IMarketDataService marketData)
    {
        _context    = context;
        _marketData = marketData;
    }

    public async Task<ExchangeRate> GetUsdIdrAsync(CancellationToken ct = default)
    {
        var cached = await _context.PriceCaches
            .FirstOrDefaultAsync(p => p.Ticker == CacheTicker, ct);

        var isFresh = cached is not null
            && cached.CurrentPrice > 0
            && DateTimeOffset.UtcNow - cached.UpdatedAt < MaxAge;

        if (isFresh)
            return new ExchangeRate(cached!.CurrentPrice, cached.UpdatedAt, IsLive: true);

        var live = await _marketData.GetUsdIdrRateAsync(ct);

        if (live is > 0)
        {
            var now = DateTimeOffset.UtcNow;

            if (cached is null)
            {
                _context.PriceCaches.Add(new PriceCache
                {
                    Ticker        = CacheTicker,
                    Currency      = CurrencyType.IDR,
                    CurrentPrice  = live.Value,
                    PreviousClose = live.Value,
                    UpdatedAt     = now
                });
            }
            else
            {
                cached.PreviousClose = cached.CurrentPrice;
                cached.CurrentPrice  = live.Value;
                cached.UpdatedAt     = now;
            }

            await _context.SaveChangesAsync(ct);
            return new ExchangeRate(live.Value, now, IsLive: true);
        }

        // Provider unreachable. A stale rate still beats adding dollars to rupiah, so it is
        // used - but reported as not live.
        return cached is not null && cached.CurrentPrice > 0
            ? new ExchangeRate(cached.CurrentPrice, cached.UpdatedAt, IsLive: false)
            : new ExchangeRate(FallbackRate, DateTimeOffset.MinValue, IsLive: false);
    }
}
