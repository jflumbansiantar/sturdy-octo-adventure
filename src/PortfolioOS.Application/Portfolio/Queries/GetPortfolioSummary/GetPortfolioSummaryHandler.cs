using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Application.Holdings.Queries.GetHoldings;

namespace PortfolioOS.Application.Portfolio.Queries.GetPortfolioSummary;

public class GetPortfolioSummaryHandler : IRequestHandler<GetPortfolioSummaryQuery, PortfolioSummaryDto>
{
    private readonly IMediator _mediator;

    public GetPortfolioSummaryHandler(IMediator mediator) => _mediator = mediator;

    public async Task<PortfolioSummaryDto> Handle(GetPortfolioSummaryQuery request, CancellationToken ct)
    {
        // Reuse GetHoldings to avoid duplicating enrichment logic
        var holdings = await _mediator.Send(new GetHoldingsQuery(), ct);

        var totalValue     = holdings.Sum(h => h.MarketValue);
        var totalCostBasis = holdings.Sum(h => h.CostBasis);
        var totalGainLoss  = totalValue - totalCostBasis;
        var totalGainLossPct = totalCostBasis == 0 ? 0
            : Math.Round(totalGainLoss / totalCostBasis * 100, 2);
        var todayGainLoss  = holdings.Sum(h => h.DayGainLoss);

        return new PortfolioSummaryDto(
            totalValue, totalCostBasis, totalGainLoss, totalGainLossPct,
            todayGainLoss, holdings.Count, holdings);
    }
}
