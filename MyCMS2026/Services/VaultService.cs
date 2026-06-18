using MyCMS2026.Models;

namespace MyCMS2026.Services;

public class VaultService
{
    private readonly string _vaultRoot;

    public VaultService(IWebHostEnvironment env)
    {
        _vaultRoot = Path.Combine(env.ContentRootPath, "App_Data", "vault");
        Directory.CreateDirectory(_vaultRoot);
    }

    // ── Ordnerstruktur ────────────────────────────────────────────────────

    public List<VaultFolder> GetFolders(string gruppe)
    {
        var result  = new List<VaultFolder>();
        var gruppeDir = GetGruppeDir(gruppe);
        if (!Directory.Exists(gruppeDir)) return result;

        result.Add(new VaultFolder
        {
            FolderName = "",
            Files = GetFilesInDir(gruppeDir, "")
        });

        foreach (var sub in Directory.GetDirectories(gruppeDir).OrderBy(d => d))
        {
            var name = Path.GetFileName(sub)!;
            result.Add(new VaultFolder
            {
                FolderName = name,
                Files = GetFilesInDir(sub, name)
            });
        }
        return result;
    }

    public List<VaultFile> GetRecentFiles(string gruppe, int take = 10)
        => GetFolders(gruppe)
            .SelectMany(f => f.Files)
            .OrderByDescending(f => f.UploadedAt)
            .Take(take)
            .ToList();

    public bool CreateFolder(string gruppe, string folderName)
    {
        var safe = Sanitize(folderName);
        if (string.IsNullOrWhiteSpace(safe)) return false;
        var path = Path.Combine(GetGruppeDir(gruppe), safe);
        if (Directory.Exists(path)) return false;
        Directory.CreateDirectory(path);
        return true;
    }

    public bool DeleteFolder(string gruppe, string folderName)
    {
        var safe = Sanitize(folderName);
        if (string.IsNullOrWhiteSpace(safe)) return false;
        var path = Path.Combine(GetGruppeDir(gruppe), safe);
        if (!Directory.Exists(path)) return false;
        Directory.Delete(path, recursive: true);
        return true;
    }

    // ── Dateien ──────────────────────────────────────────────────────────

    public async Task<bool> UploadAsync(string gruppe, IFormFile file, string description, string uploadedBy, string subfolder = "")
    {
        var dir = GetDir(gruppe, subfolder);
        Directory.CreateDirectory(dir);

        var safeName = Path.GetFileName(file.FileName);
        var filePath = Path.Combine(dir, safeName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        await File.WriteAllTextAsync(filePath + ".desc", description ?? "");
        return true;
    }

    public bool DeleteFile(string gruppe, string fileName, string subfolder = "")
    {
        var path = GetFilePath(gruppe, fileName, subfolder);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        var desc = path + ".desc";
        if (File.Exists(desc)) File.Delete(desc);
        return true;
    }

    public bool RenameFile(string gruppe, string oldName, string newName, string subfolder = "")
    {
        var dir     = GetDir(gruppe, subfolder);
        var oldPath = Path.Combine(dir, Path.GetFileName(oldName));
        if (!File.Exists(oldPath)) return false;

        var ext      = Path.GetExtension(oldName);
        var newExt   = Path.GetExtension(newName.Trim());
        var newFinal = string.IsNullOrEmpty(newExt)
            ? Path.GetFileNameWithoutExtension(newName.Trim()) + ext
            : Path.GetFileName(newName.Trim());
        newFinal = Path.GetFileName(newFinal);

        var newPath = Path.Combine(dir, newFinal);
        if (File.Exists(newPath)) return false;

        File.Move(oldPath, newPath);
        var oldDesc = oldPath + ".desc";
        if (File.Exists(oldDesc)) File.Move(oldDesc, newPath + ".desc");
        return true;
    }

    public void UpdateDescription(string gruppe, string fileName, string description, string subfolder = "")
    {
        var path = GetFilePath(gruppe, fileName, subfolder);
        File.WriteAllText(path + ".desc", description ?? "");
    }

    public string GetFilePath(string gruppe, string fileName, string subfolder = "")
        => Path.Combine(GetDir(gruppe, subfolder), Path.GetFileName(fileName));

    public string GetMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf"  => "application/pdf",
            ".doc" or ".docx" => "application/msword",
            ".xls" or ".xlsx" => "application/vnd.ms-excel",
            ".ppt" or ".pptx" => "application/vnd.ms-powerpoint",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png"  => "image/png",
            ".gif"  => "image/gif",
            ".txt"  => "text/plain",
            ".zip"  => "application/zip",
            _ => "application/octet-stream"
        };
    }

    // ── Intern ────────────────────────────────────────────────────────────

    private string GetGruppeDir(string gruppe)
    {
        var safe = Sanitize(gruppe);
        if (string.IsNullOrWhiteSpace(safe)) safe = "_default";
        return Path.Combine(_vaultRoot, safe);
    }

    private string GetDir(string gruppe, string subfolder)
    {
        var base_ = GetGruppeDir(gruppe);
        return string.IsNullOrWhiteSpace(subfolder)
            ? base_
            : Path.Combine(base_, Sanitize(subfolder));
    }

    private static List<VaultFile> GetFilesInDir(string dir, string subfolder)
        => Directory.GetFiles(dir)
            .Where(f => !f.EndsWith(".desc"))
            .Select(f =>
            {
                var fi   = new FileInfo(f);
                var desc = f + ".desc";
                return new VaultFile
                {
                    FileName    = fi.Name,
                    Description = File.Exists(desc) ? File.ReadAllText(desc) : "",
                    SizeBytes   = fi.Length,
                    UploadedAt  = fi.LastWriteTime,
                    SubFolder   = subfolder
                };
            })
            .OrderBy(f => f.FileName)
            .ToList();

    private static string Sanitize(string name)
        => string.Concat((name ?? "").Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ' ')).Trim();
}
