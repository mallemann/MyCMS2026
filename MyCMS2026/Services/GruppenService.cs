using System.Text.Json;

namespace MyCMS2026.Services;

/// <summary>
/// Verwaltet die Liste der verfügbaren Gruppen (werden als ConfigString in Nav-Einträgen
/// für wToDo / wMeetings verwendet und in Todo.Gruppe / Meeting.Gruppe gespeichert).
/// </summary>
public class GruppenService
{
    private readonly string _dataFile;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<string>? _cache;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public GruppenService(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _dataFile = Path.Combine(dataDir, "gruppen.json");
        if (!File.Exists(_dataFile))
            File.WriteAllText(_dataFile, JsonSerializer.Serialize(new List<string>(), _jsonOpts));
    }

    private async Task<List<string>> LoadAsync()
    {
        if (_cache != null) return _cache;
        await _lock.WaitAsync();
        try
        {
            if (_cache != null) return _cache;
            var json = await File.ReadAllTextAsync(_dataFile);
            _cache = JsonSerializer.Deserialize<List<string>>(json, _jsonOpts) ?? new();
            return _cache;
        }
        finally { _lock.Release(); }
    }

    private async Task SaveAsync(List<string> data)
    {
        await _lock.WaitAsync();
        try
        {
            _cache = data;
            await File.WriteAllTextAsync(_dataFile, JsonSerializer.Serialize(data, _jsonOpts));
        }
        finally { _lock.Release(); }
    }

    public async Task<List<string>> GetAllAsync()
        => (await LoadAsync()).OrderBy(g => g).ToList();

    public async Task AddAsync(string name)
    {
        name = name.Trim();
        if (string.IsNullOrEmpty(name)) return;
        var data = await LoadAsync();
        if (!data.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            data.Add(name);
            await SaveAsync(data);
        }
    }

    public async Task DeleteAsync(string name)
    {
        var data = await LoadAsync();
        data.RemoveAll(g => string.Equals(g, name, StringComparison.OrdinalIgnoreCase));
        await SaveAsync(data);
    }

    public void InvalidateCache() => _cache = null;
}
