using System.Text.Json;
using MyCMS2026.Models;

namespace MyCMS2026.Services;

public class MeetingService
{
    private readonly string _dataFile;
    private readonly string _uploadDir;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<Meeting>? _cache;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public MeetingService(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _dataFile = Path.Combine(dataDir, "meetings.json");
        _uploadDir = Path.Combine(dataDir, "uploads", "meetings");
        Directory.CreateDirectory(_uploadDir);
        if (!File.Exists(_dataFile))
            File.WriteAllText(_dataFile, "[]");
    }

    // ── Laden / Speichern ────────────────────────────────────────────────────

    private async Task<List<Meeting>> LoadAsync()
    {
        if (_cache != null) return _cache;
        await _lock.WaitAsync();
        try
        {
            if (_cache != null) return _cache;
            var json = await File.ReadAllTextAsync(_dataFile);
            var items = JsonSerializer.Deserialize<List<Meeting>>(json, _jsonOpts) ?? new();
            await AssignNrsIfNeededAsync(items);
            _cache = items;
            return _cache;
        }
        finally { _lock.Release(); }
    }

    private async Task SaveAsync(List<Meeting> items)
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

    private async Task AssignNrsIfNeededAsync(List<Meeting> items)
    {
        if (!items.Any(m => m.MeetingNr == 0)) return;
        var nextNr = items.Where(m => m.MeetingNr > 0).Select(m => m.MeetingNr).DefaultIfEmpty(0).Max() + 1;
        foreach (var m in items.Where(m => m.MeetingNr == 0).OrderBy(m => m.CreatedAt))
            m.MeetingNr = nextNr++;
        var json = JsonSerializer.Serialize(items, _jsonOpts);
        await File.WriteAllTextAsync(_dataFile, json);
    }

    // ── CRUD ────────────────────────────────────────────────────────────────

    public async Task<List<Meeting>> GetAllAsync() =>
        (await LoadAsync()).OrderByDescending(m => m.Datum).ToList();

    public async Task<Meeting?> GetByIdAsync(string id) =>
        (await LoadAsync()).FirstOrDefault(m => m.Id == id);

    public async Task<Meeting> CreateAsync(Meeting meeting, List<IFormFile> files)
    {
        Normalize(meeting);
        var items = await LoadAsync();
        meeting.Id        = Guid.NewGuid().ToString();
        meeting.MeetingNr = items.Count == 0 ? 1 : items.Max(m => m.MeetingNr) + 1;
        meeting.CreatedAt = DateTime.UtcNow;
        meeting.UpdatedAt = DateTime.UtcNow;
        Directory.CreateDirectory(GetMeetingDir(meeting.MeetingNr));
        await AttachFilesAsync(meeting, files);
        items.Add(meeting);
        await SaveAsync(items);
        return meeting;
    }

    public async Task<bool> UpdateAsync(Meeting updated, List<IFormFile> newFiles)
    {
        Normalize(updated);
        var items = await LoadAsync();
        var idx = items.FindIndex(m => m.Id == updated.Id);
        if (idx < 0) return false;
        var existing = items[idx];
        updated.Files     = existing.Files;
        updated.MeetingNr = existing.MeetingNr;
        updated.CreatedAt = existing.CreatedAt;
        updated.CreatedBy = existing.CreatedBy;
        updated.UpdatedAt = DateTime.UtcNow;
        Directory.CreateDirectory(GetMeetingDir(updated.MeetingNr));
        await AttachFilesAsync(updated, newFiles);
        items[idx] = updated;
        await SaveAsync(items);
        return true;
    }

    public async Task<bool> DeleteFileAsync(string meetingId, string fileId)
    {
        var items = await LoadAsync();
        var meeting = items.FirstOrDefault(m => m.Id == meetingId);
        if (meeting == null) return false;
        var file = meeting.Files.FirstOrDefault(f => f.Id == fileId);
        if (file == null) return false;
        var path = GetFilePath(meeting.MeetingNr, file.StoredName);
        if (File.Exists(path)) File.Delete(path);
        meeting.Files.Remove(file);
        await SaveAsync(items);
        return true;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var items = await LoadAsync();
        var meeting = items.FirstOrDefault(m => m.Id == id);
        if (meeting == null) return false;
        var dir = GetMeetingDir(meeting.MeetingNr);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        items.Remove(meeting);
        await SaveAsync(items);
        return true;
    }

    // ── Hilfsmethoden ───────────────────────────────────────────────────────

    private static void Normalize(Meeting m)
    {
        m.Thema        = m.Thema        ?? "";
        m.Leitung      = m.Leitung      ?? "";
        m.Beschreibung = m.Beschreibung ?? "";
        m.Content      = m.Content      ?? "";
        m.ContentType  = m.ContentType  ?? "Text";
        m.Status       = m.Status       ?? "Geplant";
        m.Klasse       = m.Klasse       ?? "";
    }

    private string GetMeetingDir(int nr) => Path.Combine(_uploadDir, nr.ToString());

    public string GetFilePath(int meetingNr, string storedName) =>
        Path.Combine(GetMeetingDir(meetingNr), storedName);

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

    private async Task AttachFilesAsync(Meeting meeting, List<IFormFile> files)
    {
        var dir = GetMeetingDir(meeting.MeetingNr);
        Directory.CreateDirectory(dir);
        foreach (var file in files)
        {
            if (file.Length == 0) continue;
            var original   = Path.GetFileName(file.FileName);
            var storedName = ResolveStoredName(dir, original);
            var path       = Path.Combine(dir, storedName);
            using var stream = File.Create(path);
            await file.CopyToAsync(stream);
            meeting.Files.Add(new MeetingFile
            {
                OriginalName = original,
                StoredName   = storedName,
                Size         = file.Length
            });
        }
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
