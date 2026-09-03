namespace PortfolioOS.Infrastructure.Demo;

/// <summary>
/// Settings for the public "test drive" account, bound from the <c>Demo</c> configuration
/// section.
/// </summary>
/// <remarks>
/// The credentials live here rather than being generated because the whole point is that a
/// visitor can be handed one fixed account: it is printed on the login page, it never changes,
/// and there is no sign-up to go wrong. Isolation is not what the password buys - every demo
/// login gets its own database schema, so sharing the password costs nothing.
/// </remarks>
public sealed class DemoOptions
{
    public const string SectionName = "Demo";

    /// <summary>
    /// Turns the demo login off entirely. The login page hides its card when false.
    /// </summary>
    /// <remarks>
    /// Off unless a deployment asks for it. A public account that spends database resources
    /// should never appear because someone upgraded without reading the release notes; the
    /// repository's own appsettings.json and compose file switch it on explicitly.
    /// </remarks>
    public bool Enabled { get; set; }

    public string Username { get; set; } = "demo";

    public string Password { get; set; } = "demo123";

    /// <summary>Hard ceiling on one session. The JWT expires with it.</summary>
    public int SessionMinutes { get; set; } = 60;

    /// <summary>
    /// A session with no request for this long is treated as abandoned and purged. Testers
    /// close the tab far more often than they press logout, so this - not the logout button -
    /// is what actually keeps the database clean.
    /// </summary>
    public int IdleMinutes { get; set; } = 20;

    /// <summary>
    /// How many sandboxes may exist at once. Each is a real schema holding real tables, so
    /// this is the knob that stops a shared demo link from filling the disk.
    /// </summary>
    public int MaxConcurrentSessions { get; set; } = 5;
}
