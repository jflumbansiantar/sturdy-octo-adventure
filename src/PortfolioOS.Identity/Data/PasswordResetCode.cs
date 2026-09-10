namespace PortfolioOS.Identity.Data;

/// <summary>
/// Kode sekali pakai yang dikirim lewat email saat user lupa password.
/// </summary>
/// <remarks>
/// Tidak memakai <c>EmailTokenProvider</c> bawaan Identity karena provider itu berbasis TOTP
/// dengan timestep tetap 3 menit dan toleransi ±2 step — masa berlakunya tidak bisa dipatok
/// tepat 10 menit, dan kodenya tetap sah sampai jendela TOTP berikutnya walau sudah dipakai.
/// Baris tersendiri memberi kendali penuh: kedaluwarsa eksplisit, sekali pakai, dan jumlah
/// percobaan yang bisa dibatasi.
/// </remarks>
public class PasswordResetCode
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    /// <summary>
    /// Hash dari kode, bukan kodenya. Kode enam digit gampang ditebak kalau isi tabel bocor,
    /// jadi yang disimpan hasil <see cref="Microsoft.AspNetCore.Identity.IPasswordHasher{TUser}"/>
    /// — PBKDF2 yang sama dengan password.
    /// </summary>
    public string CodeHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Diisi saat kode berhasil diverifikasi. Non-null berarti kode sudah habis dipakai.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }

    /// <summary>Jumlah percobaan verifikasi yang gagal. Dipakai untuk menutup brute force.</summary>
    public int AttemptCount { get; set; }

    public bool IsUsable(DateTimeOffset now) => ConsumedAt is null && ExpiresAt > now;
}
