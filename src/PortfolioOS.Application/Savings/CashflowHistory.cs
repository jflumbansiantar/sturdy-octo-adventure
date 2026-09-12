using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Savings;

/// <summary>
/// Reads the recorded ledger as the monthly cash-flow series every downstream analysis works from.
/// </summary>
/// <remarks>
/// What is decided here is only what counts as data: which transactions, which months, and which
/// currency they are allowed to be in. Everything opinionated lives in the advisor or forecaster
/// that consumes the series, which is what keeps those pure and testable without a database.
/// <para>
/// It lives apart from any one handler because the savings advice and the cash-flow forecast must
/// read the same months the same way — the rules below are subtle enough that a second copy would
/// drift, and the two features would then disagree about the same household.
/// </para>
/// </remarks>
public static class CashflowHistory
{
    /// <summary>Fewer than three months says nothing about a habit.</summary>
    public const int MinWindow = 3;

    /// <summary>More than two years describes a life the user no longer lives.</summary>
    public const int MaxWindow = 24;

    /// <param name="months">Window length in months, counting the current one. Clamped to 3-24.</param>
    public static async Task<List<MonthlyCashflow>> ReadAsync(
        IApplicationDbContext context,
        DateOnly today,
        int months,
        CancellationToken ct)
    {
        var window = Math.Clamp(months, MinWindow, MaxWindow);
        var thisMonth = new DateOnly(today.Year, today.Month, 1);
        var from = thisMonth.AddMonths(-(window - 1));

        // Exclusive upper bound. A row dated ahead of today - a scheduled instalment, a mistyped
        // year - would otherwise open a month of its own at the end of the series, and consumers
        // read the *last* months as "now": one future row and the whole answer is built on a month
        // that has not happened yet.
        var until = thisMonth.AddMonths(1);

        var rows = await context.Transactions
            .AsNoTracking()
            .Where(t => t.Date >= from && t.Date < until && t.Category != TransactionCategory.Stock)
            .Select(t => new { t.Date, t.Category, t.Type, t.Name, t.Total, t.Market })
            .ToListAsync(ct);

        return rows
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
    }

    /// <summary>
    /// What the user is contractually on the hook for each month, whether or not this month's
    /// instalment has been recorded yet. Used as a floor for debt service, so a month where an
    /// instalment was simply not logged cannot inflate the amount that looks available.
    /// </summary>
    public static async Task<decimal> ReadCommittedDebtPaymentAsync(
        IApplicationDbContext context, CancellationToken ct) =>
        await context.Debts
            .AsNoTracking()
            .Where(d => d.Status != DebtStatus.Lunas && d.Currency == CurrencyType.IDR)
            .SumAsync(d => d.MinimumPayment, ct);
}
