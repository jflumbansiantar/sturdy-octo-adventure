using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioOS.Application.Settings.Commands.UpdateSetting;
using PortfolioOS.Application.Settings.Queries.GetSettings;

namespace PortfolioOS.API.Controllers;

[Authorize]
[ApiController]
[Route("api/settings")]
public class SettingsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await mediator.Send(new GetSettingsQuery(), ct));

    [HttpPatch]
    public async Task<IActionResult> Update([FromBody] UpdateSettingCommand cmd, CancellationToken ct)
    {
        await mediator.Send(cmd, ct);
        return NoContent();
    }
}
