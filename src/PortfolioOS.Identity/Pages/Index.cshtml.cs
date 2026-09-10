using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PortfolioOS.Identity.Pages;

[AllowAnonymous]
public class IndexModel(IWebHostEnvironment env) : PageModel
{
    public string Issuer { get; private set; } = string.Empty;
    public string Environment => env.EnvironmentName;
    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;
    public string? UserName => User.Identity?.Name;

    public void OnGet()
    {
        Issuer = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
    }
}
