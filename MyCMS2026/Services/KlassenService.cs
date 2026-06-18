using System.Text.Json;

namespace MyCMS2026.Services;

public class KlassenService
{
    private readonly string _dataFile;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Dictionary<string, List<string>>? _cache;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static readonly Dictionary<string, List<string>> _defaults = new()
    {
        ["todos"]     = new() { "Allgemein", "Projekt", "IT", "Admin", "Diverses" },
        ["meetings"]  = new() { "Team", "Projekt", "Kunden", "Management", "Diverses" },
        ["downloads"] = new() { "Instruktion", "Info", "Template", "Protokoll", "Finanzen", "Diverses" }
    };

    public KlassenService(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _dataFile = Path.Combine(dataDir, "klassen.json");
        if (!File.Exists(_dataFile))
            File.WriteAllText(_dataFile, JsonSerializer.Serialize(_defaults, _jsonOpts));
    }

    private async Task<Dictionary<string, List<string>>> LoadAsync()
    {
        if (_cache != null) return _cache;
        await _lock.WaitAsync();
        try
        {
            if (_cache != null) return _cache;
            var json = await File.ReadAllTextAsync(_dataFile);
            var data = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json, _jsonOpts) ?? new();
            // Defaults für fehlende Keys ergänzen
            foreach (var kv in _defaults)
                if (!data.ContainsKey(kv.Key)) data[kv.Key] = kv.Value;
            _cache = data;
            return _cache;
        }
        finally { _lock.Release(); }
    }

    private async Task SaveAsync(Dictionary<string, List<string>> data)
    {
        await _lock.WaitAsync();
        try
        {
            _cache = data;
            var json = JsonSerializer.Serialize(data, _jsonOpts);
            await File.WriteAllTextAsync(_dataFile, json);
        }
        finally { _lock.Release(); }
    }

    public async Task<List<string>> GetKlassenAsync(string type)
    {
        var data = await LoadAsync();
        return data.TryGetValue(type, out var list) ? list : new();
    }

    public async Task SetKlassenAsync(string type, List<string> klassen)
    {
        var data = await LoadAsync();
        data[type] = klassen.Select(k => k.Trim()).Where(k => k.Length > 0).Distinct().ToList();
        await SaveAsync(data);
    }

    public static IReadOnlyList<string> Types => new[] { "todos", "meetings", "downloads" };
}
