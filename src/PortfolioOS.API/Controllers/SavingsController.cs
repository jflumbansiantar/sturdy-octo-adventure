using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioOS.Application.Savings.Queries.GetSavingsSuggestion;

namespace PortfolioOS.API.Controllers;

[Authorize]
[ApiController]
[Route("api/savings")]
public class SavingsController(IMediator mediator) : ControllerBase
{
    /// <param name="months">Length of the history window, counting this month. Clamped to 3-24.</param>
    [HttpGet("suggestion")]
    public async Task<IActionResult> GetSuggestion(
        CancellationToken ct,
        [FromQuery] int months = 6)
        => Ok(await mediator.Send(new GetSavingsSuggestionQuery(months), ct));
}
