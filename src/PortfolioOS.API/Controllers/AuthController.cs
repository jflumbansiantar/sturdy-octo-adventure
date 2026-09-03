using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using PortfolioOS.API.Auth;
using PortfolioOS.Infrastructure.Demo;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PortfolioOS.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    IConfiguration config,
    DemoSessionManager demoSessions,
    ILogger<AuthController> logger) : ControllerBase
{
    public record LoginRequest(string Username, string Password);

    public record LoginResponse(string Token, DateTimeOffset ExpiresAt, bool IsDemo, string Username);

    /// <param name="Enabled">False hides the test-drive card on the login page entirely.</param>
    /// <param name="SessionMinutes">How long a sandbox lives before it is deleted.</param>
    /// <param name="IdleMinutes">How long an abandoned sandbox survives with no requests.</param>
    public record DemoInfoResponse(
        bool Enabled, string Username, string Password, int SessionMinutes, int IdleMinutes);

    /// <summary>
    /// The fixed test-drive credentials, for the login page to display and submit.
    /// </summary>
    /// <remarks>
    /// Anonymous, and it really does hand out the password. That is the feature: the account is
    /// a shared front door to a throwaway sandbox, printed on the page so a visitor has nothing
    /// to type and nothing to change. Knowing it grants no more than clicking the button does -
    /// every login behind it gets its own empty schema and can never reach the owner's data.
    /// </remarks>
    [HttpGet("demo")]
    public IActionResult DemoInfo()
    {
        var demo = demoSessions.Options;

        // Withheld when the feature is off. The credentials are harmless while a sandbox is the
        // only thing they open, but a stack with demo mode disabled has no reason to publish a
        // configured password to anonymous callers - least of all one an operator may have
        // reused from somewhere else.
        if (!demoSessions.IsEnabled)
            return Ok(new DemoInfoResponse(false, "", "", 0, 0));

        return Ok(new DemoInfoResponse(
            true,
            demo.Username,
            demo.Password,
            demo.SessionMinutes,
            demo.IdleMinutes));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var demo = demoSessions.Options;

        if (demoSessions.IsEnabled &&
            req.Username == demo.Username &&
            req.Password == demo.Password)
        {
            return await StartDemoAsync(req.Username, ct);
        }

        var expectedUsername = config["Auth:Username"];
        var expectedPassword = config["Auth:Password"];

        // Unset credentials must never authenticate anyone. [ApiController] model validation
        // already rejects a null or empty username with a 400 before this runs, so this is a
        // second lock rather than the only one - but the comparisons below read `null == null`
        // as a match, and a deployment that omits the Auth section (the demo stack has no use
        // for an owner login) should not be relying on framework behaviour to stay shut.
        if (string.IsNullOrEmpty(expectedUsername) || string.IsNullOrEmpty(expectedPassword))
            return Unauthorized(new { error = "Invalid credentials" });

        if (req.Username != expectedUsername || req.Password != expectedPassword)
            return Unauthorized(new { error = "Invalid credentials" });

        var expires = DateTimeOffset.UtcNow.AddHours(int.Parse(config["Jwt:ExpiryHours"] ?? "24"));
        var token = IssueToken(req.Username, expires, [
            new Claim(PortfolioClaims.Role, PortfolioClaims.OwnerRole)
        ]);

        return Ok(new LoginResponse(token, expires, IsDemo: false, req.Username));
    }

    /// <summary>
    /// Ends the caller's session. For a demo account this deletes everything it wrote.
    /// </summary>
    /// <remarks>
    /// The token itself is not revoked - it is a stateless JWT - but for a demo session that
    /// makes no difference: the sandbox it names is gone, and the demo middleware turns away
    /// any request whose session is no longer in the registry.
    /// </remarks>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var isDemo = User.FindFirst(PortfolioClaims.Role)?.Value == PortfolioClaims.DemoRole;
        if (!isDemo)
            return Ok(new { dataDeleted = false });

        if (!Guid.TryParse(User.FindFirst(PortfolioClaims.DemoSessionId)?.Value, out var sessionId))
            return Ok(new { dataDeleted = false });

        var deleted = await demoSessions.EndAsync(sessionId, ct);
        return Ok(new { dataDeleted = deleted });
    }

    private async Task<IActionResult> StartDemoAsync(string username, CancellationToken ct)
    {
        DemoSessionRecord session;
        try
        {
            session = await demoSessions.StartAsync(ct);
        }
        catch (DemoCapacityException ex)
        {
            logger.LogInformation("Demo login refused: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                error = $"Semua {ex.Limit} sesi test drive sedang dipakai. Coba lagi beberapa menit lagi."
            });
        }

        // The token expires exactly when the sandbox does, so a client that trusts the token's
        // own `exp` and the server's registry can never disagree about when the demo is over.
        var token = IssueToken(username, session.ExpiresAt, [
            new Claim(PortfolioClaims.Role, PortfolioClaims.DemoRole),
            new Claim(PortfolioClaims.DemoSessionId, session.Id.ToString())
        ]);

        return Ok(new LoginResponse(token, session.ExpiresAt, IsDemo: true, username));
    }

    private string IssueToken(string username, DateTimeOffset expires, IEnumerable<Claim> extraClaims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim> { new(ClaimTypes.Name, username) };
        claims.AddRange(extraClaims);

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
