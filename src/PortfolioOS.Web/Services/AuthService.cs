using Microsoft.JSInterop;

namespace PortfolioOS.Web.Services;

public class AuthService(IJSRuntime js)
{
    private const string TokenKey = "portfolioos_token";
    private string? _cachedToken;

    public async Task<string?> GetTokenAsync()
    {
        _cachedToken ??= await js.InvokeAsync<string?>("localStorage.getItem", TokenKey);
        return _cachedToken;
    }

    public async Task<bool> IsAuthenticatedAsync()
        => !string.IsNullOrEmpty(await GetTokenAsync());

    public async Task SetTokenAsync(string token)
    {
        _cachedToken = token;
        await js.InvokeVoidAsync("localStorage.setItem", TokenKey, token);
    }

    public async Task ClearTokenAsync()
    {
        _cachedToken = null;
        await js.InvokeVoidAsync("localStorage.removeItem", TokenKey);
    }
}
