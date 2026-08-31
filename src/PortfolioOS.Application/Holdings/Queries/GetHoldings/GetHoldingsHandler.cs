using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Holdings.Queries.GetHoldings;

public class GetHoldingsHandler : IRequestHandler<GetHoldingsQuery, List<HoldingDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IExchangeRateService _exchangeRate;

    public GetHoldingsHandler(IApplicationDbContext context, IExchangeRateService exchangeRate)
    {
        _context      = context;
        _exchangeRate = exchangeRate;
    }

    public async Task<List<HoldingDto>> Handle(GetHoldingsQuery request, CancellationToken ct)
    {
        var holdings = await _context.Holdings.AsNoTracking().ToListAsync(ct);

        var tickers = holdings.Select(h => h.Ticker).ToHashSet();
        var priceMap = await _context.PriceCaches
            .AsNoTracking()
            .Where(p => tickers.Contains(p.Ticker))
            .ToDictionaryAsync(p => p.Ticker, ct);

        // One lookup for the whole list: every dollar-denominated holding is converted with
        // the same rate, so the totals stay internally consistent.
        var rate = await _exchangeRate.GetUsdIdrAsync(ct);

        return holdings.Select(h =>
        {
            priceMap.TryGetValue(h.Ticker, out var pc);
            var currentPrice   = pc?.CurrentPrice ?? 0m;
            var previousClose  = pc?.PreviousClose ?? 0m;

            // With no cached price the exchange still tells us how the instrument is quoted;
            // guessing IDR for a US listing would understate it by four orders of magnitude.
            var priceCurrency  = pc?.Currency ?? (h.Market == Market.US ? CurrencyType.USD : CurrencyType.IDR);
            var toBase         = priceCurrency == CurrencyType.USD ? rate.UsdIdr : 1m;

            var dayChange      = currentPrice - previousClose;
            var gainLossPct    = h.AvgCost == 0 ? 0 : Math.Round((currentPrice - h.AvgCost) / h.AvgCost * 100, 2);
            var dayChangePct   = previousClose == 0 ? 0 : Math.Round(dayChange / previousClose * 100, 2);

            // Prices stay in their native currency; the summable amounts move to base currency.
            var costBasis      = h.Shares * h.AvgCost * toBase;
            var marketValue    = h.Shares * currentPrice * toBase;
            var gainLoss       = marketValue - costBasis;
            var dayGainLoss    = h.Shares * dayChange * toBase;

            return new HoldingDto(
                h.Id, h.Ticker, h.Name,
                h.Type.ToString(), h.SubType, h.Market.ToString(),
                h.Shares, h.AvgCost,
                currentPrice, previousClose,
                priceCurrency.ToString(),
                costBasis, marketValue, gainLoss, gainLossPct,
                dayChange, dayChangePct, dayGainLoss,
                pc?.UpdatedAt);
        }).ToList();
    }
}
