using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Common.Interfaces;

namespace PortfolioOS.Application.Holdings.Queries.GetHoldings;

public class GetHoldingsHandler : IRequestHandler<GetHoldingsQuery, List<HoldingDto>>
{
    private readonly IApplicationDbContext _context;

    public GetHoldingsHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<HoldingDto>> Handle(GetHoldingsQuery request, CancellationToken ct)
    {
        var holdings = await _context.Holdings.AsNoTracking().ToListAsync(ct);

        var tickers = holdings.Select(h => h.Ticker).ToHashSet();
        var priceMap = await _context.PriceCaches
            .AsNoTracking()
            .Where(p => tickers.Contains(p.Ticker))
            .ToDictionaryAsync(p => p.Ticker, ct);

        return holdings.Select(h =>
        {
            priceMap.TryGetValue(h.Ticker, out var pc);
            var currentPrice   = pc?.CurrentPrice ?? 0m;
            var previousClose  = pc?.PreviousClose ?? 0m;
            var costBasis      = h.Shares * h.AvgCost;
            var marketValue    = h.Shares * currentPrice;
            var gainLoss       = marketValue - costBasis;
            var gainLossPct    = h.AvgCost == 0 ? 0 : Math.Round((currentPrice - h.AvgCost) / h.AvgCost * 100, 2);
            var dayChange      = currentPrice - previousClose;
            var dayChangePct   = previousClose == 0 ? 0 : Math.Round(dayChange / previousClose * 100, 2);
            var dayGainLoss    = h.Shares * dayChange;

            return new HoldingDto(
                h.Id, h.Ticker, h.Name,
                h.Type.ToString(), h.SubType, h.Market.ToString(),
                h.Shares, h.AvgCost,
                currentPrice, previousClose,
                pc?.Currency.ToString(),
                costBasis, marketValue, gainLoss, gainLossPct,
                dayChange, dayChangePct, dayGainLoss,
                pc?.UpdatedAt);
        }).ToList();
    }
}
