using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioOS.Application.Ledger.Commands.CreateAccount;
using PortfolioOS.Application.Ledger.Commands.CreateJournalEntry;
using PortfolioOS.Application.Ledger.Commands.DeleteJournalEntry;
using PortfolioOS.Application.Ledger.Commands.UpdateAccount;
using PortfolioOS.Application.Ledger.Queries.GetAccounts;
using PortfolioOS.Application.Ledger.Queries.GetEntries;
using PortfolioOS.Application.Ledger.Queries.GetLedgerSummary;

namespace PortfolioOS.API.Controllers;

[Authorize]
[ApiController]
[Route("api/ledger")]
public class LedgerController(IMediator mediator) : ControllerBase
{
    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts(CancellationToken ct)
        => Ok(await mediator.Send(new GetAccountsQuery(), ct));

    [HttpPost("accounts")]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountCommand cmd, CancellationToken ct)
    {
        await mediator.Send(cmd, ct);
        return Created($"api/ledger/accounts/{cmd.Id}", new { id = cmd.Id });
    }

    [HttpPatch("accounts/{id}")]
    public async Task<IActionResult> UpdateAccount(string id, [FromBody] UpdateAccountCommand cmd, CancellationToken ct)
    {
        await mediator.Send(cmd with { Id = id }, ct);
        return NoContent();
    }

    [HttpGet("entries")]
    public async Task<IActionResult> GetEntries(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
        => Ok(await mediator.Send(new GetEntriesQuery(from, to), ct));

    [HttpPost("entries")]
    public async Task<IActionResult> CreateEntry([FromBody] CreateJournalEntryCommand cmd, CancellationToken ct)
    {
        await mediator.Send(cmd, ct);
        return Created($"api/ledger/entries/{cmd.Id}", new { id = cmd.Id });
    }

    [HttpDelete("entries/{id}")]
    public async Task<IActionResult> DeleteEntry(string id, CancellationToken ct)
    {
        await mediator.Send(new DeleteJournalEntryCommand(id), ct);
        return NoContent();
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
        => Ok(await mediator.Send(new GetLedgerSummaryQuery(), ct));
}
