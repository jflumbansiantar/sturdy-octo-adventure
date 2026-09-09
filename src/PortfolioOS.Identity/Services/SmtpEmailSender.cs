using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using PortfolioOS.Identity.Config;

namespace PortfolioOS.Identity.Services;

/// <summary>
/// Mengirim lewat SMTP biasa memakai <see cref="SmtpClient"/> dari BCL — cukup untuk satu
/// jenis email transaksional dan tidak menambah dependensi paket ke service ini.
/// </summary>
public class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(
        string toAddress, string subject, string htmlBody, string textBody, CancellationToken ct = default)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = subject,
            Body = textBody,
            IsBodyHtml = false,
        };
        message.To.Add(toAddress);

        // Body teks jadi isi utama, versi HTML ditempel sebagai alternate view. Klien email
        // memilih yang terbaik yang bisa ia tampilkan, dan kode tetap terbaca di klien
        // yang memblokir HTML.
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            htmlBody, System.Text.Encoding.UTF8, "text/html"));

        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.UseSsl,
        };

        if (!string.IsNullOrWhiteSpace(_options.UserName))
        {
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
        }

        await client.SendMailAsync(message, ct);
        logger.LogInformation("Email '{Subject}' dikirim ke {To}", subject, toAddress);
    }
}
