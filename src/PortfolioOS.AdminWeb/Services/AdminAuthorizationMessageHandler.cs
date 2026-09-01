using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace PortfolioOS.AdminWeb.Services;

/// <summary>
/// Menempelkan access token ke tiap panggilan menuju service admin — dan hanya ke sana.
/// Daftar URL terotorisasi penting: tanpa itu token bisa ikut terkirim ke host lain.
/// </summary>
public class AdminAuthorizationMessageHandler : AuthorizationMessageHandler
{
    public AdminAuthorizationMessageHandler(
        IAccessTokenProvider provider,
        NavigationManager navigation,
        IConfiguration configuration)
        : base(provider, navigation)
    {
        var adminApi = configuration["AdminApiBaseUrl"] ?? "https://localhost:7197";

        // Scope tidak disebutkan di sini: token yang tersimpan sudah membawa seluruh scope
        // yang diminta saat login, dan meminta ulang subset-nya hanya memicu request token
        // tambahan tanpa menambah izin apa pun.
        ConfigureHandler(authorizedUrls: [adminApi]);
    }
}
