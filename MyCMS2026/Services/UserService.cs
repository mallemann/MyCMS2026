using System.Security.Claims;
using System.Text.Json;
using MyCMS2026.Models;

namespace MyCMS2026.Services;

/// <summary>Ergebnis einer Anmeldeprüfung inkl. Brute-Force-Sperre.</summary>
public enum LoginStatus { Success, InvalidCredentials, LockedOut }

public record LoginResult(LoginStatus Status, AppUser? User, DateTime? LockoutEnd = null);

public class UserService
{
    // Brute-Force-Schutz analog MasSafe: 5 Fehlversuche -> 15 Minuten Sperre.
    private const int MaxFailedAccessAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly string _usersFile;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<AppUser>? _cache;
    private readonly KontextService _kontext;

    public UserService(IWebHostEnvironment env, KontextService kontext)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _usersFile = Path.Combine(dataDir, "users.json");
        _kontext = kontext;
        EnsureDefaultAdmin();
    }

    private void EnsureDefaultAdmin()
    {
        if (!File.Exists(_usersFile))
        {
            var admin = new AppUser
            {
                UserName = "Admin",
                Kuerzel = "ADM",
                Email = "",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@2026"),
                Roles = new List<string> { "Administrator", "Member" },
                IsActive = true
            };
            var json = JsonSerializer.Serialize(new List<AppUser> { admin },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_usersFile, json);
        }
    }

    private async Task<List<AppUser>> LoadAsync()
    {
        if (_cache != null) return _cache;
        await _lock.WaitAsync();
        try
        {
            if (_cache != null) return _cache;
            var json = await File.ReadAllTextAsync(_usersFile);
            _cache = JsonSerializer.Deserialize<List<AppUser>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            return _cache;
        }
        finally { _lock.Release(); }
    }

    private async Task SaveAsync(List<AppUser> users)
    {
        await _lock.WaitAsync();
        try
        {
            _cache = users;
            var json = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_usersFile, json);
        }
        finally { _lock.Release(); }
    }

    public async Task<LoginResult> ValidateAsync(string userName, string password)
    {
        var users = await LoadAsync();
        var user = users.FirstOrDefault(u =>
            u.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase) && u.IsActive);
        if (user == null) return new LoginResult(LoginStatus.InvalidCredentials, null);

        // Bereits gesperrt? -> auch bei korrektem Passwort abweisen (wie ASP.NET Identity).
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
            return new LoginResult(LoginStatus.LockedOut, null, user.LockoutEnd);

        // Passwort falsch -> Fehlversuch zählen, ggf. sperren.
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= MaxFailedAccessAttempts)
            {
                user.LockoutEnd = DateTime.UtcNow.Add(LockoutDuration);
                user.AccessFailedCount = 0;
                await SaveAsync(users);
                return new LoginResult(LoginStatus.LockedOut, null, user.LockoutEnd);
            }
            await SaveAsync(users);
            return new LoginResult(LoginStatus.InvalidCredentials, null);
        }

        // Erfolg -> Zähler und Sperre zurücksetzen.
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        user.LoginCount++;
        user.LastLoginAt = DateTime.Now;
        await SaveAsync(users);
        return new LoginResult(LoginStatus.Success, user);
    }

    public async Task<List<AppUser>> GetAllAsync() => await LoadAsync();

    public async Task<AppUser?> GetByNameAsync(string userName)
    {
        var users = await LoadAsync();
        return users.FirstOrDefault(u => u.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> CreateAsync(string userName, string email, string password,
        List<string> roles, string kuerzel = "")
    {
        var users = await LoadAsync();
        if (users.Any(u => u.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase)))
            return false;
        users.Add(new AppUser
        {
            UserName = userName,
            Kuerzel = kuerzel,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Roles = roles,
            IsActive = true
        });
        await SaveAsync(users);
        return true;
    }

    public async Task<bool> DeleteAsync(string userName)
    {
        var users = await LoadAsync();
        var user = users.FirstOrDefault(u => u.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));
        if (user == null) return false;
        users.Remove(user);
        await SaveAsync(users);
        return true;
    }

    public async Task<(bool ok, string error)> UpdateUserAsync(
        string originalUserName, string newUserName, string newEmail,
        string? newPassword, List<string> newRoles, bool isActive, string kuerzel = "")
    {
        var users = await LoadAsync();
        var user = users.FirstOrDefault(u => u.UserName.Equals(originalUserName, StringComparison.OrdinalIgnoreCase));
        if (user == null) return (false, $"Benutzer '{originalUserName}' nicht gefunden.");

        if (!originalUserName.Equals(newUserName, StringComparison.OrdinalIgnoreCase)
            && users.Any(u => u.UserName.Equals(newUserName, StringComparison.OrdinalIgnoreCase)))
            return (false, $"Benutzername '{newUserName}' ist bereits vergeben.");

        user.UserName = newUserName.Trim();
        user.Kuerzel = kuerzel.Trim();
        user.Email = newEmail.Trim();
        user.Roles = newRoles;
        user.IsActive = isActive;

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            if (newPassword.Length < 8)
                return (false, "Passwort muss mindestens 8 Zeichen lang sein.");
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        }

        await SaveAsync(users);
        return (true, "");
    }

    /// <summary>Setzt das Passwort ohne andere Felder (Rollen, E-Mail etc.) zu ändern.</summary>
    public async Task<bool> SetPasswordAsync(string userName, string newPassword)
    {
        var users = await LoadAsync();
        var user = users.FirstOrDefault(u => u.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));
        if (user == null) return false;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await SaveAsync(users);
        return true;
    }

    public async Task<bool> ChangePasswordAsync(string userName, string currentPassword, string newPassword)
    {
        var users = await LoadAsync();
        var user = users.FirstOrDefault(u => u.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));
        if (user == null) return false;
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash)) return false;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await SaveAsync(users);
        return true;
    }

    public void InvalidateCache() => _cache = null;

    // ── Passwort-Reset ────────────────────────────────────────────────────────

    public async Task<(string? token, string? email, string? userName)> GeneratePasswordResetTokenAsync(string userNameOrEmail)
    {
        var users = await LoadAsync();
        var user = users.FirstOrDefault(u =>
            u.IsActive && (
                u.UserName.Equals(userNameOrEmail, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(u.Email) && u.Email.Equals(userNameOrEmail, StringComparison.OrdinalIgnoreCase))
            ));

        if (user == null || string.IsNullOrEmpty(user.Email)) return (null, null, null);

        var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
                           .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        user.PasswordResetToken = token;
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
        await SaveAsync(users);

        return (token, user.Email, user.UserName);
    }

    public async Task<AppUser?> GetByResetTokenAsync(string token)
    {
        var users = await LoadAsync();
        return users.FirstOrDefault(u =>
            u.PasswordResetToken == token &&
            u.PasswordResetTokenExpiry.HasValue &&
            u.PasswordResetTokenExpiry.Value > DateTime.UtcNow);
    }

    public async Task<bool> ResetPasswordWithTokenAsync(string token, string newPassword)
    {
        var users = await LoadAsync();
        var user = users.FirstOrDefault(u =>
            u.PasswordResetToken == token &&
            u.PasswordResetTokenExpiry.HasValue &&
            u.PasswordResetTokenExpiry.Value > DateTime.UtcNow);

        if (user == null) return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;
        await SaveAsync(users);
        return true;
    }

    private static ClaimsPrincipal BuildFromRoles(AppUser user, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Email, user.Email),
            new("Kuerzel", user.Kuerzel)
        };
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var identity = new ClaimsIdentity(claims, "MyCMSCookies");
        return new ClaimsPrincipal(identity);
    }

    /// <summary>Rohe Rollen (Admin-Grant), ohne Kontext-Deaktivierung.</summary>
    public ClaimsPrincipal BuildPrincipal(AppUser user) => BuildFromRoles(user, user.Roles);

    /// <summary>Principal mit EFFEKTIVEN Rollen (deaktivierte Kontexte herausgerechnet). Für Login und Re-SignIn.</summary>
    public async Task<ClaimsPrincipal> BuildPrincipalAsync(AppUser user)
        => BuildFromRoles(user, await GetEffectiveRolesAsync(user));

    /// <summary>
    /// Effektive Rollen = Rollen minus Rollen deaktivierter Kontexte – aber nur, wenn kein aktiver
    /// (nicht deaktivierter) Kontext dieselbe Rolle noch benötigt.
    /// </summary>
    public async Task<List<string>> GetEffectiveRolesAsync(AppUser user)
    {
        if (user.DeaktivierteKontexte == null || user.DeaktivierteKontexte.Count == 0)
            return user.Roles.ToList();

        var kontexte    = await _kontext.GetAllAsync();
        var deaktiviert = user.DeaktivierteKontexte;

        var rollenAktiv = kontexte
            .Where(k => !deaktiviert.Contains(k.Gruppe, StringComparer.OrdinalIgnoreCase))
            .Select(k => k.Rolle)
            .Where(r => !string.IsNullOrEmpty(r))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var entfernen = kontexte
            .Where(k => deaktiviert.Contains(k.Gruppe, StringComparer.OrdinalIgnoreCase))
            .Select(k => k.Rolle)
            .Where(r => !string.IsNullOrEmpty(r) && !rollenAktiv.Contains(r))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return user.Roles.Where(r => !entfernen.Contains(r)).ToList();
    }

    /// <summary>Setzt die vom User deaktivierten Kontexte (Gruppen) und gibt den aktualisierten User zurück.</summary>
    public async Task<AppUser?> SetDeaktivierteKontexteAsync(string userName, List<string> deaktiviert)
    {
        var users = await LoadAsync();
        var user = users.FirstOrDefault(u => u.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));
        if (user == null) return null;
        user.DeaktivierteKontexte = deaktiviert ?? new List<string>();
        await SaveAsync(users);
        return user;
    }

    public List<string> AvailableRoles => new() { "Administrator", "Member", "Guest" };
}
