using System.Text.RegularExpressions;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Shared.Scanning.Parsers;

/// <summary>
/// Tagihan kartu kredit or lembar angsuran. Files under Debt.
/// Two traps live here: the minimum payment is not the bill, and the due date is not the
/// transaction date - both are the wrong number sitting right next to the right one.
/// </summary>
public partial class BillParser : IDocumentParser
{
    public DocumentKind Kind => DocumentKind.Bill;

    private const string BillTotalLabel =
        @"\b(total\s*tagihan|jumlah\s*tagihan|total\s*bayar|tagihan\s*bulan|angsuran\s*(per\s*bulan|bulanan)?|jumlah\s*angsuran)\b";

    private const string MinimumPaymentLabel =
        @"\b(pembayaran\s*minimum|minimum\s*payment|min\.?\s*payment|pembayaran\s*min)\b";

    [GeneratedRegex(@"\b(jatuh\s*tempo|due\s*date)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DueDateLabel();

    public TransactionDraft Parse(OcrText ocr)
    {
        var warnings = new List<string>();

        var total = AmountPicker.ForLabel(ocr, BillTotalLabel, fromBottom: true);

        if (!total.HasValue)
        {
            total = AmountPicker.ForLabel(ocr, MinimumPaymentLabel, fromBottom: true);
            if (total.HasValue)
            {
                total = total with { Confidence = Confidence.Low };
                warnings.Add("Total tagihan tidak ditemukan - dipakai pembayaran minimum. Mohon diperiksa.");
            }
        }

        if (!total.HasValue)
        {
            total = AmountPicker.FindTotal(ocr);
            if (total.Confidence == Confidence.Low)
                warnings.Add("Nominal tagihan tidak pasti. Mohon diperiksa.");
        }

        var issuer = MerchantName.AfterLabel(ocr, @"\b(penerbit|bank|kartu|nama\s*produk)\b");
        if (!issuer.HasValue) issuer = MerchantName.FromHeader(ocr);

        var date = FindStatementDate(ocr, warnings);

        return new TransactionDraft
        {
            Kind = Kind,
            Category = new FieldGuess<TransactionCategory>(TransactionCategory.Debt, Confidence.High),
            Type = new FieldGuess<string>("Debit", Confidence.High),
            Date = date,
            Name = issuer,
            Total = total,
            RawText = ocr.AllText,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Prefers a date that is NOT the due date. A statement's due date is in the future, so
    /// using it would post the transaction into a month that has not happened yet.
    /// </summary>
    private static FieldGuess<DateOnly> FindStatementDate(OcrText ocr, List<string> warnings)
    {
        foreach (var line in ocr.InReadingOrder)
        {
            if (DueDateLabel().IsMatch(line.Text)) continue;

            var guess = IndoDateParser.ParseLine(line.Text);
            if (guess.HasValue) return guess with { Evidence = line.Text.Trim() };
        }

        var fallback = IndoDateParser.Find(ocr);
        if (fallback.HasValue)
        {
            warnings.Add("Hanya tanggal jatuh tempo yang terbaca, bukan tanggal transaksi. Mohon diperiksa.");
            return fallback with { Confidence = Confidence.Low };
        }

        warnings.Add("Tanggal tidak terbaca - dipakai tanggal hari ini.");
        return FieldGuess<DateOnly>.Missing;
    }
}
