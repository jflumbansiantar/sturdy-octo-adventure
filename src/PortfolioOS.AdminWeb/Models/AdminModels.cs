namespace PortfolioOS.AdminWeb.Models;

// ---------------------------------------------------------------------------
// User & role
// ---------------------------------------------------------------------------

public record AdminUser(
    Guid Id,
    string Email,
    string DisplayName,
    string PreferredCurrency,
    bool IsActive,
    bool LockedOut,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    string[] Roles)
{
    public string RoleLabel => Roles.Length == 0 ? "-" : string.Join(", ", Roles);

    public string StatusLabel => !IsActive ? "Nonaktif" : LockedOut ? "Terkunci" : "Aktif";
}

public record AdminRole(string Name, string Description);

public record CreateUserRequest(
    string Email,
    string Password,
    string DisplayName,
    string Role,
    string? PreferredCurrency);

public record SetRolesRequest(string[] Roles);

public record ResetPasswordRequest(string NewPassword);

// ---------------------------------------------------------------------------
// Settings
// ---------------------------------------------------------------------------

/// <summary>Setting aplikasi — key/value bebas milik database bisnis.</summary>
public record ApplicationSetting(string Key, string Value);

/// <summary>Setting web — key-nya tetap, metadatanya menentukan editor yang dipakai.</summary>
public record WebSetting(
    string Key,
    string Value,
    string DefaultValue,
    string ValueType,
    string Category,
    string Description,
    string[] Options,
    int SortOrder,
    DateTimeOffset UpdatedAt,
    string? UpdatedBy)
{
    public bool IsModified => !string.Equals(Value, DefaultValue, StringComparison.Ordinal);

    /// <summary>Nama key tanpa awalan "web.", dengan underscore diganti spasi.</summary>
    public string Label
    {
        get
        {
            var name = Key.StartsWith("web.", StringComparison.Ordinal) ? Key[4..] : Key;
            return string.Join(' ', name.Split('_').Select(w =>
                w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..]));
        }
    }
}

public record WebSettingValue(string Key, string Value);

public record UpdateWebSettingsRequest(WebSettingValue[] Items);

public static class WebSettingTypes
{
    public const string String = "string";
    public const string Text = "text";
    public const string Bool = "bool";
    public const string Int = "int";
    public const string Select = "select";
}
