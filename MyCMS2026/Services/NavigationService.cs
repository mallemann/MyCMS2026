using System.Text.Json;
using MyCMS2026.Models;

namespace MyCMS2026.Services;

public class NavigationService
{
    private readonly string _navFile;
    private readonly string _pagesDir;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<NavItem>? _cache;

    public NavigationService(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _navFile  = Path.Combine(dataDir, "navigation.json");
        _pagesDir = Path.Combine(dataDir, "pages");
        EnsureDefaults();
    }

    private void EnsureDefaults()
    {
        if (!File.Exists(_navFile))
        {
            var defaults = new List<NavItem>
            {
                new() { Id = "1", ParentId = null, Title = "Home", NavigationText = "Home",
                         VisibilityRole = "Public", BasicAccessRole = "Public",
                         ExtendedAccessRole = "Administrator", Widget = "wHome",
                         ConfigString = "", MenuOrder = 1 },
                new() { Id = "3", ParentId = null, Title = "Administration", NavigationText = "Admin",
                         VisibilityRole = "Administrator", BasicAccessRole = "Administrator",
                         ExtendedAccessRole = "Administrator", Widget = "wAdmin",
                         ConfigString = "", MenuOrder = 99 }
            };
            var json = JsonSerializer.Serialize(defaults, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_navFile, json);
        }
    }

    private async Task<List<NavItem>> LoadFlatAsync()
    {
        if (_cache != null) return _cache;
        await _lock.WaitAsync();
        try
        {
            if (_cache != null) return _cache;
            var json = await File.ReadAllTextAsync(_navFile);
            _cache = JsonSerializer.Deserialize<List<NavItem>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            return _cache;
        }
        finally { _lock.Release(); }
    }

    private async Task SaveAsync(List<NavItem> items)
    {
        await _lock.WaitAsync();
        try
        {
            _cache = items;
            // Nur flache Felder serialisieren – Children nicht speichern
            var toSave = items.Select(i => new NavItem
            {
                Id = i.Id, ParentId = i.ParentId, Title = i.Title,
                NavigationText = i.NavigationText, VisibilityRole = i.VisibilityRole,
                BasicAccessRole = i.BasicAccessRole, ExtendedAccessRole = i.ExtendedAccessRole,
                Widget = i.Widget, ConfigString = i.ConfigString, MenuOrder = i.MenuOrder
            }).ToList();
            var json = JsonSerializer.Serialize(toSave, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_navFile, json);
        }
        finally { _lock.Release(); }
    }

    /// <summary>Alle NavItems flach (ohne Children-Verschachtelung)</summary>
    public async Task<List<NavItem>> GetAllAsync() => await LoadFlatAsync();

    /// <summary>Einzelnen NavItem nach ID</summary>
    public async Task<NavItem?> GetByIdAsync(string id)
    {
        var items = await LoadFlatAsync();
        return items.FirstOrDefault(i => i.Id == id);
    }

    /// <summary>
    /// Hierarchie aufbauen – gefiltert nach Rollen des Benutzers.
    /// "Public" ist immer sichtbar, "Administrator" sieht alles.
    /// </summary>
    public async Task<List<NavItem>> GetTreeAsync(IEnumerable<string> userRoles)
    {
        var flat = await LoadFlatAsync();
        var roles = userRoles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool isAdmin = roles.Contains("Administrator");

        bool CanSee(NavItem item) =>
            item.VisibilityRole.Equals("Public", StringComparison.OrdinalIgnoreCase)
            || isAdmin
            || roles.Contains(item.VisibilityRole);

        // Top-Level
        var roots = flat
            .Where(i => string.IsNullOrEmpty(i.ParentId) && CanSee(i))
            .OrderBy(i => i.MenuOrder)
            .Select(i => Clone(i))
            .ToList();

        foreach (var root in roots)
            root.Children = flat
                .Where(i => i.ParentId == root.Id && CanSee(i))
                .OrderBy(i => i.MenuOrder)
                .Select(i => Clone(i))
                .ToList();

        return roots;
    }

