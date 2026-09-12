using MediatR;

namespace PortfolioOS.Application.Forecasting.Queries.GetDebtPayoff;

/// <param name="ExtraPayment">
/// Voluntary amount on top of every minimum, aimed at the head of the payoff queue. Ignored under
/// <see cref="PayoffStrategy.MinimumOnly"/>.
/// </param>
/// <param name="Strategy">
/// How the surplus is aimed. Avalanche is the default because it is always the cheaper of the two.
/// </param>
public record GetDebtPayoffQuery(
    decimal ExtraPayment = 0m,
    PayoffStrategy Strategy = PayoffStrategy.Avalanche) : IRequest<DebtPayoffPlanDto>;
