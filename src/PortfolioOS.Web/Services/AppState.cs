namespace PortfolioOS.Web.Services;

/// <summary>
/// Cross-page display state for the two app-bar buttons: the privacy blur and the IDR/USD
/// switch.
///
/// The API hands every monetary value over already converted to the portfolio's base
/// currency, IDR. Showing dollars is therefore a display concern only - this class divides
/// by the rate on the way to the screen and never the other way round, so there is exactly
/// one conversion in the system and it lives on the server.
/// </summary>
public class AppState
{
    /// <summary>Only used until the real rate arrives from the API, and never for a total
    /// the user acts on - <see cref="IsRateLoaded"/> says which state we are in.</summary>
    private const decimal PlaceholderRate = 16_000m;

    public bool PrivacyMode { get; private set; }

    /// <summary>IDR is the default because it is the portfolio's base currency.</summary>
    public bool ShowInIdr { get; private set; } = true;

    /// <summary>Rupiah per USD, as reported by the API.</summary>
    public decimal UsdIdrRate { get; private set; } = PlaceholderRate;

    public bool IsRateLoaded { get; private set; }

    /// <summary>False when the API served a cached rate because the provider was unreachable.</summary>
    public bool IsRateLive { get; private set; } = true;

    public DateTimeOffset RateAsOf { get; private set; }

    public event Action? OnChange;

    public void TogglePrivacy()
    {
        PrivacyMode = !PrivacyMode;
        OnChange?.Invoke();
    }

    public void ToggleCurrency()
    {
        ShowInIdr = !ShowInIdr;
        OnChange?.Invoke();
    }

    /// <summary>Called once the API's FX endpoint answers; re-renders everything on screen.</summary>
    public void SetRate(decimal usdIdr, DateTimeOffset asOf, bool isLive)
    {
        if (usdIdr <= 0) return;   // a zero rate would blank out every dollar figure

        UsdIdrRate   = usdIdr;
        RateAsOf     = asOf;
        IsRateLive   = isLive;
        IsRateLoaded = true;
        OnChange?.Invoke();
    }

    /// <param name="value">Amount in IDR, the base currency the API returns.</param>
    public string FormatValue(decimal value)
    {
        if (PrivacyMode) return "••••••";

        return ShowInIdr
            ? $"Rp {value:N0}"
            : $"${value / UsdIdrRate:N2}";
    }

    /// <param name="value">Amount in IDR, the base currency the API returns.</param>
    public string FormatCompact(decimal value)
    {
        if (PrivacyMode) return "••••";

        return ShowInIdr
            ? $"Rp {value / 1_000_000:N1}M"
            : $"${value / UsdIdrRate / 1_000:N1}K";
    }

    /// <summary>
    /// Formats an amount that is stored in the currency it was traded in rather than in base
    /// currency. Transaction totals are recorded as they happened - a US stock purchase is
    /// kept in dollars - so blanket "Rp" formatting turns an $11,000 trade into Rp 11.000.
    /// </summary>
    /// <param name="market">"US" means the amount is in USD; anything else means IDR.</param>
    public string FormatMarketValue(decimal value, string? market)
    {
        if (PrivacyMode) return "••••••";

        var isUsd = string.Equals(market, "US", StringComparison.OrdinalIgnoreCase);
        var inIdr = isUsd ? value * UsdIdrRate : value;

        return ShowInIdr
            ? $"Rp {inIdr:N0}"
            : $"${inIdr / UsdIdrRate:N2}";
    }

    /// <summary>Label for the currency toggle button - the currency currently on screen.</summary>
    public string CurrencyLabel => ShowInIdr ? "IDR" : "USD";
}
