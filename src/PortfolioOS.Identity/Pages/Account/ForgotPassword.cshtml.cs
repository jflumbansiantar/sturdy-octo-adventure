using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PortfolioOS.Identity.Services;

namespace PortfolioOS.Identity.Pages.Account;

/// <summary>Langkah 1 — user memasukkan email, kode sekali pakai dikirim ke sana.</summary>
[AllowAnonymous]
public class ForgotPasswordModel(PasswordResetService resetService) : PageModel
{
    /// <summary>
    /// Email dibawa antar langkah lewat TempData, bukan query string, supaya tidak ikut
    /// tercatat di log akses server maupun riwayat browser.
    /// </summary>
    public const string EmailKey = "PasswordReset.Email";

    /// <summary>Pesan saat langkah berikutnya memulangkan user ke sini, mis. token sudah mati.</summary>
    public const string RestartKey = "PasswordReset.Restart";

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Email wajib diisi")]
        [EmailAddress(ErrorMessage = "Format email tidak valid")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
    }

    public void OnGet()
    {
        ErrorMessage = TempData[RestartKey] as string;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();

        var email = Input.Email.Trim();
        await resetService.RequestCodeAsync(email, ct);

        // Selalu lanjut ke langkah verifikasi, terdaftar atau tidak. Halaman yang berhenti
        // di sini untuk email asing akan membocorkan daftar user.
        TempData[EmailKey] = email;
        return RedirectToPage("VerifyCode");
    }
}
