using MediatR;
using PortfolioOS.Application.Common.Interfaces;

namespace PortfolioOS.Application.Savings.Queries.GetSavingsSuggestion;

/// <summary>
/// Hands the recorded monthly cash flow to <see cref="SavingsAdvisor"/> and returns its answer.
/// </summary>
/// <remarks>
/// Reading the ledger into a month series is <see cref="CashflowHistory"/>'s job, shared with the
/// cash-flow forecast so both features read the same months the same way. Everything opinionated
/// lives in the advisor, which is pure and therefore testable without a database.
/// </remarks>
public class GetSavingsSuggestionHandler(IApplicationDbContext context, TimeProvider timeProvider)
    : IRequestHandler<GetSavingsSuggestionQuery, SavingsSuggestionDto>
{
    public async Task<SavingsSuggestionDto> Handle(GetSavingsSuggestionQuery request, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

        var cashflow = await CashflowHistory.ReadAsync(context, today, request.Months, ct);
        var committedDebt = await CashflowHistory.ReadCommittedDebtPaymentAsync(context, ct);

        return SavingsAdvisor.Recommend(cashflow, today, committedDebt);
    }
}
