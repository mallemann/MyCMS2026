using System.Text.Json;
using MyCMS2026.Models;

namespace MyCMS2026.Services;

public class OkrService
{
    private readonly string _dataFile;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<OkrObjective>? _cache;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public OkrService(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _dataFile = Path.Combine(dataDir, "okrs.json");
        if (!File.Exists(_dataFile))
            File.WriteAllText(_dataFile, "[]");
    }

    private async Task<List<OkrObjective>> LoadAsync()
    {
        if (_cache != null) return _cache;
        await _lock.WaitAsync();
        try
        {
            if (_cache != null) return _cache;
            var json = await File.ReadAllTextAsync(_dataFile);
            _cache = JsonSerializer.Deserialize<List<OkrObjective>>(json, _jsonOpts) ?? new();
            return _cache;
        }
        finally { _lock.Release(); }
    }

    private async Task SaveAsync(List<OkrObjective> items)
    {
        await _lock.WaitAsync();
        try
        {
            _cache = items;
            var json = JsonSerializer.Serialize(items, _jsonOpts);
            await File.WriteAllTextAsync(_dataFile, json);
        }
        finally { _lock.Release(); }
    }

    public async Task<List<OkrObjective>> GetAllAsync() =>
        (await LoadAsync()).OrderByDescending(o => o.Year).ThenBy(o => o.CreatedAt).ToList();

    public async Task<List<OkrObjective>> GetByYearAsync(int year) =>
        (await LoadAsync()).Where(o => o.Year == year).OrderBy(o => o.CreatedAt).ToList();

    public async Task<OkrObjective?> GetByIdAsync(string id) =>
        (await LoadAsync()).FirstOrDefault(o => o.Id == id);

    public async Task<OkrObjective> CreateObjectiveAsync(OkrObjective obj)
    {
        var items = await LoadAsync();
        obj.Id = Guid.NewGuid().ToString();
        obj.CreatedAt = DateTime.UtcNow;
        items.Add(obj);
        await SaveAsync(items);
        return obj;
    }

    public async Task<bool> UpdateObjectiveAsync(string id, string text, string status, int year)
    {
        var items = await LoadAsync();
        var obj = items.FirstOrDefault(o => o.Id == id);
        if (obj == null) return false;
        obj.Text   = text;
        obj.Status = status;
        obj.Year   = year;
        await SaveAsync(items);
        return true;
    }

    public async Task<bool> DeleteObjectiveAsync(string id)
    {
        var items = await LoadAsync();
        var obj = items.FirstOrDefault(o => o.Id == id);
        if (obj == null) return false;
        items.Remove(obj);
        await SaveAsync(items);
        return true;
    }

    public async Task<OkrKeyResult?> AddKeyResultAsync(string objectiveId, OkrKeyResult kr)
    {
        var items = await LoadAsync();
        var obj = items.FirstOrDefault(o => o.Id == objectiveId);
        if (obj == null) return null;
        kr.Id = Guid.NewGuid().ToString();
        obj.KeyResults.Add(kr);
        await SaveAsync(items);
        return kr;
    }

    public async Task<bool> UpdateKeyResultAsync(string objectiveId, string krId, string text, double target, double current)
    {
        var items = await LoadAsync();
        var obj = items.FirstOrDefault(o => o.Id == objectiveId);
        var kr = obj?.KeyResults.FirstOrDefault(k => k.Id == krId);
        if (kr == null) return false;
        kr.Text         = text;
        kr.TargetValue  = target;
        kr.CurrentValue = current;
        await SaveAsync(items);
        return true;
    }

    public async Task<bool> UpdateProgressAsync(string objectiveId, string krId, double current)
    {
        var items = await LoadAsync();
        var obj = items.FirstOrDefault(o => o.Id == objectiveId);
        var kr = obj?.KeyResults.FirstOrDefault(k => k.Id == krId);
        if (kr == null) return false;
        kr.CurrentValue = current;
        await SaveAsync(items);
        return true;
    }

    public async Task<bool> DeleteKeyResultAsync(string objectiveId, string krId)
    {
        var items = await LoadAsync();
        var obj = items.FirstOrDefault(o => o.Id == objectiveId);
        if (obj == null) return false;
        var kr = obj.KeyResults.FirstOrDefault(k => k.Id == krId);
        if (kr == null) return false;
        obj.KeyResults.Remove(kr);
        await SaveAsync(items);
        return true;
    }

    public async Task<List<int>> GetYearsAsync()
    {
        var items = await LoadAsync();
        var years = items.Select(o => o.Year).Distinct().OrderByDescending(y => y).ToList();
        if (!years.Contains(DateTime.Now.Year))
            years.Insert(0, DateTime.Now.Year);
        return years;
    }
}
