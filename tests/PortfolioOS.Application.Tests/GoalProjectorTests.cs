using FluentAssertions;
using PortfolioOS.Application.Forecasting;

namespace PortfolioOS.Application.Tests;

/// <summary>
/// The projector only accumulates a schedule it is handed, so these cases fix the two things that
/// could quietly go wrong: the arithmetic of when a target lands, and the refusal to produce a date
/// when nothing is being contributed.
/// </summary>
public class GoalProjectorTests
{
    private static readonly DateOnly Today = new(2026, 6, 15);
    private static readonly DateOnly ThisMonth = new(2026, 6, 1);

    private const decimal Juta = 1_000_000m;

    private static List<decimal> Flat(decimal amount, int months = 6) =>
        [.. Enumerable.Repeat(amount, months)];

    [Fact]
    public void SteadyContribution_LandsOnThePlainDivision()
    {
        var result = GoalProjector.Project(
            "Dana darurat", targetAmount: 12 * Juta, currentAmount: 0m,
            contributionRamp: Flat(1 * Juta), today: Today);

        result.IsReachable.Should().BeTrue();
        result.MonthsToTarget.Should().Be(12);
        result.TargetMonth.Should().Be(ThisMonth.AddMonths(12));
        result.Shortfall.Should().Be(12 * Juta);
        result.ProgressShare.Should().Be(0m);
    }

    [Fact]
    public void TheLastRungOfTheRampRepeatsOnceItRunsOut()
    {
        // A Save More Tomorrow ramp is only six rungs long; after that the escalation has landed
        // and the contribution simply stays there.
        List<decimal> ramp = [100_000m, 200_000m, 300_000m, 400_000m, 500_000m, 1 * Juta];

        var result = GoalProjector.Project(
            "Dana darurat", targetAmount: 5.5m * Juta, currentAmount: 0m, contributionRamp: ramp, today: Today);

        result.MonthlyContribution.Should().Be(1 * Juta, "the steady state is the last rung");

        // The five climbing rungs put in 1,5 juta; the sixth is already the full juta. Four more
        // months at that rate clears 5,5 juta, so it lands on the ninth.
        result.MonthsToTarget.Should().Be(9);
        result.Trajectory[5].Contribution.Should().Be(1 * Juta);
        result.Trajectory[6].Contribution.Should().Be(1 * Juta);
    }

    [Fact]
    public void ExistingBalanceCountsTowardsTheTarget()
    {
        var result = GoalProjector.Project(
            "Dana darurat", targetAmount: 12 * Juta, currentAmount: 6 * Juta,
            contributionRamp: Flat(1 * Juta), today: Today);

        result.MonthsToTarget.Should().Be(6);
        result.ProgressShare.Should().Be(50m);
        result.Shortfall.Should().Be(6 * Juta);
    }

    [Fact]
    public void AlreadyFundedTarget_NeedsNoProjection()
    {
        var result = GoalProjector.Project(
            "Dana darurat", targetAmount: 10 * Juta, currentAmount: 12 * Juta,
            contributionRamp: Flat(1 * Juta), today: Today);

        result.IsReachable.Should().BeTrue();
        result.MonthsToTarget.Should().Be(0);
        result.ProgressShare.Should().Be(100m);
        result.Notes.Should().ContainSingle().Which.Should().Contain("sudah tercapai");
    }

    [Fact]
    public void NothingBeingSetAside_RefusesToInventADate()
    {
        var result = GoalProjector.Project(
            "Dana darurat", targetAmount: 12 * Juta, currentAmount: 1 * Juta,
            contributionRamp: [], today: Today);

        result.IsReachable.Should().BeFalse();
        result.MonthsToTarget.Should().BeNull();
        result.TargetMonth.Should().BeNull();
        result.Notes.Should().Contain(n => n.Contains("tidak punya tanggal"));
    }

    [Fact]
    public void ZeroRamp_TerminatesRatherThanSearchingToTheBound()
    {
        var result = GoalProjector.Project(
            "Dana darurat", targetAmount: 12 * Juta, currentAmount: 0m,
            contributionRamp: Flat(0m), today: Today);

        result.IsReachable.Should().BeFalse();
        result.Trajectory.Should().HaveCountLessThan(60,
            "a contribution of nothing stops the search instead of running to the bound");
    }

    [Fact]
    public void NoTarget_ReportsNothingToProjectAgainst()
    {
        var result = GoalProjector.Project(
            "Dana darurat", targetAmount: 0m, currentAmount: 5 * Juta,
            contributionRamp: Flat(1 * Juta), today: Today);

        result.IsReachable.Should().BeFalse();
        result.Notes.Should().ContainSingle().Which.Should().Contain("Target belum ditentukan");
    }

    [Fact]
    public void NoInvestmentReturnIsAssumed()
    {
        var result = GoalProjector.Project(
            "Dana darurat", targetAmount: 12 * Juta, currentAmount: 0m,
            contributionRamp: Flat(1 * Juta), today: Today);

        result.Trajectory[0].Balance.Should().Be(1 * Juta);
        result.Trajectory[5].Balance.Should().Be(6 * Juta, "contributions accumulate at face value");
        result.Notes.Should().Contain(n => n.Contains("tidak mengasumsikan imbal hasil"));
    }
}
