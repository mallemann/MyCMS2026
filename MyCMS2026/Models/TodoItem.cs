namespace MyCMS2026.Models;

public class TodoItem
{
    public string Id { get; set; } = "";
    public int TaskNr { get; set; } = 0;
    public string Thema { get; set; } = "";
    public string Verantwortlich { get; set; } = "";
    public DateTime Anlagedatum { get; set; } = DateTime.Today;
    public DateTime ErledigenBis { get; set; } = DateTime.Today.AddDays(30);
    public bool Erledigt { get; set; } = false;
    public string Klasse { get; set; } = "Allgemein";
    public string Gruppe { get; set; } = "";
    public string Beschreibung { get; set; } = "";
    public string? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public List<TodoFile> Files { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = "";

    public List<TodoLogEntry> History { get; set; } = new();

    public static readonly string[] Klassen =
    {
        "Allgemein", "Projekt", "IT", "Admin", "Diverses"
    };
}

public class TodoLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string User { get; set; } = "";
    public string Aktion { get; set; } = "";
}

public class TodoFile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string OriginalName { get; set; } = "";
    public string StoredName { get; set; } = "";
    public long Size { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
