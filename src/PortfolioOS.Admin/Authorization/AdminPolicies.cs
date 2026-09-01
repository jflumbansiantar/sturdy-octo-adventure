namespace PortfolioOS.Admin.Authorization;

/// <summary>
/// Seluruh permukaan service ini adalah area admin, jadi hanya ada satu policy.
/// Nilai scope harus sama persis dengan IdentityServerConfig.Scopes di PortfolioOS.Identity.
/// </summary>
public static class AdminPolicies
{
    /// <summary>Butuh scope <c>portfolioos.admin</c> sekaligus role <c>admin</c>.</summary>
    public const string AdminOnly = "admin.only";

    public static class Scopes
    {
        public const string Read = "portfolioos.read";
        public const string Write = "portfolioos.write";
        public const string Admin = "portfolioos.admin";
    }

    public const string AdminRole = "admin";
}
