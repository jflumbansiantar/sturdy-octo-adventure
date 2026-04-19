using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioOS.Application.Transactions.Commands.CreateTransaction;
using PortfolioOS.Application.Transactions.Commands.DeleteTransaction;
using PortfolioOS.Application.Transactions.Queries.GetTransactions;
using PortfolioOS.Domain.Enums;

namespace PortfolioOS.API.Controllers;

[Authorize]
[ApiController]
[Route("api/transactions")]
public class TransactionsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] TransactionCategory? category,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
        => Ok(await mediator.Send(new GetTransactionsQuery(category, from, to), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTransactionCommand cmd, CancellationToken ct)
    {
        var id = await mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetAll), new { id }, new { id });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteTransactionCommand(id), ct);
        return NoContent();
    }
}
