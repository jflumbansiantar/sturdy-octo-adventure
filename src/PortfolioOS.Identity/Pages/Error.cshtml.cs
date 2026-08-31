using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PortfolioOS.Identity.Pages;

[AllowAnonymous]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ErrorModel(IIdentityServerInteractionService interaction, IWebHostEnvironment env) : PageModel
{
    public string? Error { get; private set; }
    public string? ErrorDescription { get; private set; }
    public string? RequestId { get; private set; }

    public async Task OnGetAsync(string? errorId)
    {
        RequestId = HttpContext.TraceIdentifier;

        var message = await interaction.GetErrorContextAsync(errorId);
        if (message is null) return;

        Error = message.Error;

        // Detail error protokol hanya ditampilkan di Development — di produksi bisa
        // membocorkan konfigurasi client ke pihak yang salah.
        ErrorDescription = env.IsDevelopment() ? message.ErrorDescription : null;
    }
}
