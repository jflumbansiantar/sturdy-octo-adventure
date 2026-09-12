using MediatR;
using PortfolioOS.Application.Common.Interfaces;
using PortfolioOS.Application.Savings;

namespace PortfolioOS.Application.Forecasting.Queries.GetGoalProjection;

/// <summary>
/// Turns the savings advice into a date, by running its monthly ramp forward until it clears a
/// target.
/// </summary>
/// <remarks>
/// Composition rather than a fourth model: <see cref="SavingsAdvisor"/> already decides what can be
/// set aside each month and produces the Save More Tomorrow ramp, so the only missing step is
/// accumulating it. Asking the advisor rather than re-deriving a savings figure also means the goal
/// date can never contradict the number shown on the savings page.
/// </remarks>
public class GetGoalProjectionHandler(IApplicationDbContext context, TimeProvider timeProvider)
    : IRequestHandler<GetGoalProjectionQuery, GoalProjectionDto>
{
    public async Task<GoalProjectionDto> Handle(GetGoalProjectionQuery request, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var policy = ForecastPolicy.Default;

        var history = await CashflowHistory.ReadAsync(context, today, request.Months, ct);
        var committedDebt = await CashflowHistory.ReadCommittedDebtPaymentAsync(context, ct);
        var cash = await ForecastInputs.ReadCashBalanceAsync(context, ct);

        var suggestion = SavingsAdvisor.Recommend(history, today, committedDebt);

        var fundMonths = Math.Clamp(
            request.EmergencyFundMonths ?? policy.EmergencyFundMonths, 1, 24);

        // An emergency fund covers essentials — kebutuhan and cicilan. Discretionary spending is
        // precisely what stops in the emergency the fund exists for, so sizing the target to
        // include it would ask the user to save for holidays they would not be taking.
        //
        // The instalment figure is the contractual minimum rather than the advisor's
        // MonthlyDebtService, which is max(recorded, committed) and so carries any voluntary
        // overpayment the user has been making. Prepaying a loan is discretionary by exactly the
        // same argument as the wants above: it is the first thing that stops in an emergency. It
        // also keeps this target consistent with the cash-flow forecast, which deducts the
        // amortised minimum — the two must not disagree about the same household.
        var essentialDebt = committedDebt > 0m ? committedDebt : suggestion.MonthlyDebtService;

        var target = request.TargetAmount
            ?? (suggestion.MonthlyNeeds + essentialDebt) * fundMonths;

        var name = request.GoalName
            ?? (request.TargetAmount is null ? $"Dana darurat {fundMonths} bulan" : "Target tabungan");

        var ramp = suggestion.Ramp.Select(r => r.Amount).ToList();

        return GoalProjector.Project(name, target, Math.Max(0m, cash), ramp, today);
    }
}
