using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortfolioOS.Domain.Entities;

namespace PortfolioOS.Infrastructure.Persistence.Configurations;

public class LedgerAccountConfiguration : IEntityTypeConfiguration<LedgerAccount>
{
    public void Configure(EntityTypeBuilder<LedgerAccount> builder)
    {
        builder.ToTable("ledger_accounts");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(20).IsRequired();

        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.OpeningBalance).HasColumnName("opening_balance").HasPrecision(18, 6).HasDefaultValue(0m);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // AccountType values match exactly: Asset, Liability, Equity, Income, Expense
        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasColumnType("account_type")
            .IsRequired();

        // NormalBalanceType values match exactly: Debit, Credit
        builder.Property(x => x.NormalBalance)
            .HasColumnName("normal_balance")
            .HasConversion<string>()
            .HasColumnType("normal_balance_type")
            .IsRequired();

        builder.HasMany(x => x.JournalLines)
            .WithOne(x => x.Account)
            .HasForeignKey(x => x.AccountId);
    }
}
