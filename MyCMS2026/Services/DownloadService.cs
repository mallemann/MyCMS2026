using System.Text.Json;
using MyCMS2026.Models;

namespace MyCMS2026.Services;

public class DownloadService
{
    private readonly string _dataFile;
    private readonly string _uploadDir;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<Download>? _cache;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public DownloadService(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _dataFile = Path.Combine(dataDir, "downloads.json");
        _uploadDir = Path.Combine(dataDir, "uploads", "downloads");
        Directory.CreateDirectory(_uploadDir);
        if (!File.Exists(_dataFile))
            File.WriteAllText(_dataFile, "[]");
    }

    // ── Laden / Speichern ────────────────────────────────────────────────────

    private async Task<List<Download>> LoadAsync()
    {
        if (_cache != null) return _cache;
        await _lock.WaitAsync();
        try
        {
            if (_cache != null) return _cache;
            var json = await File.ReadAllTextAsync(_dataFile);
            _cache = JsonSerializer.Deserialize<List<Download>>(json, _jsonOpts) ?? new();
            return _cache;
        }
        finally { _lock.Release(); }
    }

    private async Task SaveAsync(List<Download> items)
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

    // ── CRUD ────────────────────────────────────────────────────────────────

    public async Task<List<Download>> GetAllAsync() =>
        (await LoadAsync()).OrderByDescending(d => d.CreatedAt).ToList();

    public async Task<Download?> GetByIdAsync(string id) =>
        (await LoadAsync()).FirstOrDefault(d => d.Id == id);

    public async Task<Download?> GetByStoredNameAsync(string storedName) =>
        (await LoadAsync()).FirstOrDefault(d => d.StoredName == storedName);

    public async Task<Download> CreateAsync(Download item, IFormFile file)
    {
        var original   = Path.GetFileName(file.FileName);
        var storedName = ResolveStoredName(original);
        var path       = Path.Combine(_uploadDir, storedName);
        using (var stream = File.Create(path))
            await file.CopyToAsync(stream);

        item.Id           = Guid.NewGuid().ToString();
        item.OriginalName = original;
        item.StoredName   = storedName;
        item.Size         = file.Length;
        item.CreatedAt    = DateTime.UtcNow;

        var items = await LoadAsync();
        items.Add(item);
        await SaveAsync(items);
        return item;
    }

    public async Task<bool> UpdateAsync(string id, string beschreibung, string klasse, string? gruppe = null)
    {
        var items = await LoadAsync();
        var item = items.FirstOrDefault(d => d.Id == id);
        if (item == null) return false;
        item.Beschreibung = beschreibung;
        item.Klasse = klasse;
        if (!string.IsNullOrWhiteSpace(gruppe)) item.Gruppe = gruppe;
        await SaveAsync(items);
        return true;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var items = await LoadAsync();
        var item = items.FirstOrDefault(d => d.Id == id);
        if (item == null) return false;
        var path = Path.Combine(_uploadDir, item.StoredName);
        if (File.Exists(path)) File.Delete(path);
        items.Remove(item);
        await SaveAsync(items);
        return true;
    }

    public string GetFilePath(string storedName) => Path.Combine(_uploadDir, storedName);

    // ── Hilfsmethoden ───────────────────────────────────────────────────────

    private string ResolveStoredName(string originalName)
    {
        var name      = Path.GetFileNameWithoutExtension(originalName);
        var ext       = Path.GetExtension(originalName);
        var candidate = originalName;
        var counter   = 2;
        while (File.Exists(Path.Combine(_uploadDir, candidate)))
        {
            candidate = $"{name}_{counter}{ext}";
            counter++;
        }
        return candidate;
    }

    public string GetMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf"  => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png"  => "image/png",
            ".zip"  => "application/zip",
            ".txt"  => "text/plain",
            _       => "application/octet-stream"
        };
    }
}
