namespace PortfolioOS.Application.Forecasting;

/// <summary>
/// The tunable constants behind every projection — one knob per assumption, so each forecast
/// number can be traced back to a value that is visible and arguable.
/// </summary>
/// <remarks>
/// The forecaster deliberately has fewer knobs than <see cref="Savings.SavingsPolicy"/>, because
/// it makes fewer claims. With a history of at most 24 monthly observations, a level model plus
/// an honest error band beats anything with a trend term in it: a slope fitted to eight points of
/// household spending is noise given a straight line to follow.
/// </remarks>
public sealed record ForecastPolicy
{
    /// <summary>How many months ahead to project by default.</summary>
    public int HorizonMonths { get; init; } = 12;

    /// <summary>
    /// Ceiling on the horizon. Two years out, a forecast built on a level model is no longer
    /// telling the user anything they could act on this month.
    /// </summary>
    public int MaxHorizonMonths { get; init; } = 24;

    /// <summary>
    /// Months averaged into the level the forecast starts from. Longer than the savings
    /// advisor's baseline of three: the advisor describes "now" and wants to react to a recent
    /// change, a forecast wants the steadier number.
    /// </summary>
    public int BaselineMonths { get; init; } = 6;

    /// <summary>
    /// Below this many observations the dispersion measured from the history is not believable
    /// and <see cref="FallbackDispersionShare"/> is used instead.
    /// </summary>
    public int MinMonthsForBand { get; init; } = 4;

    /// <summary>
    /// Price drift applied to kebutuhan, as an annual rate. Wants are deliberately left flat —
    /// see <see cref="CashflowForecaster"/> for why only one of the two gets a drift term.
    /// </summary>
    public decimal AnnualInflationRate { get; init; } = 0.03m;

    /// <summary>
    /// Scale factor turning a median absolute deviation into a standard deviation under
    /// normality. MAD is used rather than the standard deviation itself because one holiday
    /// month would otherwise set the width of the whole band.
    /// </summary>
    public decimal MadToSigma { get; init; } = 1.4826m;

    /// <summary>Standard normal quantile for the 10th/90th percentile band.</summary>
    public decimal BandZ { get; init; } = 1.2816m;

    /// <summary>
    /// Dispersion assumed, as a share of monthly income, when the history is too short or too
    /// flat to measure it. Deliberately wide: an unmeasurable band should look uncertain.
    /// </summary>
    public decimal FallbackDispersionShare { get; init; } = 0.15m;

    /// <summary>
    /// Hard stop for the amortisation loop. A debt whose payment does not cover its interest
    /// never amortises at all, so the schedule needs a bound that does not depend on the balance
    /// ever reaching zero. Fifty years is past the point where the answer is "this plan does not
    /// work" rather than a date.
    /// </summary>
    public int MaxAmortizationMonths { get; init; } = 600;

    /// <summary>
    /// Months of essential spending an emergency fund should cover. Six is the common rule for a
    /// single income; the goal query accepts an override.
    /// </summary>
    public int EmergencyFundMonths { get; init; } = 6;

    /// <summary>
    /// Converts the annual percentage on a debt to a monthly one when the debt carries no
    /// explicit monthly rate. Twelve, not a twelfth root, because that is the convention the rest
    /// of this app already uses: the seeded 27% p.a. card carries 2,25% per month, which is
    /// 27/12. Compounding it properly would give 2,01% and contradict the app's own data.
    /// </summary>
    public decimal MonthsPerYear { get; init; } = 12m;

    public static ForecastPolicy Default { get; } = new();

    /// <summary>Monthly equivalent of <see cref="AnnualInflationRate"/>, compounded.</summary>
    public decimal MonthlyInflationRate =>
        (decimal)(Math.Pow(1d + (double)AnnualInflationRate, 1d / 12d) - 1d);
}
