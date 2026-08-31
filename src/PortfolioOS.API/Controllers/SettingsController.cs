using MediatR;
using Microsoft.AspNetCore.Authorization;
using PortfolioOS.API.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioOS.Application.Settings.Commands.UpdateSetting;
using PortfolioOS.Application.Settings.Queries.GetSettings;

namespace PortfolioOS.API.Controllers;

[Authorize(Policy = PortfolioPolicies.Read)]
[ApiController]
[Route("api/settings")]
public class SettingsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await mediator.Send(new GetSettingsQuery(), ct));

    [Authorize(Policy = PortfolioPolicies.Write)]
    [HttpPatch]
    public async Task<IActionResult> Update([FromBody] UpdateSettingCommand cmd, CancellationToken ct)
    {
        await mediator.Send(cmd, ct);
        return NoContent();
    }
}
