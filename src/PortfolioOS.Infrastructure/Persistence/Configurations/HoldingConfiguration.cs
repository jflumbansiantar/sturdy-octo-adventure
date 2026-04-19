using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortfolioOS.Domain.Entities;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Infrastructure.Persistence.Configurations;

public class HoldingConfiguration : IEntityTypeConfiguration<Holding>
{
    public void Configure(EntityTypeBuilder<Holding> builder)
    {
        builder.ToTable("holdings");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Ticker).HasColumnName("ticker").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.SubType).HasColumnName("sub_type").HasMaxLength(100).HasDefaultValue("");
        builder.Property(x => x.Shares).HasColumnName("shares").HasPrecision(18, 8).IsRequired();
        builder.Property(x => x.AvgCost).HasColumnName("avg_cost").HasPrecision(18, 6).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasConversion(
                v => v == HoldingType.MutualFund ? "Mutual Fund" : v.ToString(),
                v => v == "Mutual Fund" ? HoldingType.MutualFund : Enum.Parse<HoldingType>(v))
            .HasColumnType("holding_type")
            .IsRequired();

        builder.Property(x => x.Market)
            .HasColumnName("market")
            .HasConversion<string>()
            .HasColumnType("market_type")
            .IsRequired();

        builder.HasIndex(x => x.Ticker).IsUnique().HasDatabaseName("uq_holdings_ticker");
        builder.HasIndex(x => x.Market).HasDatabaseName("idx_holdings_market");
        builder.HasIndex(x => x.Type).HasDatabaseName("idx_holdings_type");
    }
}
