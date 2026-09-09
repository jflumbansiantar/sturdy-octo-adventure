namespace PortfolioOS.Application.Savings;

/// <summary>
/// The tunable constants behind a savings suggestion — one knob per assumption, so every number
/// the advisor produces can be traced back to a value that is visible and arguable.
/// </summary>
/// <remarks>
/// The three methods the advisor combines each contribute their own parameters:
/// <list type="bullet">
/// <item><b>Alokasi proporsional 50/30/20</b> — <see cref="NeedsShare"/>, <see cref="WantsShare"/>,
/// <see cref="TargetSavingShare"/>, <see cref="MaxWantsCut"/>.</item>
/// <item><b>Keynesian MPS</b> — <see cref="MinRegressionMonths"/>, <see cref="FullConfidenceMonths"/>,
/// <see cref="MaxMpsWeight"/>, <see cref="FallbackConfidence"/>, <see cref="MinMpc"/>, <see cref="MaxMpc"/>.</item>
/// <item><b>Save More Tomorrow</b> — <see cref="RaiseCaptureRate"/>, <see cref="EscalationRate"/>,
/// <see cref="HorizonMonths"/>.</item>
/// </list>
/// </remarks>
public sealed record SavingsPolicy
{
    /// <summary>Ceiling for essentials (kebutuhan + cicilan) in the 50/30/20 rule.</summary>
    public decimal NeedsShare { get; init; } = 0.50m;

    /// <summary>Ceiling for discretionary spending (keinginan) in the 50/30/20 rule.</summary>
    public decimal WantsShare { get; init; } = 0.30m;

    /// <summary>The normative savings target of the rule: 20% of take-home income.</summary>
    public decimal TargetSavingShare { get; init; } = 0.20m;

    /// <summary>
    /// How much of today's discretionary spending the advisor is willing to assume away when it
    /// works out the maximum that could physically be saved. Half is deliberate: a plan built on
    /// "stop wanting anything" is a plan nobody follows, and it would let the target exceed what
    /// the SMarT ramp can ever reach.
    /// </summary>
    public decimal MaxWantsCut { get; init; } = 0.50m;

    /// <summary>Months averaged to describe "now". Long enough to absorb one odd month.</summary>
    public int BaselineMonths { get; init; } = 3;

    /// <summary>
    /// Below this many observations the consumption function is fitted as a plain average
    /// propensity to consume instead of a regression — with three points a straight line fits
    /// almost perfectly and the R² it reports would be self-flattery.
    /// </summary>
    public int MinRegressionMonths { get; init; } = 4;

    /// <summary>Sample size at which the regression is trusted at its full R².</summary>
    public int FullConfidenceMonths { get; init; } = 6;

    /// <summary>
    /// Cap on how much the behavioural (MPS) estimate may pull the target away from the 50/30/20
    /// anchor. Even a perfect fit only describes what this person has done so far, so it gets a
    /// vote, never a veto.
    /// </summary>
    public decimal MaxMpsWeight { get; init; } = 0.50m;

    /// <summary>Weight given to the MPS leg when it came from an average rather than a fit.</summary>
    public decimal FallbackConfidence { get; init; } = 0.25m;

    /// <summary>Floor for the estimated marginal propensity to consume.</summary>
    public decimal MinMpc { get; init; } = 0.05m;

    /// <summary>Ceiling for the estimated marginal propensity to consume.</summary>
    public decimal MaxMpc { get; init; } = 0.99m;

    /// <summary>
    /// Share of a pay rise that goes straight to savings. This is the mechanism SMarT is built
    /// on: the increase is funded by money the household has not started spending yet, so
    /// take-home pay never falls and loss aversion never gets a chance to fire.
    /// </summary>
    public decimal RaiseCaptureRate { get; init; } = 0.50m;

    /// <summary>
    /// Automatic escalation per month, as a share of income, applied on top of any raise capture.
    /// One percentage point a month is small enough to be invisible in a monthly budget and still
    /// closes a twelve-point gap inside a year.
    /// </summary>
    public decimal EscalationRate { get; init; } = 0.01m;

    /// <summary>How many months of the escalation ladder to spell out.</summary>
    public int HorizonMonths { get; init; } = 6;

    /// <summary>
    /// Suggested amounts are floored to this step. Rounding down rather than to nearest keeps a
    /// rounded figure inside the capacity that was just proven affordable.
    /// </summary>
    public decimal RoundingStep { get; init; } = 10_000m;

    public static SavingsPolicy Default { get; } = new();
}
