using System.Text.Json;
using MyCMS2026.Models;

namespace MyCMS2026.Services;

/// <summary>
/// Singleton-Service für Pendenzen. Speichert alle Listen in App_Data/pendenzen.json.
/// Die ConfigString-Property auf dem Pendenz-Record unterscheidet verschiedene Listen.
/// </summary>
public class PendenzService
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<Pendenz>? _cache;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public PendenzService(IWebHostEnvironment env)
    {
        var dir = System.IO.Path.Combine(env.ContentRootPath, "App_Data");
        System.IO.Directory.CreateDirectory(dir);
        _filePath = System.IO.Path.Combine(dir, "pendenzen.json");
    }

    // ─── interne Lade-/Speicherlogik ──────────────────────────────────────────

    private async Task<List<Pendenz>> LoadAsync()
    {
        if (_cache != null) return _cache;

        if (!System.IO.File.Exists(_filePath))
        {
            _cache = new List<Pendenz>();
            return _cache;
        }

        var json = await System.IO.File.ReadAllTextAsync(_filePath);
        _cache = JsonSerializer.Deserialize<List<Pendenz>>(json, _jsonOpts) ?? new();
        return _cache;
    }

    private async Task SaveAsync(List<Pendenz> list)
    {
        _cache = list;
        var json = JsonSerializer.Serialize(list, _jsonOpts);
        await System.IO.File.WriteAllTextAsync(_filePath, json);
    }

    // ─── öffentliche API ───────────────────────────────────────────────────────

    /// <summary>Alle Pendenzen ohne Filterung (Admin-Übersicht).</summary>
    public async Task<List<Pendenz>> GetAllAsync()
    {
        await _lock.WaitAsync();
        try { return (await LoadAsync()).OrderBy(p => p.Nr).ToList(); }
        finally { _lock.Release(); }
    }

    /// <summary>Persönliche Pendenzen eines Users: alle Pendenzen wo Verantwortlich=user.</summary>
    public async Task<List<Pendenz>> GetPersonalAsync(string user)
    {
        await _lock.WaitAsync();
        try
        {
            var all = await LoadAsync();
            return all.Where(p => p.Verantwortlich == user)
                      .OrderBy(p => p.Nr).ToList();
        }
        finally { _lock.Release(); }
    }

    /// <summary>Einzelnen Datensatz per Id.</summary>
    public async Task<Pendenz?> GetByIdAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            var all = await LoadAsync();
            return all.FirstOrDefault(p => p.Id == id);
        }
        finally { _lock.Release(); }
    }

    /// <summary>Neue Pendenz anlegen. Nr wird automatisch vergeben.</summary>
    public async Task<Pendenz> CreateAsync(Pendenz pendenz, string currentUser)
    {
        await _lock.WaitAsync();
        try
        {
            var all = await LoadAsync();

            // Nr = Max in dieser Liste + 1
            var maxNr = all.Where(p => p.ConfigString == pendenz.ConfigString)
                           .Select(p => p.Nr)
                           .DefaultIfEmpty(0)
                           .Max();

            pendenz.Id = Guid.NewGuid().ToString();
            pendenz.Nr = maxNr + 1;
            pendenz.ErfasstAm = DateTime.Now;
            pendenz.ErfasstVon = currentUser;
            pendenz.Erledigt = false;
            pendenz.ConfigString ??= "";  // null → "" normalisieren
            pendenz.History ??= new();
            pendenz.History.Insert(0, new PendenzLogEntry
            {
                Timestamp = DateTime.Now,
                User = currentUser,
                Aktion = "Erstellt"
            });

            all.Add(pendenz);
            await SaveAsync(all);
            return pendenz;
        }
        finally { _lock.Release(); }
    }

    /// <summary>Bestehende Pendenz aktualisieren.</summary>
    public async Task<bool> UpdateAsync(Pendenz updated, string currentUser)
    {
        await _lock.WaitAsync();
        try
        {
            var all = await LoadAsync();
            var idx = all.FindIndex(p => p.Id == updated.Id);
            if (idx < 0) return false;

            var existing = all[idx];

            // Unveränderliche Felder übernehmen
            updated.Id = existing.Id;
            updated.Nr = existing.Nr;
            updated.ErfasstAm = existing.ErfasstAm;
            updated.ErfasstVon = existing.ErfasstVon;
            updated.ConfigString = existing.ConfigString;
            updated.History = existing.History;

            // Wenn gerade erledigt markiert
            if (updated.Erledigt && !existing.Erledigt)
            {
                updated.ErledigtAm = DateTime.Now;
                updated.ErledigtVon = currentUser;
                updated.History.Insert(0, new PendenzLogEntry
                {
                    Timestamp = DateTime.Now,
                    User = currentUser,
                    Aktion = "Als erledigt markiert"
                });
            }
            // Wenn Erledigung rückgängig gemacht
            else if (!updated.Erledigt && existing.Erledigt)
            {
                updated.ErledigtAm = null;
                updated.ErledigtVon = null;
                updated.History.Insert(0, new PendenzLogEntry
                {
                    Timestamp = DateTime.Now,
                    User = currentUser,
                    Aktion = "Erledigung zurückgenommen"
                });
            }
            else
            {
                updated.History.Insert(0, new PendenzLogEntry
                {
                    Timestamp = DateTime.Now,
                    User = currentUser,
                    Aktion = "Bearbeitet"
                });
            }

            all[idx] = updated;
            await SaveAsync(all);
            return true;
        }
        finally { _lock.Release(); }
    }

    /// <summary>Erledigt-Status schnell umschalten (aus der Liste heraus).</summary>
    public async Task<bool> ToggleErledigtAsync(string id, string currentUser)
    {
        await _lock.WaitAsync();
        try
        {
            var all = await LoadAsync();
            var p = all.FirstOrDefault(x => x.Id == id);
            if (p == null) return false;

            p.Erledigt = !p.Erledigt;
            if (p.Erledigt)
            {
                p.ErledigtAm = DateTime.Now;
                p.ErledigtVon = currentUser;
                p.History.Insert(0, new PendenzLogEntry
                {
                    Timestamp = DateTime.Now, User = currentUser, Aktion = "Als erledigt markiert"
                });
            }
            else
            {
                p.ErledigtAm = null;
                p.ErledigtVon = null;
                p.History.Insert(0, new PendenzLogEntry
                {
                    Timestamp = DateTime.Now, User = currentUser, Aktion = "Erledigung zurückgenommen"
                });
            }

            await SaveAsync(all);
            return true;
        }
        finally { _lock.Release(); }
    }

    /// <summary>Löschen – nur erledigte Einträge; nur eigene oder Administrator.</summary>
    public async Task<(bool success, string message)> DeleteAsync(string id, string currentUser, bool isAdmin)
    {
        await _lock.WaitAsync();
        try
        {
            var all = await LoadAsync();
            var p = all.FirstOrDefault(x => x.Id == id);
            if (p == null) return (false, "Pendenz nicht gefunden.");
            if (!isAdmin && !p.Erledigt) return (false, "Nur erledigte Pendenzen können gelöscht werden.");
            if (!isAdmin && p.Verantwortlich != currentUser)
                return (false, "Sie können nur Ihre eigenen Pendenzen löschen.");

            all.Remove(p);
            await SaveAsync(all);
            return (true, "Gelöscht.");
        }
        finally { _lock.Release(); }
    }
}
