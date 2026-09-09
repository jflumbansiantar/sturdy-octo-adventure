using FluentAssertions;
using PortfolioOS.Application.Savings;

namespace PortfolioOS.Application.Tests;

/// <summary>
/// The advisor is pure arithmetic over a month series, so every case here is written as the
/// series plus the number a user would be shown. Where the blend produces an untidy figure the
/// test asserts the property the design promises (never above capacity, never below what is
/// already being saved) rather than a magic constant.
/// </summary>
public class SavingsAdvisorTests
{
    private static readonly DateOnly Today = new(2026, 6, 15);

    private const decimal Juta = 1_000_000m;

    /// <param name="month">1-based month of 2026.</param>
    private static MonthlyCashflow Month(
        int month, decimal income, decimal needs, decimal wants, decimal debt = 0m) =>
        new(new DateOnly(2026, month, 1), income, needs, wants, debt);

    [Fact]
    public void NoTransactions_ReportsNotEnoughData()
    {
        var result = SavingsAdvisor.Recommend([], Today);

        result.HasEnoughData.Should().BeFalse();
        result.RecommendedSaving.Should().Be(0m);
        result.Ramp.Should().BeEmpty();
        result.Notes.Should().ContainSingle().Which.Should().Contain("Income dan Expense");
    }

    [Fact]
    public void MonthWithIncomeButNoSpending_IsSkippedRatherThanReadAsPerfectThrift()
    {
        // May has a payslip and nothing else - that is an unlogged month, not a 100% savings rate.
        var history = new List<MonthlyCashflow>
        {
            Month(5, income: 10 * Juta, needs: 0m, wants: 0m),
            Month(6, income: 10 * Juta, needs: 5 * Juta, wants: 4 * Juta),
        };

        var result = SavingsAdvisor.Recommend(history, Today);

        result.MonthsAnalyzed.Should().Be(1);
        result.CurrentSavingRate.Should().Be(10m);
        result.History.Should().HaveCount(2, "a skipped month is still shown, so the gap is visible");
        result.Notes.Should().Contain(n => n.Contains("menabung 100%"));
    }

    [Fact]
    public void StableSpending_RampsFromTodaysSavingTowardsTheBlendedTarget()
    {
        // Six identical months: 10jt in, 5jt needs, 4jt wants. Saving 1jt (10%) today.
        var history = Enumerable.Range(1, 6)
            .Select(m => Month(m, income: 10 * Juta, needs: 5 * Juta, wants: 4 * Juta))
            .ToList();

        var result = SavingsAdvisor.Recommend(history, Today);

        result.HasEnoughData.Should().BeTrue();
        result.MonthsAnalyzed.Should().Be(6);
        result.BaseCurrency.Should().Be("IDR");

        result.CurrentSaving.Should().Be(1 * Juta);

        // 50/30/20 leg: 20% of 10jt, and the ceiling on what is available - essentials untouched,
        // at most half of the 4jt of wants given up.
        result.Allocation.IdealSaving.Should().Be(2 * Juta);
        result.MaxCapacity.Should().Be(3 * Juta);

        // MPS leg: income never moved, so the slope is unidentified and the average propensity to
        // consume (90%) stands in, at a deliberately small weight.
        result.Mps.IsRegression.Should().BeFalse();
        result.Mps.Mps.Should().Be(0.1m);
        result.Mps.Weight.Should().Be(12.5m);

        // Blend: 0.875 x 2jt + 0.125 x 1jt = 1,875,000, floored to the rounding step.
        result.TargetSaving.Should().Be(1_870_000m);

        // SMarT leg: no raise to capture, so the ramp is one point of income a month.
        result.Smart.Raise.Should().Be(0m);
        result.Smart.MonthlyStep.Should().Be(100_000m);
        result.RecommendedSaving.Should().Be(1_100_000m);
        result.RecommendedSavingRate.Should().Be(11m);
        result.Smart.MonthsToTarget.Should().Be(9);
        result.Smart.ReachesTargetWithinHorizon.Should().BeFalse();

        result.Ramp.Should().HaveCount(6);
        result.Ramp.Select(r => r.Amount).Should().BeInAscendingOrder();
        result.Ramp.Should().OnlyContain(r => r.Amount <= result.TargetSaving);
        result.Ramp[0].Month.Should().Be(new DateOnly(2026, 7, 1), "the first rung is next month");

        result.Notes.Should().Contain(n => n.Contains("gaya hidup"));
    }

    [Fact]
    public void MovingIncome_FitsTheConsumptionFunctionAndGivesItFullWeight()
    {
        // C = 2jt + 0.6Y, exactly, over six months of rising income.
        var incomes = new[] { 10m, 10.5m, 11m, 11.5m, 12m, 12.5m };
        var history = incomes.Select((y, i) =>
        {
            var consumption = 2 * Juta + 0.6m * y * Juta;
            return Month(i + 1, y * Juta, needs: consumption * 0.75m, wants: consumption * 0.25m);
        }).ToList();

        var result = SavingsAdvisor.Recommend(history, Today);

        result.Mps.IsRegression.Should().BeTrue();
        result.Mps.Mpc.Should().Be(0.6m);
        result.Mps.Mps.Should().Be(0.4m);
        result.Mps.AutonomousConsumption.Should().Be(2 * Juta);
        result.Mps.RSquared.Should().Be(100m);
        result.Mps.Weight.Should().Be(50m, "six months of a perfect fit earns the full MPS weight");

        // S = (1 - b)Y - a at the baseline income of 12jt, which is what they do in fact save.
        result.Mps.Saving.Should().Be(2.8m * Juta);
        result.CurrentSaving.Should().Be(2.8m * Juta);

        // Already saving more than the blend asks for: hold, do not climb.
        result.TargetSaving.Should().Be(2.8m * Juta);
        result.RecommendedSaving.Should().Be(2.8m * Juta);
        result.Notes.Should().Contain(n => n.Contains("mempertahankannya"));
    }

