using MediatR;

namespace PortfolioOS.Application.Portfolio.Queries.GetPortfolioSummary;

public record GetPortfolioSummaryQuery : IRequest<PortfolioSummaryDto>;
