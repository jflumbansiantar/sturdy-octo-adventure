using FluentAssertions;
using PortfolioOS.Application.Forecasting;
using PortfolioOS.Application.Savings;

namespace PortfolioOS.Application.Tests;

/// <summary>
/// The forecaster is pure arithmetic over a month series, so each case is written as the series
/// plus the band a user would be shown. Several cases assert what the design refuses to do —
/// extrapolate a trend, inflate discretionary spending, carry a cleared instalment forward —
/// because those are the choices most likely to be undone by accident later.
/// </summary>
public class CashflowForecasterTests
{
    private static readonly DateOnly Today = new(2026, 6, 15);
    private static readonly DateOnly ThisMonth = new(2026, 6, 1);

    private const decimal Juta = 1_000_000m;

    /// <param name="month">1-based month of 2026.</param>
    private static MonthlyCashflow Month(
        int month, decimal income, decimal needs, decimal wants, decimal debt = 0m) =>
        new(new DateOnly(2026, month, 1), income, needs, wants, debt);

    private static List<MonthlyCashflow> Steady(
        int count, decimal income, decimal needs, decimal wants, decimal debt = 0m) =>
        [.. Enumerable.Range(1, count).Select(m => Month(m, income, needs, wants, debt))];

    [Fact]
    public void NoTransactions_ReportsNotEnoughData()
    {
        var result = CashflowForecaster.Forecast([], Today);

        result.HasEnoughData.Should().BeFalse();
        result.Forecast.Should().BeEmpty();
        result.Notes.Should().Contain(n => n.Contains("Income dan Expense"));
    }

    [Fact]
    public void MonthWithIncomeButNoSpending_IsSkippedRatherThanReadAsPerfectThrift()
    {
        var history = new List<MonthlyCashflow>
        {
            Month(5, income: 10 * Juta, needs: 0m, wants: 0m),
            Month(6, income: 10 * Juta, needs: 5 * Juta, wants: 4 * Juta),
        };

        var result = CashflowForecaster.Forecast(history, Today);

        result.MonthsAnalyzed.Should().Be(1);
        result.History.Should().HaveCount(2, "a skipped month is still shown, so the gap is visible");
        result.Notes.Should().Contain(n => n.Contains("terlalu optimis"));
    }

    [Fact]
    public void SteadyMonths_ProjectTheSameNetCashFlowForward()
    {
        // 10jt in, 5jt needs, 3jt wants, nothing owed. 2jt a month left over, less the drift on
        // kebutuhan — which is the only component allowed to creep.
        var result = CashflowForecaster.Forecast(Steady(6, 10 * Juta, 5 * Juta, 3 * Juta), Today, horizonMonths: 12);

        result.HasEnoughData.Should().BeTrue();
        result.MonthlyIncome.Should().Be(10 * Juta);
        result.MonthlyNeeds.Should().Be(5 * Juta);
        result.MonthlyWants.Should().Be(3 * Juta);
        result.Forecast.Should().HaveCount(12);

        result.Forecast[0].NetP50.Should().BeInRange(1.98m * Juta, 2 * Juta);
        result.Forecast[^1].NetP50.Should().BeLessThan(result.Forecast[0].NetP50,
            "kebutuhan drifts with inflation while income is held flat");
    }

    [Fact]
    public void RisingIncome_IsNotExtrapolatedIntoARaiseNobodyPromised()
    {
        // Five months climbing 8→12 juta. A trend model would project 13, then 14. A salary is a
        // step function, so the level is carried forward and a real raise arrives as a new level.
        var history = new List<MonthlyCashflow>
        {
            Month(2, income: 8 * Juta, needs: 4 * Juta, wants: 2 * Juta),
            Month(3, income: 9 * Juta, needs: 4 * Juta, wants: 2 * Juta),
            Month(4, income: 10 * Juta, needs: 4 * Juta, wants: 2 * Juta),
            Month(5, income: 11 * Juta, needs: 4 * Juta, wants: 2 * Juta),
            Month(6, income: 12 * Juta, needs: 4 * Juta, wants: 2 * Juta),
        };

        var result = CashflowForecaster.Forecast(history, Today, horizonMonths: 6);

        result.MonthlyIncome.Should().Be(10 * Juta, "the median of the window, not its last value");
        result.Forecast.Should().OnlyContain(r => r.Income == 10 * Juta);
        result.Notes.Should().Contain(n => n.Contains("kenaikan gaji tidak"));
    }

