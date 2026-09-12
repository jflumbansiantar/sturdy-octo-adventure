using MediatR;
using PortfolioOS.Application.Common.Interfaces;

namespace PortfolioOS.Application.Forecasting.Queries.GetDebtPayoff;

/// <summary>
/// Hands the recorded debts to <see cref="DebtAmortizer"/> and returns the schedule.
/// </summary>
/// <remarks>
/// The thinnest of the three forecast handlers, because the amortiser needs nothing estimated —
/// balance, rate and instalment are all recorded facts.
/// </remarks>
public class GetDebtPayoffHandler(IApplicationDbContext context, TimeProvider timeProvider)
    : IRequestHandler<GetDebtPayoffQuery, DebtPayoffPlanDto>
{
    public async Task<DebtPayoffPlanDto> Handle(GetDebtPayoffQuery request, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var debts = await ForecastInputs.ReadActiveDebtsAsync(context, ct);

        return DebtAmortizer.Plan(debts, today, request.ExtraPayment, request.Strategy);
    }
}
