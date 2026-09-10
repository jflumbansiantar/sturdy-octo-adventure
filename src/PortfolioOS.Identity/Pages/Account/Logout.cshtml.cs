using Duende.IdentityServer.Events;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PortfolioOS.Identity.Data;

namespace PortfolioOS.Identity.Pages.Account;

[AllowAnonymous]
public class LogoutModel(
    SignInManager<ApplicationUser> signInManager,
    IIdentityServerInteractionService interaction,
    IEventService events) : PageModel
{
    [BindProperty]
    public string? LogoutId { get; set; }

    public bool SignedOut { get; private set; }
    public string? ClientName { get; private set; }
    public string? PostLogoutRedirectUri { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? logoutId)
    {
        LogoutId = logoutId;

        var context = await interaction.GetLogoutContextAsync(logoutId);

        // Kalau permintaan logout datang dari client yang sudah terverifikasi
        // (id_token_hint valid), tidak perlu konfirmasi manual.
        if (context?.ShowSignoutPrompt == false) return await OnPostAsync();

        if (User.Identity?.IsAuthenticated != true) return await OnPostAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            // Menyimpan konteks logout dulu supaya post_logout_redirect_uri tidak hilang
            // begitu cookie session dihapus.
            LogoutId ??= await interaction.CreateLogoutContextAsync();

            await signInManager.SignOutAsync();

            await events.RaiseAsync(new UserLogoutSuccessEvent(
                User.GetSubjectId(), User.GetDisplayName()));
        }

        var context = await interaction.GetLogoutContextAsync(LogoutId);

        SignedOut = true;
        ClientName = string.IsNullOrWhiteSpace(context?.ClientName) ? context?.ClientId : context.ClientName;
        PostLogoutRedirectUri = context?.PostLogoutRedirectUri;

        if (!string.IsNullOrWhiteSpace(PostLogoutRedirectUri))
            return Redirect(PostLogoutRedirectUri);

        return Page();
    }
}
