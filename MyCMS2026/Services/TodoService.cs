using System.Text.Json;
using MyCMS2026.Models;

namespace MyCMS2026.Services;

public class TodoService
{
    private readonly string _dataFile;
    private readonly string _uploadDir;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<TodoItem>? _cache;
    private readonly ProjectService _projects;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public TodoService(IWebHostEnvironment env, ProjectService projects)
    {
        _projects = projects;
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _dataFile = Path.Combine(dataDir, "todos.json");
        _uploadDir = Path.Combine(dataDir, "uploads", "todos");
        Directory.CreateDirectory(_uploadDir);
        if (!File.Exists(_dataFile))
            File.WriteAllText(_dataFile, "[]");
    }

    // ── Laden / Speichern ────────────────────────────────────────────────────

    private async Task<List<TodoItem>> LoadAsync()
    {
        if (_cache != null) return _cache;
        await _lock.WaitAsync();
        try
        {
            if (_cache != null) return _cache;
            var json = await File.ReadAllTextAsync(_dataFile);
            var items = JsonSerializer.Deserialize<List<TodoItem>>(json, _jsonOpts) ?? new();
            await AssignNrsIfNeededAsync(items);
            _cache = items;
            return _cache;
        }
        finally { _lock.Release(); }
    }

    private async Task SaveAsync(List<TodoItem> items)
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

    private async Task AssignNrsIfNeededAsync(List<TodoItem> items)
    {
        bool dirty = false;

        // Nr. vergeben falls noch keine vorhanden
        if (items.Any(t => t.TaskNr == 0))
        {
            var nextNr = items.Where(t => t.TaskNr > 0).Select(t => t.TaskNr).DefaultIfEmpty(0).Max() + 1;
            foreach (var t in items.Where(t => t.TaskNr == 0).OrderBy(t => t.CreatedAt))
                t.TaskNr = nextNr++;
            dirty = true;
        }

        // Migration: Klasse "" → "Allgemein"
        foreach (var t in items.Where(t => string.IsNullOrEmpty(t.Klasse)))
        {
            t.Klasse = "Allgemein";
            dirty = true;
        }

        if (dirty)
        {
            var json = JsonSerializer.Serialize(items, _jsonOpts);
            await File.WriteAllTextAsync(_dataFile, json);
        }
    }

    // ── CRUD ────────────────────────────────────────────────────────────────

    public async Task<List<TodoItem>> GetAllAsync()
    {
        var items = await LoadAsync();
        // Backfill ProjectName für bestehende Einträge mit ProjectId
        var needsFill = items.Where(t => !string.IsNullOrEmpty(t.ProjectId) && string.IsNullOrEmpty(t.ProjectName)).ToList();
        if (needsFill.Any())
        {
            foreach (var t in needsFill)
            {
                var proj = await _projects.GetByIdAsync(t.ProjectId!);
                t.ProjectName = proj?.Name;
            }
            await SaveAsync(items);
        }
        return items.OrderBy(t => t.Erledigt).ThenBy(t => t.ErledigenBis).ToList();
    }

    public async Task<TodoItem?> GetByIdAsync(string id) =>
        (await LoadAsync()).FirstOrDefault(t => t.Id == id);

    public async Task<TodoItem> CreateAsync(TodoItem item, List<IFormFile> files)
    {
        item.Thema         = item.Thema         ?? "";
        item.Verantwortlich= item.Verantwortlich?? "";
        item.Klasse        = string.IsNullOrEmpty(item.Klasse) ? "Allgemein" : item.Klasse;
        item.Beschreibung  = item.Beschreibung  ?? "";
        if (!string.IsNullOrEmpty(item.ProjectId))
        {
            var proj = await _projects.GetByIdAsync(item.ProjectId);
            item.ProjectName = proj?.Name;
        }
        var items = await LoadAsync();
        item.Id        = Guid.NewGuid().ToString();
        item.TaskNr    = items.Count == 0 ? 1 : items.Max(t => t.TaskNr) + 1;
        item.CreatedAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;
        Directory.CreateDirectory(GetTaskDir(item.TaskNr));
        await AttachFilesAsync(item, files);
        item.History.Add(new TodoLogEntry { User = item.CreatedBy, Aktion = "Erstellt" });
        items.Add(item);
        await SaveAsync(items);
        return item;
    }

