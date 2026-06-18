namespace MyCMS2026.Models;

public class SiteConfig
{
    public string Title { get; set; } = "MyCMS";
    public string Status { get; set; } = "Active";   // "Active" | "Offline"
    public string FooterText { get; set; } = "";
    public string LogoUrl { get; set; } = "";
    public bool WeeklyMailEnabled { get; set; } = false;
}
