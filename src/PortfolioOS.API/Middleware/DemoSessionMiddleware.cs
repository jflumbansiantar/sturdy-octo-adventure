using System.Text.Json;
using PortfolioOS.API.Auth;
using PortfolioOS.Infrastructure.Demo;

namespace PortfolioOS.API.Middleware;

/// <summary>
/// Points a demo request at its own sandbox schema, and rejects it if that sandbox is gone.
/// </summary>
/// <remarks>
/// Must run after <c>UseAuthentication</c> (it reads the token's claims) and before anything
/// resolves a DbContext, which is why it sits between authentication and authorisation. The
/// binding it does is what every query in the request then follows; a request that reaches a
/// controller with an unbound context reads and writes <c>public</c>.
/// <para>
/// The registry lookup on every request is not just bookkeeping - it is the check that stops a
/// still-valid token from outliving its schema. Without it, a purged session would keep working
/// and, with <c>public</c> on the connection's search path, would land on the owner's data.
/// </para>
/// </remarks>
public sealed class DemoSessionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        DemoSessionContext demoContext,
        DemoSessionManager sessions)
    {
        var user = context.User;

        if (user.Identity?.IsAuthenticated == true &&
            user.FindFirst(PortfolioClaims.Role)?.Value == PortfolioClaims.DemoRole)
        {
            if (!Guid.TryParse(user.FindFirst(PortfolioClaims.DemoSessionId)?.Value, out var sessionId))
            {
                await RejectAsync(context, "Demo token is missing its session id.");
                return;
            }

            // Also resets the idle timer, so an active tester is never purged out from under
            // themselves.
            var session = await sessions.TouchAsync(sessionId, context.RequestAborted);
            if (session is null)
            {
                await RejectAsync(context, "This demo session has ended and its data was deleted.");
                return;
            }

            demoContext.Bind(session.Id, session.Schema);
        }

        await next(context);
    }

    /// <summary>
    /// 401 rather than 403: to the client this is an expired session, and its existing
    /// unauthorized handler already sends the user back to the login page.
    /// </summary>
    private static Task RejectAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
    }
}
