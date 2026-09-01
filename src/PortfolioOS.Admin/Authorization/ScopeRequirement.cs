using Microsoft.AspNetCore.Authorization;

namespace PortfolioOS.Admin.Authorization;

public class ScopeRequirement(string scope) : IAuthorizationRequirement
{
    public string Scope { get; } = scope;
}

/// <summary>
/// Duende menuliskan tiap scope sebagai claim <c>scope</c> terpisah, sementara
/// authorization server lain kadang menggabungkannya jadi satu string dipisah spasi.
/// Berbeda dengan PortfolioOS.API, di sini tidak ada jalur token lama sama sekali:
/// token HS256 dari <c>POST /api/auth/login</c> tidak mengenal scope dan tidak boleh
/// membuka pintu admin.
/// </summary>
public class ScopeRequirementHandler : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        var scopes = context.User.FindAll("scope")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (scopes.Contains(requirement.Scope)) context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
