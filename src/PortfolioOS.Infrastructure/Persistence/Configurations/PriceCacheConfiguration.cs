using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortfolioOS.Domain.Entities;

namespace PortfolioOS.Infrastructure.Persistence.Configurations;

public class PriceCacheConfiguration : IEntityTypeConfiguration<PriceCache>
{
    public void Configure(EntityTypeBuilder<PriceCache> builder)
    {
        builder.ToTable("price_caches");

        builder.HasKey(x => x.Ticker);
        builder.Property(x => x.Ticker).HasColumnName("ticker").HasMaxLength(20).IsRequired();

        builder.Property(x => x.CurrentPrice).HasColumnName("current_price").HasPrecision(18, 6).IsRequired();
        builder.Property(x => x.PreviousClose).HasColumnName("previous_close").HasPrecision(18, 6).IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.Property(x => x.Currency)
            .HasColumnName("currency")
            .HasConversion<string>()
            .HasColumnType("currency_type")
            .IsRequired();

        builder.HasIndex(x => x.UpdatedAt).HasDatabaseName("idx_price_caches_updated_at");
    }
}
