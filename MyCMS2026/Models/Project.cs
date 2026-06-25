namespace MyCMS2026.Models;

public class Project
{
    public string Id { get; set; } = "";
    public int ProjectNr { get; set; } = 0;
    public string Name { get; set; } = "";
    public string Beschreibung { get; set; } = "";
    public string Status { get; set; } = "Aktiv";
    public string Projektleiter { get; set; } = "";
    public DateTime? StartDatum { get; set; }
    public DateTime? EndDatum { get; set; }
    public string Gruppe { get; set; } = "";
    public string LeseRolle { get; set; } = "Member";
    public string BearbeitenRolle { get; set; } = "Administrator";
    public List<JournalEntry> Journal { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = "";

    public static readonly string[] StatusOptions = { "Aktiv", "Abgeschlossen", "Pausiert" };
}

public class JournalEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Titel { get; set; } = "";
    public string Content { get; set; } = "";   // HTML (TinyMCE)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = "";
    public List<JournalComment> Comments { get; set; } = new();
    // Optionale Verknüpfung mit einer Aufgabe oder Sitzung
    public string? LinkedTodoId { get; set; }
    public string? LinkedMeetingId { get; set; }
}

public class JournalComment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Text { get; set; } = "";   // plain text
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "";
}
