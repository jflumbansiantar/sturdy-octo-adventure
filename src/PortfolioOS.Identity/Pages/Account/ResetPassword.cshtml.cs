using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using PortfolioOS.Identity.Data;

namespace PortfolioOS.Identity.Pages.Account;

/// <summary>Langkah 3 — password baru diisi, lalu user dikembalikan ke halaman masuk.</summary>
[AllowAnonymous]
public class ResetPasswordModel(
    UserManager<ApplicationUser> userManager,
    IOptions<IdentityOptions> identityOptions,
    ILogger<ResetPasswordModel> logger) : PageModel
{
    private PasswordOptions Policy => identityOptions.Value.Password;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>Aturan password yang berlaku, ditampilkan sebagai petunjuk di atas form.</summary>
    public IReadOnlyList<string> PasswordRules
    {
        get
        {
            var rules = new List<string> { $"minimal {Policy.RequiredLength} karakter" };
            if (Policy.RequireUppercase) rules.Add("satu huruf kapital");
            if (Policy.RequireLowercase) rules.Add("satu huruf kecil");
            if (Policy.RequireDigit) rules.Add("satu angka");
            if (Policy.RequireNonAlphanumeric) rules.Add("satu simbol");
            return rules;
        }
    }

    public class InputModel
    {
        /// <summary>Ditampilkan ke user sekaligus dipakai mencari akun yang akan diubah.</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Token reset bawaan Identity, hasil verifikasi kode di langkah sebelumnya. Token ini
        /// yang menentukan boleh-tidaknya password diganti — terikat pada user dan security
        /// stamp-nya, jadi tidak bisa dikarang dari sisi browser.
        /// </summary>
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password baru wajib diisi")]
        [DataType(DataType.Password)]
        [Display(Name = "Password baru")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Konfirmasi password wajib diisi")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Konfirmasi password tidak sama")]
        [Display(Name = "Ulangi password baru")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        if (TempData.Peek(ForgotPasswordModel.EmailKey) is not string email || string.IsNullOrWhiteSpace(email))
            return RedirectToPage("ForgotPassword");

        if (TempData.Peek(VerifyCodeModel.TokenKey) is not string token || string.IsNullOrWhiteSpace(token))
            return RedirectToPage("ForgotPassword");

        Input.Email = email;
        Input.Token = token;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Input.Email) || string.IsNullOrWhiteSpace(Input.Token))
            return RedirectToPage("ForgotPassword");

        if (!ModelState.IsValid) return Page();

        var user = await userManager.FindByEmailAsync(Input.Email);
        if (user is null || !user.IsActive) return RestartFlow();

        var result = await userManager.ResetPasswordAsync(user, Input.Token, Input.NewPassword);

        if (!result.Succeeded)
        {
            // Token mati berarti langkah verifikasi harus diulang — tidak ada gunanya
            // membiarkan user mencoba password lain di form yang sudah tidak sah.
            if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken)))
            {
                logger.LogWarning("Token reset password sudah tidak berlaku untuk user {UserId}", user.Id);
                return RestartFlow();
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, Describe(error));

            return Page();
        }

        // Password baru menghapus alasan akun terkunci: percobaan gagal sebelumnya adalah
        // percobaan atas password lama. Tanpa ini user bisa berhasil reset tapi tetap
        // tertahan lockout saat mencoba masuk.
        await userManager.ResetAccessFailedCountAsync(user);
        await userManager.SetLockoutEndDateAsync(user, null);

        TempData.Remove(ForgotPasswordModel.EmailKey);
        TempData.Remove(VerifyCodeModel.TokenKey);
        TempData[LoginModel.StatusKey] = "Password berhasil diperbarui. Silakan masuk dengan password baru.";

        logger.LogInformation("Password user {UserId} direset lewat alur lupa password", user.Id);
        return RedirectToPage("Login");
    }

    private IActionResult RestartFlow()
    {
        TempData.Remove(ForgotPasswordModel.EmailKey);
        TempData.Remove(VerifyCodeModel.TokenKey);
        TempData[ForgotPasswordModel.RestartKey] =
            "Sesi reset password sudah tidak berlaku. Silakan minta kode baru.";
        return RedirectToPage("ForgotPassword");
    }

    /// <summary>
    /// Aturan password diterjemahkan di sini karena pesan bawaan Identity berbahasa Inggris,
    /// sementara seluruh halaman ini berbahasa Indonesia. Kode yang tidak dikenal dibiarkan
    /// apa adanya daripada hilang.
    /// </summary>
    private string Describe(IdentityError error) => error.Code switch
    {
        nameof(IdentityErrorDescriber.PasswordTooShort) =>
            $"Password terlalu pendek — minimal {Policy.RequiredLength} karakter.",
        nameof(IdentityErrorDescriber.PasswordRequiresDigit) => "Password harus memuat minimal satu angka.",
        nameof(IdentityErrorDescriber.PasswordRequiresUpper) => "Password harus memuat minimal satu huruf kapital.",
        nameof(IdentityErrorDescriber.PasswordRequiresLower) => "Password harus memuat minimal satu huruf kecil.",
        nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric) =>
            "Password harus memuat minimal satu simbol, misalnya # atau !.",
        nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars) =>
            "Password harus memakai lebih banyak karakter yang berbeda.",
        _ => error.Description,
    };
}
