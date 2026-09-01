namespace PortfolioOS.Admin.Configuration;

/// <summary>
/// Alamat service yang di-proxy oleh admin service. Keduanya tetap jadi pemilik datanya
/// masing-masing — admin service hanya meneruskan access token milik pemanggil, jadi
/// otorisasi tetap diputuskan di service tujuan.
/// </summary>
public class DownstreamOptions
{
    public const string SectionName = "Downstream";

    /// <summary>Base URL PortfolioOS.Identity — sumber data user dan role.</summary>
    public string IdentityBaseUrl { get; set; } = "https://localhost:7196";

    /// <summary>Base URL PortfolioOS.API — sumber data setting aplikasi (tabel app_settings).</summary>
    public string ApiBaseUrl { get; set; } = "https://localhost:7195";

    /// <summary>
    /// Melewati validasi sertifikat saat memanggil service tujuan. Hanya berlaku di
    /// Development — sertifikat dev ASP.NET Core sering belum di-trust oleh runtime.
    /// </summary>
    public bool AllowInvalidCertificates { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 30;
}
