using System.Text.RegularExpressions;

namespace PortfolioOS.Shared.Scanning;

/// <summary>
/// Works out what kind of document was photographed, by scoring keyword hits. Every kind maps
/// onto exactly one <see cref="Domain.Enums.TransactionCategory"/>, so getting this wrong files
/// the transaction under the wrong category even when every number is read correctly.
/// </summary>
public static class DocumentClassifier
{
    private record Signal(string Pattern, int Weight);

    private static readonly Dictionary<DocumentKind, Signal[]> Signals = new()
    {
        [DocumentKind.BrokerTrade] =
        [
            new(@"\b(stockbit|ajaib|ipot|bions|mirae|sekuritas|securities)\b", 3),
            new(@"\b(lot|lembar)\b", 2),
            new(@"\bsaham\b", 2),
            new(@"\b(kode\s*saham|ticker|stock\s*code)\b", 3),
            new(@"\b(komisi|levy|fee\s*(beli|jual)|broker)\b", 2),
            new(@"\b(beli|buy|jual|sell)\b", 1)
        ],
        [DocumentKind.Payslip] =
        [
            new(@"\b(slip\s*gaji|payslip|pay\s*slip)\b", 5),
            new(@"\b(gaji\s*pokok|take\s*home\s*pay|thp)\b", 3),
            new(@"\b(tunjangan|lembur|insentif)\b", 2),
            new(@"\b(bpjs|pph\s*21|potongan)\b", 2),
            new(@"\b(gaji|penghasilan|dividen)\b", 1)
        ],
        [DocumentKind.Bill] =
        [
            new(@"\b(jatuh\s*tempo|due\s*date)\b", 3),
            new(@"\b(pembayaran\s*minimum|minimum\s*payment|min\.?\s*payment)\b", 3),
            new(@"\btagihan\b", 3),
            new(@"\b(angsuran|cicilan|installment)\b", 2),
            new(@"\b(sisa\s*pokok|outstanding|saldo\s*tagihan)\b", 2),
            new(@"\b(kartu\s*kredit|credit\s*card|limit\s*kredit)\b", 2),
            new(@"\b(denda|bunga)\b", 1)
        ],
        [DocumentKind.Transfer] =
        [
            new(@"\b(gopay|ovo|dana|shopeepay|linkaja|qris|e-?wallet)\b", 3),
            new(@"\btransfer\b", 3),
            new(@"\b(no\.?\s*ref|nomor\s*referensi|ref(erence)?\s*(no|number)|kode\s*transaksi)\b", 2),
            new(@"\b(penerima|rekening\s*tujuan|sumber\s*dana|dari\s*rekening)\b", 2),
            new(@"\b(berhasil|sukses|success(ful)?)\b", 2),
            new(@"\b(saldo|berita|catatan)\b", 1)
        ],
        [DocumentKind.Receipt] =
        [
            new(@"\b(kasir|cashier|struk|nota)\b", 3),
            new(@"\b(sub\s*total)\b", 2),
            new(@"\b(tunai|cash|kembali(an)?|change)\b", 2),
            new(@"\b(npwp|ppn|qty|harga\s*satuan)\b", 1),
            new(@"\b(terima\s*kasih|thank\s*you)\b", 1)
        ]
    };

    /// <summary>Below this, the hits are noise rather than evidence.</summary>
    private const int MinimumScore = 3;

    public static DocumentKind Classify(OcrText ocr) => Classify(ocr.AllText);

    public static DocumentKind Classify(string? allText)
    {
        if (string.IsNullOrWhiteSpace(allText)) return DocumentKind.Unknown;

        var best = DocumentKind.Unknown;
        var bestScore = 0;

        foreach (var (kind, signals) in Signals)
        {
            var score = signals.Sum(s =>
                Regex.IsMatch(allText, s.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                    ? s.Weight
                    : 0);

            if (score > bestScore)
            {
                bestScore = score;
                best = kind;
            }
        }

        return bestScore >= MinimumScore ? best : DocumentKind.Unknown;
    }
}
