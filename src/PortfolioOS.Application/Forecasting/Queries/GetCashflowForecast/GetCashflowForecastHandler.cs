using MediatR;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Application.Savings;

namespace PortfolioOS.Application.Forecasting.Queries.GetCashflowForecast;

/// <summary>
/// Assembles the three stored inputs the forecast needs — the month series, the debt schedule and
/// the cash balance — and hands them to <see cref="CashflowForecaster"/>.
/// </summary>
/// <remarks>
/// The debt schedule is built here rather than left to the forecaster so the instalment column is
/// an amortised fact instead of an extrapolated average. That is the one part of the projection
/// that is known rather than guessed, and running it through the amortiser first is what lets the
/// forecast show cash flow jumping in the month a loan is actually cleared.
/// </remarks>
public class GetCashflowForecastHandler(IApplicationDbContext context, TimeProvider timeProvider)
    : IRequestHandler<GetCashflowForecastQuery, CashflowForecastDto>
{
    public async Task<CashflowForecastDto> Handle(GetCashflowForecastQuery request, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var horizon = Math.Clamp(request.Horizon, 1, ForecastPolicy.Default.MaxHorizonMonths);

        var history = await CashflowHistory.ReadAsync(context, today, request.Months, ct);
        var debts = await ForecastInputs.ReadActiveDebtsAsync(context, ct);
        var cash = await ForecastInputs.ReadCashBalanceAsync(context, ct);

        var plan = DebtAmortizer.Plan(debts, today, request.ExtraPayment, request.DebtStrategy);
        var byMonth = plan.Schedule.ToDictionary(r => r.MonthOffset, r => r.Payment);

        // Months past the end of the schedule carry no instalment: the debt is cleared by then.
        // With no active debts at all the whole column is zero, which is also the right answer —
        // carrying a historical median forward would keep charging a loan that is already closed.
        var scheduled = Enumerable.Range(1, horizon)
            .Select(h => byMonth.GetValueOrDefault(h, 0m))
            .ToList();

        // A ledger that credits out more cash than it ever held is mis-entered, not overdrawn.
        // Treating it as no usable balance suppresses a runway figure built on a negative number.
        var openingCash = Math.Max(0m, cash);

        return CashflowForecaster.Forecast(history, today, scheduled, openingCash, horizon);
    }
}
