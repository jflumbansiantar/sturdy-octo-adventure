using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PortfolioOS.Mobile.Services;

namespace PortfolioOS.Mobile.ViewModels;

/// <summary>
/// Halaman akun. Logout ditaruh di tab sendiri karena <c>ToolbarItem</c> pada Shell
/// tidak dirender di nav bar Android, sehingga tombolnya tidak pernah terlihat user.
/// </summary>
public partial class AccountViewModel : ObservableObject
{
    private readonly AuthService _auth;

    public AccountViewModel(AuthService auth) => _auth = auth;

    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _role = string.Empty;

    public void Load()
    {
        var claims = ReadTokenClaims(_auth.GetToken());

        DisplayName = Pick(claims, "name", "preferred_username", "unique_name",
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name") ?? "Pengguna PortfolioOS";

        Role = Pick(claims, "role",
            "http://schemas.microsoft.com/ws/2008/06/identity/claims/role") ?? string.Empty;
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var shell = Shell.Current;
        if (shell is null) return;

        var confirmed = await shell.DisplayAlert(
            "Keluar", "Yakin ingin keluar dari akun ini?", "Keluar", "Batal");

        if (!confirmed) return;

        _auth.ClearToken();

        try
        {
            // Navigasi Shell harus di UI thread; lanjutan setelah DisplayAlert tidak
            // dijamin kembali ke sana.
            await MainThread.InvokeOnMainThreadAsync(() => shell.GoToAsync("//login"));
        }
        catch (Exception ex)
        {
            // AsyncRelayCommand menelan exception diam-diam, jadi kegagalan navigasi
            // harus dilaporkan sendiri — kalau tidak, user hanya melihat layar yang tidak berubah.
            await shell.DisplayAlert("Gagal keluar", ex.Message, "OK");
        }
    }

    private static string? Pick(Dictionary<string, string> claims, params string[] keys)
    {
        foreach (var key in keys)
            if (claims.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;

        return null;
    }

    /// <summary>
    /// Membaca payload JWT sekadar untuk ditampilkan. Tanda tangannya tidak diverifikasi —
    /// itu tugas API; di sini isinya hanya dipakai sebagai label, bukan dasar keputusan akses.
    /// </summary>
    private static Dictionary<string, string> ReadTokenClaims(string? token)
    {
        var claims = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(token)) return claims;

        try
        {
            var parts = token.Split('.');
            if (parts.Length < 2) return claims;

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                claims[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.Array => string.Join(", ", prop.Value.EnumerateArray().Select(e => e.ToString())),
                    _ => prop.Value.ToString(),
                };
            }
        }
        catch
        {
            // Token rusak atau bukan JWT — halaman tetap tampil dengan nama default.
        }

        return claims;
    }
}
