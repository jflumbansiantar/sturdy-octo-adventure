using MediatR;

namespace PortfolioOS.Application.Forecasting.Queries.GetCashflowForecast;

/// <param name="Months">How many months back to read, counting the current one. Clamped to 3-24.</param>
/// <param name="Horizon">How many months ahead to project. Clamped to 1-24.</param>
/// <param name="ExtraPayment">
/// Extra debt repayment to assume. Only has an effect alongside a rollover strategy — the point of
/// the pair is to let the page answer "what does my cash flow look like if I follow the payoff plan".
/// </param>
/// <param name="DebtStrategy">
/// How instalments are assumed to be paid over the horizon.
/// <see cref="PayoffStrategy.MinimumOnly"/> is the default on purpose: a forecast should describe
/// what happens if nothing changes, and any other value assumes a behaviour change the user has not
/// made yet.
/// </param>
public record GetCashflowForecastQuery(
    int Months = 6,
    int Horizon = 12,
    decimal ExtraPayment = 0m,
    PayoffStrategy DebtStrategy = PayoffStrategy.MinimumOnly) : IRequest<CashflowForecastDto>;
