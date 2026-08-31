using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Identity.Config;
using PortfolioOS.Identity.Data;

namespace PortfolioOS.Identity.Controllers;

/// <summary>
/// Manajemen user dan role. Dipakai aplikasi admin PortfolioOS; butuh access token
/// dengan scope <c>portfolioos.admin</c> sekaligus role <c>admin</c>.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize(Policy = AuthorizationPolicies.AdminApi)]
public class UsersController(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager) : ControllerBase
{
    public record UserResponse(
        Guid Id,
        string Email,
        string DisplayName,
        string PreferredCurrency,
        bool IsActive,
        bool LockedOut,
        DateTimeOffset CreatedAt,
        DateTimeOffset? LastLoginAt,
        IEnumerable<string> Roles);

    public record CreateUserRequest(
        string Email,
        string Password,
        string DisplayName,
        string Role,
        string? PreferredCurrency);

    public record SetRolesRequest(string[] Roles);

    public record ResetPasswordRequest(string NewPassword);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserResponse>>> List(CancellationToken ct)
    {
        var users = await userManager.Users
            .OrderBy(u => u.Email)
            .ToListAsync(ct);

        var result = new List<UserResponse>(users.Count);
        foreach (var user in users)
            result.Add(await ToResponseAsync(user));

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponse>> Get(Guid id)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        return user is null ? NotFound() : Ok(await ToResponseAsync(user));
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create([FromBody] CreateUserRequest request)
    {
        var role = string.IsNullOrWhiteSpace(request.Role) ? Roles.User : request.Role;
        if (!await roleManager.RoleExistsAsync(role))
            return BadRequest(new { error = $"Role '{role}' tidak dikenal" });

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Email : request.DisplayName,
            PreferredCurrency = string.IsNullOrWhiteSpace(request.PreferredCurrency) ? "IDR" : request.PreferredCurrency,
        };

        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded) return BadRequest(Problem(created));

        var assigned = await userManager.AddToRoleAsync(user, role);
        if (!assigned.Succeeded) return BadRequest(Problem(assigned));

        return CreatedAtAction(nameof(Get), new { id = user.Id }, await ToResponseAsync(user));
    }

    [HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> SetRoles(Guid id, [FromBody] SetRolesRequest request)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        foreach (var role in request.Roles)
            if (!await roleManager.RoleExistsAsync(role))
                return BadRequest(new { error = $"Role '{role}' tidak dikenal" });

        var current = await userManager.GetRolesAsync(user);

        var removed = await userManager.RemoveFromRolesAsync(user, current.Except(request.Roles));
        if (!removed.Succeeded) return BadRequest(Problem(removed));

        var added = await userManager.AddToRolesAsync(user, request.Roles.Except(current));
        if (!added.Succeeded) return BadRequest(Problem(added));

        return Ok(await ToResponseAsync(user));
    }

    /// <summary>
    /// Menonaktifkan user. Access token yang masih hidup ikut ditolak lewat
    /// <see cref="Services.PortfolioProfileService.IsActiveAsync"/> saat di-refresh.
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id) => await SetActiveAsync(id, false);

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id) => await SetActiveAsync(id, true);

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);

        return result.Succeeded ? NoContent() : BadRequest(Problem(result));
    }

    [HttpGet("/api/roles")]
    public async Task<ActionResult<IEnumerable<object>>> ListRoles(CancellationToken ct)
    {
        var roles = await roleManager.Roles
            .OrderBy(r => r.Name)
            .Select(r => new { r.Name, r.Description })
            .ToListAsync(ct);

        return Ok(roles);
    }

    private async Task<IActionResult> SetActiveAsync(Guid id, bool isActive)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();

        user.IsActive = isActive;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded) return BadRequest(Problem(result));

        // Memaksa token lama gagal validasi pada refresh berikutnya.
        await userManager.UpdateSecurityStampAsync(user);

        return Ok(await ToResponseAsync(user));
    }

    private async Task<UserResponse> ToResponseAsync(ApplicationUser user) => new(
        user.Id,
        user.Email ?? string.Empty,
        user.DisplayName,
        user.PreferredCurrency,
        user.IsActive,
        await userManager.IsLockedOutAsync(user),
        user.CreatedAt,
        user.LastLoginAt,
        await userManager.GetRolesAsync(user));

    private static object Problem(IdentityResult result) => new
    {
        error = "Operasi identity gagal",
        details = result.Errors.Select(e => new { e.Code, e.Description }),
    };
}
