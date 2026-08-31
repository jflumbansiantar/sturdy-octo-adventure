using Microsoft.AspNetCore.Authorization;

namespace PortfolioOS.API.Authorization;

public class ScopeRequirement(string scope) : IAuthorizationRequirement
{
    public string Scope { get; } = scope;
}

/// <summary>
/// Memeriksa claim <c>scope</c> pada access token IdentityServer. Duende menuliskan
/// scope sebagai beberapa claim terpisah, tapi authorization server lain kadang
/// menggabungkannya jadi satu string dipisah spasi — keduanya ditangani di sini.
/// </summary>
public class ScopeRequirementHandler(bool allowLegacyTokens) : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        var scopes = context.User.FindAll("scope")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (scopes.Contains(requirement.Scope))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Token login lama tidak mengenal scope sama sekali. Selama masa migrasi
        // token tersebut dianggap punya seluruh akses seperti perilaku sebelumnya.
        var isLegacy = context.User.HasClaim(
            PortfolioPolicies.LegacyTokenClaim, PortfolioPolicies.LegacyTokenClaimValue);

        if (allowLegacyTokens && isLegacy) context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
