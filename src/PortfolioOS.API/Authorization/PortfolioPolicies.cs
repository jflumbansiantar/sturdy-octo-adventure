namespace PortfolioOS.API.Authorization;

/// <summary>
/// Nama policy dan scope OAuth yang dipakai API. Nilai scope harus sama persis
/// dengan yang didefinisikan di PortfolioOS.Identity (IdentityServerConfig.Scopes).
/// </summary>
public static class PortfolioPolicies
{
    public const string Read = "portfolio.read";
    public const string Write = "portfolio.write";
    public const string Admin = "portfolio.admin";

    public static class Scopes
    {
        public const string Read = "portfolioos.read";
        public const string Write = "portfolioos.write";
        public const string Admin = "portfolioos.admin";
    }

    /// <summary>Scheme untuk token HS256 lama yang diterbitkan <c>AuthController</c>.</summary>
    public const string LegacyScheme = "LegacyJwt";

    /// <summary>Scheme untuk token yang diterbitkan PortfolioOS.Identity.</summary>
    public const string IdentityServerScheme = "IdentityServer";

    /// <summary>Scheme pemilih: menentukan handler berdasarkan issuer token.</summary>
    public const string SmartScheme = "Smart";

    /// <summary>Claim penanda bahwa token berasal dari jalur login lama (tanpa scope).</summary>
    public const string LegacyTokenClaim = "auth_flavor";

    public const string LegacyTokenClaimValue = "legacy";
}
