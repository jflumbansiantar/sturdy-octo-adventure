using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioOS.Admin.Authorization;
using PortfolioOS.Admin.Data;
using PortfolioOS.Admin.Models;

namespace PortfolioOS.Admin.Controllers;

/// <summary>
/// Setting web/admin — satu-satunya data yang benar-benar dimiliki service ini.
/// Kumpulan key-nya ditentukan kode (<see cref="WebSettingDefaults"/>), jadi tidak ada
/// endpoint create/delete: admin hanya mengubah nilai key yang sudah dikenal.
/// </summary>
[ApiController]
[Route("api/admin/settings/web")]
[Authorize(Policy = AdminPolicies.AdminOnly)]
[Produces("application/json")]
public class WebSettingsController(AdminDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<WebSettingDto>>> List(CancellationToken ct)
    {
        var settings = await db.WebSettings
            .AsNoTracking()
            .OrderBy(s => s.Category).ThenBy(s => s.SortOrder)
            .ToListAsync(ct);

        return Ok(settings.Select(ToDto).ToList());
    }

    [HttpPut("{key}")]
    public async Task<ActionResult<WebSettingDto>> Update(
        string key, [FromBody] UpdateWebSettingRequest request, CancellationToken ct)
    {
        var setting = await db.WebSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null) return NotFound(new { error = $"Setting '{key}' tidak dikenal." });

        if (Validate(setting, request.Value) is { } error)
            return BadRequest(new { error });

        Apply(setting, request.Value);
        await db.SaveChangesAsync(ct);

        return Ok(ToDto(setting));
    }

    /// <summary>
    /// Menyimpan beberapa setting sekaligus. Divalidasi seluruhnya lebih dulu supaya satu
    /// nilai yang salah tidak menyisakan sebagian perubahan tersimpan.
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<List<WebSettingDto>>> UpdateMany(
        [FromBody] UpdateWebSettingsRequest request, CancellationToken ct)
    {
        var keys = request.Items.Select(i => i.Key).ToList();

        var duplicates = keys.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
            return BadRequest(new { error = $"Key dikirim lebih dari sekali: {string.Join(", ", duplicates)}" });

        var settings = await db.WebSettings.Where(s => keys.Contains(s.Key)).ToDictionaryAsync(s => s.Key, ct);

        var unknown = keys.Where(k => !settings.ContainsKey(k)).ToList();
        if (unknown.Count > 0)
            return BadRequest(new { error = $"Setting tidak dikenal: {string.Join(", ", unknown)}" });

        var errors = new List<object>();
        foreach (var item in request.Items)
            if (Validate(settings[item.Key], item.Value) is { } error)
                errors.Add(new { key = item.Key, message = error });

        if (errors.Count > 0) return BadRequest(new { error = "Ada nilai yang tidak valid", details = errors });

        foreach (var item in request.Items)
            Apply(settings[item.Key], item.Value);

        await db.SaveChangesAsync(ct);

        return Ok(request.Items.Select(i => ToDto(settings[i.Key])).ToList());
    }

    /// <summary>Mengembalikan satu setting ke nilai bawaan dari kode.</summary>
    [HttpPost("{key}/reset")]
    public async Task<ActionResult<WebSettingDto>> Reset(string key, CancellationToken ct)
    {
        var setting = await db.WebSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null) return NotFound(new { error = $"Setting '{key}' tidak dikenal." });

        Apply(setting, setting.DefaultValue);
        await db.SaveChangesAsync(ct);

        return Ok(ToDto(setting));
    }

    private void Apply(WebSetting setting, string value)
    {
        var normalized = setting.ValueType == WebSettingTypes.Bool
            ? bool.Parse(value).ToString().ToLowerInvariant()
            : value.Trim();

        if (setting.Value == normalized) return;

        setting.Value = normalized;
        setting.UpdatedAt = DateTimeOffset.UtcNow;
        setting.UpdatedBy = User.FindFirstValue("email")
                            ?? User.FindFirstValue("name")
                            ?? User.FindFirstValue("sub");
    }

    /// <summary>Pesan error kalau nilainya tidak cocok dengan tipe setting, null kalau sah.</summary>
    private static string? Validate(WebSetting setting, string value) => setting.ValueType switch
    {
        WebSettingTypes.Bool when !bool.TryParse(value, out _)
            => "Nilai harus 'true' atau 'false'",

        WebSettingTypes.Int when !int.TryParse(value, out _)
            => "Nilai harus berupa angka bulat",

        WebSettingTypes.Select when !SplitOptions(setting.Options)
            .Contains(value.Trim(), StringComparer.OrdinalIgnoreCase)
            => $"Nilai harus salah satu dari: {string.Join(", ", SplitOptions(setting.Options))}",

        _ when value.Length > 2000 => "Nilai maksimal 2000 karakter",

        _ => null,
    };

    private static string[] SplitOptions(string? options) =>
        string.IsNullOrWhiteSpace(options)
            ? []
            : options.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static WebSettingDto ToDto(WebSetting s) => new(
        s.Key,
        s.Value,
        s.DefaultValue,
        s.ValueType,
        s.Category,
        s.Description,
        SplitOptions(s.Options),
        s.SortOrder,
        s.UpdatedAt,
        s.UpdatedBy);
}
