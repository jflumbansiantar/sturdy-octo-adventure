using System.Text.RegularExpressions;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Forecasting;

/// <summary>
/// Decides which asset accounts hold spendable cash, so a runway figure counts money that could
/// actually pay next month's bills.
/// </summary>
/// <remarks>
/// The chart of accounts has no "is cash" flag, and summing every <see cref="AccountType.Asset"/>
/// row would fold a 150 juta investment portfolio and an unpaid receivable into the cash balance —
/// turning a household one month from trouble into one that looks years clear.
/// <para>
/// Same shape as <see cref="Savings.ExpenseClassifier"/>, and the same two decisions that keep it
/// honest: non-cash names are checked first, so "Piutang Bank Mandiri" cannot be claimed by the
/// "bank" keyword; and anything unrecognised is <b>not</b> cash. That understates the runway,
/// which is the direction an unrecognised account should push a safety margin.
/// </para>
/// </remarks>
public static class CashAccountClassifier
{
    private static readonly Regex NotCash = Build([
        "piutang", "receivable", "portofolio", "portfolio", "investasi", "investment",
        "saham", "stock", "reksadana", "obligasi", "crypto", "emas", "gold",
        "properti", "property", "kendaraan", "inventaris", "aset tetap", "fixed asset",
        "deposito", "time deposit", "dana pensiun", "pension",
    ]);

    private static readonly Regex Cash = Build([
        "kas", "cash", "tunai", "petty cash",
        "bank", "rekening", "tabungan", "savings account", "giro", "checking",
        "e-wallet", "ewallet", "dompet digital", "gopay", "ovo", "dana", "shopeepay", "linkaja",
    ]);

    /// <summary>
    /// True when this account is spendable cash. Only <see cref="AccountType.Asset"/> accounts
    /// can qualify — a liability with "bank" in its name is a card, not a balance to spend.
    /// </summary>
    public static bool IsCash(AccountType type, string? name, string? code)
    {
        if (type != AccountType.Asset) return false;

        var text = $"{name} {code}";
        if (NotCash.IsMatch(text)) return false;
        return Cash.IsMatch(text);
    }

    private static Regex Build(string[] keywords) => new(
        @"\b(" + string.Join("|", keywords.Select(Regex.Escape)) + @")\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
}
