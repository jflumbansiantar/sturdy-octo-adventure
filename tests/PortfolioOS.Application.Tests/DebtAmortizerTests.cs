using FluentAssertions;
using PortfolioOS.Application.Forecasting;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.Application.Tests;

/// <summary>
/// The amortiser estimates nothing, so every case here asserts an exact consequence of the recorded
/// numbers rather than a property. Where a figure is untidy the test states the arithmetic that
/// produces it, so a failing assertion says which assumption moved.
/// </summary>
public class DebtAmortizerTests
{
    private static readonly DateOnly Today = new(2026, 6, 15);
    private static readonly DateOnly ThisMonth = new(2026, 6, 1);

    private const decimal Juta = 1_000_000m;

    private static DebtPosition Debt(
        string name,
        decimal balance,
        decimal monthlyRatePercent,
        decimal minimumPayment,
        int? tenor = null) =>
        new(Guid.NewGuid(), name, DebtType.Other, balance,
            AnnualRatePercent: monthlyRatePercent * 12m,
            MonthlyRatePercent: monthlyRatePercent,
            MinimumPayment: minimumPayment,
            Tenor: tenor);

    [Fact]
    public void NoDebts_ReportsNothingToSchedule()
    {
        var plan = DebtAmortizer.Plan([], Today);

        plan.HasDebts.Should().BeFalse();
        plan.Schedule.Should().BeEmpty();
        plan.TotalInterest.Should().Be(0m);
        plan.Notes.Should().ContainSingle().Which.Should().Contain("Tidak ada utang aktif");
    }

    [Fact]
    public void InterestFreeDebt_PaysOffByPlainDivision()
    {
        // 1,2 juta at 0%, paid 100rb a month, is twelve months and not a rupiah of interest.
        var plan = DebtAmortizer.Plan(
            [Debt("Pinjaman keluarga", balance: 1.2m * Juta, monthlyRatePercent: 0m, minimumPayment: 100_000m)],
            Today);

        plan.MonthsToDebtFree.Should().Be(12);
        plan.DebtFreeMonth.Should().Be(ThisMonth.AddMonths(12));
        plan.TotalInterest.Should().Be(0m);
        plan.TotalPaid.Should().Be(1.2m * Juta);
        plan.Debts.Should().ContainSingle().Which.PaysOff.Should().BeTrue();
    }

    [Fact]
    public void InstalmentBelowInterest_ReportsNoPayoffDateAtAll()
    {
        // 10 juta at 2% is 200rb of interest a month against a 150rb instalment. The balance grows
        // every month, so there is no payoff date to report and inventing one would be a lie.
        var plan = DebtAmortizer.Plan(
            [Debt("Paylater", balance: 10 * Juta, monthlyRatePercent: 2m, minimumPayment: 150_000m)],
            Today);

        plan.AllPaidOff.Should().BeFalse();
        plan.MonthsToDebtFree.Should().BeNull();
        plan.DebtFreeMonth.Should().BeNull();

        var debt = plan.Debts.Should().ContainSingle().Subject;
        debt.PaysOff.Should().BeFalse();
        debt.MonthsToPayoff.Should().BeNull();
        debt.MonthlyInterestAtStart.Should().Be(200_000m);

        plan.Notes.Should().Contain(n => n.Contains("tidak pernah lunas"));
        plan.Schedule[^1].RemainingBalance.Should().BeGreaterThan(10 * Juta,
            "a balance that outruns its instalment ends higher than it started");
    }

    [Fact]
    public void ClosedDebtsInstalmentRollsOntoTheNextOne()
    {
        // Two debts, 200rb + 100rb of minimums. Once the small one closes its 100rb must keep
        // working — that rollover is the whole mechanism behind both strategies.
        var debts = new[]
        {
            Debt("Besar", balance: 2 * Juta, monthlyRatePercent: 0m, minimumPayment: 200_000m),
            Debt("Kecil", balance: 500_000m, monthlyRatePercent: 0m, minimumPayment: 100_000m),
        };

        var rolled = DebtAmortizer.Plan(debts, Today, strategy: PayoffStrategy.Snowball);
        var minimumOnly = DebtAmortizer.Plan(debts, Today, strategy: PayoffStrategy.MinimumOnly);

        rolled.MonthlyBudget.Should().Be(300_000m);
        rolled.Schedule.Take(rolled.MonthsToDebtFree!.Value - 1)
            .Should().OnlyContain(r => r.Payment == 300_000m,
                "the outlay is held constant until the last month, which only pays what is left");

        // 2,5 juta at 300rb a month clears in nine months with the rollover; paying each minimum
        // separately leaves the big debt crawling at its own 200rb.
        rolled.MonthsToDebtFree.Should().Be(9);
        minimumOnly.MonthsToDebtFree.Should().Be(10);
    }

