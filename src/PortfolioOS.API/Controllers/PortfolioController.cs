using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioOS.Application.Portfolio.Queries.GetPortfolioSummary;

namespace PortfolioOS.API.Controllers;

[Authorize]
[ApiController]
[Route("api/portfolio")]
public class PortfolioController(IMediator mediator) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
        => Ok(await mediator.Send(new GetPortfolioSummaryQuery(), ct));
}
