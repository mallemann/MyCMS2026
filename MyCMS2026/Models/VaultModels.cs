namespace MyCMS2026.Models;

public class VaultFile
{
    public string FileName    { get; set; } = "";
    public string Description { get; set; } = "";
    public long   SizeBytes   { get; set; }
    public DateTime UploadedAt { get; set; }
    public string SubFolder   { get; set; } = "";

    public string SizeText => SizeBytes > 1024 * 1024
        ? $"{SizeBytes / 1024.0 / 1024.0:F1} MB"
        : $"{SizeBytes / 1024.0:F0} KB";
}

public class VaultFolder
{
    public string FolderName { get; set; } = "";
    public List<VaultFile> Files { get; set; } = new();
    public bool IsRoot => string.IsNullOrEmpty(FolderName);
}
