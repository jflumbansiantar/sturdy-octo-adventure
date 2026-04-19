namespace PortfolioOS.Web.Services;

public class AppState
{
    public bool ShowInIDR { get; private set; }
    public decimal UsdIdrRate { get; set; } = 16000m;
    public bool PrivacyMode { get; private set; }

    public event Action? OnChange;

    public void ToggleCurrency()
    {
        ShowInIDR = !ShowInIDR;
        OnChange?.Invoke();
    }

    public void TogglePrivacy()
    {
        PrivacyMode = !PrivacyMode;
        OnChange?.Invoke();
    }

    public string FormatValue(decimal usdValue)
    {
        if (PrivacyMode) return "••••••";
        if (ShowInIDR)
            return $"Rp {usdValue * UsdIdrRate:N0}";
        return $"${usdValue:N2}";
    }

    public string FormatCompact(decimal usdValue)
    {
        if (PrivacyMode) return "••••";
        var v = ShowInIDR ? usdValue * UsdIdrRate : usdValue;
        return ShowInIDR ? $"Rp {v / 1_000_000:N1}M" : $"${v:N2}";
    }
}
