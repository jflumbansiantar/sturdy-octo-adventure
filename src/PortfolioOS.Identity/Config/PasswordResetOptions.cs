namespace PortfolioOS.Identity.Config;

/// <summary>Aturan kode sekali pakai untuk alur lupa password.</summary>
public class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    /// <summary>Jumlah digit kode yang dikirim ke email.</summary>
    public int CodeLength { get; set; } = 6;

    /// <summary>Masa berlaku kode terhitung sejak dikirim.</summary>
    public int CodeLifetimeMinutes { get; set; } = 10;

    /// <summary>Batas percobaan salah sebelum kode dibuang dan user harus minta yang baru.</summary>
    public int MaxVerifyAttempts { get; set; } = 5;

    /// <summary>
    /// Jeda minimum antar pengiriman untuk satu email. Tanpa ini tombol "kirim ulang"
    /// bisa dipakai membanjiri inbox orang lain.
    /// </summary>
    public int ResendCooldownSeconds { get; set; } = 60;
}
