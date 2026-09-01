using System.ComponentModel.DataAnnotations;

namespace PortfolioOS.Admin.Models;

/// <summary>Setting aplikasi — milik database bisnis, di-proxy dari PortfolioOS.API.</summary>
public record ApplicationSettingDto(string Key, string Value);

public class UpdateApplicationSettingRequest
{
    [Required(ErrorMessage = "Key wajib diisi")]
    public string Key { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = true)]
    public string Value { get; set; } = string.Empty;
}

/// <summary>Setting web — milik database admin service sendiri.</summary>
public record WebSettingDto(
    string Key,
    string Value,
    string DefaultValue,
    string ValueType,
    string Category,
    string Description,
    string[] Options,
    int SortOrder,
    DateTimeOffset UpdatedAt,
    string? UpdatedBy);

public class UpdateWebSettingRequest
{
    [Required(AllowEmptyStrings = true)]
    public string Value { get; set; } = string.Empty;
}

public class UpdateWebSettingsRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "Tidak ada setting yang dikirim")]
    public WebSettingValue[] Items { get; set; } = [];
}

public class WebSettingValue
{
    [Required]
    public string Key { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = true)]
    public string Value { get; set; } = string.Empty;
}
