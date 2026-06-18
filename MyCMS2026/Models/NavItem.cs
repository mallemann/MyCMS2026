namespace MyCMS2026.Models;

/// <summary>
/// Entspricht einem Eintrag in der navigation.json.
/// Spiegelt die Struktur der alten Navigation.mdb wider.
/// </summary>
public class NavItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? ParentId { get; set; }

    /// <summary>Seitentitel (Browserzeile / Page-Heading)</summary>
    public string Title { get; set; } = "";

    /// <summary>Text im Navigations-Menü</summary>
    public string NavigationText { get; set; } = "";

    /// <summary>Rolle, die die Seite im Menü sehen darf ("Public" = alle)</summary>
    public string VisibilityRole { get; set; } = "Member";

    /// <summary>Rolle, die die Seite lesen darf</summary>
    public string BasicAccessRole { get; set; } = "Member";

    /// <summary>Rolle mit erweiterten Rechten (z.B. Bearbeiten)</summary>
    public string ExtendedAccessRole { get; set; } = "Administrator";

    /// <summary>Name des Widgets/Partial, das auf dieser Seite geladen wird</summary>
    public string Widget { get; set; } = "";

    /// <summary>Optionaler Konfigurationsstring für das Widget</summary>
    public string ConfigString { get; set; } = "";

    /// <summary>Sortierfolge innerhalb der Geschwister</summary>
    public int MenuOrder { get; set; } = 99;

    // ── Navigations-Hilfseigenschaften (nicht in JSON) ────────────────────────
    public List<NavItem> Children { get; set; } = new();
}
