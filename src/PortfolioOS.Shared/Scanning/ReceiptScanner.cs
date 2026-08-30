using PortfolioOS.Shared.Scanning.Parsers;

namespace PortfolioOS.Shared.Scanning;

/// <summary>
/// The single entry point the mobile app calls: recognised text in, draft transaction out.
/// Holds no state and touches no I/O, so the whole extraction path is testable without a camera.
/// </summary>
public class ReceiptScanner
{
    private readonly IReadOnlyList<IDocumentParser> _parsers;
    private readonly IDocumentParser _fallback;

    public ReceiptScanner() : this([
        new ReceiptParser(),
        new TransferParser(),
        new PayslipParser(),
        new BillParser(),
        new BrokerTradeParser()
    ])
    { }

    public ReceiptScanner(IReadOnlyList<IDocumentParser> parsers)
    {
        _parsers = parsers;
        _fallback = parsers.FirstOrDefault(p => p.Kind == DocumentKind.Receipt) ?? parsers[0];
    }

    public TransactionDraft Scan(OcrText ocr)
    {
        if (ocr.Lines.Count == 0)
        {
            return new TransactionDraft
            {
                RawText = ocr.AllText,
                Warnings = ["Tidak ada teks yang terbaca dari gambar. Coba foto ulang dengan cahaya lebih terang."]
            };
        }

        var kind = DocumentClassifier.Classify(ocr);
        var parser = _parsers.FirstOrDefault(p => p.Kind == kind) ?? _fallback;

        var draft = parser.Parse(ocr);

        if (kind == DocumentKind.Unknown)
        {
            draft = draft with
            {
                Warnings = [.. draft.Warnings, "Jenis dokumen tidak dikenali - dibaca sebagai struk belanja. Mohon periksa kategorinya."],
                Category = draft.Category with { Confidence = Confidence.Low }
            };
        }

        return Sanitise(draft);
    }

    /// <summary>
    /// Last line of defence before the draft reaches the UI: keeps every value inside what
    /// CreateTransactionValidator will accept, so a bad read shows up as a warning here rather
    /// than as a 400 from the API after the user has already hit save.
    /// </summary>
    private static TransactionDraft Sanitise(TransactionDraft draft)
    {
        var warnings = draft.Warnings.ToList();

        var total = draft.Total;
        if (total.HasValue && total.Value < 0)
        {
            // E-wallets print outgoing amounts as "-Rp25.000"; the sign lives in Category here.
            total = total with { Value = Math.Abs(total.Value) };
        }

        var name = draft.Name;
        if (name.HasValue && name.Value!.Length > MerchantName.MaxLength)
            name = name with { Value = name.Value[..MerchantName.MaxLength] };

        if (draft.Category.Value == Domain.Enums.TransactionCategory.Stock &&
            (!draft.Market.HasValue || !draft.Shares.HasValue || !draft.Price.HasValue))
        {
            warnings.Add("Transaksi saham butuh Market, Lembar, dan Harga terisi sebelum bisa disimpan.");
        }

        return draft with { Total = total, Name = name, Warnings = warnings };
    }
}
