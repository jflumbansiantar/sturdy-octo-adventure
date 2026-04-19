using MediatR;

namespace PortfolioOS.Application.Holdings.Queries.GetHoldings;

public record GetHoldingsQuery : IRequest<List<HoldingDto>>;
