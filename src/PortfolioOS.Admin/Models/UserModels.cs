using System.ComponentModel.DataAnnotations;

namespace PortfolioOS.Admin.Models;

/// <summary>Bentuk user seperti yang dikembalikan PortfolioOS.Identity.</summary>
public record AdminUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string PreferredCurrency,
    bool IsActive,
    bool LockedOut,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    string[] Roles);

public record RoleDto(string Name, string Description);

public class CreateUserRequest
{
    [Required(ErrorMessage = "Email wajib diisi")]
    [EmailAddress(ErrorMessage = "Format email tidak valid")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password wajib diisi")]
    [MinLength(10, ErrorMessage = "Password minimal 10 karakter")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nama tampilan wajib diisi")]
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role wajib dipilih")]
    public string Role { get; set; } = "user";

    public string? PreferredCurrency { get; set; }
}

public class SetRolesRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "Minimal satu role harus dipilih")]
    public string[] Roles { get; set; } = [];
}

public class ResetPasswordRequest
{
    [Required(ErrorMessage = "Password baru wajib diisi")]
    [MinLength(10, ErrorMessage = "Password minimal 10 karakter")]
    public string NewPassword { get; set; } = string.Empty;
}
