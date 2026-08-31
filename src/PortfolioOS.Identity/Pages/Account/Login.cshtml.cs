using System.ComponentModel.DataAnnotations;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PortfolioOS.Identity.Data;

namespace PortfolioOS.Identity.Pages.Account;

[AllowAnonymous]
public class LoginModel(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IIdentityServerInteractionService interaction,
    IEventService events,
    ILogger<LoginModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Email wajib diisi")]
        [Display(Name = "Email")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password wajib diisi")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Ingat saya")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string? returnUrl)
    {
        Input.ReturnUrl = returnUrl;

        // Kalau IdentityServer mengirim balik dengan pesan error (mis. client tidak valid),
        // tampilkan di halaman login alih-alih membiarkan user menebak.
        var context = await interaction.GetAuthorizationContextAsync(returnUrl);
        if (context?.LoginHint is { Length: > 0 } hint) Input.Username = hint;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var context = await interaction.GetAuthorizationContextAsync(Input.ReturnUrl);

        if (!ModelState.IsValid) return Page();

        var user = await userManager.FindByEmailAsync(Input.Username)
                   ?? await userManager.FindByNameAsync(Input.Username);

        if (user is null || !user.IsActive)
        {
            // Pesan sengaja disamakan dengan kasus password salah agar tidak
            // membocorkan email mana yang terdaftar (user enumeration).
            await events.RaiseAsync(new UserLoginFailureEvent(
                Input.Username, "user tidak ditemukan atau nonaktif", clientId: context?.Client.ClientId));
            return Failed();
        }

        var result = await signInManager.PasswordSignInAsync(
            user.UserName!, Input.Password, Input.RememberMe, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            logger.LogWarning("Login ditolak — akun {UserId} terkunci", user.Id);
            ErrorMessage = "Akun terkunci sementara karena terlalu banyak percobaan gagal. Coba lagi dalam 15 menit.";
            return Page();
        }

        if (!result.Succeeded)
        {
            await events.RaiseAsync(new UserLoginFailureEvent(
                Input.Username, "password salah", clientId: context?.Client.ClientId));
            return Failed();
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);

        await events.RaiseAsync(new UserLoginSuccessEvent(
            user.UserName, user.Id.ToString(), user.DisplayName, clientId: context?.Client.ClientId));

        // Alur OIDC: kembalikan ke authorize endpoint supaya code/token diterbitkan.
        if (context is not null) return Redirect(Input.ReturnUrl!);

        if (Url.IsLocalUrl(Input.ReturnUrl)) return Redirect(Input.ReturnUrl!);
        if (string.IsNullOrWhiteSpace(Input.ReturnUrl)) return Redirect("~/");

        // ReturnUrl eksternal yang tidak dikenal IdentityServer — indikasi open redirect.
        logger.LogError("ReturnUrl tidak valid setelah login: {ReturnUrl}", Input.ReturnUrl);
        return Redirect("~/");
    }

    private PageResult Failed()
    {
        ErrorMessage = "Email atau password salah.";
        return Page();
    }
}
