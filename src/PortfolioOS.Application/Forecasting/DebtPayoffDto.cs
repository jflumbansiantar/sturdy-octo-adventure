namespace PortfolioOS.Application.Forecasting;

/// <summary>
/// A full repayment schedule for every active debt, and what it costs.
/// </summary>
/// <remarks>
/// Nothing in here is estimated. Balance, rate and instalment are recorded facts, and compound
/// interest is arithmetic — so unlike <see cref="CashflowForecastDto"/> this projection carries no
/// error band. The only assumption is that the user keeps paying what they said they would pay.
/// <para>
/// Every monetary field is in <see cref="DebtAmortizer.BaseCurrency"/>; every <c>*Rate</c> field is
/// a percentage (27 means 27%), matching the rest of the app.
/// </para>
/// </remarks>
/// <param name="MonthlyBudget">
/// Total outlay per month the plan holds constant: the sum of every minimum payment, plus
/// <paramref name="ExtraPayment"/>. When a debt closes, its instalment is not saved — it rolls
/// onto the next debt, which is the entire mechanism behind both strategies.
/// </param>
/// <param name="MonthsToDebtFree">Null when the plan never clears the debt. See <paramref name="AllPaidOff"/>.</param>
public record DebtPayoffPlanDto(
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
    IReadOnlyList<DebtPayoffItemDto> Debts,
    IReadOnlyList<DebtScheduleRowDto> Schedule,
    StrategyComparisonDto? Comparison,
    IReadOnlyList<string> Notes);

/// <summary>One debt's outcome under the chosen strategy.</summary>
/// <param name="Priority">
/// 1-based position in the payoff queue. The debt at 1 receives every rupiah of surplus until it
/// closes — which is what distinguishes avalanche from snowball, since they differ only in how
/// this queue is sorted.
/// </param>
/// <param name="MinimumPayment">
/// The instalment actually used. Equal to the recorded minimum, unless that was zero and a tenor
/// was available to derive an annuity payment from.
/// </param>
/// <param name="PaysOff">
/// False when the instalment never covers the interest. The balance then grows every month and no
/// payoff date exists — the honest answer, and the one the recorded numbers imply.
/// </param>
public record DebtPayoffItemDto(
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

/// <summary>One month of the combined schedule, across every debt.</summary>
public record DebtScheduleRowDto(
    int MonthOffset,
    DateOnly Month,
    decimal Payment,
    decimal Interest,
    decimal Principal,
    decimal RemainingBalance);

/// <summary>
/// What the chosen plan is worth, against the two alternatives.
/// </summary>
/// <remarks>
/// <paramref name="MinimumOnlyInterest"/> is the do-nothing baseline: every minimum paid, no extra,
/// and nothing rolled over when a debt closes. The gap against it therefore prices two decisions at
/// once — adding the extra payment, and rolling freed instalments forward — which is the pair the
/// user is actually choosing between.
/// </remarks>
/// <param name="Better">
/// Which of the two strategies costs less interest, or "Sama" when the difference is under one
/// rupiah — as it always is with a single debt, where the queue has nothing to sort.
/// </param>
public record StrategyComparisonDto(
    int? AvalancheMonths,
    decimal AvalancheInterest,
    int? SnowballMonths,
    decimal SnowballInterest,
    int? MinimumOnlyMonths,
    decimal MinimumOnlyInterest,
    decimal InterestSavedVsMinimumOnly,
    int? MonthsSavedVsMinimumOnly,
    string Better);
