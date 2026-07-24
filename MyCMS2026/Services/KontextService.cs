using System.Text.Json;
using MyCMS2026.Models;

namespace MyCMS2026.Services;

/// <summary>
/// Verwaltet die Kontext-Konfiguration (Gruppe → primäre Sichtbarkeitsrolle + Beschreibung).
/// Gespeichert in App_Data/kontexts.json.
/// </summary>
public class KontextService
{
    private readonly string _dataFile;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<Kontext>? _cache;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public KontextService(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _dataFile = Path.Combine(dataDir, "kontexts.json");
        if (!File.Exists(_dataFile))
            File.WriteAllText(_dataFile, "[]");
    }

    private async Task<List<Kontext>> LoadAsync()
    {
        if (_cache != null) return _cache;
        await _lock.WaitAsync();
        try
        {
            if (_cache != null) return _cache;
            var json = await File.ReadAllTextAsync(_dataFile);
            _cache = JsonSerializer.Deserialize<List<Kontext>>(json, _jsonOpts) ?? new();
            return _cache;
        }
        finally { _lock.Release(); }
    }

    public async Task<List<Kontext>> GetAllAsync() => (await LoadAsync()).ToList();

    public async Task<Kontext?> GetByGruppeAsync(string gruppe)
        => (await LoadAsync()).FirstOrDefault(k => string.Equals(k.Gruppe, gruppe, StringComparison.OrdinalIgnoreCase));

    public async Task SaveAllAsync(List<Kontext> list)
    {
        await _lock.WaitAsync();
        try
        {
            _cache = list;
            await File.WriteAllTextAsync(_dataFile, JsonSerializer.Serialize(list, _jsonOpts));
        }
        finally { _lock.Release(); }
    }

    public void InvalidateCache() => _cache = null;
}