    [Fact]
    public void PayRise_LiftsTheFirstStepByHalfTheRise()
    {
        // Identical spending in both worlds; the only difference is that income rose 600k.
        var flat = Enumerable.Range(1, 6)
            .Select(m => Month(m, income: 10 * Juta, needs: 6 * Juta, wants: 3 * Juta))
            .ToList();

        var raised = Enumerable.Range(1, 6)
            .Select(m => Month(m, income: m <= 3 ? 9.4m * Juta : 10 * Juta, needs: 6 * Juta, wants: 3 * Juta))
            .ToList();

        var withoutRaise = SavingsAdvisor.Recommend(flat, Today);
        var withRaise = SavingsAdvisor.Recommend(raised, Today);

        withoutRaise.Smart.Raise.Should().Be(0m);
        withRaise.Smart.Raise.Should().Be(600_000m);
        withRaise.Smart.RaiseCapture.Should().Be(300_000m);

        // The extra setoran is funded entirely out of the rise, so nothing has to be given up.
        (withRaise.RecommendedSaving - withoutRaise.RecommendedSaving).Should().Be(300_000m);
        withRaise.RecommendedSaving.Should().BeLessThanOrEqualTo(withRaise.TargetSaving);
    }

    [Fact]
    public void EssentialsOverTheCeiling_CapTheTargetBelowTwentyPercent()
    {
        // 9jt of a 10jt income is committed before anything discretionary happens.
        var history = Enumerable.Range(1, 6)
            .Select(m => Month(m, income: 10 * Juta, needs: 5.5m * Juta, wants: 1 * Juta, debt: 3.5m * Juta))
            .ToList();

        var result = SavingsAdvisor.Recommend(history, Today);

        result.Allocation.EssentialsOverCeiling.Should().BeTrue();
        result.Allocation.EssentialsShare.Should().Be(90m);
        result.Allocation.IdealSaving.Should().Be(2 * Juta);

        // Only half of the 1jt of wants is ever assumed away, so 500k is the ceiling on advice.
        result.MaxCapacity.Should().Be(500_000m);
        result.Allocation.FeasibleSaving.Should().Be(500_000m);
        result.TargetSaving.Should().BeLessThanOrEqualTo(500_000m);
        result.Ramp.Should().OnlyContain(r => r.Amount <= 500_000m);

        result.Notes.Should().Contain(n => n.Contains("pagu 50,00%"));
        result.Notes.Should().Contain(n => n.Contains("batas aman 30%"));
    }

    [Fact]
    public void CommittedInstalments_AreTreatedAsSpentEvenWhenNotRecorded()
    {
        var history = Enumerable.Range(1, 6)
            .Select(m => Month(m, income: 10 * Juta, needs: 5 * Juta, wants: 2 * Juta))
            .ToList();

        var withoutDebt = SavingsAdvisor.Recommend(history, Today);
        var withDebt = SavingsAdvisor.Recommend(history, Today, committedDebtPayment: 2 * Juta);

        withDebt.MonthlyDebtService.Should().Be(2 * Juta);
        withDebt.CurrentSaving.Should().Be(withoutDebt.CurrentSaving - 2 * Juta);
        withDebt.MaxCapacity.Should().Be(withoutDebt.MaxCapacity - 2 * Juta);
        withDebt.Notes.Should().Contain(n => n.Contains("Kewajiban minimum utang aktif"));
    }

    [Fact]
    public void HeadlineFigures_ReportTheBaselineTheyWereAveragedOver_NotTheWholeWindow()
    {
        // A year of history, but the last three months are the ones that describe "now": income
        // doubles in April, and the reported figure follows the recent months rather than the
        // twelve-month mean. The page cites BaselineMonths so it cannot claim otherwise.
        var history = Enumerable.Range(1, 6)
            .Select(m => Month(m, income: m > 3 ? 20 * Juta : 10 * Juta, needs: 5 * Juta, wants: 2 * Juta))
            .ToList();

        var result = SavingsAdvisor.Recommend(history, Today);

        result.MonthsAnalyzed.Should().Be(6);
        result.BaselineMonths.Should().Be(3);
        result.MonthlyIncome.Should().Be(20 * Juta, "only the last three months are averaged");
    }

    [Fact]
    public void SpendingMoreThanIncome_NeverSuggestsANegativeAmount()
    {
        var history = Enumerable.Range(1, 6)
            .Select(m => Month(m, income: 5 * Juta, needs: 6 * Juta, wants: 1 * Juta))
            .ToList();

        var result = SavingsAdvisor.Recommend(history, Today);

        result.CurrentSaving.Should().Be(0m);
        result.MaxCapacity.Should().Be(0m);
        result.TargetSaving.Should().Be(0m);
        result.RecommendedSaving.Should().Be(0m);
        result.Ramp.Should().OnlyContain(r => r.Amount == 0m);
    }
}
