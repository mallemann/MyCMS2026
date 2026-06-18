namespace MyCMS2026.Models;

public class SiteConfig
{
    public string Title { get; set; } = "MyCMS";
    public string Status { get; set; } = "Active";
    public string HTTPHost { get; set; } = "";
    public bool ForceHTTPS { get; set; } = false;
    public int MaximumMenuLevels { get; set; } = 2;
    public string FooterText { get; set; } = "";
    public string LogoUrl { get; set; } = "";
    public bool WeeklyMailEnabled { get; set; } = false;
}
