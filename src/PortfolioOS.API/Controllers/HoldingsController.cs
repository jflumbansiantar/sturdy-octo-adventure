using MediatR;
using Microsoft.AspNetCore.Authorization;
using PortfolioOS.API.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioOS.Application.Holdings.Commands.CreateHolding;
using PortfolioOS.Application.Holdings.Commands.DeleteHolding;
using PortfolioOS.Application.Holdings.Commands.UpdateHolding;
using PortfolioOS.Application.Holdings.Queries.GetHoldings;

namespace PortfolioOS.API.Controllers;

[Authorize(Policy = PortfolioPolicies.Read)]
[ApiController]
[Route("api/holdings")]
public class HoldingsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await mediator.Send(new GetHoldingsQuery(), ct));

    [Authorize(Policy = PortfolioPolicies.Write)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHoldingCommand cmd, CancellationToken ct)
    {
        var id = await mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [Authorize(Policy = PortfolioPolicies.Write)]
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateHoldingCommand cmd, CancellationToken ct)
    {
        await mediator.Send(cmd with { Id = id }, ct);
        return NoContent();
    }

    [Authorize(Policy = PortfolioPolicies.Write)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteHoldingCommand(id), ct);
        return NoContent();
    }
}
