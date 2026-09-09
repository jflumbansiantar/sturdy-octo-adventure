using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Savings.Queries.GetSavingsSuggestion;
using PortfolioOS.Domain.Entities;
using PortfolioOS.Domain.Enums;
using PortfolioOS.Infrastructure.Persistence;

namespace PortfolioOS.Application.Tests;

/// <summary>
/// The advisor is tested on its own; what is checked here is only what the handler decides —
/// which rows count as cash flow, which month they land in, and where debt service comes from.
/// </summary>
public class SavingsSuggestionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 9, 0, 0, TimeSpan.Zero);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Transaction Tx(
        DateOnly date, TransactionCategory category, string name, string type, decimal total,
        Market? market = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Date = date,
            Category = category,
            Name = name,
            Type = type,
            Total = total,
            Market = market,
            CreatedAt = Now,
            UpdatedAt = Now,
        };

    private static async Task<ApplicationDbContext> SeededContextAsync()
    {
        var ctx = CreateContext();

        foreach (var month in new[] { 4, 5, 6 })
        {
            var first = new DateOnly(2026, month, 1);
            ctx.Transactions.AddRange(
                Tx(first, TransactionCategory.Income, "Gaji", "Salary", 10_000_000m),
                Tx(first.AddDays(4), TransactionCategory.Expense, "Belanja Bulanan", "Groceries", 4_000_000m),
                Tx(first.AddDays(9), TransactionCategory.Expense, "Makan & Nongkrong", "Food", 2_000_000m),
                Tx(first.AddDays(14), TransactionCategory.Debt, "Cicilan KTA", "Debt Payment", 1_000_000m),
                // Buying shares is what savings are *spent on*, not consumption.
                Tx(first.AddDays(20), TransactionCategory.Stock, "BBCA", "Buy", 5_000_000m, Market.ID),
                // Dollar-denominated, so it cannot join a rupiah total.
                Tx(first.AddDays(21), TransactionCategory.Income, "Dividen AAPL", "Dividend", 500m, Market.US));
        }

        // Well outside the six-month window.
        ctx.Transactions.Add(
            Tx(new DateOnly(2025, 6, 1), TransactionCategory.Income, "Bonus lama", "Bonus", 99_000_000m));

        ctx.Debts.AddRange(
            new Debt
            {
                Id = Guid.NewGuid(), Name = "KTA Mandiri", Type = DebtType.PersonalLoan, Balance = 12_000_000m,
                InterestRate = 18m, MinimumPayment = 1_500_000m, DueDay = 10,
                Currency = CurrencyType.IDR, Status = DebtStatus.Active, CreatedAt = Now, UpdatedAt = Now,
            },
            new Debt
            {
                Id = Guid.NewGuid(), Name = "KPR lunas", Type = DebtType.Mortgage, Balance = 0m,
                InterestRate = 9m, MinimumPayment = 5_000_000m, DueDay = 1,
                Currency = CurrencyType.IDR, Status = DebtStatus.Lunas, CreatedAt = Now, UpdatedAt = Now,
            },
            new Debt
            {
                Id = Guid.NewGuid(), Name = "Card USD", Type = DebtType.CreditCard, Balance = 1_000m,
                InterestRate = 22m, MinimumPayment = 100m, DueDay = 20,
                Currency = CurrencyType.USD, Status = DebtStatus.Active, CreatedAt = Now, UpdatedAt = Now,
            });

        await ctx.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task Handle_ReadsOnlyBaseCurrencyCashflow_AndSplitsNeedsFromWants()
    {
        await using var ctx = await SeededContextAsync();
        var handler = new GetSavingsSuggestionHandler(ctx, new FixedTime(Now));

        var result = await handler.Handle(new GetSavingsSuggestionQuery(), default);

        result.HasEnoughData.Should().BeTrue();
        result.MonthsAnalyzed.Should().Be(3);
        result.History.Should().HaveCount(3, "the 2025 row is outside the window");

        result.MonthlyIncome.Should().Be(10_000_000m, "the dollar dividend cannot be added to rupiah");
        result.MonthlyNeeds.Should().Be(4_000_000m, "groceries are a need");
        result.MonthlyWants.Should().Be(2_000_000m, "makan & nongkrong is discretionary");

        // 1.5jt minimum on the active rupiah debt beats the 1jt actually recorded; the paid-off
        // debt and the dollar card are ignored.
        result.MonthlyDebtService.Should().Be(1_500_000m);

        result.CurrentSaving.Should().Be(2_500_000m);
        result.MaxCapacity.Should().Be(3_500_000m);
        result.Notes.Should().Contain(n => n.Contains("Kewajiban minimum utang aktif"));
    }

    [Fact]
    public async Task Handle_IgnoresFutureDatedRows_SoTheyCannotBecomeTheBaseline()
    {
        await using var ctx = await SeededContextAsync();

        // Next month's payslip, entered early. Left in, it opens a July bucket - and the advisor
        // reads the newest months as "now", so this one row would become the whole baseline.
        ctx.Transactions.Add(
            Tx(new DateOnly(2026, 7, 1), TransactionCategory.Income, "Gaji Juli", "Salary", 40_000_000m));
        await ctx.SaveChangesAsync();

        var handler = new GetSavingsSuggestionHandler(ctx, new FixedTime(Now));

        var result = await handler.Handle(new GetSavingsSuggestionQuery(), default);

        result.MonthsAnalyzed.Should().Be(3);
        result.History.Should().NotContain(h => h.Month > new DateOnly(2026, 6, 1));
        result.MonthlyIncome.Should().Be(10_000_000m, "July has not happened yet");
    }

    [Fact]
    public async Task Handle_WithNothingRecorded_SaysSoRatherThanGuessing()
    {
        await using var ctx = CreateContext();
        var handler = new GetSavingsSuggestionHandler(ctx, new FixedTime(Now));

        var result = await handler.Handle(new GetSavingsSuggestionQuery(), default);

        result.HasEnoughData.Should().BeFalse();
        result.RecommendedSaving.Should().Be(0m);
    }
}
