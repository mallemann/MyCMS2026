using System.Text.Json;
using MyCMS2026.Models;

namespace MyCMS2026.Services;

public class ProjectService
{
    private readonly string _dataFile;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<Project>? _cache;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ProjectService(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _dataFile = Path.Combine(dataDir, "projects.json");
        if (!File.Exists(_dataFile))
            File.WriteAllText(_dataFile, "[]");
    }

    // ── Laden / Speichern ────────────────────────────────────────────────────

    private async Task<List<Project>> LoadAsync()
    {
        if (_cache != null) return _cache;
        await _lock.WaitAsync();
        try
        {
            if (_cache != null) return _cache;
            var json = await File.ReadAllTextAsync(_dataFile);
            var items = JsonSerializer.Deserialize<List<Project>>(json, _jsonOpts) ?? new();
            // Assign project numbers if needed
            if (items.Any(p => p.ProjectNr == 0))
            {
                var nextNr = items.Where(p => p.ProjectNr > 0).Select(p => p.ProjectNr).DefaultIfEmpty(0).Max() + 1;
                foreach (var p in items.Where(p => p.ProjectNr == 0).OrderBy(p => p.CreatedAt))
                    p.ProjectNr = nextNr++;
                var fixedJson = JsonSerializer.Serialize(items, _jsonOpts);
                await File.WriteAllTextAsync(_dataFile, fixedJson);
            }
            _cache = items;
            return _cache;
        }
        finally { _lock.Release(); }
    }

    private async Task SaveAsync(List<Project> items)
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

    // ── Access helpers ───────────────────────────────────────────────────────

    public bool CanRead(Project p, bool isAdmin, IEnumerable<string> userRoles) =>
        isAdmin || string.IsNullOrEmpty(p.LeseRolle) || userRoles.Contains(p.LeseRolle);

    public bool CanEdit(Project p, bool isAdmin, IEnumerable<string> userRoles) =>
        isAdmin || (!string.IsNullOrEmpty(p.BearbeitenRolle) && userRoles.Contains(p.BearbeitenRolle));

    // ── Project CRUD ─────────────────────────────────────────────────────────

    public async Task<List<Project>> GetAllAsync() =>
        (await LoadAsync()).OrderBy(p => p.Status == "Abgeschlossen").ThenBy(p => p.Name).ToList();

    public async Task<List<Project>> GetVisibleAsync(bool isAdmin, IEnumerable<string> userRoles)
    {
        var roles = userRoles.ToList();
        return (await LoadAsync())
            .Where(p => CanRead(p, isAdmin, roles))
            .OrderBy(p => p.Status == "Abgeschlossen").ThenBy(p => p.Name)
            .ToList();
    }

    public async Task<Project?> GetByIdAsync(string id) =>
        (await LoadAsync()).FirstOrDefault(p => p.Id == id);

    public async Task<Project> CreateAsync(Project project, string createdBy)
    {
        var items = await LoadAsync();
        project.Id        = Guid.NewGuid().ToString();
        project.ProjectNr = items.Count == 0 ? 1 : items.Max(p => p.ProjectNr) + 1;
        project.CreatedAt = DateTime.UtcNow;
        project.UpdatedAt = DateTime.UtcNow;
        project.CreatedBy = createdBy;
        project.UpdatedBy = createdBy;
        project.Journal   = new List<JournalEntry>();
        items.Add(project);
        await SaveAsync(items);
        return project;
    }

    public async Task<bool> UpdateAsync(Project updated, string updatedBy)
    {
        var items = await LoadAsync();
        var idx = items.FindIndex(p => p.Id == updated.Id);
        if (idx < 0) return false;
        var existing = items[idx];
        // Preserve immutable fields
        updated.ProjectNr = existing.ProjectNr;
        updated.CreatedAt = existing.CreatedAt;
        updated.CreatedBy = existing.CreatedBy;
        updated.Journal   = existing.Journal;
        updated.UpdatedAt = DateTime.UtcNow;
        updated.UpdatedBy = updatedBy;
        items[idx] = updated;
        await SaveAsync(items);
        return true;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var items = await LoadAsync();
        var item = items.FirstOrDefault(p => p.Id == id);
        if (item == null) return false;
        items.Remove(item);
        await SaveAsync(items);
        return true;
    }

    // ── Journal CRUD ─────────────────────────────────────────────────────────

    public async Task<JournalEntry?> AddJournalEntryAsync(string projectId, string titel, string content, string createdBy)
    {
        var items = await LoadAsync();
        var project = items.FirstOrDefault(p => p.Id == projectId);
        if (project == null) return null;

        var entry = new JournalEntry
        {
            Id        = Guid.NewGuid().ToString(),
            Titel     = titel,
            Content   = content,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
            Comments  = new List<JournalComment>()
        };
        project.Journal.Insert(0, entry);   // newest first
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = createdBy;
        await SaveAsync(items);
        return entry;
    }

    public async Task<bool> UpdateJournalEntryAsync(string projectId, string entryId, string titel, string content, string updatedBy)
    {
        var items = await LoadAsync();
        var project = items.FirstOrDefault(p => p.Id == projectId);
        if (project == null) return false;
        var entry = project.Journal.FirstOrDefault(e => e.Id == entryId);
        if (entry == null) return false;
        entry.Titel     = titel;
        entry.Content   = content;
        entry.UpdatedAt = DateTime.UtcNow;
        entry.UpdatedBy = updatedBy;
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = updatedBy;
        await SaveAsync(items);
        return true;
    }

    public async Task<bool> DeleteJournalEntryAsync(string projectId, string entryId)
    {
        var items = await LoadAsync();
        var project = items.FirstOrDefault(p => p.Id == projectId);
        if (project == null) return false;
        var entry = project.Journal.FirstOrDefault(e => e.Id == entryId);
        if (entry == null) return false;
        project.Journal.Remove(entry);
        project.UpdatedAt = DateTime.UtcNow;
        await SaveAsync(items);
        return true;
    }

    // ── Comment CRUD ─────────────────────────────────────────────────────────

    public async Task<JournalComment?> AddCommentAsync(string projectId, string entryId, string text, string createdBy)
    {
        var items = await LoadAsync();
        var project = items.FirstOrDefault(p => p.Id == projectId);
        if (project == null) return null;
        var entry = project.Journal.FirstOrDefault(e => e.Id == entryId);
        if (entry == null) return null;

        var comment = new JournalComment
        {
            Id        = Guid.NewGuid().ToString(),
            Text      = text,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };
        entry.Comments.Add(comment);
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = createdBy;
        await SaveAsync(items);
        return comment;
    }

    public async Task<bool> DeleteCommentAsync(string projectId, string entryId, string commentId)
    {
        var items = await LoadAsync();
        var project = items.FirstOrDefault(p => p.Id == projectId);
        if (project == null) return false;
        var entry = project.Journal.FirstOrDefault(e => e.Id == entryId);
        if (entry == null) return false;
        var comment = entry.Comments.FirstOrDefault(c => c.Id == commentId);
        if (comment == null) return false;
        entry.Comments.Remove(comment);
        project.UpdatedAt = DateTime.UtcNow;
        await SaveAsync(items);
        return true;
    }
}
