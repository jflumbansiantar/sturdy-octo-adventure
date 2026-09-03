using System.Text.Json;
using Microsoft.JSInterop;
using PortfolioOS.Web.Models;

namespace PortfolioOS.Web.Services;

public class AuthService(IJSRuntime js)
{
    private const string TokenKey = "portfolioos_token";

    /// <summary>Claim the API stamps on every token: <c>owner</c> or <c>demo</c>.</summary>
    private const string RoleClaim = "pos_role";

    private const string DemoRole = "demo";

    private string? _cachedToken;

    public async Task<string?> GetTokenAsync()
    {
        _cachedToken ??= await js.InvokeAsync<string?>("localStorage.getItem", TokenKey);
        return _cachedToken;
    }

    /// <summary>
    /// True only when a token is present *and* has not expired.
    /// </summary>
    /// <remarks>
    /// The expiry check matters: tokens last 24 hours, so simply having one stored is no
    /// guarantee it still works. Without this, an overnight-old session sails past the layout's
    /// auth gate, every API call comes back 401, and the user is left on an empty dashboard
    /// under Blazor's "An unhandled error has occurred" bar with no hint that they need to log
    /// in again.
    /// <para>
    /// This is a convenience check, not a security one - the API validates the signature. Its
    /// job is to send the user to the login page instead of a broken one.
    /// </para>
    /// </remarks>
    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetTokenAsync();
        if (string.IsNullOrEmpty(token)) return false;

        var expiry = ReadExpiry(token);

        // A token we cannot read is treated as usable and left for the API to reject: a parsing
        // quirk should not lock someone out of a session that would have worked.
        if (expiry is null) return true;

        // A minute of leeway absorbs small clock differences between browser and server.
        return expiry > DateTimeOffset.UtcNow.AddMinutes(-1);
    }

    /// <summary>
    /// What kind of session the stored token represents, read from the token itself.
    /// </summary>
    /// <remarks>
    /// Read from the JWT rather than kept alongside it in local storage so there is one source
    /// of truth. A separate "is demo" flag could survive a logout, or disagree with the token
    /// after one is replaced - and the whole demo warning hangs on this answer being right.
    /// </remarks>
    public async Task<SessionInfo> GetSessionAsync()
    {
        var token = await GetTokenAsync();
        if (string.IsNullOrEmpty(token)) return new SessionInfo(false, null);

        using var payload = ReadPayload(token);
        if (payload is null) return new SessionInfo(false, null);

        var isDemo = payload.RootElement.TryGetProperty(RoleClaim, out var role) &&
                     role.ValueKind == JsonValueKind.String &&
                     role.GetString() == DemoRole;

        return new SessionInfo(isDemo, ReadExpiry(payload));
    }

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

    private static DateTimeOffset? ReadExpiry(string token)
    {
        using var payload = ReadPayload(token);
        return payload is null ? null : ReadExpiry(payload);
    }

    private static DateTimeOffset? ReadExpiry(JsonDocument payload) =>
        payload.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;

    /// <summary>Decodes a JWT's claims without validating its signature.</summary>
    private static JsonDocument? ReadPayload(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

            return JsonDocument.Parse(Convert.FromBase64String(payload));
        }
        catch
        {
            return null;   // malformed - let the API be the judge
        }
    }
}
