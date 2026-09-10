using MediatR;
using Microsoft.AspNetCore.Authorization;
using PortfolioOS.API.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioOS.Application.MarketData.Queries.GetExchangeRate;
using PortfolioOS.Application.MarketData.Queries.GetLiveQuotes;

namespace PortfolioOS.API.Controllers;

[Authorize(Policy = PortfolioPolicies.Read)]
[ApiController]
[Route("api/market")]
public class MarketController(IMediator mediator) : ControllerBase
{
    [HttpGet("quotes")]
    public async Task<IActionResult> GetQuotes(CancellationToken ct)
        => Ok(await mediator.Send(new GetLiveQuotesQuery(), ct));

    /// <summary>USD-IDR rate, so clients can display any amount in either currency.</summary>
    [HttpGet("fx")]
    public async Task<IActionResult> GetExchangeRate(CancellationToken ct)
        => Ok(await mediator.Send(new GetExchangeRateQuery(), ct));
}
