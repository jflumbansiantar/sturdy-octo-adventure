using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PortfolioOS.AdminWeb.Models;

namespace PortfolioOS.AdminWeb.Services;

/// <summary>Permintaan ditolak service admin; pesannya sudah siap ditampilkan ke admin.</summary>
public class AdminApiException(string message) : Exception(message);

/// <summary>
/// Satu-satunya pintu keluar konsol admin. Token dipasang <see cref="AdminAuthorizationMessageHandler"/>,
/// jadi kelas ini tidak pernah menyentuh access token sendiri.
/// </summary>
public class AdminApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // --- Users ---

    public Task<List<AdminUser>> GetUsersAsync(CancellationToken ct = default)
        => GetAsync<List<AdminUser>>("api/admin/users", ct);

    public Task<List<AdminRole>> GetRolesAsync(CancellationToken ct = default)
        => GetAsync<List<AdminRole>>("api/admin/roles", ct);

    public async Task<AdminUser> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/admin/users", request, Json, ct);
        return await ReadAsync<AdminUser>(response, ct);
    }

    public async Task<AdminUser> SetRolesAsync(Guid id, string[] roles, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync(
            $"api/admin/users/{id}/roles", new SetRolesRequest(roles), Json, ct);

        return await ReadAsync<AdminUser>(response, ct);
    }

    public async Task<AdminUser> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var action = isActive ? "activate" : "deactivate";
        var response = await http.PostAsync($"api/admin/users/{id}/{action}", content: null, ct);

        return await ReadAsync<AdminUser>(response, ct);
    }

    public async Task ResetPasswordAsync(Guid id, string newPassword, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync(
            $"api/admin/users/{id}/reset-password", new ResetPasswordRequest(newPassword), Json, ct);

        await EnsureAsync(response, ct);
    }

    // --- Setting aplikasi (database bisnis, lewat PortfolioOS.API) ---

    public Task<List<ApplicationSetting>> GetApplicationSettingsAsync(CancellationToken ct = default)
        => GetAsync<List<ApplicationSetting>>("api/admin/settings/application", ct);

    public async Task UpdateApplicationSettingAsync(string key, string value, CancellationToken ct = default)
    {
        var response = await http.PatchAsJsonAsync(
            "api/admin/settings/application", new ApplicationSetting(key, value), Json, ct);

        await EnsureAsync(response, ct);
    }

    // --- Setting web (database admin service) ---

    public Task<List<WebSetting>> GetWebSettingsAsync(CancellationToken ct = default)
        => GetAsync<List<WebSetting>>("api/admin/settings/web", ct);

    public async Task<List<WebSetting>> SaveWebSettingsAsync(
        IEnumerable<WebSettingValue> items, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync(
            "api/admin/settings/web", new UpdateWebSettingsRequest(items.ToArray()), Json, ct);

        return await ReadAsync<List<WebSetting>>(response, ct);
    }

    public async Task<WebSetting> ResetWebSettingAsync(string key, CancellationToken ct = default)
    {
        var response = await http.PostAsync($"api/admin/settings/web/{key}/reset", content: null, ct);
        return await ReadAsync<WebSetting>(response, ct);
    }

    // --- Helper ---

    private async Task<T> GetAsync<T>(string url, CancellationToken ct)
    {
        var response = await http.GetAsync(url, ct);
        return await ReadAsync<T>(response, ct);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await EnsureAsync(response, ct);

        var value = await response.Content.ReadFromJsonAsync<T>(Json, ct);
        return value ?? throw new AdminApiException("Service admin mengembalikan jawaban kosong.");
    }

    private static async Task EnsureAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        throw new AdminApiException(await DescribeAsync(response, ct));
    }

    /// <summary>
    /// Menyusun pesan dari body error. Backend meneruskan body asli service tujuan, jadi
    /// detail validasi Identity (mis. syarat password) ikut terbaca di sini.
    /// </summary>
    private static async Task<string> DescribeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var fallback = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Sesi Anda sudah berakhir. Silakan login ulang.",
            HttpStatusCode.Forbidden => "Akun Anda tidak punya akses admin.",
            HttpStatusCode.NotFound => "Data tidak ditemukan.",
            HttpStatusCode.BadGateway => "Service tujuan tidak dapat dihubungi.",
            _ => $"Permintaan gagal ({(int)response.StatusCode}).",
        };

        string body;
        try { body = await response.Content.ReadAsStringAsync(ct); }
        catch { return fallback; }

        if (string.IsNullOrWhiteSpace(body)) return fallback;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var message = root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String
                ? error.GetString()!
                : fallback;

            if (!root.TryGetProperty("details", out var details) || details.ValueKind != JsonValueKind.Array)
                return message;

            var parts = details.EnumerateArray()
                .Select(d => d.ValueKind == JsonValueKind.String
                    ? d.GetString()
                    : d.TryGetProperty("description", out var desc) ? desc.GetString()
                    : d.TryGetProperty("message", out var msg) ? msg.GetString()
                    : null)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToArray();

            return parts.Length == 0 ? message : $"{message}: {string.Join(" ", parts)}";
        }
        catch (JsonException)
        {
            return fallback;
        }
    }
}