    [Fact]
    public void OnlyNeedsCarryInflation_WantsAreLeftFlat()
    {
        var result = CashflowForecaster.Forecast(Steady(6, 10 * Juta, 5 * Juta, 3 * Juta), Today, horizonMonths: 12);

        result.Forecast.Should().OnlyContain(r => r.Wants == 3 * Juta,
            "discretionary spending is substituted, not paid up");
        result.Forecast[^1].Needs.Should().BeGreaterThan(result.Forecast[0].Needs);

        // 3% a year over twelve months, on 5 juta, is about 150rb.
        (result.Forecast[^1].Needs - 5 * Juta).Should().BeApproximately(150_000m, 10_000m);
    }

    [Fact]
    public void IdenticalMonths_AdmitTheBandIsAssumedRatherThanMeasured()
    {
        var result = CashflowForecaster.Forecast(Steady(6, 10 * Juta, 5 * Juta, 3 * Juta), Today);

        result.DispersionIsAssumed.Should().BeTrue("six identical months measure no spread at all");
        result.Dispersion.Should().Be(1.5m * Juta, "15% of income, the deliberately wide fallback");
        result.Notes.Should().Contain(n => n.Contains("sengaja dibuat lebar"));
    }

    [Fact]
    public void VaryingMonths_MeasureTheBandFromTheHistory()
    {
        // Nets run 2,1,3,2,1,3 juta. Median 2, absolute deviations 0,1,1,0,1,1 → MAD 1 juta.
        var history = new List<MonthlyCashflow>
        {
            Month(1, 10 * Juta, 5 * Juta, 3 * Juta),
            Month(2, 10 * Juta, 5 * Juta, 4 * Juta),
            Month(3, 10 * Juta, 5 * Juta, 2 * Juta),
            Month(4, 10 * Juta, 5 * Juta, 3 * Juta),
            Month(5, 10 * Juta, 5 * Juta, 4 * Juta),
            Month(6, 10 * Juta, 5 * Juta, 2 * Juta),
        };

        var result = CashflowForecaster.Forecast(history, Today);

        result.DispersionIsAssumed.Should().BeFalse();
        result.Dispersion.Should().BeApproximately(1.4826m * Juta, 1_000m, "1,4826 × MAD");
        result.Notes.Should().Contain(n => n.Contains("naik-turun dari"));
    }

    [Fact]
    public void MonthlyBandStaysParallelWhileTheCumulativeOneOpensAsSqrtOfHorizon()
    {
        var result = CashflowForecaster.Forecast(
            Steady(6, 10 * Juta, 5 * Juta, 3 * Juta), Today, horizonMonths: 12);

        var firstWidth = result.Forecast[0].NetP90 - result.Forecast[0].NetP50;
        var lastWidth = result.Forecast[^1].NetP90 - result.Forecast[^1].NetP50;
        lastWidth.Should().Be(firstWidth, "a level model says next June is no harder to call");

        // Four months of independent error add in quadrature: √4 = 2.
        var atOne = result.Forecast[0].CumulativeP90 - result.Forecast[0].CumulativeP50;
        var atFour = result.Forecast[3].CumulativeP90 - result.Forecast[3].CumulativeP50;
        atFour.Should().BeApproximately(2m * atOne, 1_000m);
    }

    [Fact]
    public void SpendingAboveIncome_FlagsTheFirstDeficitMonth()
    {
        var result = CashflowForecaster.Forecast(
            Steady(6, income: 10 * Juta, needs: 7 * Juta, wants: 5 * Juta), Today);

        result.FirstDeficitMonth.Should().Be(ThisMonth.AddMonths(1));
        result.Forecast[0].NetP50.Should().BeLessThan(0m);
        result.Notes.Should().Contain(n => n.Contains("melebihi"));
    }

