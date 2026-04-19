using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PortfolioOS.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IConfiguration config) : ControllerBase
{
    public record LoginRequest(string Username, string Password);
    public record LoginResponse(string Token, DateTimeOffset ExpiresAt);

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest req)
    {
        var expectedUsername = config["Auth:Username"];
        var expectedPassword = config["Auth:Password"];

        if (req.Username != expectedUsername || req.Password != expectedPassword)
            return Unauthorized(new { error = "Invalid credentials" });

        var secret = config["Jwt:Secret"]!;
        var issuer  = config["Jwt:Issuer"];
        var audience = config["Jwt:Audience"];
        var expiryHours = int.Parse(config["Jwt:ExpiryHours"] ?? "24");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTimeOffset.UtcNow.AddHours(expiryHours);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: [new Claim(ClaimTypes.Name, req.Username)],
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        return Ok(new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expires));
    }
}
