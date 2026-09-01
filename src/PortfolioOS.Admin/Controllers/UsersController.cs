using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortfolioOS.Admin.Authorization;
using PortfolioOS.Admin.Models;
using PortfolioOS.Admin.Services;

namespace PortfolioOS.Admin.Controllers;

/// <summary>
/// Manajemen user dan role. Datanya milik PortfolioOS.Identity — controller ini
/// menambahkan pengaman khusus konsol admin di atasnya (lihat <see cref="IsSelf"/>).
/// </summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = AdminPolicies.AdminOnly)]
[Produces("application/json")]
public class UsersController(IdentityAdminClient identity) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AdminUserDto>>> List(CancellationToken ct)
        => Ok(await identity.ListUsersAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminUserDto>> Get(Guid id, CancellationToken ct)
        => Ok(await identity.GetUserAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<AdminUserDto>> Create(
        [FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var user = await identity.CreateUserAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
    }

    [HttpPut("{id:guid}/roles")]
    public async Task<ActionResult<AdminUserDto>> SetRoles(
        Guid id, [FromBody] SetRolesRequest request, CancellationToken ct)
    {
        // Melepas role admin dari diri sendiri akan mengunci pemakainya keluar dari
        // konsol ini, dan tidak ada jalan balik selain lewat database.
        if (IsSelf(id) && !request.Roles.Contains(AdminPolicies.AdminRole, StringComparer.OrdinalIgnoreCase))
            return Conflict(new { error = "Anda tidak bisa melepas role admin dari akun sendiri." });

        return Ok(await identity.SetRolesAsync(id, request.Roles, ct));
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<AdminUserDto>> Activate(Guid id, CancellationToken ct)
        => Ok(await identity.SetActiveAsync(id, isActive: true, ct));

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<AdminUserDto>> Deactivate(Guid id, CancellationToken ct)
    {
        if (IsSelf(id))
            return Conflict(new { error = "Anda tidak bisa menonaktifkan akun sendiri." });

        return Ok(await identity.SetActiveAsync(id, isActive: false, ct));
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        Guid id, [FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        await identity.ResetPasswordAsync(id, request.NewPassword, ct);
        return NoContent();
    }

    /// <summary>Apakah id ini milik admin yang sedang login.</summary>
    private bool IsSelf(Guid id)
    {
        // MapInboundClaims dimatikan, jadi subject tetap bernama "sub" seperti di token.
        var subject = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out var currentId) && currentId == id;
    }
}

[ApiController]
[Route("api/admin/roles")]
[Authorize(Policy = AdminPolicies.AdminOnly)]
[Produces("application/json")]
public class RolesController(IdentityAdminClient identity) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<RoleDto>>> List(CancellationToken ct)
        => Ok(await identity.ListRolesAsync(ct));
}
