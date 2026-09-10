using Microsoft.AspNetCore.Identity;

namespace PortfolioOS.Identity.Data;

/// <summary>
/// User PortfolioOS. Menambah beberapa profil di atas <see cref="IdentityUser{TKey}"/>
/// supaya bisa dikirim sebagai claim ke token tanpa lookup tambahan dari API.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Mata uang default yang dipakai user (IDR/USD). Ikut jadi claim di access token.</summary>
    public string PreferredCurrency { get; set; } = "IDR";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
}
