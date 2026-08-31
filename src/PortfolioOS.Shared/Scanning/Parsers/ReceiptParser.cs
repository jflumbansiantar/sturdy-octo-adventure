using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Shared.Scanning.Parsers;

/// <summary>Struk belanja - minimarket, restoran, SPBU. Always an expense.</summary>
public class ReceiptParser : IDocumentParser
{
    public DocumentKind Kind => DocumentKind.Receipt;

    public TransactionDraft Parse(OcrText ocr)
    {
        var warnings = new List<string>();

        var total = AmountPicker.FindTotal(ocr);
        if (total.Confidence == Confidence.Low)
            warnings.Add("Baris TOTAL tidak ditemukan - nominal diambil dari angka terbesar. Mohon diperiksa.");
        if (!total.HasValue)
            warnings.Add("Nominal tidak terbaca. Isi manual.");

        var date = IndoDateParser.Find(ocr);
        if (!date.HasValue)
            warnings.Add("Tanggal tidak terbaca - dipakai tanggal hari ini.");
        else if (date.Confidence == Confidence.Low)
            warnings.Add("Format tanggal ambigu (dibaca hari/bulan). Mohon diperiksa.");

        var name = MerchantName.FromHeader(ocr);
        if (!name.HasValue)
            warnings.Add("Nama merchant tidak terbaca. Isi manual.");

        return new TransactionDraft
        {
            Kind = Kind,
            Category = new FieldGuess<TransactionCategory>(TransactionCategory.Expense, Confidence.High),
            Type = new FieldGuess<string>("Debit", Confidence.High),
            Date = date,
            Name = name,
            Total = total,
            RawText = ocr.AllText,
            Warnings = warnings
        };
    }
}
