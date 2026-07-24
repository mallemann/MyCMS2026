using MailKit.Net.Smtp;
using MimeKit;
using MyCMS2026.Models;

namespace MyCMS2026.Services;

public class EmailService
{
    private readonly SiteService _site;
    private readonly ILogger<EmailService> _log;

    public EmailService(SiteService site, ILogger<EmailService> log)
    {
        _site = site;
        _log  = log;
    }

    /// <summary>Regulärer Versand: bei fehlender SMTP-Konfiguration nur Log-Warnung (kein Fehler).</summary>
    public async Task SendAsync(string to, string subject, string htmlBody)
        => await SendInternalAsync(to, subject, htmlBody, throwIfNotConfigured: false);

    /// <summary>
    /// Test-/Diagnose-Versand: wirft bei fehlender Konfiguration oder SMTP-Fehler
    /// und liefert die Serverantwort zurück (z.B. "250 OK").
    /// </summary>
    public async Task<string> SendTestAsync(string to, string subject, string htmlBody)
        => await SendInternalAsync(to, subject, htmlBody, throwIfNotConfigured: true) ?? "";

    private async Task<string?> SendInternalAsync(string to, string subject, string htmlBody, bool throwIfNotConfigured)
    {
        var cfg  = await _site.GetAsync();
        var smtp = cfg.Smtp;

        if (string.IsNullOrEmpty(smtp.Host))
        {
            if (throwIfNotConfigured)
                throw new InvalidOperationException("SMTP ist nicht konfiguriert (kein Host hinterlegt).");
            _log.LogWarning("SMTP nicht konfiguriert – E-Mail nicht gesendet: {Subject}", subject);
            return null;
        }

        var msg = new MimeMessage();
        msg.From.Add(MailboxAddress.Parse(smtp.From));
        msg.To.Add(MailboxAddress.Parse(to));
        msg.Subject = subject;
        msg.Body = new TextPart("html") { Text = htmlBody };

        var sslMode = smtp.SslMode.ToLowerInvariant() switch
        {
            "none"              => MailKit.Security.SecureSocketOptions.None,
            "ssltls"            => MailKit.Security.SecureSocketOptions.SslOnConnect,
            "sslonconnect"      => MailKit.Security.SecureSocketOptions.SslOnConnect,
            "starttlsifavail"   => MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable,
            _                   => MailKit.Security.SecureSocketOptions.StartTls
        };

        using var client = new SmtpClient();
        await client.ConnectAsync(smtp.Host, smtp.Port, sslMode);
        if (!string.IsNullOrEmpty(smtp.User) && !string.IsNullOrEmpty(smtp.Password))
            await client.AuthenticateAsync(smtp.User, smtp.Password);
        var response = await client.SendAsync(msg);
        await client.DisconnectAsync(true);
        return response;
    }
}
