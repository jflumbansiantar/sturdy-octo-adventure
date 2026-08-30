using System.Text.RegularExpressions;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Shared.Scanning.Parsers;

/// <summary>
/// Bukti transfer m-banking or e-wallet. The one thing that must be decided here is the
/// direction: the same layout serves money going out (Expense) and money coming in (Income),
/// and the amount itself carries no sign.
/// </summary>
public partial class TransferParser : IDocumentParser
{
    public DocumentKind Kind => DocumentKind.Transfer;

    [GeneratedRegex(@"\b(dana\s*masuk|uang\s*masuk|dana\s*diterima|diterima\s*dari|terima\s*dari|top\s*up|topup|cashback|refund|pengembalian|masuk\b|kredit\b|incoming|received)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IncomingMarker();

    /// <summary>
    /// "Penerima" and "Rekening Tujuan" are listed as outgoing markers because a plain
    /// "Transfer Berhasil" header says nothing about direction, and naming a recipient is
    /// what every outgoing slip does.
    /// </summary>
    [GeneratedRegex(@"\b(transfer\s*ke|kirim|bayar|pembayaran|pengeluaran|keluar\b|debit\b|outgoing|sent\s*to|penerima|rekening\s*tujuan|bank\s*tujuan)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OutgoingMarker();

    public TransactionDraft Parse(OcrText ocr)
    {
        var warnings = new List<string>();
        var text = ocr.AllText;

        var incoming = IncomingMarker().IsMatch(text);
        var outgoing = OutgoingMarker().IsMatch(text);

        // When both or neither appear the direction is a coin flip, so it is flagged rather
        // than trusted - filing income as an expense is the worst failure this parser has.
        var (category, confidence) = (incoming, outgoing) switch
        {
            (true, false) => (TransactionCategory.Income, Confidence.High),
            (false, true) => (TransactionCategory.Expense, Confidence.High),
            _ => (TransactionCategory.Expense, Confidence.Low)
        };

        if (confidence == Confidence.Low)
            warnings.Add("Arah dana tidak jelas (masuk atau keluar) - default Expense. Mohon diperiksa.");

        // Whoever is on the other side of the transfer becomes the description.
        var counterparty = category == TransactionCategory.Income
            ? MerchantName.AfterLabel(ocr, @"\b(pengirim|dari|sumber\s*dana|from)\b")
            : MerchantName.AfterLabel(ocr, @"\b(penerima|kepada|tujuan|merchant|to)\b");

        if (!counterparty.HasValue)
            counterparty = MerchantName.FromHeader(ocr);

        var total = AmountPicker.ForLabel(ocr, @"\b(nominal|jumlah\s*transfer|total\s*transfer|amount)\b");
        if (!total.HasValue) total = AmountPicker.FindTotal(ocr);

        var date = IndoDateParser.Find(ocr);
        if (!date.HasValue) warnings.Add("Tanggal tidak terbaca - dipakai tanggal hari ini.");
        if (!total.HasValue) warnings.Add("Nominal tidak terbaca. Isi manual.");

        return new TransactionDraft
        {
            Kind = Kind,
            Category = new FieldGuess<TransactionCategory>(category, confidence),
            Type = new FieldGuess<string>(category == TransactionCategory.Income ? "Credit" : "Debit", confidence),
            Date = date,
            Name = counterparty,
            Total = total,
            RawText = ocr.AllText,
            Warnings = warnings
        };
    }
}
