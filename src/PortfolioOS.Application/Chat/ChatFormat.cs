using System.Globalization;

namespace PortfolioOS.Application.Chat;

/// <summary>
/// Number formatting for answer text.
/// </summary>
/// <remarks>
/// The currency split this app maintains everywhere applies here too: anything summed across
/// holdings has already been converted to IDR by the query layer, so it is formatted with
/// <see cref="Idr"/>; a per-unit price stays in the currency of its own exchange and must be
/// formatted with <see cref="Money"/> passing that currency. Formatting a converted total as
/// USD (or a US share price as rupiah) is the specific bug this separation prevents.
/// </remarks>
public static class ChatFormat
{
    private static readonly CultureInfo Id = new("id-ID");

    /// <summary>Base-currency amount. Rupiah has no meaningful minor unit at these magnitudes.</summary>
    public static string Idr(decimal value) => "Rp " + Math.Round(value).ToString("N0", Id);

    public static string Money(decimal value, string? currency) =>
        string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase)
            ? "USD " + value.ToString("N2", CultureInfo.InvariantCulture)
            : Idr(value);

    /// <summary>
    /// Signed percentage for a *change*, e.g. "+12,34%". The sign is the point — it says
    /// which way something moved.
    /// </summary>
    public static string Pct(decimal value) =>
        (value >= 0 ? "+" : "") + value.ToString("N2", Id) + "%";

    /// <summary>
    /// Unsigned percentage for a *level* — an interest rate, a share of a total. A rate is not
    /// a movement, and "+27,00% per tahun" reads like the rate went up by 27 points.
    /// </summary>
    public static string Rate(decimal value) => value.ToString("N2", Id) + "%";

    /// <summary>Signed money, so a loss reads as a loss rather than a bare number.</summary>
    public static string SignedIdr(decimal value) =>
        (value >= 0 ? "+" : "-") + Idr(Math.Abs(value));

    public static string Units(decimal value) => value.ToString("0.########", Id);

    public static string Date(DateOnly date) => date.ToString("d MMMM yyyy", Id);
}
