using System.Text.Json;
using MyCMS2026.Models;

namespace MyCMS2026.Services;

/// <summary>
/// Speichert Anmelde- und Navigationsaktivitäten pro Tag pro Benutzer.
/// Ein Eintrag pro (User, Datum). Seitenbesuche ohne Duplikate.
/// Administratoren werden nicht getrackt.
/// </summary>
public class ActivityService
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ActivityService(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "activity-log.json");
    }

    // ── Öffentliche API ──────────────────────────────────────────────────────

    /// <summary>Beim Login aufrufen – legt Tageseintrag an falls nötig.</summary>
    public Task RecordLoginAsync(string user) =>
        UpdateEntryAsync(user, _ => { /* FirstLogin wird beim Anlegen gesetzt */ });

    /// <summary>Beim Seitenaufruf aufrufen – fügt Seite hinzu wenn noch nicht vorhanden.</summary>
    public Task RecordPageAsync(string user, string page) =>
        UpdateEntryAsync(user, entry =>
        {
            if (!entry.Pages.Contains(page))
                entry.Pages.Add(page);
        });

    /// <summary>Alle Einträge lesen, neueste zuerst.</summary>
    public async Task<List<ActivityEntry>> GetAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var entries = await ReadAsync();
            return entries
                .OrderByDescending(e => e.Date)
                .ThenByDescending(e => e.FirstLogin)
                .ToList();
        }
        finally { _lock.Release(); }
    }

    // ── Interna ──────────────────────────────────────────────────────────────

    private async Task UpdateEntryAsync(string user, Action<ActivityEntry> mutate)
    {
        await _lock.WaitAsync();
        try
        {
            var entries = await ReadAsync();
            var today   = DateTime.Now.ToString("yyyy-MM-dd");
            var entry   = entries.FirstOrDefault(e => e.User == user && e.Date == today);

            if (entry is null)
            {
                entry = new ActivityEntry
                {
                    User       = user,
                    Date       = today,
                    FirstLogin = DateTime.Now.ToString("HH:mm")
                };
                entries.Add(entry);
            }

            mutate(entry);
            await WriteAsync(entries);
        }
        finally { _lock.Release(); }
    }

    private async Task<List<ActivityEntry>> ReadAsync()
    {
        if (!File.Exists(_filePath)) return new();
        var json = await File.ReadAllTextAsync(_filePath);
        return JsonSerializer.Deserialize<List<ActivityEntry>>(json, _json) ?? new();
    }

    private Task WriteAsync(List<ActivityEntry> entries)
    {
        var json = JsonSerializer.Serialize(entries, _json);
        return File.WriteAllTextAsync(_filePath, json);
    }
}
