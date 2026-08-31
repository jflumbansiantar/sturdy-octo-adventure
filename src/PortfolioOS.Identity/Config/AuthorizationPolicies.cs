namespace PortfolioOS.Identity.Config;

/// <summary>Nama policy authorization milik service identity.</summary>
public static class AuthorizationPolicies
{
    /// <summary>Endpoint manajemen user — butuh scope admin sekaligus role admin.</summary>
    public const string AdminApi = "identity.admin";
}
