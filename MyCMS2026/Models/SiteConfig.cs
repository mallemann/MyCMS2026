namespace MyCMS2026.Models;

public class SiteConfig
{
    public string Title { get; set; } = "MyCMS";
    public string Status { get; set; } = "Active";   // "Active" | "Offline"
    public string FooterText { get; set; } = "";
    public string LogoUrl { get; set; } = "";
    public string BaseUrl { get; set; } = "";   // z.B. https://mycms.example.com (für Links in Mails)
    public bool WeeklyMailEnabled { get; set; } = false;
    public SmtpSettings Smtp { get; set; } = new();
}

public class SmtpSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    /// <summary>StartTls | SslOnConnect | None</summary>
    public string SslMode { get; set; } = "StartTls";
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "";
}
