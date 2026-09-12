using PortfolioOS.Application.Savings;

namespace PortfolioOS.Application.Forecasting;

/// <summary>
/// Where the monthly cash flow is heading, and how much of that is guesswork.
/// </summary>
/// <remarks>
/// Every monetary field is in <see cref="CashflowForecaster.BaseCurrency"/> and every
/// <c>*Rate</c> / <c>*Share</c> field is a percentage (20 means 20%), matching the rest of the app.
/// <para>
/// Read <see cref="Forecast"/> as a band, never as a line. The P50 column is the middle of a range,
/// not a prediction, and the distance to P10 is the honest part of the answer: a household whose
/// spending swings by 3 juta a month has a band 3 juta wide, and no amount of modelling narrows it.
/// </para>
/// </remarks>
/// <param name="OpeningCash">
/// Cash on hand the cumulative column starts from — ledger asset accounts that hold spendable
/// money, per <see cref="CashAccountClassifier"/>. Zero when no such account is recognised, which
/// turns the cumulative column into a change rather than a balance.
/// </param>
/// <param name="Dispersion">
/// One standard deviation of monthly net cash flow, estimated from the history. This sets the
/// width of every band in <see cref="Forecast"/>.
/// </param>
/// <param name="DispersionIsAssumed">
/// True when the history was too short or too flat to measure <see cref="Dispersion"/> and a
/// deliberately wide assumption was used instead. A band the user should trust less.
/// </param>
/// <param name="FirstDeficitMonth">
/// First month where the middle of the band is negative — spending exceeds income. Null when no
/// projected month does.
/// </param>
/// <param name="CashRunsOutMonth">
/// First month where cumulative cash is projected to go below zero. This is the number the whole
/// forecast exists to produce.
/// </param>
/// <param name="RunwayMonths">
/// How many months the cash on hand would cover if income stopped today. Null when there is no
/// recognised cash balance to measure.
/// </param>
public record CashflowForecastDto(
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
    IReadOnlyList<CashflowForecastMonthDto> Forecast,
    IReadOnlyList<MonthlyCashflowDto> History,
    IReadOnlyList<string> Notes);

/// <summary>One projected month.</summary>
/// <remarks>
/// The monthly band (<see cref="NetP10"/>..<see cref="NetP90"/>) has the same width every month,
/// because a level model says next June is no harder to call than next month. The cumulative band
/// widens as the square root of the horizon, which is what independent monthly errors do when they
/// are added up — and is why the fan opens out to the right rather than staying parallel.
/// </remarks>
/// <param name="DebtService">
/// Taken from the amortisation schedule when one was supplied, so it falls on the month a debt is
/// actually cleared rather than being averaged across the horizon.
/// </param>
public record CashflowForecastMonthDto(
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
