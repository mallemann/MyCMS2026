namespace MyCMS2026.Models;

public class Meeting
{
    public string Id { get; set; } = "";
    public int MeetingNr { get; set; } = 0;
    public string Thema { get; set; } = "";
    public string Leitung { get; set; } = "";
    public string Beschreibung { get; set; } = "";
    public DateTime Datum { get; set; } = DateTime.Today;
    public string Status { get; set; } = "Geplant";
    public string Klasse { get; set; } = "";
    public string Gruppe { get; set; } = "";
    public string ContentType { get; set; } = "Text";
    public string Content { get; set; } = "";
    public string? ProjectId { get; set; }
    public List<MeetingFile> Files { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = "";

    public static readonly string[] Klassen =
    {
        "Team", "Projekt", "Kunden", "Management", "Diverses"
    };

    public static readonly string[] StatusOptions = { "Geplant", "Abgeschlossen", "Abgesagt" };
}

public class MeetingFile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string OriginalName { get; set; } = "";
    public string StoredName { get; set; } = "";
    public long Size { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
