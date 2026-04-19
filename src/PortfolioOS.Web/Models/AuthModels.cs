namespace PortfolioOS.Web.Models;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, DateTimeOffset ExpiresAt);
public record SettingModel(string Key, string Value);
