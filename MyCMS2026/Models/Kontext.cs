namespace MyCMS2026.Models;

/// <summary>
/// Ein Kontext verknüpft eine Gruppe mit einer primären Sichtbarkeitsrolle (plus Beschreibung).
/// 1 Kontext = 1 Gruppe. Wird vom Admin auf der Kontext-Seite gepflegt.
/// </summary>
public class Kontext
{
    public string Gruppe { get; set; } = "";
    public string Rolle { get; set; } = "";
    public string Beschreibung { get; set; } = "";
}
