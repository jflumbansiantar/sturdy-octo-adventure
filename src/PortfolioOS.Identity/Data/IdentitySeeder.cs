using Microsoft.AspNetCore.Identity;

namespace PortfolioOS.Identity.Data;

/// <summary>User awal yang dibuat saat database identity masih kosong.</summary>
public class SeedUserOptions
{
    public const string SectionName = "SeedUsers";

    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.User;
    public string PreferredCurrency { get; set; } = "IDR";
}

public static class IdentitySeeder
{
    /// <summary>
    /// Membuat role standar dan user awal. Idempoten — aman dipanggil tiap startup.
    /// User yang sudah ada tidak diubah, jadi password yang sudah diganti admin
    /// tidak akan ditimpa balik ke nilai seed.
    /// </summary>
    public static async Task SeedAsync(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IEnumerable<SeedUserOptions> seedUsers,
        ILogger logger)
    {
        foreach (var (name, description) in Roles.All)
        {
            if (await roleManager.RoleExistsAsync(name)) continue;

            var result = await roleManager.CreateAsync(new ApplicationRole(name) { Description = description });
            if (result.Succeeded)
                logger.LogInformation("Role '{Role}' dibuat", name);
            else
                logger.LogError("Gagal membuat role '{Role}': {Errors}", name, Describe(result));
        }

        foreach (var seed in seedUsers)
        {
            if (string.IsNullOrWhiteSpace(seed.Email) || string.IsNullOrWhiteSpace(seed.Password))
            {
                logger.LogWarning("Seed user dilewati karena email atau password kosong");
                continue;
            }

            if (await userManager.FindByEmailAsync(seed.Email) is not null) continue;

            var user = new ApplicationUser
            {
                UserName = seed.Email,
                Email = seed.Email,
                EmailConfirmed = true,
                DisplayName = string.IsNullOrWhiteSpace(seed.DisplayName) ? seed.Email : seed.DisplayName,
                PreferredCurrency = seed.PreferredCurrency,
            };

            var created = await userManager.CreateAsync(user, seed.Password);
            if (!created.Succeeded)
            {
                logger.LogError("Gagal membuat user '{Email}': {Errors}", seed.Email, Describe(created));
                continue;
            }

            var role = string.IsNullOrWhiteSpace(seed.Role) ? Roles.User : seed.Role;
            var assigned = await userManager.AddToRoleAsync(user, role);
            if (!assigned.Succeeded)
                logger.LogError("Gagal memberi role '{Role}' ke '{Email}': {Errors}",
                    role, seed.Email, Describe(assigned));

            logger.LogInformation("User '{Email}' dibuat dengan role '{Role}'", seed.Email, role);
        }
    }

    private static string Describe(IdentityResult result)
        => string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
}
