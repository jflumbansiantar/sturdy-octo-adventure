using System.Text.RegularExpressions;

namespace PortfolioOS.Shared.Scanning;

/// <summary>
/// Decides which number on the page is the transaction amount.
///
/// The naive answer - take the largest number - is wrong on almost every receipt, because
/// TUNAI (cash tendered) is normally larger than the total, and a card number or NPWP is
/// larger still. So the label is what is searched for, and the amount is read off the row
/// the label sits on, using the line coordinates: receipts print the label in a left column
/// and the value in a right one, and OCR engines usually emit those as two separate lines.
/// </summary>
public static partial class AmountPicker
{
    /// <summary>Labels that mark the final payable amount. Beats <see cref="PlainTotal"/>.</summary>
    [GeneratedRegex(@"\b(grand\s*total|total\s*(bayar|belanja|akhir|tagihan|transaksi)|jumlah\s*bayar|net(to)?\s*(total)?|total\s*due|amount\s*(paid|due))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GrandTotal();

    /// <summary>Weaker labels, used only when no grand total is present.</summary>
    [GeneratedRegex(@"\b(total|jumlah|nominal|tagihan|amount|harga\s*total)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PlainTotal();

    /// <summary>
    /// Lines that look like a total but are not it. TUNAI/KEMBALI are the classic trap -
    /// pay 50.000 for a 27.750 bill and the two largest numbers on the receipt are both wrong.
    /// </summary>
    [GeneratedRegex(@"\b(sub\s*total|total\s*(item|qty|barang|qty\.?|kuantitas|diskon|hemat|poin)|tunai|cash|kembali(an)?|change|ppn|pajak|tax|diskon|disc|hemat|poin|point|saving|dpp|kartu|card|debit\s*card|nomor|no\.?\s*ref)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NotATotal();

    public static FieldGuess<decimal> FindTotal(OcrText ocr)
    {
        if (ocr.Lines.Count == 0) return FieldGuess<decimal>.Missing;

        // Bottom-most match wins within a tier: the grand total is printed after the
        // line items, and a running "Total" near the top is a column header.
        var byTier = ocr.InReadingOrder
            .Where(l => !NotATotal().IsMatch(l.Text))
            .Select(l => (Line: l, Tier: GrandTotal().IsMatch(l.Text) ? 1 : PlainTotal().IsMatch(l.Text) ? 2 : 0))
            .Where(x => x.Tier > 0)
            .ToList();

        foreach (var tier in (int[])[1, 2])
        {
            foreach (var (line, _) in byTier.Where(x => x.Tier == tier).Reverse())
            {
                var amount = AmountFor(line, ocr);
                if (amount is not null)
                    return new FieldGuess<decimal>(amount.Value, tier == 1 ? Confidence.High : Confidence.Medium, line.Text.Trim());
            }
        }

        // Nothing labelled. E-wallet screenshots often show the amount alone in a large font,
        // so the biggest number is a reasonable guess - but the caller must warn the user.
        var largest = ocr.Lines.SelectMany(l => MoneyParser.ExtractAll(l.Text))
            .DefaultIfEmpty()
            .Max();

        return largest > 0
            ? new FieldGuess<decimal>(largest, Confidence.Low, "no total label found")
            : FieldGuess<decimal>.Missing;
    }

    /// <summary>
    /// The amount sitting on the row of an arbitrary label - "Harga", "Nominal", "Angsuran".
    /// <paramref name="fromBottom"/> selects the last match instead of the first; totals live
    /// below the line items, whereas per-field labels are read top-down.
    /// </summary>
    public static FieldGuess<decimal> ForLabel(OcrText ocr, string labelPattern, bool fromBottom = false)
    {
        var lines = ocr.InReadingOrder.ToList();
        if (fromBottom) lines.Reverse();

        foreach (var line in lines)
        {
            if (!Regex.IsMatch(line.Text, labelPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                continue;

            var amount = AmountFor(line, ocr);
            if (amount is not null)
                return new FieldGuess<decimal>(amount.Value, Confidence.High, line.Text.Trim());
        }

        return FieldGuess<decimal>.Missing;
    }

    /// <summary>
    /// The amount belonging to a label line: on the line itself, else in the same horizontal
    /// band to its right, else on the line directly below.
    /// </summary>
    private static decimal? AmountFor(OcrLine label, OcrText ocr)
    {
        var onLine = MoneyParser.LastIn(label.Text);
        if (onLine is not null) return onLine;

        foreach (var neighbour in ocr.SameRowAs(label).Where(n => n.X >= label.X).Reverse())
        {
            var amount = MoneyParser.LastIn(neighbour.Text);
            if (amount is not null) return amount;
        }

        var below = ocr.InReadingOrder.FirstOrDefault(l => l.CenterY > label.CenterY + label.Height / 2);
        return below is null ? null : MoneyParser.LastIn(below.Text);
    }
}
