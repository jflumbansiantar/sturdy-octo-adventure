namespace PortfolioOS.Admin.Data;

/// <summary>
/// Setting yang dimiliki sendiri oleh admin service: hal-hal yang mengatur tampilan dan
/// perilaku aplikasi web/admin (branding, tema default, feature flag, mode pemeliharaan).
///
/// Dibedakan dari <c>app_settings</c> di database bisnis — yang itu dibaca oleh domain
/// portofolio lewat PortfolioOS.API dan tetap jadi miliknya. Admin service hanya
/// mem-proxy-nya, tidak menyalinnya.
/// </summary>
public class WebSetting
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    /// <summary>Menentukan editor di UI sekaligus validasi nilai. Lihat <see cref="WebSettingTypes"/>.</summary>
    public string ValueType { get; set; } = WebSettingTypes.String;

    /// <summary>Pengelompokan di halaman Settings, mis. "Tampilan".</summary>
    public string Category { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Pilihan yang sah untuk <see cref="WebSettingTypes.Select"/>, dipisah koma.</summary>
    public string? Options { get; set; }

    /// <summary>Nilai bawaan dari kode — dipakai tombol "kembalikan ke default" di UI.</summary>
    public string DefaultValue { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Email admin yang terakhir mengubah nilai; null selama masih nilai seed.</summary>
    public string? UpdatedBy { get; set; }
}

public static class WebSettingTypes
{
    public const string String = "string";
    public const string Text = "text";
    public const string Bool = "bool";
    public const string Int = "int";
    public const string Select = "select";

    public static readonly string[] All = [String, Text, Bool, Int, Select];
}
