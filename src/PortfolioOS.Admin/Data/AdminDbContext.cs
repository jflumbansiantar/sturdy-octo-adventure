using Microsoft.EntityFrameworkCore;

namespace PortfolioOS.Admin.Data;

public class AdminDbContext(DbContextOptions<AdminDbContext> options) : DbContext(options)
{
    public DbSet<WebSetting> WebSettings => Set<WebSetting>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<WebSetting>(e =>
        {
            e.ToTable("web_settings");

            e.HasKey(x => x.Key);

            e.Property(x => x.Key).HasColumnName("key").HasMaxLength(100);
            e.Property(x => x.Value).HasColumnName("value").HasMaxLength(2000).IsRequired();
            e.Property(x => x.ValueType).HasColumnName("value_type").HasMaxLength(20).IsRequired();
            e.Property(x => x.Category).HasColumnName("category").HasMaxLength(50).IsRequired();
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(500).IsRequired();
            e.Property(x => x.Options).HasColumnName("options").HasMaxLength(500);
            e.Property(x => x.DefaultValue).HasColumnName("default_value").HasMaxLength(2000).IsRequired();
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(256);

            e.HasIndex(x => new { x.Category, x.SortOrder }).HasDatabaseName("ix_web_settings_category");
        });

        base.OnModelCreating(builder);
    }
}