    public async Task<bool> UpdateAsync(TodoItem updated, List<IFormFile> newFiles)
    {
        updated.Thema         = updated.Thema         ?? "";
        updated.Verantwortlich= updated.Verantwortlich?? "";
        updated.Klasse        = updated.Klasse        ?? "";
        updated.Beschreibung  = updated.Beschreibung  ?? "";
        if (!string.IsNullOrEmpty(updated.ProjectId))
        {
            var proj = await _projects.GetByIdAsync(updated.ProjectId);
            updated.ProjectName = proj?.Name;
        }
        else updated.ProjectName = null;
        var items = await LoadAsync();
        var idx = items.FindIndex(t => t.Id == updated.Id);
        if (idx < 0) return false;
        var existing = items[idx];

        // Änderungen erkennen
        var changes = new List<string>();
        if (existing.Thema         != updated.Thema)         changes.Add($"Thema: «{updated.Thema}»");
        if (existing.Verantwortlich!= updated.Verantwortlich) changes.Add($"Verantwortlich: {updated.Verantwortlich}");
        if (existing.Klasse        != updated.Klasse)         changes.Add($"Klasse: {updated.Klasse}");
        if (existing.Gruppe        != updated.Gruppe)         changes.Add($"Gruppe: {updated.Gruppe}");
        if (existing.ErledigenBis  != updated.ErledigenBis)   changes.Add($"Fällig: {updated.ErledigenBis:dd.MM.yyyy}");
        if (existing.Erledigt      != updated.Erledigt)       changes.Add(updated.Erledigt ? "→ Erledigt" : "→ Wieder offen");

        updated.Files     = existing.Files;
        updated.History   = existing.History;
        updated.TaskNr    = existing.TaskNr;
        updated.CreatedAt = existing.CreatedAt;
        updated.CreatedBy = existing.CreatedBy;
        updated.UpdatedAt = DateTime.UtcNow;

        var aktion = changes.Any() ? "Aktualisiert: " + string.Join(", ", changes) : "Gespeichert (keine Änderungen)";
        updated.History.Add(new TodoLogEntry { User = updated.UpdatedBy, Aktion = aktion });

        Directory.CreateDirectory(GetTaskDir(updated.TaskNr));
        await AttachFilesAsync(updated, newFiles);

        if (newFiles.Any(f => f.Length > 0))
        {
            var names = newFiles.Where(f => f.Length > 0).Select(f => f.FileName).ToList();
            updated.History.Add(new TodoLogEntry
            {
                User   = updated.UpdatedBy,
                Aktion = "Datei(en) angehängt: " + string.Join(", ", names)
            });
        }

        items[idx] = updated;
        await SaveAsync(items);
        return true;
    }

    public async Task<bool> ToggleErledigtAsync(string id, string userName = "")
    {
        var items = await LoadAsync();
        var item = items.FirstOrDefault(t => t.Id == id);
        if (item == null) return false;
        item.Erledigt  = !item.Erledigt;
        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = userName;
        item.History.Add(new TodoLogEntry
        {
            User   = userName,
            Aktion = item.Erledigt ? "Als erledigt markiert" : "Erledigung zurückgenommen"
        });
        await SaveAsync(items);
        return true;
    }

    public async Task<bool> DeleteFileAsync(string todoId, string fileId)
    {
        var items = await LoadAsync();
        var todo = items.FirstOrDefault(t => t.Id == todoId);
        if (todo == null) return false;
        var file = todo.Files.FirstOrDefault(f => f.Id == fileId);
        if (file == null) return false;
        var path = GetFilePath(todo.TaskNr, file.StoredName);
        if (File.Exists(path)) File.Delete(path);
        todo.Files.Remove(file);
        await SaveAsync(items);
        return true;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var items = await LoadAsync();
        var item = items.FirstOrDefault(t => t.Id == id);
        if (item == null) return false;
        var dir = GetTaskDir(item.TaskNr);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        items.Remove(item);
        await SaveAsync(items);
        return true;
    }

    // ── Hilfsmethoden ───────────────────────────────────────────────────────

    private string GetTaskDir(int nr) => Path.Combine(_uploadDir, nr.ToString());

    public string GetFilePath(int taskNr, string storedName) =>
        Path.Combine(GetTaskDir(taskNr), storedName);

    private static string ResolveStoredName(string dir, string originalName)
    {
        var name      = Path.GetFileNameWithoutExtension(originalName);
        var ext       = Path.GetExtension(originalName);
        var candidate = originalName;
        var counter   = 2;
        while (File.Exists(Path.Combine(dir, candidate)))
        {
            candidate = $"{name}_{counter}{ext}";
            counter++;
        }
        return candidate;
    }

    private async Task AttachFilesAsync(TodoItem item, List<IFormFile> files)
    {
        var dir = GetTaskDir(item.TaskNr);
        Directory.CreateDirectory(dir);
        foreach (var file in files)
        {
            if (file.Length == 0) continue;
            var original   = Path.GetFileName(file.FileName);
            var storedName = ResolveStoredName(dir, original);
            var path       = Path.Combine(dir, storedName);
            using var stream = File.Create(path);
            await file.CopyToAsync(stream);
            item.Files.Add(new TodoFile
            {
                OriginalName = original,
                StoredName   = storedName,
                Size         = file.Length
            });
        }
    }
}
