namespace MyCMS2026.Models;

public class AppUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserName { get; set; } = "";
    public string Kuerzel { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public List<string> Roles { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int LoginCount { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiry { get; set; }

    // Brute-Force-Schutz: Fehlversuche zählen und Konto temporär sperren.
    public int AccessFailedCount { get; set; }
    public DateTime? LockoutEnd { get; set; }
}
