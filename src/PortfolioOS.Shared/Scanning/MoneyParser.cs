using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PortfolioOS.Shared.Scanning;

/// <summary>
/// Reads money the way Indonesian documents write it: "Rp 1.250.000", "Rp1.250.000,00",
/// "IDR 1.250.000,-". Dot is the thousands separator and comma the decimal one - the exact
/// opposite of the invariant culture - and plenty of apps print the US form instead, so the
/// separators have to be judged per token rather than assumed.
/// </summary>
public static partial class MoneyParser
{
    /// <summary>
    /// Runs of digits and separators, optionally introduced by a currency marker.
    /// The marker is part of the pattern because "Rp1.250.000" has no space to tokenise on,
    /// and the leading guard would otherwise reject a number glued to a letter.
    /// Letters that OCR confuses with digits are allowed inside the run and repaired later.
    /// </summary>
    [GeneratedRegex(@"(?:(?:rp|idr|usd)\.?\s*|\$\s*|(?<![a-z0-9]))(?<n>[0-9olisbz|]+(?:[.,][0-9olisbz|]+)*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MoneyToken();

    /// <summary>
    /// Currency markers, stripped before the OCR digit repair runs. "IDR" is made of letters
    /// the repair would happily turn into digits, which reads "IDR 1.250.000" as 101,250,000.
    /// </summary>
    [GeneratedRegex(@"\b(?:rp|idr|usd|rm)\.?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CurrencyMarker();

    /// <summary>Characters OCR routinely mistakes for digits on low-contrast thermal paper.</summary>
    private static char FixDigit(char c) => c switch
    {
        'O' or 'o' or 'D' => '0',
        'l' or 'I' or 'i' or '|' => '1',
        'S' or 's' => '5',
        'B' => '8',
        'Z' or 'z' => '2',
        _ => c
    };

    public static bool TryParse(string? token, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(token)) return false;

        var cleaned = Clean(token);
        if (cleaned is null) return false;

        return decimal.TryParse(cleaned, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Every amount on a line, left to right.</summary>
    public static IReadOnlyList<decimal> ExtractAll(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var found = new List<decimal>();
        foreach (Match m in MoneyToken().Matches(text))
        {
            var raw = m.Groups["n"].Value;

            // A run made purely of OCR-confusable letters ("OSI") is a word, not a number.
            if (!raw.Any(char.IsDigit)) continue;

            if (TryParse(raw, out var v)) found.Add(v);
        }
        return found;
    }

    /// <summary>
    /// The rightmost amount on a line. Receipts print the value at the end of the row,
    /// after the label and any quantity, so this is the one that usually matters.
    /// </summary>
    public static decimal? LastIn(string? text)
    {
        var all = ExtractAll(text);
        return all.Count == 0 ? null : all[^1];
    }

    /// <summary>Rewrites a token into something invariant-culture decimal.TryParse accepts.</summary>
    private static string? Clean(string token)
    {
        // Without a real digit there is nothing to read, and the repair below would otherwise
        // turn a plain word into a number - "TOTAL" has an O in it.
        if (!token.Any(char.IsDigit)) return null;

        var stripped = CurrencyMarker().Replace(token, " ");
        var sb = new StringBuilder(stripped.Length);
        var negative = stripped.TrimStart().StartsWith('-');

        foreach (var ch in stripped)
        {
            if (char.IsWhiteSpace(ch) || ch == '\u00A0') continue;
            var fixedCh = FixDigit(ch);
            if (char.IsDigit(fixedCh) || fixedCh is '.' or ',') sb.Append(fixedCh);
        }

        var s = sb.ToString().Trim('.', ',');   // drops the trailing ",-" idiom
        if (s.Length == 0) return null;

        var lastDot = s.LastIndexOf('.');
        var lastComma = s.LastIndexOf(',');
        var lastSep = Math.Max(lastDot, lastComma);

        if (lastSep < 0) return negative ? "-" + s : s;

        // The digits after the final separator decide what that separator is: exactly three
        // means it closed a thousands group ("1.250.000"), anything else means it is decimal
        // ("1.250,50" and, for a stray OCR split, "1.234.56").
        var tailLength = s.Length - lastSep - 1;
        var decimalAt = tailLength == 3 ? -1 : lastSep;

        var result = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] is '.' or ',')
            {
                if (i == decimalAt) result.Append('.');
                continue;   // every other separator is grouping - drop it
            }
            result.Append(s[i]);
        }

        var final = result.ToString();
        if (final.Length == 0) return null;
        return negative ? "-" + final : final;
    }
}
