using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Forecasting;

/// <summary>
/// Reads the two stored inputs every projection needs beyond the cash-flow history: what is owed,
/// and what is in the bank.
/// </summary>
/// <remarks>
/// Shared by all three forecast handlers for the same reason <see cref="Savings.CashflowHistory"/>
/// is shared — the debt payoff schedule and the cash-flow forecast must agree about which debts are
/// active, or the forecast would deduct instalments the payoff plan says are finished.
/// </remarks>
public static class ForecastInputs
{
    /// <summary>
    /// Active rupiah debts. Foreign-currency debts are dropped rather than converted: a payoff
    /// schedule mixing currencies would report a rupiah total that is not owed in rupiah, and the
    /// same rule already governs what the savings advisor counts.
    /// </summary>
    public static async Task<List<DebtPosition>> ReadActiveDebtsAsync(
        IApplicationDbContext context, CancellationToken ct) =>
        await context.Debts
            .AsNoTracking()
            .Where(d => d.Status != DebtStatus.Lunas && d.Currency == CurrencyType.IDR && d.Balance > 0m)
            .Select(d => new DebtPosition(
                d.Id, d.Name, d.Type, d.Balance,
                d.InterestRate, d.MonthlyInterestRate, d.MinimumPayment, d.Tenor))
            .ToListAsync(ct);

    /// <summary>
    /// Spendable cash across the ledger: each recognised account's opening balance plus its posted
    /// movements.
    /// </summary>
    /// <remarks>
    /// Which accounts count is <see cref="CashAccountClassifier"/>'s decision, and anything it does
    /// not recognise is left out. A total that comes back negative means the ledger disagrees with
    /// itself — more credited out of cash than was ever in it — and callers treat that as no usable
    /// balance rather than as a debt.
    /// </remarks>
    public static async Task<decimal> ReadCashBalanceAsync(
        IApplicationDbContext context, CancellationToken ct)
    {
        var accounts = await context.LedgerAccounts
            .AsNoTracking()
            .Where(a => a.Type == AccountType.Asset)
            .Select(a => new { a.Id, a.Code, a.Name, a.Type, a.NormalBalance, a.OpeningBalance })
            .ToListAsync(ct);

        var cash = accounts
            .Where(a => CashAccountClassifier.IsCash(a.Type, a.Name, a.Code))
            .ToList();

        if (cash.Count == 0) return 0m;

        var ids = cash.Select(a => a.Id).ToList();

        var movements = await context.JournalLines
            .AsNoTracking()
            .Where(l => ids.Contains(l.AccountId))
            .GroupBy(l => l.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                Debit = g.Sum(x => x.Debit),
                Credit = g.Sum(x => x.Credit),
            })
            .ToListAsync(ct);

        var byAccount = movements.ToDictionary(m => m.AccountId);

        return cash.Sum(a =>
        {
            var posted = byAccount.GetValueOrDefault(a.Id);
            var debit = posted?.Debit ?? 0m;
            var credit = posted?.Credit ?? 0m;

            return a.NormalBalance == NormalBalanceType.Debit
                ? a.OpeningBalance + debit - credit
                : a.OpeningBalance + credit - debit;
        });
    }
}
