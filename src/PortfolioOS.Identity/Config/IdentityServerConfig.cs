using Duende.IdentityServer;
using Duende.IdentityServer.Models;

namespace PortfolioOS.Identity.Config;

/// <summary>
/// Definisi resource, scope, dan client IdentityServer.
/// Client PortfolioOS jumlahnya tetap dan first-party, jadi dikonfigurasi in-memory
/// (bukan lewat ConfigurationDbContext) agar perubahannya ikut code review + versioning.
/// </summary>
public static class IdentityServerConfig
{
    public static class Scopes
    {
        public const string Read = "portfolioos.read";
        public const string Write = "portfolioos.write";
        public const string Admin = "portfolioos.admin";
    }

    public const string ApiResourceName = "portfolioos-api";

    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
        new IdentityResources.Email(),
        new IdentityResource(
            name: "roles",
            displayName: "Role yang Anda miliki",
            userClaims: ["role"]),
    ];

    public static IEnumerable<ApiScope> ApiScopes =>
    [
        new(Scopes.Read, "Membaca portofolio, transaksi, utang, dan ledger"),
        new(Scopes.Write, "Membuat dan mengubah data portofolio"),
        new(Scopes.Admin, "Administrasi: manajemen user dan pengaturan sistem"),
    ];

    public static IEnumerable<ApiResource> ApiResources =>
    [
        new(ApiResourceName, "PortfolioOS API")
        {
            Scopes = [Scopes.Read, Scopes.Write, Scopes.Admin],

            // Claim ini ikut masuk ke access token supaya API bisa authorize
            // tanpa memanggil /connect/userinfo per request.
            UserClaims = ["role", "name", "email", "preferred_currency"],
        },
    ];

    public static IEnumerable<Client> Clients(ClientUrlOptions urls)
    {
        var web = urls.WebBaseUrl.TrimEnd('/');
        var api = urls.ApiBaseUrl.TrimEnd('/');
        var admin = urls.AdminWebBaseUrl.TrimEnd('/');

        var clients = new List<Client>
        {
            // --- Blazor WebAssembly (public client, authorization code + PKCE) ---
            new()
            {
                ClientId = "portfolioos-web",
                ClientName = "PortfolioOS Web",
                AllowedGrantTypes = GrantTypes.Code,
                RequireClientSecret = false,
                RequirePkce = true,
                RequireConsent = false,

                RedirectUris = { web + "/authentication/login-callback" },
                PostLogoutRedirectUris = { web + "/authentication/logout-callback" },
                AllowedCorsOrigins = { web },

                AllowedScopes =
                {
                    IdentityServerConstants.StandardScopes.OpenId,
                    IdentityServerConstants.StandardScopes.Profile,
                    IdentityServerConstants.StandardScopes.Email,
                    "roles",
                    Scopes.Read,
                    Scopes.Write,
                },

                AllowOfflineAccess = true,
                RefreshTokenUsage = TokenUsage.OneTimeOnly,      // rotasi refresh token
                RefreshTokenExpiration = TokenExpiration.Sliding,
                SlidingRefreshTokenLifetime = 60 * 60 * 24 * 14, // 14 hari
                AccessTokenLifetime = 60 * 60,                   // 1 jam
            },

            // --- .NET MAUI (native client, authorization code + PKCE) ---
            new()
            {
                ClientId = "portfolioos-mobile",
                ClientName = "PortfolioOS Mobile",
                AllowedGrantTypes = GrantTypes.Code,
                RequireClientSecret = false,
                RequirePkce = true,
                RequireConsent = false,

                RedirectUris = { urls.MobileRedirectUri },
                PostLogoutRedirectUris = { urls.MobilePostLogoutRedirectUri },

                AllowedScopes =
                {
                    IdentityServerConstants.StandardScopes.OpenId,
                    IdentityServerConstants.StandardScopes.Profile,
                    IdentityServerConstants.StandardScopes.Email,
                    "roles",
                    Scopes.Read,
                    Scopes.Write,
                },

                AllowOfflineAccess = true,
                RefreshTokenUsage = TokenUsage.OneTimeOnly,
                RefreshTokenExpiration = TokenExpiration.Sliding,
                SlidingRefreshTokenLifetime = 60 * 60 * 24 * 30, // 30 hari — mobile jarang login ulang
                AccessTokenLifetime = 60 * 60,
            },

            // --- Konsol admin (PortfolioOS.AdminWeb) ---
            // Satu-satunya client yang boleh meminta scope admin. Dipisahkan dari
            // "portfolioos-web" supaya token aplikasi biasa tidak pernah bisa membawa
            // scope admin sekalipun yang login kebetulan seorang admin.
            new()
            {
                ClientId = "portfolioos-admin",
                ClientName = "PortfolioOS Admin Console",
                AllowedGrantTypes = GrantTypes.Code,
                RequireClientSecret = false,
                RequirePkce = true,
                RequireConsent = false,

                RedirectUris = { admin + "/authentication/login-callback" },
                PostLogoutRedirectUris = { admin + "/authentication/logout-callback" },
                AllowedCorsOrigins = { admin },

                AllowedScopes =
                {
                    IdentityServerConstants.StandardScopes.OpenId,
                    IdentityServerConstants.StandardScopes.Profile,
                    IdentityServerConstants.StandardScopes.Email,
                    "roles",
                    Scopes.Read,
                    Scopes.Write,
                    Scopes.Admin,
                },

                // Konsol membaca role dari id_token untuk memutuskan apa yang boleh
                // ditampilkan; tanpa ini ia harus memanggil /connect/userinfo dulu.
                AlwaysIncludeUserClaimsInIdToken = true,

                AllowOfflineAccess = true,
                RefreshTokenUsage = TokenUsage.OneTimeOnly,
                RefreshTokenExpiration = TokenExpiration.Sliding,

                // Sengaja jauh lebih pendek daripada client lain: sesi admin yang
                // menganggur tidak perlu bisa dilanjutkan berhari-hari kemudian.
                SlidingRefreshTokenLifetime = 60 * 60 * 8,       // 8 jam
                AccessTokenLifetime = 60 * 30,                   // 30 menit
            },

            // --- Swagger UI di PortfolioOS.API ---
            new()
            {
                ClientId = "portfolioos-swagger",
                ClientName = "PortfolioOS API - Swagger UI",
                AllowedGrantTypes = GrantTypes.Code,
                RequireClientSecret = false,
                RequirePkce = true,
                RequireConsent = false,

                RedirectUris = { api + "/swagger/oauth2-redirect.html" },
                PostLogoutRedirectUris = { api + "/swagger/index.html" },
                AllowedCorsOrigins = { api },

                AllowedScopes =
                {
                    IdentityServerConstants.StandardScopes.OpenId,
                    IdentityServerConstants.StandardScopes.Profile,
                    "roles",
                    Scopes.Read,
                    Scopes.Write,
                    Scopes.Admin,
                },

                AccessTokenLifetime = 60 * 60,
            },

            // --- Background job / service-to-service (client credentials) ---
            new()
            {
                ClientId = "portfolioos-jobs",
                ClientName = "PortfolioOS Background Jobs",
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                ClientSecrets = { new Secret(urls.JobsClientSecret.Sha256()) },
                AllowedScopes = { Scopes.Read, Scopes.Write },
                AccessTokenLifetime = 60 * 15,
            },
        };

        // Jembatan migrasi: login username/password langsung ke /connect/token,
        // supaya halaman login Web/Mobile yang sudah ada tidak perlu dirombak dulu.
        if (urls.EnableLegacyPasswordClient)
        {
            clients.Add(new Client
            {
                ClientId = "portfolioos-legacy",
                ClientName = "PortfolioOS Legacy Password Login",
                AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
                ClientSecrets = { new Secret(urls.LegacyClientSecret.Sha256()) },
                AllowedScopes =
                {
                    IdentityServerConstants.StandardScopes.OpenId,
                    IdentityServerConstants.StandardScopes.Profile,
                    IdentityServerConstants.StandardScopes.Email,
                    "roles",
                    Scopes.Read,
                    Scopes.Write,
                },
                AllowOfflineAccess = true,
                AccessTokenLifetime = 60 * 60,
            });
        }

        return clients;
    }
}
