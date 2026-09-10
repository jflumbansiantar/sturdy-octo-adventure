using System.Security.Claims;
using IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Identity;
using PortfolioOS.Identity.Data;

namespace PortfolioOS.Identity.Services;

/// <summary>
/// Mengisi claim yang dibawa id_token / access token. Role sengaja selalu disertakan
/// supaya PortfolioOS.API bisa melakukan authorization berbasis role tanpa round-trip
/// tambahan ke endpoint /connect/userinfo.
/// </summary>
public class PortfolioProfileService(
    UserManager<ApplicationUser> userManager,
    ILogger<PortfolioProfileService> logger) : IProfileService
{
    public async Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        var user = await FindUserAsync(context.Subject);
        if (user is null)
        {
            logger.LogWarning("Profile diminta untuk subject yang tidak dikenal: {Subject}",
                context.Subject.GetSubjectId());
            return;
        }

        var claims = new List<Claim>
        {
            new(JwtClaimTypes.Subject, user.Id.ToString()),
            new(JwtClaimTypes.Name, string.IsNullOrWhiteSpace(user.DisplayName)
                ? user.UserName ?? string.Empty
                : user.DisplayName),
            new(JwtClaimTypes.PreferredUserName, user.UserName ?? string.Empty),
            new("preferred_currency", user.PreferredCurrency),
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(JwtClaimTypes.Email, user.Email));
            claims.Add(new Claim(JwtClaimTypes.EmailVerified, user.EmailConfirmed ? "true" : "false",
                ClaimValueTypes.Boolean));
        }

        foreach (var role in await userManager.GetRolesAsync(user))
            claims.Add(new Claim(JwtClaimTypes.Role, role));

        // Claim per-user (mis. izin granular) yang disimpan di tabel user_claims.
        claims.AddRange(await userManager.GetClaimsAsync(user));

        // IdentityServer menyaring sendiri sesuai scope yang diminta client.
        context.AddRequestedClaims(claims);
    }

    /// <summary>
    /// Dipanggil setiap kali token di-refresh. User yang dinonaktifkan atau sedang
    /// terkunci langsung kehilangan akses tanpa harus menunggu access token expired.
    /// </summary>
    public async Task IsActiveAsync(IsActiveContext context)
    {
        var user = await FindUserAsync(context.Subject);

        context.IsActive = user is { IsActive: true }
                           && !await userManager.IsLockedOutAsync(user);
    }

    private async Task<ApplicationUser?> FindUserAsync(ClaimsPrincipal subject)
    {
        var subjectId = subject.GetSubjectId();
        return Guid.TryParse(subjectId, out _)
            ? await userManager.FindByIdAsync(subjectId)
            : null;
    }
}