    [Fact]
    public void ClearedInstalmentStopsBeingDeducted()
    {
        // The schedule says the loan closes after two months. From the third the money is the
        // household's again, and the projection has to show that jump.
        var history = Steady(6, income: 10 * Juta, needs: 5 * Juta, wants: 2 * Juta, debt: 1 * Juta);
        decimal[] schedule = [1 * Juta, 1 * Juta, 0m, 0m, 0m, 0m];

        var result = CashflowForecaster.Forecast(history, Today, schedule, horizonMonths: 6);

        result.DebtServiceIsScheduled.Should().BeTrue();
        result.Forecast[1].DebtService.Should().Be(1 * Juta);
        result.Forecast[2].DebtService.Should().Be(0m);
        result.Forecast[2].NetP50.Should().BeGreaterThan(result.Forecast[1].NetP50 + 900_000m);
        result.Notes.Should().Contain(n => n.Contains("melompat naik"));
    }

    [Fact]
    public void WithoutASchedule_TheRecordedInstalmentIsCarriedFlat()
    {
        var history = Steady(6, income: 10 * Juta, needs: 5 * Juta, wants: 2 * Juta, debt: 1 * Juta);

        var result = CashflowForecaster.Forecast(history, Today, horizonMonths: 6);

        result.DebtServiceIsScheduled.Should().BeFalse();
        result.Forecast.Should().OnlyContain(r => r.DebtService == 1 * Juta);
        result.Notes.Should().Contain(n => n.Contains("dibawa datar"));
    }

    [Fact]
    public void OpeningCash_TurnsTheCumulativeColumnIntoABalanceAndGivesARunway()
    {
        var history = Steady(6, income: 10 * Juta, needs: 5 * Juta, wants: 3 * Juta);

        var result = CashflowForecaster.Forecast(
            history, Today, openingCash: 24 * Juta, horizonMonths: 6);

        result.OpeningCash.Should().Be(24 * Juta);
        result.RunwayMonths.Should().Be(3, "24 juta against 8 juta of monthly spending");
        result.Forecast[0].CumulativeP50.Should().BeGreaterThan(24 * Juta, "the month is in surplus");
        result.Notes.Should().Contain(n => n.Contains("sisa napas") || n.Contains("bulan pengeluaran"));
    }

    [Fact]
    public void WithoutACashAccount_NoRunwayIsClaimed()
    {
        var result = CashflowForecaster.Forecast(Steady(6, 10 * Juta, 5 * Juta, 3 * Juta), Today);

        result.OpeningCash.Should().Be(0m);
        result.RunwayMonths.Should().BeNull();
        result.CashRunsOutMonth.Should().BeNull(
            "a balance of nothing cannot run out, and saying so would read as a warning");
        result.Notes.Should().Contain(n => n.Contains("perubahan kas, bukan saldo"));
    }

    [Fact]
    public void DeficitAgainstARealBalance_ReportsWhenTheCashIsGone()
    {
        // 2 juta short every month against 10 juta of cash: gone in the fifth month.
        var history = Steady(6, income: 10 * Juta, needs: 7 * Juta, wants: 5 * Juta);

        var result = CashflowForecaster.Forecast(
            history, Today, openingCash: 10 * Juta, horizonMonths: 12);

        result.CashRunsOutMonth.Should().Be(ThisMonth.AddMonths(5));
        result.Notes.Should().Contain(n => n.Contains("Kas diproyeksikan habis"));
    }

    [Fact]
    public void HorizonIsClampedToThePolicyCeiling()
    {
        var result = CashflowForecaster.Forecast(
            Steady(6, 10 * Juta, 5 * Juta, 3 * Juta), Today, horizonMonths: 120);

        result.HorizonMonths.Should().Be(ForecastPolicy.Default.MaxHorizonMonths);
        result.Forecast.Should().HaveCount(ForecastPolicy.Default.MaxHorizonMonths);
    }

    [Fact]
    public void ForecastStartsTheMonthAfterToday()
    {
        var result = CashflowForecaster.Forecast(Steady(6, 10 * Juta, 5 * Juta, 3 * Juta), Today, horizonMonths: 3);

        result.Forecast[0].Month.Should().Be(new DateOnly(2026, 7, 1),
            "the current month is history, not forecast");
        result.Forecast.Select(r => r.MonthOffset).Should().Equal(1, 2, 3);
    }
}
