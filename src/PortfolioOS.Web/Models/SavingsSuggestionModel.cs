namespace PortfolioOS.Web.Models;

/// <summary>Mirror of the API's savings suggestion. Money in IDR, every rate a percentage.</summary>
public record SavingsSuggestionModel(
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
    AllocationMethodModel Allocation,
    MpsMethodModel Mps,
    SmartMethodModel Smart,
    IReadOnlyList<SavingsRampStepModel> Ramp,
    IReadOnlyList<MonthlyCashflowModel> History,
    IReadOnlyList<string> Notes);

public record AllocationMethodModel(
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

public record MpsMethodModel(
    decimal Mpc,
    decimal Mps,
    decimal AutonomousConsumption,
    decimal RSquared,
    decimal Confidence,
    decimal Weight,
    decimal Saving,
    bool IsRegression);

public record SmartMethodModel(
    decimal Raise,
    decimal RaiseCaptureRate,
    decimal EscalationRate,
    decimal RaiseCapture,
    decimal MonthlyStep,
    int MonthsToTarget,
    bool ReachesTargetWithinHorizon);

public record SavingsRampStepModel(int MonthOffset, DateOnly Month, decimal Amount, decimal Rate);

public record MonthlyCashflowModel(
    DateOnly Month,
    decimal Income,
    decimal Needs,
    decimal DebtService,
    decimal Wants,
    decimal Saving,
    decimal SavingRate);
