namespace MyCMS2026.Models;

public class WeeklyMailConfig
{
    public List<WeeklyMailRecipient> Recipients { get; set; } = new();
    public DateTime? LastSentAt { get; set; }
}

public class WeeklyMailRecipient
{
    public string UserId   { get; set; } = "";   // AppUser.UserName
    public string Email    { get; set; } = "";
    public bool ReceiveTodos    { get; set; } = true;
    public bool ReceiveMeetings { get; set; } = true;
    public bool ReceiveJournal  { get; set; } = true;

    /// <summary>
    /// Welche Gruppen (Klassen) dieser Empfänger bei Todos und Meetings sehen darf.
    /// Leer = nur Einträge ohne Gruppe (und ohne Projekt).
    /// </summary>
    public List<string> AllowedGruppen { get; set; } = new();
}
