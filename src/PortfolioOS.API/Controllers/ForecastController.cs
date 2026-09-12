using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioOS.Application.Forecasting;
using PortfolioOS.Application.Forecasting.Queries.GetCashflowForecast;
using PortfolioOS.Application.Forecasting.Queries.GetDebtPayoff;
using PortfolioOS.Application.Forecasting.Queries.GetGoalProjection;

namespace PortfolioOS.API.Controllers;

[Authorize]
[ApiController]
[Route("api/forecast")]
public class ForecastController(IMediator mediator) : ControllerBase
{
    /// <summary>Monthly cash flow projected forward as a P10-P50-P90 band.</summary>
    /// <param name="months">Length of the history window, counting this month. Clamped to 3-24.</param>
    /// <param name="horizon">Months to project ahead. Clamped to 1-24.</param>
    /// <param name="extraPayment">Extra debt repayment to assume, alongside a rollover strategy.</param>
    /// <param name="strategy">
    /// How instalments are assumed to be paid. Defaults to minimums only — a forecast describes
    /// what happens if nothing changes.
    /// </param>
    [HttpGet("cashflow")]
    public async Task<IActionResult> GetCashflow(
        CancellationToken ct,
        [FromQuery] int months = 6,
        [FromQuery] int horizon = 12,
        [FromQuery] decimal extraPayment = 0m,
        [FromQuery] PayoffStrategy strategy = PayoffStrategy.MinimumOnly)
        => Ok(await mediator.Send(new GetCashflowForecastQuery(months, horizon, extraPayment, strategy), ct));

    /// <summary>Amortisation schedule for every active debt, and what the interest costs.</summary>
    /// <param name="extraPayment">Voluntary amount on top of the minimums, aimed at the payoff queue.</param>
    /// <param name="strategy">Avalanche (cheapest), Snowball (fastest first win), or MinimumOnly (baseline).</param>
    [HttpGet("debt-payoff")]
    public async Task<IActionResult> GetDebtPayoff(
        CancellationToken ct,
        [FromQuery] decimal extraPayment = 0m,
        [FromQuery] PayoffStrategy strategy = PayoffStrategy.Avalanche)
        => Ok(await mediator.Send(new GetDebtPayoffQuery(extraPayment, strategy), ct));

    /// <summary>When a savings target is reached at the rate currently being set aside.</summary>
    /// <param name="targetAmount">Goal in rupiah. Omit for an emergency fund sized from own spending.</param>
    /// <param name="emergencyFundMonths">Months of essentials to cover, when no target is given. Clamped to 1-24.</param>
    /// <param name="months">Length of the history window feeding the savings figure. Clamped to 3-24.</param>
    [HttpGet("goal")]
    public async Task<IActionResult> GetGoal(
        CancellationToken ct,
        [FromQuery] decimal? targetAmount = null,
        [FromQuery] string? goalName = null,
        [FromQuery] int? emergencyFundMonths = null,
        [FromQuery] int months = 6)
        => Ok(await mediator.Send(
            new GetGoalProjectionQuery(targetAmount, goalName, emergencyFundMonths, months), ct));
}
