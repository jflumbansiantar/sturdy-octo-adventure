using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using PortfolioOS.Identity.Config;
using PortfolioOS.Identity.Services;

namespace PortfolioOS.Identity.Pages.Account;

/// <summary>Langkah 2 — user memasukkan kode dari email dan menekan Verifikasi.</summary>
[AllowAnonymous]
public class VerifyCodeModel(
    PasswordResetService resetService,
    IOptions<PasswordResetOptions> options) : PageModel
{
    public const string TokenKey = "PasswordReset.Token";
    private const string InfoKey = "PasswordReset.Info";

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }
    public string? InfoMessage { get; private set; }

    public int CodeLength => options.Value.CodeLength;
    public int LifetimeMinutes => options.Value.CodeLifetimeMinutes;

    public class InputModel
    {
        /// <summary>
        /// Dibawa di field tersembunyi supaya halaman ini tetap utuh kalau di-refresh.
        /// Aman diserahkan ke browser: menggantinya tidak menolong siapa pun — kode yang
        /// cocok untuk email lain tetap harus dibaca dari inbox email itu.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Kode wajib diisi")]
        [Display(Name = "Kode")]
        public string Code { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        // Peek, bukan indexer: nilainya masih dibutuhkan langkah berikutnya, dan halaman ini
        // harus tetap hidup kalau user me-refresh sebelum kodenya sempat masuk.
        if (TempData.Peek(ForgotPasswordModel.EmailKey) is not string email || string.IsNullOrWhiteSpace(email))
            return RedirectToPage("ForgotPassword");

        Input.Email = email;
        InfoMessage = TempData[InfoKey] as string;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Input.Email)) return RedirectToPage("ForgotPassword");
        if (!ModelState.IsValid) return Page();

        var result = await resetService.VerifyCodeAsync(Input.Email, Input.Code, ct);

        switch (result.Status)
        {
            case VerifyCodeStatus.Success:
                TempData[ForgotPasswordModel.EmailKey] = Input.Email;
                TempData[TokenKey] = result.ResetToken;
                return RedirectToPage("ResetPassword");

            case VerifyCodeStatus.TooManyAttempts:
                ErrorMessage = "Terlalu banyak percobaan. Minta kode baru untuk melanjutkan.";
                return Page();

            default:
                ErrorMessage = "Kode salah atau sudah kedaluwarsa.";
                return Page();
        }
    }

    public async Task<IActionResult> OnPostResendAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Input.Email)) return RedirectToPage("ForgotPassword");

        // Kirim ulang tidak butuh isi kode, jadi validasinya dibuang lebih dulu — kalau tidak,
        // field kosong akan menahan permintaan yang justru diminta karena kodenya belum ada.
        ModelState.Remove($"{nameof(Input)}.{nameof(InputModel.Code)}");

        await resetService.RequestCodeAsync(Input.Email, ct);

        TempData[ForgotPasswordModel.EmailKey] = Input.Email;
        TempData[InfoKey] = "Kode baru sudah dikirim kalau email tersebut terdaftar.";
        return RedirectToPage("VerifyCode");
    }
}