    /// <summary>Prüft ob ein Benutzer Lesezugriff auf eine Seite hat.</summary>
    public async Task<bool> CanAccessAsync(string id, IEnumerable<string> userRoles)
    {
        var item = await GetByIdAsync(id);
        if (item == null) return false;
        var roles = userRoles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return item.BasicAccessRole.Equals("Public", StringComparison.OrdinalIgnoreCase)
               || roles.Contains("Administrator")
               || roles.Contains(item.BasicAccessRole);
    }

    /// <summary>Prüft ob ein Benutzer erweiterte Rechte (Bearbeiten) auf einer Seite hat.</summary>
    public async Task<bool> HasExtendedAccessAsync(string id, IEnumerable<string> userRoles)
    {
        var item = await GetByIdAsync(id);
        if (item == null) return false;
        var roles = userRoles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return roles.Contains("Administrator") || roles.Contains(item.ExtendedAccessRole);
    }

    /// <summary>
    /// Prüft ob ein Benutzer Zugriff auf eine Vault-Gruppe hat.
    /// Massgeblich sind die Nav-Einträge mit Widget "wVault": der Benutzer braucht
    /// Lesezugriff (BasicAccessRole) auf einen Eintrag mit passendem ConfigString.
    /// Mit requireExtended=true wird stattdessen ExtendedAccessRole geprüft (z.B. für Uploads).
    /// Administratoren haben immer Zugriff.
    /// </summary>
    public async Task<bool> CanAccessVaultGruppeAsync(
        string? gruppe, IEnumerable<string> userRoles, bool requireExtended = false)
    {
        var roles = userRoles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (roles.Contains("Administrator")) return true;

        var items = await LoadFlatAsync();
        return items.Any(i =>
            string.Equals(i.Widget, "wVault", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(i.ConfigString ?? "", gruppe ?? "", StringComparison.OrdinalIgnoreCase) &&
            (requireExtended
                ? roles.Contains(i.ExtendedAccessRole)
                : i.BasicAccessRole.Equals("Public", StringComparison.OrdinalIgnoreCase)
                  || roles.Contains(i.BasicAccessRole)));
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public async Task<bool> CreateAsync(NavItem item)
    {
        var items = await LoadFlatAsync();
        if (items.Any(i => i.Id == item.Id)) return false;
        if (string.IsNullOrEmpty(item.Id)) item.Id = Guid.NewGuid().ToString();
        items.Add(item);
        await SaveAsync(items);
        return true;
    }

    public async Task<bool> UpdateAsync(NavItem updated)
    {
        var items = await LoadFlatAsync();
        var existing = items.FirstOrDefault(i => i.Id == updated.Id);
        if (existing == null) return false;
        var idx = items.IndexOf(existing);
        items[idx] = updated;
        await SaveAsync(items);
        return true;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var items = await LoadFlatAsync();
        var item = items.FirstOrDefault(i => i.Id == id);
        if (item == null) return false;

        // Kinder-Einträge ebenfalls entfernen
        items.RemoveAll(i => i.Id == id || i.ParentId == id);
        await SaveAsync(items);

        // HTML-Datei löschen wenn wHTMLPage oder wHome mit explizitem ConfigString
        // und kein anderer Nav-Eintrag dieselbe Datei noch referenziert
        if (!string.IsNullOrEmpty(item.ConfigString) &&
            (item.Widget == "wHTMLPage" || item.Widget == "wHome"))
        {
            var stillReferenced = items.Any(i =>
                (i.Widget == "wHTMLPage" || i.Widget == "wHome") &&
                i.ConfigString == item.ConfigString);

            if (!stillReferenced)
            {
                var filePath = Path.Combine(_pagesDir, item.ConfigString);
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        return true;
    }

    public void InvalidateCache() => _cache = null;

    private static NavItem Clone(NavItem i) => new()
    {
        Id = i.Id, ParentId = i.ParentId, Title = i.Title,
        NavigationText = i.NavigationText, VisibilityRole = i.VisibilityRole,
        BasicAccessRole = i.BasicAccessRole, ExtendedAccessRole = i.ExtendedAccessRole,
        Widget = i.Widget, ConfigString = i.ConfigString, MenuOrder = i.MenuOrder
    };
}
