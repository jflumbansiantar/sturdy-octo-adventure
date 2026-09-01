using System.Net.Http.Json;
using PortfolioOS.Admin.Models;

namespace PortfolioOS.Admin.Services;

/// <summary>
/// Klien untuk endpoint manajemen user di PortfolioOS.Identity. Identity tetap jadi
/// pemilik data user — admin service tidak menyalin satu pun tabelnya.
/// </summary>
public class IdentityAdminClient(HttpClient http)
{
    public const string ServiceName = "PortfolioOS.Identity";

    public async Task<List<AdminUserDto>> ListUsersAsync(CancellationToken ct)
    {
        var response = await http.GetAsync("api/users", ct);
        return await response.ReadAsync<List<AdminUserDto>>(ServiceName, ct);
    }

    public async Task<AdminUserDto> GetUserAsync(Guid id, CancellationToken ct)
    {
        var response = await http.GetAsync($"api/users/{id}", ct);
        return await response.ReadAsync<AdminUserDto>(ServiceName, ct);
    }

    public async Task<AdminUserDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct)
    {
        var response = await http.PostAsJsonAsync("api/users", new
        {
            request.Email,
            request.Password,
            request.DisplayName,
            request.Role,
            PreferredCurrency = string.IsNullOrWhiteSpace(request.PreferredCurrency)
                ? "IDR"
                : request.PreferredCurrency,
        }, DownstreamHttp.Json, ct);

        return await response.ReadAsync<AdminUserDto>(ServiceName, ct);
    }

    public async Task<AdminUserDto> SetRolesAsync(Guid id, string[] roles, CancellationToken ct)
    {
        var response = await http.PutAsJsonAsync($"api/users/{id}/roles",
            new { Roles = roles }, DownstreamHttp.Json, ct);

        return await response.ReadAsync<AdminUserDto>(ServiceName, ct);
    }

    public async Task<AdminUserDto> SetActiveAsync(Guid id, bool isActive, CancellationToken ct)
    {
        var action = isActive ? "activate" : "deactivate";
        var response = await http.PostAsync($"api/users/{id}/{action}", content: null, ct);

        return await response.ReadAsync<AdminUserDto>(ServiceName, ct);
    }

    public async Task ResetPasswordAsync(Guid id, string newPassword, CancellationToken ct)
    {
        var response = await http.PostAsJsonAsync($"api/users/{id}/reset-password",
            new { NewPassword = newPassword }, DownstreamHttp.Json, ct);

        await response.EnsureAsync(ServiceName, ct);
    }

    public async Task<List<RoleDto>> ListRolesAsync(CancellationToken ct)
    {
        var response = await http.GetAsync("api/roles", ct);
        return await response.ReadAsync<List<RoleDto>>(ServiceName, ct);
    }
}