    /// <summary>
    /// Deliberately opposed debts: the expensive one is also the larger, so the two strategies
    /// genuinely disagree about which to attack first.
    /// </summary>
    private static DebtPosition[] OpposedDebts() =>
    [
        Debt("Kartu kredit", balance: 2 * Juta, monthlyRatePercent: 5m, minimumPayment: 200_000m),
        Debt("Cicilan HP", balance: 500_000m, monthlyRatePercent: 1m, minimumPayment: 100_000m),
    ];

    [Fact]
    public void WithNoSurplusToAim_TheStrategiesCannotDifferAtAll()
    {
        // Every rupiah of the budget is already spoken for by the minimums, so there is nothing
        // left to point at either debt and the queue order changes nothing. Worth pinning down:
        // it is the case where advice to "switch strategy" is empty, and the notes say so.
        var plan = DebtAmortizer.Plan(OpposedDebts(), Today, strategy: PayoffStrategy.Avalanche);
        var comparison = plan.Comparison!;

        comparison.AvalancheInterest.Should().Be(comparison.SnowballInterest);
        comparison.Better.Should().Be("Sama");
        plan.Notes.Should().Contain(n => n.Contains("sama persis"));
    }

    [Fact]
    public void GivenSurplus_AvalancheCostsLessInterestThanSnowball()
    {
        var debts = OpposedDebts();

        var plan = DebtAmortizer.Plan(
            debts, Today, extraPayment: 300_000m, strategy: PayoffStrategy.Avalanche);
        var comparison = plan.Comparison!;

        comparison.AvalancheInterest.Should().BeLessThan(comparison.SnowballInterest,
            "the surplus aimed at 5% a month buys more than the same surplus aimed at 1%");
        comparison.Better.Should().Be("Avalanche");

        plan.Debts.Single(d => d.Name == "Kartu kredit").Priority.Should().Be(1,
            "avalanche attacks the highest rate first");

        var snowball = DebtAmortizer.Plan(
            debts, Today, extraPayment: 300_000m, strategy: PayoffStrategy.Snowball);
        snowball.Debts.Single(d => d.Name == "Cicilan HP").Priority.Should().Be(1,
            "snowball attacks the smallest balance first");
        snowball.TotalInterest.Should().BeGreaterThan(plan.TotalInterest);
    }

    [Fact]
    public void ExtraPaymentSavesInterestAndMonthsAgainstTheMinimumOnlyBaseline()
    {
        var debts = new[]
        {
            Debt("Kartu kredit", balance: 5 * Juta, monthlyRatePercent: 2.25m, minimumPayment: 500_000m),
        };

        var plain = DebtAmortizer.Plan(debts, Today);
        var boosted = DebtAmortizer.Plan(debts, Today, extraPayment: 500_000m);

        boosted.MonthlyBudget.Should().Be(1 * Juta);
        boosted.TotalInterest.Should().BeLessThan(plain.TotalInterest);
        boosted.MonthsToDebtFree.Should().BeLessThan(plain.MonthsToDebtFree!.Value);

        boosted.Comparison!.InterestSavedVsMinimumOnly.Should().BeGreaterThan(0m);
        boosted.Comparison.MonthsSavedVsMinimumOnly.Should().BeGreaterThan(0);
        boosted.Notes.Should().Contain(n => n.Contains("menghemat bunga"));
    }

    [Fact]
    public void MinimumOnlyStrategyIgnoresAnyExtraPayment()
    {
        var debts = new[]
        {
            Debt("Kartu kredit", balance: 5 * Juta, monthlyRatePercent: 2.25m, minimumPayment: 500_000m),
        };

        var plan = DebtAmortizer.Plan(
            debts, Today, extraPayment: 500_000m, strategy: PayoffStrategy.MinimumOnly);

        plan.ExtraPayment.Should().Be(0m);
        plan.MonthlyBudget.Should().Be(500_000m);
        plan.Notes.Should().Contain(n => n.Contains("diabaikan"));
    }

