using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PortfolioOS.Identity.Data;

/// <summary>
/// Store user/role ASP.NET Core Identity. Terpisah dari database bisnis (portfolioos)
/// supaya microservice identity bisa dideploy dan di-scale sendiri.
/// </summary>
public class PortfolioIdentityDbContext(DbContextOptions<PortfolioIdentityDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e =>
        {
            e.ToTable("users");
            e.Property(u => u.DisplayName).HasMaxLength(128).IsRequired();
            e.Property(u => u.PreferredCurrency).HasMaxLength(3).IsRequired();
        });

        builder.Entity<ApplicationRole>(e =>
        {
            e.ToTable("roles");
            e.Property(r => r.Description).HasMaxLength(256);
        });

        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>>().ToTable("role_claims");
    }
}
