using MediatR;

namespace PortfolioOS.Application.Ledger.Queries.GetAccounts;

public record GetAccountsQuery : IRequest<List<LedgerAccountDto>>;
