namespace PortfolioOS.Identity.Services;

/// <summary>Pengirim email service identity. Implementasinya dipilih di <c>Program.cs</c>.</summary>
public interface IEmailSender
{
    Task SendAsync(string toAddress, string subject, string htmlBody, string textBody, CancellationToken ct = default);
}
