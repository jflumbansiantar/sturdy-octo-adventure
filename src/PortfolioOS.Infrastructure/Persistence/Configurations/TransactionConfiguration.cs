using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PortfolioOS.Domain.Entities;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    // Named arguments and switch expressions aren't allowed in EF expression trees — use ValueConverter
    private static readonly ValueConverter<TransactionCategory, string> CategoryConverter = new(
        v => v.ToString().ToUpperInvariant(),
        v => (TransactionCategory)Enum.Parse(typeof(TransactionCategory), v, true));

    private static readonly ValueConverter<Market?, string?> NullableMarketConverter = new(
        v => v.HasValue ? v.Value.ToString() : null,
        v => v != null ? (Market?)Enum.Parse<Market>(v) : null);

    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Date).HasColumnName("date").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Type).HasColumnName("type").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Total).HasColumnName("total").HasPrecision(18, 6).IsRequired();
        builder.Property(x => x.Shares).HasColumnName("shares").HasPrecision(18, 8);
        builder.Property(x => x.Price).HasColumnName("price").HasPrecision(18, 6);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.Property(x => x.Category)
            .HasColumnName("category")
            .HasConversion(CategoryConverter)
            .HasColumnType("transaction_category")
            .IsRequired();

        builder.Property(x => x.Market)
            .HasColumnName("market")
            .HasConversion(NullableMarketConverter)
            .HasColumnType("market_type");

        builder.HasIndex(x => x.Date).HasDatabaseName("idx_transactions_date");
        builder.HasIndex(x => x.Category).HasDatabaseName("idx_transactions_category");
        builder.HasIndex(x => x.Name).HasDatabaseName("idx_transactions_name");
        builder.HasIndex(x => x.Market).HasDatabaseName("idx_transactions_market");
    }
}