    [Fact]
    public void MissingInstalmentIsDerivedFromTheTenorAsAnAnnuity()
    {
        // PMT = 12jt · 0,01 / (1 − 1,01^−24) ≈ 564.882, which clears it on the last month of tenor.
        var plan = DebtAmortizer.Plan(
            [Debt("KTA", balance: 12 * Juta, monthlyRatePercent: 1m, minimumPayment: 0m, tenor: 24)],
            Today);

        var debt = plan.Debts.Should().ContainSingle().Subject;

        debt.MinimumPayment.Should().BeApproximately(564_882m, 50m);
        debt.MonthsToPayoff.Should().Be(24);
        plan.Notes.Should().Contain(n => n.Contains("anuitas"));
    }

    [Fact]
    public void DebtWithNeitherInstalmentNorTenorIsReportedAsUnschedulable()
    {
        var plan = DebtAmortizer.Plan(
            [Debt("Utang teman", balance: 3 * Juta, monthlyRatePercent: 0m, minimumPayment: 0m)],
            Today);

        plan.AllPaidOff.Should().BeFalse();
        plan.Debts.Single().PaysOff.Should().BeFalse();
        plan.Notes.Should().Contain(n => n.Contains("tidak punya cicilan maupun tenor"));
    }

    [Fact]
    public void RecordedMonthlyRateWinsOverTheAnnualOne()
    {
        // The seeded card carries both: 27% a year and 2,25% a month. The monthly figure is what
        // the lender actually charges, so it is the one that must drive the schedule.
        var withMonthly = new DebtPosition(
            Guid.NewGuid(), "Kartu", DebtType.CreditCard, 5 * Juta,
            AnnualRatePercent: 27m, MonthlyRatePercent: 2.25m,
            MinimumPayment: 500_000m, Tenor: null);

        var annualOnly = new DebtPosition(
            Guid.NewGuid(), "KPR", DebtType.Mortgage, 5 * Juta,
            AnnualRatePercent: 12m, MonthlyRatePercent: null,
            MinimumPayment: 500_000m, Tenor: null);

        DebtAmortizer.Plan([withMonthly], Today).Debts[0].MonthlyRate.Should().Be(2.25m);
        DebtAmortizer.Plan([annualOnly], Today).Debts[0].MonthlyRate.Should().Be(1m,
            "12% a year divided by twelve, the convention the app's own seed data uses");
    }

    [Fact]
    public void ZeroBalanceDebtsAreLeftOutOfTheSchedule()
    {
        var debts = new[]
        {
            Debt("Lunas", balance: 0m, monthlyRatePercent: 2m, minimumPayment: 300_000m),
            Debt("Aktif", balance: 1 * Juta, monthlyRatePercent: 0m, minimumPayment: 500_000m),
        };

        var plan = DebtAmortizer.Plan(debts, Today);

        plan.Debts.Should().ContainSingle().Which.Name.Should().Be("Aktif");
        plan.MonthlyBudget.Should().Be(500_000m, "a cleared debt contributes no instalment");
    }

    [Fact]
    public void ScheduleAccountsForEveryRupiahOfPrincipalAndInterest()
    {
        var debts = new[]
        {
            Debt("Kartu kredit", balance: 5 * Juta, monthlyRatePercent: 2.25m, minimumPayment: 700_000m),
            Debt("Cicilan HP", balance: 2 * Juta, monthlyRatePercent: 1m, minimumPayment: 300_000m),
        };

        var plan = DebtAmortizer.Plan(debts, Today, extraPayment: 200_000m);

        plan.AllPaidOff.Should().BeTrue();
        plan.Schedule.Sum(r => r.Payment).Should().BeApproximately(plan.TotalPaid, 2m,
            "every rupiah paid is either principal or interest, and both are in the total");
        plan.Schedule.Sum(r => r.Interest).Should().BeApproximately(plan.TotalInterest, 2m);
        plan.Debts.Sum(d => d.TotalInterest).Should().BeApproximately(plan.TotalInterest, 2m);
        plan.Schedule[^1].RemainingBalance.Should().Be(0m);
    }
}
