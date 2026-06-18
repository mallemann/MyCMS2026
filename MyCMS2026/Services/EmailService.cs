using MailKit.Net.Smtp;
using MimeKit;

namespace MyCMS2026.Services;

public class EmailService
{
    private readonly IConfiguration _cfg;
    private readonly ILogger<EmailService> _log;

    public EmailService(IConfiguration cfg, ILogger<EmailService> log)
    {
        _cfg = cfg;
        _log = log;
    }

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var host = _cfg["Smtp:Host"];
        if (string.IsNullOrEmpty(host))
        {
            _log.LogWarning("SMTP nicht konfiguriert – E-Mail nicht gesendet: {Subject}", subject);
            return;
        }

        var msg = new MimeMessage();
        msg.From.Add(MailboxAddress.Parse(_cfg["Smtp:From"]));
        msg.To.Add(MailboxAddress.Parse(to));
        msg.Subject = subject;
        msg.Body = new TextPart("html") { Text = htmlBody };

        var port    = int.Parse(_cfg["Smtp:Port"] ?? "587");
        var sslMode = (_cfg["Smtp:SslMode"] ?? "StartTls").ToLowerInvariant() switch
        {
            "none"              => MailKit.Security.SecureSocketOptions.None,
            "ssltls"            => MailKit.Security.SecureSocketOptions.SslOnConnect,
            "sslonconnect"      => MailKit.Security.SecureSocketOptions.SslOnConnect,
            "starttlsifavail"   => MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable,
            _                   => MailKit.Security.SecureSocketOptions.StartTls
        };

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, sslMode);
        var user = _cfg["Smtp:User"];
        var pass = _cfg["Smtp:Password"];
        if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
            await client.AuthenticateAsync(user, pass);
        await client.SendAsync(msg);
        await client.DisconnectAsync(true);
    }
}
