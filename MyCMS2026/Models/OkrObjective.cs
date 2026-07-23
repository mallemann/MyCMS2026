namespace MyCMS2026.Models;

public class OkrObjective
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Text { get; set; } = "";
    public string Status { get; set; } = "aktiv";   // aktiv | abgeschlossen
    public string Gruppe { get; set; } = "";        // leer = für alle sichtbar (analog Todo/Meeting)
    public int Year { get; set; } = DateTime.Now.Year;
    public List<OkrKeyResult> KeyResults { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class OkrKeyResult
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Text { get; set; } = "";
    public double TargetValue { get; set; }
    public double CurrentValue { get; set; }

    public double ProgressPercent =>
        TargetValue > 0 ? Math.Min(100, Math.Round(CurrentValue / TargetValue * 100, 1)) : 0;
}
