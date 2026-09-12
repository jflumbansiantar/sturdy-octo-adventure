using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Application.Forecasting.Queries.GetGoalProjection;
using PortfolioOS.Domain.Entities;
using PortfolioOS.Domain.Enums;
using PortfolioOS.Infrastructure.Persistence;

namespace PortfolioOS.Application.Tests;

/// <summary>
/// The projector is tested on its own; what is checked here is only what the handler decides —
/// how big the emergency fund target is, and which spending counts towards it.
/// </summary>
public class GoalProjectionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 9, 0, 0, TimeSpan.Zero);

    private const decimal Juta = 1_000_000m;

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Transaction Tx(
        DateOnly date, TransactionCategory category, string name, string type, decimal total) =>
        new()
        {
            Id = Guid.NewGuid(),
            Date = date,
            Category = category,
            Name = name,
            Type = type,
            Total = total,
            CreatedAt = Now,
            UpdatedAt = Now,
        };

    /// <summary>
    /// Three months of 10 juta income against 4 juta of kebutuhan, 1 juta of keinginan, and a
    /// 2 juta instalment paid against a loan whose contractual minimum is only 1,2 juta — so the
    /// household is voluntarily overpaying its debt by 800rb a month.
    /// </summary>
    private static async Task<ApplicationDbContext> SeededContextAsync()
    {
        var ctx = CreateContext();

        foreach (var month in new[] { 4, 5, 6 })
        {
            var first = new DateOnly(2026, month, 1);
            ctx.Transactions.AddRange(
                Tx(first, TransactionCategory.Income, "Gaji", "Salary", 10 * Juta),
                Tx(first.AddDays(4), TransactionCategory.Expense, "Belanja Bulanan", "Groceries", 4 * Juta),
                Tx(first.AddDays(9), TransactionCategory.Expense, "Makan & Nongkrong", "Food", 1 * Juta),
                Tx(first.AddDays(14), TransactionCategory.Debt, "Cicilan KTA", "Debt Payment", 2 * Juta));
        }

        ctx.Debts.Add(new Debt
        {
            Id = Guid.NewGuid(),
            Name = "KTA Mandiri",
            Type = DebtType.PersonalLoan,
            Balance = 20 * Juta,
            InterestRate = 12m,
            MinimumPayment = 1.2m * Juta,
            DueDay = 10,
            Currency = CurrencyType.IDR,
            Status = DebtStatus.Active,
            CreatedAt = Now,
            UpdatedAt = Now,
        });

        await ctx.SaveChangesAsync();
        return ctx;
    }

    private static GetGoalProjectionHandler Handler(ApplicationDbContext ctx) =>
        new(ctx, new FixedTime(Now));

    [Fact]
    public async Task EmergencyFund_CoversTheContractualInstalment_NotAVoluntaryOverpayment()
    {
        await using var ctx = await SeededContextAsync();

        var result = await Handler(ctx).Handle(new GetGoalProjectionQuery(), CancellationToken.None);

        // 6 × (4 juta kebutuhan + 1,2 juta kewajiban minimum). The 2 juta actually paid carries
        // 800rb of voluntary prepayment, and prepaying a loan is the first thing that stops in the
        // emergency this fund exists for — the same argument that keeps keinginan out.
        result.TargetAmount.Should().Be(31.2m * Juta);
        result.GoalName.Should().Contain("6 bulan");
    }

    [Fact]
    public async Task EmergencyFund_LeavesDiscretionarySpendingOut()
    {
        await using var ctx = await SeededContextAsync();

        var result = await Handler(ctx).Handle(new GetGoalProjectionQuery(), CancellationToken.None);

        // Had the juta of "Makan & Nongkrong" been counted, the target would be 37,2 juta.
        result.TargetAmount.Should().BeLessThan(37 * Juta);
    }

    [Fact]
    public async Task EmergencyFundMonths_ScalesTheTargetProportionally()
    {
        await using var ctx = await SeededContextAsync();

        var three = await Handler(ctx).Handle(
            new GetGoalProjectionQuery(EmergencyFundMonths: 3), CancellationToken.None);
        var twelve = await Handler(ctx).Handle(
            new GetGoalProjectionQuery(EmergencyFundMonths: 12), CancellationToken.None);

        three.TargetAmount.Should().Be(15.6m * Juta);
        twelve.TargetAmount.Should().Be(62.4m * Juta);
    }

    [Fact]
    public async Task ExplicitTarget_OverridesTheEmergencyFundSizing()
    {
        await using var ctx = await SeededContextAsync();

        var result = await Handler(ctx).Handle(
            new GetGoalProjectionQuery(TargetAmount: 50 * Juta, GoalName: "DP rumah"),
            CancellationToken.None);

        result.TargetAmount.Should().Be(50 * Juta);
        result.GoalName.Should().Be("DP rumah");
    }

    [Fact]
    public async Task WithNoRecordedCashAccount_NothingCountsAsAlreadySaved()
    {
        await using var ctx = await SeededContextAsync();

        var result = await Handler(ctx).Handle(new GetGoalProjectionQuery(), CancellationToken.None);

        result.CurrentAmount.Should().Be(0m, "the ledger has no account the classifier reads as cash");
        result.Shortfall.Should().Be(result.TargetAmount);
    }
}
