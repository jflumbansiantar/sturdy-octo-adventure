using MediatR;
using PortfolioOS.Application.Ledger.Queries.GetAccounts;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Ledger.Queries.GetLedgerSummary;

public class GetLedgerSummaryHandler : IRequestHandler<GetLedgerSummaryQuery, LedgerSummaryDto>
{
    private readonly IMediator _mediator;

    public GetLedgerSummaryHandler(IMediator mediator) => _mediator = mediator;

    public async Task<LedgerSummaryDto> Handle(GetLedgerSummaryQuery request, CancellationToken ct)
    {
        var accounts = await _mediator.Send(new GetAccountsQuery(), ct);

        decimal Sum(AccountType type) => accounts
            .Where(a => a.Type == type.ToString())
            .Sum(a => a.Balance);

        var totalAssets      = Sum(AccountType.Asset);
        var totalLiabilities = Sum(AccountType.Liability);
        var totalEquity      = Sum(AccountType.Equity);
        var totalIncome      = Sum(AccountType.Income);
        var totalExpenses    = Sum(AccountType.Expense);
        var netWorth         = totalAssets - totalLiabilities;

        return new LedgerSummaryDto(
            totalAssets, totalLiabilities, totalEquity,
            totalIncome, totalExpenses, netWorth, accounts);
    }
}
