namespace PortfolioOS.Identity.Config;

/// <summary>
/// URL tiap client + secret machine-to-machine. Di-bind dari section "Clients"
/// supaya redirect URI bisa diganti per-environment tanpa ubah kode.
/// </summary>
public class ClientUrlOptions
{
    public const string SectionName = "Clients";

    /// <summary>Base URL Blazor WASM, mis. https://localhost:7001</summary>
    public string WebBaseUrl { get; set; } = "https://localhost:7001";

    /// <summary>Base URL API — dipakai untuk redirect Swagger UI.</summary>
    public string ApiBaseUrl { get; set; } = "https://localhost:7195";

    /// <summary>Custom URI scheme aplikasi MAUI, mis. portfolioos://callback</summary>
    public string MobileRedirectUri { get; set; } = "portfolioos://callback";

    public string MobilePostLogoutRedirectUri { get; set; } = "portfolioos://logout";

    /// <summary>Secret client credentials untuk job/background service.</summary>
    public string JobsClientSecret { get; set; } = "jobs-secret-change-me";

    /// <summary>
    /// Mengaktifkan client Resource Owner Password (grant "password"). Hanya jembatan
    /// sementara supaya login lama Web/Mobile tetap jalan selama migrasi ke code+PKCE.
    /// Matikan di produksi.
    /// </summary>
    public bool EnableLegacyPasswordClient { get; set; }

    public string LegacyClientSecret { get; set; } = "legacy-secret-change-me";
}
