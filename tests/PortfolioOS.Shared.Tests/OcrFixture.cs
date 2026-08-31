using PortfolioOS.Shared.Scanning;

namespace PortfolioOS.Shared.Tests;

/// <summary>
/// Builds OcrText the way a real engine would emit it, so the layout heuristics are exercised
/// rather than bypassed. Capture a genuine scan once, paste it in as a Column/Rows call, and
/// the parser can then be iterated on without a camera.
/// </summary>
internal static class OcrFixture
{
    private const double LineHeight = 20;
    private const double LineGap = 30;
    private const double CharWidth = 10;

    /// <summary>One line per string, stacked top to bottom in a single column.</summary>
    public static OcrText Column(params string[] lines)
    {
        var ocrLines = lines
            .Select((text, i) => new OcrLine(text, 0, i * LineGap, text.Length * CharWidth, LineHeight))
            .ToList();

        return new OcrText(string.Join("\n", lines), ocrLines);
    }

    /// <summary>
    /// A two-column layout: label on the left, value on the right, both on the same row but as
    /// separate lines - which is how receipts actually come back from OCR.
    /// </summary>
    public static OcrText Rows(params (string Left, string Right)[] rows)
    {
        var lines = new List<OcrLine>();
        var text = new List<string>();

        for (var i = 0; i < rows.Length; i++)
        {
            var (left, right) = rows[i];
            var y = i * LineGap;

            lines.Add(new OcrLine(left, 0, y, left.Length * CharWidth, LineHeight));
            if (!string.IsNullOrEmpty(right))
                lines.Add(new OcrLine(right, 300, y, right.Length * CharWidth, LineHeight));

            text.Add(string.IsNullOrEmpty(right) ? left : $"{left}  {right}");
        }

        return new OcrText(string.Join("\n", text), lines);
    }
}
