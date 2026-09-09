using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Savings.Queries.GetSavingsSuggestion;

/// <summary>
/// Turns the recorded ledger into the monthly cash-flow series <see cref="SavingsAdvisor"/> needs,
/// and hands the arithmetic to it.
/// </summary>
/// <remarks>
/// Everything opinionated lives in the advisor, which is pure and therefore testable without a
/// database. What is decided here is only what counts as data: which transactions, which months,
/// and which currency they are allowed to be in.
/// </remarks>
public class GetSavingsSuggestionHandler(IApplicationDbContext context, TimeProvider timeProvider)
    : IRequestHandler<GetSavingsSuggestionQuery, SavingsSuggestionDto>
{
    private const int MinWindow = 3;
    private const int MaxWindow = 24;

    public async Task<SavingsSuggestionDto> Handle(GetSavingsSuggestionQuery request, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var window = Math.Clamp(request.Months, MinWindow, MaxWindow);
        var thisMonth = new DateOnly(today.Year, today.Month, 1);
        var from = thisMonth.AddMonths(-(window - 1));

        // Exclusive upper bound. A row dated ahead of today - a scheduled instalment, a mistyped
        // year - would otherwise open a month of its own at the end of the series, and the
        // advisor reads the *last* months as "now": one future row and the whole suggestion is
        // built on a month that has not happened yet.
        var until = thisMonth.AddMonths(1);

        var rows = await context.Transactions
            .AsNoTracking()
            .Where(t => t.Date >= from && t.Date < until && t.Category != TransactionCategory.Stock)
            .Select(t => new { t.Date, t.Category, t.Type, t.Name, t.Total, t.Market })
            .ToListAsync(ct);

        var cashflow = rows
            // Trades are recorded in the currency they happened in, so only base-currency rows
            // can be summed. Income, expenses and instalments carry no market; this drops the
            // odd dollar-denominated row rather than adding it to a rupiah total.
            .Where(r => r.Market != Market.US)
            .GroupBy(r => new DateOnly(r.Date.Year, r.Date.Month, 1))
            .Select(g => new MonthlyCashflow(
                Month: g.Key,
                Income: g.Where(r => r.Category == TransactionCategory.Income).Sum(r => r.Total),
                Needs: g.Where(r => r.Category == TransactionCategory.Expense &&
                                    !ExpenseClassifier.IsDiscretionary(r.Type, r.Name))
                        .Sum(r => r.Total),
                Wants: g.Where(r => r.Category == TransactionCategory.Expense &&
                                    ExpenseClassifier.IsDiscretionary(r.Type, r.Name))
                        .Sum(r => r.Total),
                DebtService: g.Where(r => r.Category == TransactionCategory.Debt).Sum(r => r.Total)))
            .ToList();

        // What the user is contractually on the hook for each month, whether or not this month's
        // instalment has been recorded yet. The advisor uses it as a floor for debt service.
        var committedDebt = await context.Debts
            .AsNoTracking()
            .Where(d => d.Status != DebtStatus.Lunas && d.Currency == CurrencyType.IDR)
            .SumAsync(d => d.MinimumPayment, ct);

        return SavingsAdvisor.Recommend(cashflow, today, committedDebt);
    }
}
