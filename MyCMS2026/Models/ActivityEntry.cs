namespace MyCMS2026.Models;

/// <summary>
/// Ein Eintrag pro Tag pro Benutzer.
/// Speichert Anmeldezeitpunkt und besuchte Seiten (keine Duplikate).
/// </summary>
public class ActivityEntry
{
    public string User       { get; set; } = "";
    public string Date       { get; set; } = "";    // "yyyy-MM-dd"
    public string FirstLogin { get; set; } = "";    // "HH:mm"
    public List<string> Pages { get; set; } = new();
}
