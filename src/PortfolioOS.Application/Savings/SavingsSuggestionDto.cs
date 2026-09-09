namespace PortfolioOS.Application.Savings;

/// <summary>
/// How much this person could put aside each month, and the reasoning behind the figure.
/// </summary>
/// <remarks>
/// Every monetary field is in <see cref="BaseCurrency"/> and every <c>*Rate</c> / <c>*Share</c>
/// field is a percentage (20 means 20%), matching the rest of the app.
/// <para>
/// Two numbers matter most and they are deliberately different: <see cref="TargetSaving"/> is
/// where the plan lands, <see cref="RecommendedSaving"/> is what to set aside *this* month. The
/// gap between them is the Save More Tomorrow ramp — asking for the target on day one is exactly
/// the move that makes savings plans get abandoned.
/// </para>
/// </remarks>
/// <param name="HasEnoughData">
/// False when there is no recorded income to reason from. Every figure is then zero and
/// <see cref="Notes"/> says what to record first.
/// </param>
/// <param name="BaselineMonths">
/// How many of the analysed months were averaged into the headline figures. Only the most recent
/// few describe "now", so this is the number to quote next to them - <see cref="MonthsAnalyzed"/>
/// counts every month the window could read, which is a longer and different span.
/// </param>
/// <param name="MaxCapacity">
/// The most that could be saved per month without touching essentials, allowing for a cut of at
/// most half of discretionary spending. Nothing the advisor suggests ever exceeds this.
/// </param>
public record SavingsSuggestionDto(
    bool HasEnoughData,
    int MonthsAnalyzed,
    int BaselineMonths,
    string BaseCurrency,
    decimal MonthlyIncome,
    decimal MonthlyNeeds,
    decimal MonthlyDebtService,
    decimal MonthlyWants,
    decimal CurrentSaving,
    decimal CurrentSavingRate,
    decimal RecommendedSaving,
    decimal RecommendedSavingRate,
    decimal TargetSaving,
    decimal TargetSavingRate,
    decimal MaxCapacity,
    AllocationMethodDto Allocation,
    MpsMethodDto Mps,
    SmartMethodDto Smart,
    IReadOnlyList<SavingsRampStepDto> Ramp,
    IReadOnlyList<MonthlyCashflowDto> History,
    IReadOnlyList<string> Notes);

/// <summary>The 50/30/20 leg: what the rule says this income should produce.</summary>
/// <param name="EssentialsShare">Kebutuhan + cicilan as a share of income, against a 50% ceiling.</param>
/// <param name="IdealSaving">A flat 20% of income — the rule's answer, before feasibility.</param>
/// <param name="FeasibleSaving">
/// <paramref name="IdealSaving"/> capped by what is actually available. They differ exactly when
/// essentials have crowded the rule out.
/// </param>
public record AllocationMethodDto(
    decimal EssentialsShare,
    decimal WantsShare,
    decimal SavingShare,
    decimal EssentialsCeiling,
    decimal WantsCeiling,
    decimal TargetShare,
    decimal IdealSaving,
    decimal FeasibleSaving,
    bool EssentialsOverCeiling,
    bool WantsOverCeiling);

/// <summary>
/// The Keynesian leg: consumption fitted as <c>C = a + b·Y</c>, so savings follow as
/// <c>S = (1 − b)·Y − a</c>.
/// </summary>
/// <param name="Mpc">b — the share of each extra rupiah of income that gets consumed.</param>
/// <param name="Mps">1 − b — the marginal propensity to save.</param>
/// <param name="AutonomousConsumption">a — spending that happens at any income level.</param>
/// <param name="RSquared">Fit quality, 0-100. Zero when the average was used instead.</param>
/// <param name="Confidence">
/// How much weight this leg earns in the blend, 0-100: R² discounted by how short the history is.
/// </param>
/// <param name="IsRegression">False when too few months forced the average-propensity fallback.</param>
public record MpsMethodDto(
    decimal Mpc,
    decimal Mps,
    decimal AutonomousConsumption,
    decimal RSquared,
    decimal Confidence,
    decimal Weight,
    decimal Saving,
    bool IsRegression);

/// <summary>The Save More Tomorrow leg: how fast to close the gap, not how big it is.</summary>
/// <param name="Raise">Income growth against the previous block of months, zero if none.</param>
/// <param name="MonthlyStep">Raise capture plus one month of automatic escalation.</param>
/// <param name="MonthsToTarget">Months until the ramp reaches the target at this step.</param>
/// <param name="ReachesTargetWithinHorizon">False when the ramp is still climbing at the last step.</param>
public record SmartMethodDto(
    decimal Raise,
    decimal RaiseCaptureRate,
    decimal EscalationRate,
    decimal RaiseCapture,
    decimal MonthlyStep,
    int MonthsToTarget,
    bool ReachesTargetWithinHorizon);

/// <summary>One rung of the escalation ladder.</summary>
public record SavingsRampStepDto(int MonthOffset, DateOnly Month, decimal Amount, decimal Rate);

/// <summary>One observed month, as the advisor read it.</summary>
public record MonthlyCashflowDto(
    DateOnly Month,
    decimal Income,
    decimal Needs,
    decimal DebtService,
    decimal Wants,
    decimal Saving,
    decimal SavingRate);
