namespace PortfolioOS.Web.Models;

/// <summary>
/// Mirror of the API's cash-flow forecast. Money in IDR, every rate a percentage.
/// </summary>
/// <remarks>
/// Read the forecast rows as a band, never as a line. The P50 column is the middle of a range and
/// the distance out to P10 is the honest part of the answer — the page is laid out to keep those
/// two together for that reason.
/// </remarks>
public record CashflowForecastModel(
    bool HasEnoughData,
    int MonthsAnalyzed,
    int BaselineMonths,
    int HorizonMonths,
    string BaseCurrency,
    decimal OpeningCash,
    decimal MonthlyIncome,
    decimal MonthlyNeeds,
    decimal MonthlyWants,
    decimal MonthlyDebtService,
    decimal ExpectedMonthlyNet,
    decimal Dispersion,
    bool DispersionIsAssumed,
    decimal AnnualInflationRate,
    bool DebtServiceIsScheduled,
    DateOnly? FirstDeficitMonth,
    DateOnly? CashRunsOutMonth,
    int? RunwayMonths,
    IReadOnlyList<CashflowForecastMonthModel> Forecast,
    IReadOnlyList<MonthlyCashflowModel> History,
    IReadOnlyList<string> Notes);

/// <summary>One projected month, as a band.</summary>
public record CashflowForecastMonthModel(
    int MonthOffset,
    DateOnly Month,
    decimal Income,
    decimal Needs,
    decimal Wants,
    decimal DebtService,
    decimal NetP50,
    decimal NetP10,
    decimal NetP90,
    decimal CumulativeP50,
    decimal CumulativeP10,
    decimal CumulativeP90);

/// <summary>Mirror of the API's debt payoff plan. Nothing in it is estimated.</summary>
public record DebtPayoffPlanModel(
    bool HasDebts,
    string BaseCurrency,
    string Strategy,
    DateOnly StartMonth,
    decimal TotalBalance,
    decimal MonthlyMinimum,
    decimal ExtraPayment,
    decimal MonthlyBudget,
    decimal TotalInterest,
    decimal TotalPaid,
    int? MonthsToDebtFree,
    DateOnly? DebtFreeMonth,
    bool AllPaidOff,
    IReadOnlyList<DebtPayoffItemModel> Debts,
    IReadOnlyList<DebtScheduleRowModel> Schedule,
    StrategyComparisonModel? Comparison,
    IReadOnlyList<string> Notes);

/// <param name="PaysOff">
/// False when the instalment never covers the interest, so the balance grows and there is no
/// payoff date. The page has to show that as a warning rather than an empty cell.
/// </param>
public record DebtPayoffItemModel(
    Guid Id,
    string Name,
    string Type,
    int Priority,
    decimal Balance,
    decimal AnnualRate,
    decimal MonthlyRate,
    decimal MinimumPayment,
    decimal MonthlyInterestAtStart,
    int? MonthsToPayoff,
    DateOnly? PayoffMonth,
    decimal TotalInterest,
    bool PaysOff);

public record DebtScheduleRowModel(
    int MonthOffset,
    DateOnly Month,
    decimal Payment,
    decimal Interest,
    decimal Principal,
    decimal RemainingBalance);

public record StrategyComparisonModel(
    int? AvalancheMonths,
    decimal AvalancheInterest,
    int? SnowballMonths,
    decimal SnowballInterest,
    int? MinimumOnlyMonths,
    decimal MinimumOnlyInterest,
    decimal InterestSavedVsMinimumOnly,
    int? MonthsSavedVsMinimumOnly,
    string Better);

/// <summary>Mirror of the API's goal projection.</summary>
public record GoalProjectionModel(
    bool IsReachable,
    string BaseCurrency,
    string GoalName,
    decimal TargetAmount,
    decimal CurrentAmount,
    decimal Shortfall,
    decimal MonthlyContribution,
    int? MonthsToTarget,
    DateOnly? TargetMonth,
    decimal ProgressShare,
    IReadOnlyList<GoalMilestoneModel> Trajectory,
    IReadOnlyList<string> Notes);

public record GoalMilestoneModel(
    int MonthOffset,
    DateOnly Month,
    decimal Contribution,
    decimal Balance,
    decimal ProgressShare);
