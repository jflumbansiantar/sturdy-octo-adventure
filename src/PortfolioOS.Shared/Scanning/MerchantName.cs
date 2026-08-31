using System.Text.RegularExpressions;

namespace PortfolioOS.Shared.Scanning;

/// <summary>
/// Picks the description that goes into <c>Transaction.Name</c>. On a receipt that is the
/// shop name printed at the very top, above the address block.
/// </summary>
public static partial class MerchantName
{
    /// <summary>Header lines that are address or registration boilerplate, not the shop's name.</summary>
    [GeneratedRegex(@"^\s*(jl\.?|jalan|no\.?\s*\d|telp|tel\.?|phone|npwp|nib|kel\.?|kec\.?|blok|ruko|lantai|lt\.?\s*\d|www\.|http)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Boilerplate();

    /// <summary>Max length accepted by CreateTransactionValidator for Transaction.Name.</summary>
    public const int MaxLength = 255;

    /// <summary>
    /// The first header line that reads like a name. <paramref name="scanLines"/> caps how far
    /// down the page to look - the shop name is always in the first few lines, and searching
    /// further just finds item descriptions.
    /// </summary>
    public static FieldGuess<string> FromHeader(OcrText ocr, int scanLines = 4)
    {
        foreach (var line in ocr.InReadingOrder.Take(scanLines))
        {
            var candidate = Clean(line.Text);
            if (candidate is null) continue;

            return new FieldGuess<string>(candidate, Confidence.Medium, line.Text.Trim());
        }

        return FieldGuess<string>.Missing;
    }

    /// <summary>The value printed after a label such as "Penerima :" or "Nama Merchant".</summary>
    public static FieldGuess<string> AfterLabel(OcrText ocr, string labelPattern)
    {
        foreach (var line in ocr.InReadingOrder)
        {
            var match = Regex.Match(line.Text, labelPattern + @"\s*:?\s*(?<v>.*)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success) continue;

            // The value may sit on the same line, or in the right-hand column, or below.
            var inline = Clean(match.Groups["v"].Value);
            if (inline is not null)
                return new FieldGuess<string>(inline, Confidence.High, line.Text.Trim());

            var beside = ocr.SameRowAs(line).Where(n => n.X >= line.X)
                .Select(n => Clean(n.Text)).FirstOrDefault(v => v is not null);
            if (beside is not null)
                return new FieldGuess<string>(beside, Confidence.High, line.Text.Trim());
        }

        return FieldGuess<string>.Missing;
    }

    /// <summary>Null when the text is boilerplate, a bare number, or too short to be a name.</summary>
    private static string? Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var trimmed = Regex.Replace(text.Trim(), @"\s{2,}", " ");
        if (trimmed.Length < 3) return null;
        if (Boilerplate().IsMatch(trimmed)) return null;

        // Needs real letters - a row of digits is a date, phone number or amount.
        if (trimmed.Count(char.IsLetter) < 3) return null;

        return trimmed.Length > MaxLength ? trimmed[..MaxLength] : trimmed;
    }
}
