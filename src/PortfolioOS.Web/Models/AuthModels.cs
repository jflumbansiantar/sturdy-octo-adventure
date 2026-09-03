namespace PortfolioOS.Web.Models;

public record LoginRequest(string Username, string Password);

public record LoginResponse(string Token, DateTimeOffset ExpiresAt, bool IsDemo, string Username);

/// <summary>The fixed test-drive account, as advertised by the API.</summary>
public record DemoInfoModel(
    bool Enabled, string Username, string Password, int SessionMinutes, int IdleMinutes);

/// <summary>
/// Outcome of a login attempt, carrying the API's own message when it fails.
/// </summary>
/// <remarks>
/// A plain null was enough while "not 200" could only mean a wrong password. The demo login
/// adds a second failure the user can act on - every sandbox is taken - and it deserves its own
/// wording rather than being reported as bad credentials.
/// </remarks>
public record LoginOutcome(LoginResponse? Session, string? Error)
{
    public bool Succeeded => Session is not null;
}

/// <summary>What the current token says about the session behind it.</summary>
/// <param name="IsDemo">True for the shared test-drive account, whose data is deleted on logout.</param>
/// <param name="ExpiresAt">Null when the token carries no readable expiry.</param>
public record SessionInfo(bool IsDemo, DateTimeOffset? ExpiresAt);

public record SettingModel(string Key, string Value);
