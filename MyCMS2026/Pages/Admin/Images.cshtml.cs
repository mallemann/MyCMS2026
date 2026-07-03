using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MyCMS2026.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class ImagesModel : PageModel
{
    private readonly IWebHostEnvironment _env;

    public ImagesModel(IWebHostEnvironment env) => _env = env;

    public List<ImageInfo> Images { get; private set; } = [];
    public string?         Message { get; private set; }
    public bool            IsError { get; private set; }

    private string UploadDir =>
        Path.Combine(_env.ContentRootPath, "App_Data", "uploads", "images");

    public void OnGet() => LoadImages();

    public IActionResult OnPostDelete(string fileName)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(fileName, @"^[\w\-\.]+$"))
        {
            Message = "Ungültiger Dateiname.";
            IsError = true;
            LoadImages();
            return Page();
        }

        var filePath = Path.Combine(UploadDir, fileName);
        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
            Message = $"«{fileName}» wurde gelöscht.";
        }
        else
        {
            Message = "Datei nicht gefunden.";
            IsError = true;
        }

        LoadImages();
        return Page();
    }

    private void LoadImages()
    {
        if (!Directory.Exists(UploadDir)) { Images = []; return; }

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        // Alle HTML-Pages einlesen: Dateiname → Inhalt
        var pagesDir = Path.Combine(_env.ContentRootPath, "App_Data", "pages");
        var pageContents = Directory.Exists(pagesDir)
            ? Directory.GetFiles(pagesDir, "*.html")
                       .ToDictionary(
                           f => Path.GetFileNameWithoutExtension(f),
                           f => System.IO.File.ReadAllText(f))
            : new Dictionary<string, string>();

        Images = Directory.GetFiles(UploadDir)
            .Where(f => allowed.Contains(Path.GetExtension(f)))
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime)
            .Select(f =>
            {
                var usedIn = pageContents
                    .Where(kv => kv.Value.Contains($"/img/{f.Name}", StringComparison.OrdinalIgnoreCase))
                    .Select(kv => kv.Key)
                    .ToList();
                return new ImageInfo
                {
                    FileName     = f.Name,
                    SizeKb       = (int)(f.Length / 1024),
                    LastModified = f.LastWriteTime,
                    UsedInPages  = usedIn
                };
            })
            .ToList();
    }

    public class ImageInfo
    {
        public string        FileName     { get; set; } = "";
        public int           SizeKb       { get; set; }
        public DateTime      LastModified { get; set; }
        public List<string>  UsedInPages  { get; set; } = [];
        public bool          IsOrphaned   => !UsedInPages.Any();
    }
}
