namespace PortfolioOS.Mobile.Services;

public class AuthService
{
    private const string TokenKey = "portfolioos_token";

    public string? GetToken() => Preferences.Get(TokenKey, null);
    public bool IsAuthenticated() => !string.IsNullOrEmpty(GetToken());

    public void SetToken(string token) => Preferences.Set(TokenKey, token);
    public void ClearToken() => Preferences.Remove(TokenKey);
}
