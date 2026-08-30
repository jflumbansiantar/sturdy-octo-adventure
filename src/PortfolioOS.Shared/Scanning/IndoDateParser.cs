using System.Text.RegularExpressions;

namespace PortfolioOS.Shared.Scanning;

/// <summary>
/// Finds the transaction date in Indonesian documents. The dangerous case is "04/03/2026":
/// day-first here, month-first in the US, and getting it backwards silently files the
/// transaction in the wrong month. Day-first is assumed whenever the digits allow both.
/// </summary>
public static partial class IndoDateParser
{
    [GeneratedRegex(@"(?<d>\d{1,2})\s*[/\-.]\s*(?<m>\d{1,2})\s*[/\-.]\s*(?<y>\d{2,4})", RegexOptions.CultureInvariant)]
    private static partial Regex NumericDate();

    [GeneratedRegex(@"(?<y>\d{4})\s*[/\-.]\s*(?<m>\d{1,2})\s*[/\-.]\s*(?<d>\d{1,2})", RegexOptions.CultureInvariant)]
    private static partial Regex IsoDate();

    [GeneratedRegex(@"(?<d>\d{1,2})\s+(?<mon>[A-Za-z]{3,9})\.?\s+(?<y>\d{2,4})", RegexOptions.CultureInvariant)]
    private static partial Regex NamedMonthDate();

    [GeneratedRegex(@"\b(tgl|tanggal|date|waktu|tgl\.?\s*transaksi)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DateLabel();

    /// <summary>Indonesian month names and the abbreviations that actually appear in print,
    /// plus the English ones some banking apps emit.</summary>
    private static readonly Dictionary<string, int> Months = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jan"] = 1, ["januari"] = 1, ["january"] = 1,
        ["feb"] = 2, ["februari"] = 2, ["february"] = 2, ["peb"] = 2,
        ["mar"] = 3, ["maret"] = 3, ["march"] = 3,
        ["apr"] = 4, ["april"] = 4,
        ["mei"] = 5, ["may"] = 5,
        ["jun"] = 6, ["juni"] = 6, ["june"] = 6,
        ["jul"] = 7, ["juli"] = 7, ["july"] = 7,
        ["agu"] = 8, ["agt"] = 8, ["ags"] = 8, ["agustus"] = 8, ["aug"] = 8, ["august"] = 8,
        ["sep"] = 9, ["sept"] = 9, ["september"] = 9,
        ["okt"] = 10, ["oct"] = 10, ["oktober"] = 10, ["october"] = 10,
        ["nov"] = 11, ["november"] = 11, ["nop"] = 11,
        ["des"] = 12, ["dec"] = 12, ["desember"] = 12, ["december"] = 12
    };

    /// <summary>
    /// Scans lines in reading order and returns the first plausible date, preferring one
    /// that sits on a labelled line ("Tgl : ..."). Receipts print several dates - print time,
    /// card expiry, promo period - and the labelled one is the transaction's own.
    /// </summary>
    public static FieldGuess<DateOnly> Find(OcrText ocr)
    {
        FieldGuess<DateOnly>? unlabelled = null;

        foreach (var line in ocr.InReadingOrder)
        {
            var guess = ParseLine(line.Text);
            if (!guess.HasValue) continue;

            if (DateLabel().IsMatch(line.Text))
                return guess with { Confidence = Confidence.High, Evidence = line.Text.Trim() };

            unlabelled ??= guess with { Evidence = line.Text.Trim() };
        }

        return unlabelled ?? FieldGuess<DateOnly>.Missing;
    }

    /// <summary>Parses the first date found anywhere in a single string.</summary>
    public static FieldGuess<DateOnly> ParseLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return FieldGuess<DateOnly>.Missing;

        // ISO first: "2026-04-21" would otherwise be read as day 20, month 26 by the numeric rule.
        foreach (Match m in IsoDate().Matches(text))
        {
            if (TryBuild(int.Parse(m.Groups["d"].Value), int.Parse(m.Groups["m"].Value),
                    int.Parse(m.Groups["y"].Value), out var iso))
                return new FieldGuess<DateOnly>(iso, Confidence.Medium, m.Value);
        }

        foreach (Match m in NamedMonthDate().Matches(text))
        {
            if (!Months.TryGetValue(m.Groups["mon"].Value, out var month)) continue;
            if (TryBuild(int.Parse(m.Groups["d"].Value), month, int.Parse(m.Groups["y"].Value), out var named))
                return new FieldGuess<DateOnly>(named, Confidence.Medium, m.Value);
        }

        foreach (Match m in NumericDate().Matches(text))
        {
            var a = int.Parse(m.Groups["d"].Value);
            var b = int.Parse(m.Groups["m"].Value);
            var y = int.Parse(m.Groups["y"].Value);

            // a > 12 settles it; b > 12 means the document is month-first after all.
            // When both fit a month, Indonesian convention wins - flagged Low so the user checks.
            var (day, month, conf) = a > 12 ? (a, b, Confidence.Medium)
                : b > 12 ? (b, a, Confidence.Medium)
                : (a, b, Confidence.Low);

            if (TryBuild(day, month, y, out var numeric))
                return new FieldGuess<DateOnly>(numeric, conf, m.Value);
        }

        return FieldGuess<DateOnly>.Missing;
    }

    private static bool TryBuild(int day, int month, int year, out DateOnly date)
    {
        date = default;
        if (year < 100) year += 2000;
        if (year is < 2000 or > 2100 || month is < 1 or > 12 || day < 1) return false;
        if (day > DateTime.DaysInMonth(year, month)) return false;

        date = new DateOnly(year, month, day);
        return true;
    }
}
