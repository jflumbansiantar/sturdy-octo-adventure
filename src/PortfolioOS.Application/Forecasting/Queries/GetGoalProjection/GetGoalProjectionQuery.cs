using MediatR;

namespace PortfolioOS.Application.Forecasting.Queries.GetGoalProjection;

/// <param name="TargetAmount">
/// The goal in rupiah. Left null, the target is an emergency fund sized from the user's own
/// essential spending rather than a number they have to guess at.
/// </param>
/// <param name="EmergencyFundMonths">
/// Months of essentials the emergency fund should cover, when <paramref name="TargetAmount"/> is
/// not given. Clamped to 1-24.
/// </param>
/// <param name="Months">History window feeding the savings figure. Clamped to 3-24.</param>
public record GetGoalProjectionQuery(
    decimal? TargetAmount = null,
    string? GoalName = null,
    int? EmergencyFundMonths = null,
    int Months = 6) : IRequest<GoalProjectionDto>;
