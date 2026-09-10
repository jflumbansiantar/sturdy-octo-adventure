using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace PortfolioOS.API.Authorization;

public static class AuthenticationSetup
{
    /// <summary>
    /// Memasang dua penerbit token sekaligus:
    /// <list type="bullet">
    /// <item>PortfolioOS.Identity (OpenID Connect) — jalur utama.</item>
    /// <item>Token HS256 lama dari <c>AuthController</c> — jalur transisi.</item>
    /// </list>
    /// Keduanya hidup berdampingan supaya Web/Mobile bisa dimigrasikan bertahap.
    /// Handler dipilih dari klaim <c>iss</c> di dalam token.
    /// </summary>
    public static IServiceCollection AddPortfolioAuthentication(
        this IServiceCollection services,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        var authority = config["IdentityServer:Authority"];
        var identityServerEnabled = !string.IsNullOrWhiteSpace(authority);

        var legacySecret = config["Jwt:Secret"];
        var legacyIssuer = config["Jwt:Issuer"];
        var legacyEnabled = !string.IsNullOrWhiteSpace(legacySecret)
                            && config.GetValue("Auth:AllowLegacyTokens", true);

        if (!identityServerEnabled && !legacyEnabled)
            throw new InvalidOperationException(
                "Tidak ada penerbit token yang aktif. Isi IdentityServer:Authority atau Jwt:Secret.");

        var defaultScheme = identityServerEnabled && legacyEnabled
            ? PortfolioPolicies.SmartScheme
            : identityServerEnabled
                ? PortfolioPolicies.IdentityServerScheme
                : PortfolioPolicies.LegacyScheme;

        var authBuilder = services.AddAuthentication(defaultScheme);

        if (identityServerEnabled && legacyEnabled)
        {
            authBuilder.AddPolicyScheme(PortfolioPolicies.SmartScheme, "IdentityServer atau JWT lama", o =>
            {
                o.ForwardDefaultSelector = ctx =>
                {
                    var header = ctx.Request.Headers.Authorization.ToString();
                    if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        return PortfolioPolicies.IdentityServerScheme;

                    var token = header["Bearer ".Length..].Trim();
                    var handler = new JwtSecurityTokenHandler();

                    if (!handler.CanReadToken(token))
                        return PortfolioPolicies.IdentityServerScheme;

                    return handler.ReadJwtToken(token).Issuer == legacyIssuer
                        ? PortfolioPolicies.LegacyScheme
                        : PortfolioPolicies.IdentityServerScheme;
                };
            });
        }

        if (identityServerEnabled)
        {
            authBuilder.AddJwtBearer(PortfolioPolicies.IdentityServerScheme, o =>
            {
                o.Authority = authority;

                // Di dalam Docker, issuer yang tertulis di token adalah alamat yang
                // dilihat browser, sedangkan metadata harus diambil lewat nama service.
                var metadataAddress = config["IdentityServer:MetadataAddress"];
                if (!string.IsNullOrWhiteSpace(metadataAddress)) o.MetadataAddress = metadataAddress;

                o.Audience = config["IdentityServer:Audience"] ?? "portfolioos-api";
                o.RequireHttpsMetadata = config.GetValue("IdentityServer:RequireHttpsMetadata",
                    !env.IsDevelopment());

                // Menonaktifkan pemetaan claim bawaan Microsoft supaya nama claim tetap
                // seperti yang diterbitkan IdentityServer ("role", "name", "scope").
                o.MapInboundClaims = false;
                o.TokenValidationParameters.NameClaimType = "name";
                o.TokenValidationParameters.RoleClaimType = "role";
            });
        }

        if (legacyEnabled)
        {
            authBuilder.AddJwtBearer(PortfolioPolicies.LegacyScheme, o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = legacyIssuer,
                    ValidAudience = config["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(legacySecret!)),
                };

                o.Events = new JwtBearerEvents
                {
                    // Penanda supaya policy berbasis scope tahu token ini dari jalur lama.
                    OnTokenValidated = ctx =>
                    {
                        if (ctx.Principal?.Identity is ClaimsIdentity identity)
                            identity.AddClaim(new Claim(
                                PortfolioPolicies.LegacyTokenClaim,
                                PortfolioPolicies.LegacyTokenClaimValue));

                        return Task.CompletedTask;
                    },
                };
            });
        }

        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler>(
            _ => new ScopeRequirementHandler(legacyEnabled));

        services.AddAuthorization(o =>
        {
            o.AddPolicy(PortfolioPolicies.Read, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new ScopeRequirement(PortfolioPolicies.Scopes.Read)));

            o.AddPolicy(PortfolioPolicies.Write, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new ScopeRequirement(PortfolioPolicies.Scopes.Write)));

            o.AddPolicy(PortfolioPolicies.Admin, p => p
                .RequireAuthenticatedUser()
                .AddRequirements(new ScopeRequirement(PortfolioPolicies.Scopes.Admin)));
        });

        return services;
    }
}
