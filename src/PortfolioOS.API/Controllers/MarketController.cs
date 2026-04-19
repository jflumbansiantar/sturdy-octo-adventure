using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioOS.Application.MarketData.Queries.GetLiveQuotes;

namespace PortfolioOS.API.Controllers;

[Authorize]
[ApiController]
[Route("api/market")]
public class MarketController(IMediator mediator) : ControllerBase
{
    [HttpGet("quotes")]
    public async Task<IActionResult> GetQuotes(CancellationToken ct)
        => Ok(await mediator.Send(new GetLiveQuotesQuery(), ct));
}
