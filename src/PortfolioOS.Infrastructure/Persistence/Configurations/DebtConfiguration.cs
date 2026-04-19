using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PortfolioOS.Domain.Entities;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Infrastructure.Persistence.Configurations;

public class DebtConfiguration : IEntityTypeConfiguration<Debt>
{
    // Switch expressions aren't allowed inside EF expression trees — use ValueConverter with regular Funcs
    private static readonly ValueConverter<DebtType, string> DebtTypeConverter = new(
        v => v == DebtType.CreditCard   ? "Credit Card"   :
             v == DebtType.PersonalLoan ? "Personal Loan" :
             v == DebtType.AutoLoan     ? "Auto Loan"     :
             v == DebtType.StudentLoan  ? "Student Loan"  : v.ToString(),
        v => v == "Credit Card"   ? DebtType.CreditCard   :
             v == "Personal Loan" ? DebtType.PersonalLoan :
             v == "Auto Loan"     ? DebtType.AutoLoan     :
             v == "Student Loan"  ? DebtType.StudentLoan  : Enum.Parse<DebtType>(v));

    public void Configure(EntityTypeBuilder<Debt> builder)
    {
        builder.ToTable("debts");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Balance).HasColumnName("balance").HasPrecision(18, 6).IsRequired();
        builder.Property(x => x.InterestRate).HasColumnName("interest_rate").HasPrecision(8, 4).IsRequired();
        builder.Property(x => x.MonthlyInterestRate).HasColumnName("monthly_interest_rate").HasPrecision(8, 4);
        builder.Property(x => x.Tenor).HasColumnName("tenor");
        builder.Property(x => x.MinimumPayment).HasColumnName("minimum_payment").HasPrecision(18, 6).IsRequired();
        builder.Property(x => x.DueDay).HasColumnName("due_day").IsRequired();
        builder.Property(x => x.DebtApp).HasColumnName("debt_app").HasMaxLength(255).HasDefaultValue("");
        builder.Property(x => x.Notes).HasColumnName("notes").HasDefaultValue("");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasConversion(DebtTypeConverter)
            .HasColumnType("debt_type")
            .IsRequired();

        // DebtStatus values match: Active, Lunas
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasColumnType("debt_status")
            .IsRequired();

        // CurrencyType values match: USD, IDR
        builder.Property(x => x.Currency)
            .HasColumnName("currency")
            .HasConversion<string>()
            .HasColumnType("currency_type")
            .IsRequired();

        builder.HasIndex(x => x.Status).HasDatabaseName("idx_debts_status");
        builder.HasIndex(x => x.Type).HasDatabaseName("idx_debts_type");
    }
}
