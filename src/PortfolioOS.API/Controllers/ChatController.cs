using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioOS.Application.Chat.Commands.AskQuestion;
using PortfolioOS.Application.Common.Interfaces;

namespace PortfolioOS.API.Controllers;

[Authorize]
[ApiController]
[Route("api/chat")]
public class ChatController(IMediator mediator, IChatIndexService indexService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] AskQuestionCommand cmd, CancellationToken ct)
        => Ok(await mediator.Send(cmd, ct));

    /// <summary>
    /// Rebuilds the search index. Runs on startup and on a timer as well; this is for when you
    /// have just changed data and do not want to wait for the next sweep.
    /// </summary>
    [HttpPost("reindex")]
    public async Task<IActionResult> Reindex(CancellationToken ct)
        => Ok(await indexService.ReindexAsync(ct));
}
