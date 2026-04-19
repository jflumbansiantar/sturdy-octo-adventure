using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Debts.Queries.GetDebts;

public class GetDebtsHandler : IRequestHandler<GetDebtsQuery, List<DebtDto>>
{
    private readonly IApplicationDbContext _context;

    public GetDebtsHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<DebtDto>> Handle(GetDebtsQuery request, CancellationToken ct)
    {
        var debts = await _context.Debts.AsNoTracking().ToListAsync(ct);

        // Aggregate payments from DEBT transactions grouped by name
        var paymentMap = await _context.Transactions
            .AsNoTracking()
            .Where(t => t.Category == TransactionCategory.Debt)
            .GroupBy(t => t.Name)
            .Select(g => new { Name = g.Key, TotalPaid = g.Sum(t => t.Total), MonthsPaid = g.Count() })
            .ToDictionaryAsync(x => x.Name, ct);

        return debts.Select(d =>
        {
            paymentMap.TryGetValue(d.Name, out var pay);
            return new DebtDto(
                d.Id, d.Name, d.Type.ToString(),
                d.Balance, d.InterestRate, d.MonthlyInterestRate,
                d.Tenor, d.MinimumPayment, d.DueDay,
                d.Currency.ToString(), d.DebtApp, d.Notes, d.Status.ToString(),
                pay?.TotalPaid ?? 0, pay?.MonthsPaid ?? 0,
                d.CreatedAt, d.UpdatedAt);
        }).ToList();
    }
}
