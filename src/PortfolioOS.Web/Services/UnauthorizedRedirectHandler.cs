using System.Net;
using Microsoft.AspNetCore.Components;

namespace PortfolioOS.Web.Services;

/// <summary>
/// Sends the user back to the login page whenever the API rejects their token.
/// </summary>
/// <remarks>
/// The expiry check in <see cref="AuthService"/> catches the common case before any request is
/// made; this catches everything else - a token signed with a rotated secret, a clock far enough
/// out that the client thinks it is still valid, a session invalidated server-side. Without it
/// those all surface as an empty page and an unhandled-error bar.
/// <para>
/// Placed in the HTTP pipeline rather than in <see cref="PortfolioApiClient"/> because most of
/// its calls go through <c>GetFromJsonAsync</c>, which throws on 401 rather than handing back a
/// response to inspect.
/// </para>
/// </remarks>
public sealed class UnauthorizedRedirectHandler(AuthService auth, NavigationManager nav) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        // The login call answers 401 for a wrong password. That is the page doing its job, and
        // redirecting would replace its error message with a reload.
        var isLogin = request.RequestUri?.AbsolutePath.EndsWith("/api/auth/login", StringComparison.OrdinalIgnoreCase) == true;

        if (response.StatusCode == HttpStatusCode.Unauthorized && !isLogin)
        {
            await auth.ClearTokenAsync();
            nav.NavigateTo("/login", forceLoad: true);
        }

        return response;
    }
}
