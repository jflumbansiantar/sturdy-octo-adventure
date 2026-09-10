using Microsoft.AspNetCore.Identity;

namespace PortfolioOS.Identity.Data;

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }

    public ApplicationRole(string name) : base(name) { }

    public string Description { get; set; } = string.Empty;
}

/// <summary>Nama role yang dikenal sistem. Dipakai seeder dan policy di API.</summary>
public static class Roles
{
    public const string Admin = "admin";
    public const string User = "user";
    public const string Viewer = "viewer";

    public static readonly (string Name, string Description)[] All =
    [
        (Admin,  "Akses penuh termasuk manajemen user dan pengaturan sistem"),
        (User,   "Akses baca-tulis atas portofolio, transaksi, utang, dan ledger"),
        (Viewer, "Akses baca-saja atas seluruh data portofolio"),
    ];
}
