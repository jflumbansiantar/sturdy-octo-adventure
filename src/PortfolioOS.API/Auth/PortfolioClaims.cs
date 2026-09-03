namespace PortfolioOS.API.Auth;

/// <summary>
/// The non-standard claims this API puts in its tokens.
/// </summary>
/// <remarks>
/// Short, unmapped names rather than the <c>ClaimTypes</c> URIs: the Blazor client reads these
/// straight out of the JWT payload to decide whether to show the demo banner, and a
/// <c>schemas.microsoft.com/...</c> key would be awkward there for no gain. Nothing here is
/// used for authorisation policies, so no role mapping is needed.
/// </remarks>
public static class PortfolioClaims
{
    /// <summary>Either <see cref="OwnerRole"/> or <see cref="DemoRole"/>.</summary>
    public const string Role = "pos_role";

    /// <summary>Id of the demo session, and therefore of the sandbox schema behind it.</summary>
    /// <remarks>
    /// The schema name itself is deliberately not in the token. Sending an id means the server
    /// looks the session up in its registry on every request, so a token whose sandbox has
    /// already been dropped stops working the moment it is dropped - rather than silently
    /// falling through to the owner's data.
    /// </remarks>
    public const string DemoSessionId = "pos_sid";

    public const string OwnerRole = "owner";
    public const string DemoRole = "demo";
}
