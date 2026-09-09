using System.Text.RegularExpressions;
using PortfolioOS.Shared.Scanning;

namespace PortfolioOS.Application.Chat.Slots;

/// <summary>A date range pulled out of a question, with a label to echo back in the answer.</summary>
public sealed record DatePeriod(DateOnly From, DateOnly To, string Label);

/// <summary>
/// Reads the time expressions people actually use — "bulan lalu", "3 bulan terakhir", "januari",
/// "this month" — into a concrete range.
/// </summary>
/// <remarks>
/// Only relative and month-name phrasings are handled here. Explicit calendar dates fall through
/// to <see cref="IndoDateParser.ParseLine"/>, which already knows Indonesian month names and the
/// day-first convention, and is covered by its own tests.
/// <para>
/// <c>today</c> is a parameter rather than <c>DateTime.Now</c> so the behaviour is testable and
/// does not change meaning depending on when the suite runs.
/// </para>
/// </remarks>
public static partial class RelativePeriodParser
{
    [GeneratedRegex(@"\b(\d{1,2})\s*(bulan|month)s?\s*(terakhir|lalu|last|ago)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LastNMonths();

    [GeneratedRegex(@"\b(\d{1,2})\s*(hari|day)s?\s*(terakhir|lalu|last|ago)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LastNDays();

    private static readonly Dictionary<string, int> MonthNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["januari"] = 1, ["january"] = 1,
        ["februari"] = 2, ["february"] = 2,
        ["maret"] = 3, ["march"] = 3,
        ["april"] = 4,
        ["mei"] = 5, ["may"] = 5,
        ["juni"] = 6, ["june"] = 6,
        ["juli"] = 7, ["july"] = 7,
        ["agustus"] = 8, ["august"] = 8,
        ["september"] = 9,
        ["oktober"] = 10, ["october"] = 10,
        ["november"] = 11,
        ["desember"] = 12, ["december"] = 12,
    };

    /// <summary>
    /// Returns the period a question refers to, or null when it names no time at all —
    /// which callers should treat as "no filter", not as "today".
    /// </summary>
    public static DatePeriod? Parse(string question, DateOnly today)
    {
        var q = question.ToLowerInvariant();

        if (Contains(q, "hari ini", "today"))
            return new DatePeriod(today, today, "hari ini");

        if (Contains(q, "kemarin dulu"))
            return Day(today.AddDays(-2), "kemarin dulu");

        if (Contains(q, "kemarin", "yesterday") && !Contains(q, "bulan kemarin", "minggu kemarin"))
            return Day(today.AddDays(-1), "kemarin");

        if (Contains(q, "minggu ini", "this week", "pekan ini"))
        {
            var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            return new DatePeriod(monday, today, "minggu ini");
        }

        if (Contains(q, "minggu lalu", "minggu kemarin", "last week", "pekan lalu"))
        {
            var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)).AddDays(-7);
            return new DatePeriod(monday, monday.AddDays(6), "minggu lalu");
        }

        if (Contains(q, "bulan lalu", "bulan kemarin", "last month"))
        {
            var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
            return new DatePeriod(first, first.AddMonths(1).AddDays(-1), "bulan lalu");
        }

        if (Contains(q, "bulan ini", "this month"))
            return new DatePeriod(new DateOnly(today.Year, today.Month, 1), today, "bulan ini");

        if (Contains(q, "tahun lalu", "tahun kemarin", "last year"))
        {
            var first = new DateOnly(today.Year - 1, 1, 1);
            return new DatePeriod(first, new DateOnly(today.Year - 1, 12, 31), "tahun lalu");
        }

        if (Contains(q, "tahun ini", "this year", "ytd", "year to date"))
            return new DatePeriod(new DateOnly(today.Year, 1, 1), today, "tahun ini");

        if (LastNMonths().Match(q) is { Success: true } months)
        {
            var n = int.Parse(months.Groups[1].Value);
            return new DatePeriod(today.AddMonths(-n), today, $"{n} bulan terakhir");
        }

        if (LastNDays().Match(q) is { Success: true } days)
        {
            var n = int.Parse(days.Groups[1].Value);
            return new DatePeriod(today.AddDays(-n), today, $"{n} hari terakhir");
        }

        // A bare month name means that month in the current year - or last year if naming it
        // would otherwise point at a month that has not happened yet.
        //
        // The month named FIRST wins, rather than whichever the dictionary happens to yield:
        // enumeration order is not part of Dictionary's contract, and "maret dan januari"
        // should resolve to maret.
        (int Index, int Month, string Name)? earliest = null;
        foreach (var (name, month) in MonthNames)
        {
            var hit = Regex.Match(q, $@"\b{Regex.Escape(name)}\b", RegexOptions.IgnoreCase);
            if (!hit.Success) continue;
            if (AmbiguousInEnglish.Contains(name) && !HasNumberNearby(q, hit)) continue;

            if (earliest is null || hit.Index < earliest.Value.Index)
                earliest = (hit.Index, month, name);
        }

        if (earliest is { } found)
        {
            var year = found.Month > today.Month ? today.Year - 1 : today.Year;
            var first = new DateOnly(year, found.Month, 1);
            return new DatePeriod(first, first.AddMonths(1).AddDays(-1), $"{found.Name} {year}");
        }

        // Last resort: an explicit date written out in the question.
        var explicitDate = IndoDateParser.ParseLine(question);
        if (explicitDate.HasValue)
            return Day(explicitDate.Value, ChatFormat.Date(explicitDate.Value));

        return null;
    }

    /// <summary>
    /// English month names that are ordinary words first and months second.
    /// </summary>
    /// <remarks>
    /// "how much may I spend" was being read as May 2026, silently narrowing a whole year of
    /// spending to one month. These count as months only when a number sits beside them; the
    /// Indonesian names carry no such ambiguity and are matched unconditionally.
    /// </remarks>
    private static readonly HashSet<string> AmbiguousInEnglish =
        new(StringComparer.OrdinalIgnoreCase) { "may", "march", "august" };

    /// <summary>True when a digit sits close enough to read as a day or year for this word.</summary>
    private static bool HasNumberNearby(string text, Match match)
    {
        var start = Math.Max(0, match.Index - 6);
        var end = Math.Min(text.Length, match.Index + match.Length + 6);
        return Regex.IsMatch(text[start..end], "[0-9]");
    }

    private static DatePeriod Day(DateOnly d, string label) => new(d, d, label);

    private static bool Contains(string haystack, params string[] needles) =>
        needles.Any(n => haystack.Contains(n, StringComparison.Ordinal));
}
