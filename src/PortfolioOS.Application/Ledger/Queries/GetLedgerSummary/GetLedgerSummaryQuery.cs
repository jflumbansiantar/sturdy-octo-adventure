using MediatR;

namespace PortfolioOS.Application.Ledger.Queries.GetLedgerSummary;

public record GetLedgerSummaryQuery : IRequest<LedgerSummaryDto>;
