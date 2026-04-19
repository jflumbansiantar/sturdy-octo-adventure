using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortfolioOS.Domain.Entities;

namespace PortfolioOS.Infrastructure.Persistence.Configurations;

public class JournalLineConfiguration : IEntityTypeConfiguration<JournalLine>
{
    public void Configure(EntityTypeBuilder<JournalLine> builder)
    {
        builder.ToTable("journal_lines");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.EntryId).HasColumnName("entry_id").HasMaxLength(20).IsRequired();
        builder.Property(x => x.AccountId).HasColumnName("account_id").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Debit).HasColumnName("debit").HasPrecision(18, 6).HasDefaultValue(0m).IsRequired();
        builder.Property(x => x.Credit).HasColumnName("credit").HasPrecision(18, 6).HasDefaultValue(0m).IsRequired();

        // FK relationships are configured on the parent side (JournalEntry, LedgerAccount)
        builder.HasIndex(x => x.EntryId).HasDatabaseName("idx_journal_lines_entry_id");
        builder.HasIndex(x => x.AccountId).HasDatabaseName("idx_journal_lines_account_id");
    }
}
