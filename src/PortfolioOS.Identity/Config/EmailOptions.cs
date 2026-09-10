namespace PortfolioOS.Identity.Config;

/// <summary>Pengaturan SMTP untuk email keluar dari service identity.</summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    public string FromAddress { get; set; } = "no-reply@portfolioos.local";
    public string FromName { get; set; } = "PortfolioOS";

    /// <summary>
    /// Kosong berarti tidak ada SMTP yang dikonfigurasi; isi email dicatat ke log
    /// supaya alur reset tetap bisa dipakai saat development.
    /// </summary>
    public string SmtpHost { get; set; } = string.Empty;

    public int SmtpPort { get; set; } = 25;
    public bool UseSsl { get; set; }

    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
