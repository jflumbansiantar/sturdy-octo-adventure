using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Shared.Scanning.Parsers;

/// <summary>
/// Slip gaji. The amount that matters is take-home pay, not gross salary and not any single
/// allowance - and gross is usually the larger number, so the generic total picker cannot be
/// trusted here without trying the net labels first.
/// </summary>
public class PayslipParser : IDocumentParser
{
    public DocumentKind Kind => DocumentKind.Payslip;

    private const string NetPayLabel =
        @"\b(take\s*home\s*pay|thp|gaji\s*bersih|penerimaan\s*bersih|total\s*diterima|net\s*(pay|salary)|jumlah\s*diterima)\b";

    private const string GrossPayLabel =
        @"\b(total\s*penerimaan|gaji\s*kotor|gross|total\s*pendapatan)\b";

    public TransactionDraft Parse(OcrText ocr)
    {
        var warnings = new List<string>();

        var total = AmountPicker.ForLabel(ocr, NetPayLabel, fromBottom: true);

        if (!total.HasValue)
        {
            total = AmountPicker.ForLabel(ocr, GrossPayLabel, fromBottom: true);
            if (total.HasValue)
            {
                total = total with { Confidence = Confidence.Low };
                warnings.Add("Take home pay tidak ditemukan - dipakai total penerimaan (bruto). Mohon diperiksa.");
            }
        }

        if (!total.HasValue)
        {
            total = AmountPicker.FindTotal(ocr) with { Confidence = Confidence.Low };
            warnings.Add("Nominal gaji tidak pasti. Mohon diperiksa.");
        }

        var employer = MerchantName.AfterLabel(ocr, @"\b(perusahaan|company|pt|cv)\b");
        if (!employer.HasValue) employer = MerchantName.FromHeader(ocr);

        var name = employer.HasValue
            ? new FieldGuess<string>($"Gaji - {employer.Value}", employer.Confidence, employer.Evidence)
            : new FieldGuess<string>("Gaji", Confidence.Low);

        var date = IndoDateParser.Find(ocr);
        if (!date.HasValue) warnings.Add("Tanggal tidak terbaca - dipakai tanggal hari ini.");

        return new TransactionDraft
        {
            Kind = Kind,
            Category = new FieldGuess<TransactionCategory>(TransactionCategory.Income, Confidence.High),
            Type = new FieldGuess<string>("Credit", Confidence.High),
            Date = date,
            Name = name,
            Total = total,
            RawText = ocr.AllText,
            Warnings = warnings
        };
    }
}
