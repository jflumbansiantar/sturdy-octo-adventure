using System.Net.Http.Json;
using PortfolioOS.Admin.Models;

namespace PortfolioOS.Admin.Services;

/// <summary>
/// Klien untuk setting aplikasi (tabel <c>app_settings</c> di database bisnis).
/// Nilainya dibaca domain portofolio lewat PortfolioOS.API, jadi API tetap pemiliknya
/// dan admin service hanya menyediakan permukaan admin di atasnya.
/// </summary>
public class PortfolioApiClient(HttpClient http)
{
    public const string ServiceName = "PortfolioOS.API";

    public async Task<List<ApplicationSettingDto>> GetSettingsAsync(CancellationToken ct)
    {
        var response = await http.GetAsync("api/settings", ct);
        return await response.ReadAsync<List<ApplicationSettingDto>>(ServiceName, ct);
    }

    public async Task UpdateSettingAsync(string key, string value, CancellationToken ct)
    {
        var response = await http.PatchAsJsonAsync("api/settings",
            new { Key = key, Value = value }, DownstreamHttp.Json, ct);

        await response.EnsureAsync(ServiceName, ct);
    }
}
