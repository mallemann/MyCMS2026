namespace MyCMS2026.Models;

public class Download
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Beschreibung { get; set; } = "";
    public string Klasse { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public string StoredName { get; set; } = "";
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Gruppe { get; set; } = "";
    public string CreatedBy { get; set; } = "";

    public static readonly string[] Klassen =
    {
        "Instruktion", "Info", "Template", "Protokoll", "Finanzen", "Diverses"
    };
}
