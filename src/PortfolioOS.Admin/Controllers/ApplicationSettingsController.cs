using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioOS.Admin.Authorization;
using PortfolioOS.Admin.Models;
using PortfolioOS.Admin.Services;

namespace PortfolioOS.Admin.Controllers;

/// <summary>
/// Setting aplikasi (tabel <c>app_settings</c> di database bisnis). Sumber datanya tetap
/// PortfolioOS.API; di sini hanya dibungkus supaya konsol admin cukup bicara ke satu host.
/// </summary>
[ApiController]
[Route("api/admin/settings/application")]
[Authorize(Policy = AdminPolicies.AdminOnly)]
[Produces("application/json")]
public class ApplicationSettingsController(PortfolioApiClient api) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ApplicationSettingDto>>> List(CancellationToken ct)
        => Ok((await api.GetSettingsAsync(ct)).OrderBy(s => s.Key).ToList());

    [HttpPatch]
    public async Task<IActionResult> Update(
        [FromBody] UpdateApplicationSettingRequest request, CancellationToken ct)
    {
        await api.UpdateSettingAsync(request.Key, request.Value, ct);
        return NoContent();
    }
}
