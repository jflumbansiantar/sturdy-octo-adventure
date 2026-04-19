using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Ledger.Queries.GetAccounts;

public class GetAccountsHandler : IRequestHandler<GetAccountsQuery, List<LedgerAccountDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAccountsHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<LedgerAccountDto>> Handle(GetAccountsQuery request, CancellationToken ct)
    {
        var accounts = await _context.LedgerAccounts
            .AsNoTracking()
            .Include(a => a.JournalLines)
            .OrderBy(a => a.Id)
            .ToListAsync(ct);

        return accounts.Select(a =>
        {
            var totalDebits  = a.JournalLines.Sum(l => l.Debit);
            var totalCredits = a.JournalLines.Sum(l => l.Credit);
            var balance      = a.NormalBalance == NormalBalanceType.Debit
                ? a.OpeningBalance + totalDebits - totalCredits
                : a.OpeningBalance + totalCredits - totalDebits;

            return new LedgerAccountDto(
                a.Id, a.Code, a.Name,
                a.Type.ToString(), a.NormalBalance.ToString(),
                a.OpeningBalance, totalDebits, totalCredits, balance);
        }).ToList();
    }
}
