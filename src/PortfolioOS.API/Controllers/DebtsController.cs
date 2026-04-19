using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioOS.Application.Debts.Commands.CreateDebt;
using PortfolioOS.Application.Debts.Commands.DeleteDebt;
using PortfolioOS.Application.Debts.Commands.UpdateDebt;
using PortfolioOS.Application.Debts.Queries.GetDebts;

namespace PortfolioOS.API.Controllers;

[Authorize]
[ApiController]
[Route("api/debts")]
public class DebtsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await mediator.Send(new GetDebtsQuery(), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDebtCommand cmd, CancellationToken ct)
    {
        var id = await mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDebtCommand cmd, CancellationToken ct)
    {
        await mediator.Send(cmd with { Id = id }, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteDebtCommand(id), ct);
        return NoContent();
    }
}
