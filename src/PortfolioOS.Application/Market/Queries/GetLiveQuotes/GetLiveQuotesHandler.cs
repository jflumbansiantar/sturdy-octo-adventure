using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Domain.Entities;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.MarketData.Queries.GetLiveQuotes;

public class GetLiveQuotesHandler : IRequestHandler<GetLiveQuotesQuery, List<QuoteDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMarketDataService _marketData;

    public GetLiveQuotesHandler(IApplicationDbContext context, IMarketDataService marketData)
    {
        _context    = context;
        _marketData = marketData;
    }

    public async Task<List<QuoteDto>> Handle(GetLiveQuotesQuery request, CancellationToken ct)
    {
        var lookups = await _context.Holdings
            .AsNoTracking()
            .Select(h => new QuoteRequest(h.Ticker, h.Market, h.Type))
            .ToListAsync(ct);

        if (lookups.Count == 0) return [];

        var tickers = lookups.Select(l => l.Ticker).ToList();
        var quotes  = await _marketData.GetQuotesAsync(lookups, ct);

        var existingMap = await _context.PriceCaches
            .Where(p => tickers.Contains(p.Ticker))
            .ToDictionaryAsync(p => p.Ticker, ct);

        foreach (var q in quotes)
        {
            if (existingMap.TryGetValue(q.Ticker, out var cache))
            {
                cache.CurrentPrice  = q.CurrentPrice;
                cache.PreviousClose = q.PreviousClose;
                cache.Currency      = Enum.Parse<CurrencyType>(q.Currency, true);
                cache.UpdatedAt     = DateTimeOffset.UtcNow;
            }
            else
            {
                _context.PriceCaches.Add(new PriceCache
                {
                    Ticker        = q.Ticker,
                    CurrentPrice  = q.CurrentPrice,
                    PreviousClose = q.PreviousClose,
                    Currency      = Enum.Parse<CurrencyType>(q.Currency, true),
                    UpdatedAt     = DateTimeOffset.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync(ct);

        return quotes.Select(q =>
        {
            var dayChange    = q.CurrentPrice - q.PreviousClose;
            var dayChangePct = q.PreviousClose == 0 ? 0
                : Math.Round(dayChange / q.PreviousClose * 100, 2);
            return new QuoteDto(q.Ticker, q.CurrentPrice, q.PreviousClose,
                dayChange, dayChangePct, q.Currency, DateTimeOffset.UtcNow);
        }).ToList();
    }
}
