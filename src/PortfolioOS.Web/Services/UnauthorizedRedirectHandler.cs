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

        if (response.StatusCode == HttpStatusCode.Unauthorized && !IsSelfHandled(request))
        {
            // Read before clearing: a demo session that has been wiped server-side deserves to
            // say so, rather than dumping the user on a bare login page mid-test.
            var session = await auth.GetSessionAsync();

            await auth.ClearTokenAsync();
            nav.NavigateTo(session.IsDemo ? "/login?notice=expired" : "/login", forceLoad: true);
        }

        return response;
    }

    /// <summary>
    /// Calls whose own caller already deals with a 401.
    /// </summary>
    /// <remarks>
    /// Login answers 401 for a wrong password - that is the page doing its job, and redirecting
    /// would replace its error message with a reload. Logout can answer 401 when the session it
    /// is ending has already expired, and the layout navigates itself afterwards; letting this
    /// handler race it would swallow the "your data was deleted" notice.
    /// </remarks>
    private static bool IsSelfHandled(HttpRequestMessage request)
    {
        var path = request.RequestUri?.AbsolutePath;
        if (path is null) return false;

        return path.EndsWith("/api/auth/login", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/api/auth/logout", StringComparison.OrdinalIgnoreCase);
    }
}
