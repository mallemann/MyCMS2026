namespace MyCMS2026.Models;

public class Pendenz
{
    /// <summary>GUID – stabile ID für Referenzen zwischen Widgets</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Laufende Nummer innerhalb der Liste (für Anzeige)</summary>
    public int Nr { get; set; }

    public DateTime ErfasstAm { get; set; } = DateTime.Now;
    public string ErfasstVon { get; set; } = "";

    /// <summary>Archiv-/Referenznummer (frei)</summary>
    public string Nummer { get; set; } = "";

    public string Text { get; set; } = "";
    public DateTime? ErledigenBis { get; set; }

    public bool Erledigt { get; set; }
    public DateTime? ErledigtAm { get; set; }
    public string? ErledigtVon { get; set; }

    /// <summary>Interner Verantwortlicher (Username)</summary>
    public string Verantwortlich { get; set; } = "";

    /// <summary>Externer Verantwortlicher (Username)</summary>
    public string? Extern { get; set; }

    public bool IstErinnerung { get; set; }

    /// <summary>Unterscheidet mehrere Pendenzenlisten (aus NavItem.ConfigString)</summary>
    public string? ConfigString { get; set; } = "";

    /// <summary>Änderungsprotokoll – direkt im Record</summary>
    public List<PendenzLogEntry> History { get; set; } = new();
}

public class PendenzLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string User { get; set; } = "";
    public string Aktion { get; set; } = "";
}
