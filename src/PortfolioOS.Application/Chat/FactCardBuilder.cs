using System.Globalization;
using PortfolioOS.Domain.Entities;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Chat;

/// <summary>
/// Renders a database row as one natural sentence for the search index.
/// </summary>
/// <remarks>
/// These "fact cards" exist so free-text questions can find a specific record — the merchant on
/// a receipt, a note on a loan, a journal description. They are never the source of a number in
/// an answer: figures always come back from the MediatR queries, which apply currency conversion
/// and the rounding rules the rest of the app uses. A card is a signpost, not a fact table.
/// <para>
/// Pure and static so it can be unit-tested without a database or a model.
/// </para>
/// </remarks>
public static class FactCardBuilder
{
    private static readonly CultureInfo Id = new("id-ID");

    private static string Money(decimal value, CurrencyType currency) =>
        currency == CurrencyType.USD
            ? "USD " + value.ToString("N2", CultureInfo.InvariantCulture)
            : "Rp " + value.ToString("N0", Id);

    private static string Money(decimal value, Market market) =>
        Money(value, market == Market.US ? CurrencyType.USD : CurrencyType.IDR);

    public static string ForHolding(Holding h)
    {
        var market = h.Market == Market.ID ? "pasar Indonesia" : "pasar Amerika";
        var type = h.Type == HoldingType.MutualFund ? "reksa dana" : h.Type.ToString().ToLowerInvariant();
        var sub = string.IsNullOrWhiteSpace(h.SubType) ? "" : $", {h.SubType}";

        return $"{h.Ticker} — {h.Name}. Jenis {type}{sub}, {market}. " +
               $"Dimiliki {h.Shares.ToString("0.########", Id)} unit dengan harga rata-rata " +
               $"{Money(h.AvgCost, h.Market)} per unit.";
    }

    public static string ForDebt(Debt d)
    {
        var status = d.Status == DebtStatus.Lunas ? "sudah lunas" : "masih aktif";
        var app = string.IsNullOrWhiteSpace(d.DebtApp) ? "" : $" via {d.DebtApp}";
        var tenor = d.Tenor is > 0 ? $", tenor {d.Tenor} bulan" : "";
        var notes = string.IsNullOrWhiteSpace(d.Notes) ? "" : $" Catatan: {d.Notes}";

        return $"Utang {d.Name}{app} — jenis {Humanise(d.Type)}, {status}. " +
               $"Sisa {Money(d.Balance, d.Currency)}, bunga {d.InterestRate.ToString("0.##", Id)}% per tahun{tenor}. " +
               $"Pembayaran minimum {Money(d.MinimumPayment, d.Currency)}, jatuh tempo tanggal {d.DueDay}.{notes}";
    }

    public static string ForTransaction(Transaction t)
    {
        var kind = t.Category switch
        {
            TransactionCategory.Stock => "transaksi saham",
            TransactionCategory.Debt => "pembayaran utang",
            TransactionCategory.Income => "pemasukan",
            _ => "pengeluaran"
        };

        var detail = t is { Shares: not null, Price: not null }
            ? $" ({t.Shares.Value.ToString("0.########", Id)} unit @ {Money(t.Price.Value, t.Market ?? Market.ID)})"
            : "";

        return $"{t.Date:d MMMM yyyy} — {kind} {t.Type} \"{t.Name}\" " +
               $"senilai {Money(t.Total, t.Market ?? Market.ID)}{detail}.";
    }

    public static string ForJournalEntry(JournalEntry e) =>
        $"Jurnal {e.Id} tanggal {e.Date:d MMMM yyyy}: {e.Description}.";

    public static string ForLedgerAccount(LedgerAccount a) =>
        $"Akun {a.Code} — {a.Name}. Jenis {Humanise(a.Type)}, saldo normal " +
        $"{(a.NormalBalance == NormalBalanceType.Debit ? "debit" : "kredit")}, " +
        $"saldo awal {Money(a.OpeningBalance, CurrencyType.IDR)}.";

    private static string Humanise(DebtType type) => type switch
    {
        DebtType.CreditCard => "kartu kredit",
        DebtType.PersonalLoan => "pinjaman pribadi",
        DebtType.Mortgage => "KPR",
        DebtType.AutoLoan => "kredit kendaraan",
        DebtType.StudentLoan => "pinjaman pendidikan",
        _ => "lainnya"
    };

    private static string Humanise(AccountType type) => type switch
    {
        AccountType.Asset => "aset",
        AccountType.Liability => "kewajiban",
        AccountType.Equity => "ekuitas",
        AccountType.Income => "pendapatan",
        _ => "beban"
    };
}
