namespace PortfolioOS.Identity.Services;

/// <summary>
/// Fallback saat <c>Email:SmtpHost</c> kosong: email ditulis ke log alih-alih dikirim.
/// Dengan begitu alur lupa password tetap bisa dijalankan lewat <c>dotnet run</c> tanpa
/// menyiapkan SMTP lebih dulu — kodenya tinggal dibaca di konsol.
/// </summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(
        string toAddress, string subject, string htmlBody, string textBody, CancellationToken ct = default)
    {
        // Warning, bukan Information: isinya rahasia dan levelnya harus cukup tinggi untuk
        // lolos filter log default sekaligus terbaca sebagai "ini bukan setelan produksi".
        logger.LogWarning(
            "SMTP belum dikonfigurasi — email tidak dikirim. Kepada: {To}\nSubjek: {Subject}\n{Body}",
            toAddress, subject, textBody);

        return Task.CompletedTask;
    }
}
