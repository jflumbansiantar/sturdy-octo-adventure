namespace PortfolioOS.Web.Services;

/// <summary>
/// Cross-page display state. Every monetary value in the app is denominated in
/// IDR; this state only toggles the privacy blur and formats amounts as Rupiah.
/// </summary>
public class AppState
{
    public bool PrivacyMode { get; private set; }

    public event Action? OnChange;

    public void TogglePrivacy()
    {
        PrivacyMode = !PrivacyMode;
        OnChange?.Invoke();
    }

    /// <param name="value">Amount in IDR.</param>
    public string FormatValue(decimal value)
        => PrivacyMode ? "••••••" : $"Rp {value:N0}";

    /// <param name="value">Amount in IDR.</param>
    public string FormatCompact(decimal value)
        => PrivacyMode ? "••••" : $"Rp {value / 1_000_000:N1}M";
}
