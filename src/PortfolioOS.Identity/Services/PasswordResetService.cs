using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PortfolioOS.Identity.Config;
using PortfolioOS.Identity.Data;

namespace PortfolioOS.Identity.Services;

public enum VerifyCodeStatus
{
    /// <summary>Kode cocok. <see cref="VerifyCodeResult.ResetToken"/> siap dipakai.</summary>
    Success,

    /// <summary>Kode salah, sudah dipakai, sudah kedaluwarsa, atau emailnya tidak terdaftar.</summary>
    InvalidOrExpired,

    /// <summary>Terlalu banyak percobaan salah — kode dibuang, user harus minta yang baru.</summary>
    TooManyAttempts,
}

/// <param name="ResetToken">
/// Token reset password bawaan Identity, hanya terisi saat status <see cref="VerifyCodeStatus.Success"/>.
/// </param>
public record VerifyCodeResult(VerifyCodeStatus Status, string? ResetToken);

/// <summary>
/// Menerbitkan dan memeriksa kode sekali pakai untuk alur lupa password.
/// </summary>
/// <remarks>
/// Kode enam digit hanya membuktikan bahwa pemintanya bisa membaca inbox email tersebut.
/// Perubahan password sendiri tetap lewat <see cref="UserManager{TUser}.ResetPasswordAsync"/>
/// dengan token bawaan Identity — token itu terikat security stamp, jadi sekali password
/// berganti token lama otomatis mati, termasuk yang mungkin masih tersimpan di tab lain.
/// </remarks>
public class PasswordResetService(
    PortfolioIdentityDbContext db,
    UserManager<ApplicationUser> userManager,
    IPasswordHasher<ApplicationUser> hasher,
    IEmailSender emailSender,
    IOptions<PasswordResetOptions> options,
    ILogger<PasswordResetService> logger)
{
    private readonly PasswordResetOptions _options = options.Value;

    /// <summary>
    /// Menerbitkan kode baru dan mengirimkannya ke email tersebut.
    /// </summary>
    /// <remarks>
    /// Sengaja tidak mengembalikan apa pun. Pemanggilnya tidak boleh membedakan email yang
    /// terdaftar dari yang tidak — kalau halaman ini menjawab berbeda, ia berubah jadi alat
    /// untuk memetakan siapa saja yang punya akun.
    /// </remarks>
    public async Task RequestCodeAsync(string email, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive)
        {
            logger.LogInformation(
                "Permintaan reset password untuk email yang tidak terdaftar atau nonaktif diabaikan");
            return;
        }

        var now = DateTimeOffset.UtcNow;

        var existing = await db.PasswordResetCodes
            .Where(c => c.UserId == user.Id)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        var active = existing.FirstOrDefault(c => c.IsUsable(now));
        if (active is not null && active.CreatedAt.AddSeconds(_options.ResendCooldownSeconds) > now)
        {
            // Kode sebelumnya masih baru dan masih berlaku — biarkan yang itu yang dipakai.
            logger.LogInformation("Pengiriman ulang kode untuk user {UserId} ditahan oleh cooldown", user.Id);
            return;
        }

        // Satu user hanya boleh punya satu kode hidup: kode lama dibuang sekalian dengan
        // sisa baris yang sudah mati supaya tabel tidak menumpuk.
        db.PasswordResetCodes.RemoveRange(existing);

        var code = GenerateCode(_options.CodeLength);

        db.PasswordResetCodes.Add(new PasswordResetCode
        {
            UserId = user.Id,
            CodeHash = hasher.HashPassword(user, code),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(_options.CodeLifetimeMinutes),
        });

        await db.SaveChangesAsync(ct);

        await emailSender.SendAsync(
            user.Email!,
            "Kode reset password PortfolioOS",
            BuildHtmlBody(user.DisplayName, code, _options.CodeLifetimeMinutes),
            BuildTextBody(user.DisplayName, code, _options.CodeLifetimeMinutes),
            ct);

        logger.LogInformation("Kode reset password dikirim untuk user {UserId}", user.Id);
    }

    /// <summary>Memeriksa kode dan, kalau cocok, menerbitkan token reset password.</summary>
    public async Task<VerifyCodeResult> VerifyCodeAsync(string email, string code, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive) return new VerifyCodeResult(VerifyCodeStatus.InvalidOrExpired, null);

        var now = DateTimeOffset.UtcNow;

        var entry = await db.PasswordResetCodes
            .Where(c => c.UserId == user.Id && c.ConsumedAt == null && c.ExpiresAt > now)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (entry is null) return new VerifyCodeResult(VerifyCodeStatus.InvalidOrExpired, null);

        if (entry.AttemptCount >= _options.MaxVerifyAttempts)
        {
            db.PasswordResetCodes.Remove(entry);
            await db.SaveChangesAsync(ct);

            logger.LogWarning("Kode reset user {UserId} dibuang — melewati batas percobaan", user.Id);
            return new VerifyCodeResult(VerifyCodeStatus.TooManyAttempts, null);
        }

        var match = hasher.VerifyHashedPassword(user, entry.CodeHash, code.Trim());
        if (match == PasswordVerificationResult.Failed)
        {
            entry.AttemptCount++;
            await db.SaveChangesAsync(ct);

            // Percobaan terakhir yang gagal dilaporkan sebagai "habis" supaya user tahu
            // harus minta kode baru, bukan mencoba lagi dan lagi.
            return entry.AttemptCount >= _options.MaxVerifyAttempts
                ? new VerifyCodeResult(VerifyCodeStatus.TooManyAttempts, null)
                : new VerifyCodeResult(VerifyCodeStatus.InvalidOrExpired, null);
        }

        entry.ConsumedAt = now;
        await db.SaveChangesAsync(ct);

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        return new VerifyCodeResult(VerifyCodeStatus.Success, token);
    }

    private static string GenerateCode(int length)
    {
        // RandomNumberGenerator, bukan Random: kode ini satu-satunya yang menjaga pintu.
        var digits = new char[length];
        for (var i = 0; i < length; i++)
            digits[i] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));

        return new string(digits);
    }

    private static string BuildTextBody(string displayName, string code, int minutes) =>
        $"""
         Halo {displayName},

         Kode untuk mengatur ulang password PortfolioOS Anda:

             {code}

         Kode ini berlaku {minutes} menit dan hanya bisa dipakai sekali.

         Kalau bukan Anda yang meminta, abaikan email ini — password Anda tidak berubah.

         PortfolioOS
         """;

    private static string BuildHtmlBody(string displayName, string code, int minutes) =>
        $"""
         <div style="font-family:Segoe UI,system-ui,-apple-system,sans-serif;font-size:15px;color:#1b2733">
           <p>Halo {System.Net.WebUtility.HtmlEncode(displayName)},</p>
           <p>Kode untuk mengatur ulang password PortfolioOS Anda:</p>
           <p style="font-size:30px;font-weight:700;letter-spacing:7px;margin:22px 0">{code}</p>
           <p>Kode ini berlaku <strong>{minutes} menit</strong> dan hanya bisa dipakai sekali.</p>
           <p style="color:#5b6b7d">Kalau bukan Anda yang meminta, abaikan email ini — password Anda tidak berubah.</p>
           <p style="color:#5b6b7d">PortfolioOS</p>
         </div>
         """;
}
