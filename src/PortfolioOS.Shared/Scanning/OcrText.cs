namespace PortfolioOS.Shared.Scanning;

/// <summary>
/// One recognised line of text plus where it sits on the page.
/// Coordinates are pixels with the origin at the TOP-LEFT, matching ML Kit.
/// iOS Vision reports a bottom-left normalised origin, so its implementation
/// must convert before constructing this - otherwise every layout heuristic
/// in <see cref="AmountPicker"/> picks the wrong row on iOS only.
/// </summary>
public record OcrLine(string Text, double X, double Y, double Width, double Height)
{
    public double CenterY => Y + Height / 2;
    public double Right => X + Width;
}

/// <summary>Platform-neutral OCR output. The only thing the MAUI layer has to produce.</summary>
public record OcrText(string AllText, IReadOnlyList<OcrLine> Lines)
{
    public static readonly OcrText Empty = new(string.Empty, []);

    /// <summary>Lines ordered the way a human reads them: top to bottom, then left to right.</summary>
    public IEnumerable<OcrLine> InReadingOrder =>
        Lines.OrderBy(l => l.CenterY).ThenBy(l => l.X);

    /// <summary>
    /// Lines sharing a horizontal band with <paramref name="line"/>, itself excluded.
    /// Receipts put the label in a left column and the amount in a right column, and OCR
    /// engines frequently split that into two separate lines - this is how they get rejoined.
    /// </summary>
    public IEnumerable<OcrLine> SameRowAs(OcrLine line)
    {
        var tolerance = Math.Max(line.Height / 2, 1);
        return Lines
            .Where(l => !ReferenceEquals(l, line) && Math.Abs(l.CenterY - line.CenterY) <= tolerance)
            .OrderBy(l => l.X);
    }
}
