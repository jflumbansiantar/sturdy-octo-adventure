using Microsoft.EntityFrameworkCore;

namespace PortfolioOS.Admin.Data;

public static class WebSettingSeeder
{
    /// <summary>
    /// Idempoten — aman dipanggil tiap startup. Key baru ditambahkan, metadata key lama
    /// disegarkan dari kode, dan key yang sudah tidak dikenal lagi dihapus. Kolom
    /// <see cref="WebSetting.Value"/> sengaja tidak pernah disentuh supaya nilai yang
    /// sudah diubah admin tidak kembali ke default tiap deploy.
    /// </summary>
    public static async Task SeedAsync(AdminDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var existing = await db.WebSettings.ToDictionaryAsync(s => s.Key, ct);
        var now = DateTimeOffset.UtcNow;
        var added = 0;

        foreach (var definition in WebSettingDefaults.All)
        {
            if (existing.TryGetValue(definition.Key, out var current))
            {
                current.ValueType = definition.ValueType;
                current.Category = definition.Category;
                current.Description = definition.Description;
                current.Options = definition.Options;
                current.DefaultValue = definition.DefaultValue;
                current.SortOrder = definition.SortOrder;
                continue;
            }

            db.WebSettings.Add(new WebSetting
            {
                Key = definition.Key,
                Value = definition.Value,
                DefaultValue = definition.DefaultValue,
                ValueType = definition.ValueType,
                Category = definition.Category,
                Description = definition.Description,
                Options = definition.Options,
                SortOrder = definition.SortOrder,
                CreatedAt = now,
                UpdatedAt = now,
            });
            added++;
        }

        var known = WebSettingDefaults.All.Select(d => d.Key).ToHashSet();
        var orphans = existing.Values.Where(s => !known.Contains(s.Key)).ToList();
        if (orphans.Count > 0) db.WebSettings.RemoveRange(orphans);

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Seed web settings: {Added} ditambahkan, {Removed} dihapus, metadata disegarkan",
                added, orphans.Count);
        }
    }
}
