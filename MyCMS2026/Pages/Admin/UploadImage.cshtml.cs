using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MyCMS2026.Pages.Admin;

[Authorize]
[IgnoreAntiforgeryToken]   // AJAX-Upload; Zugriff via [Authorize] geschützt
public class UploadImageModel : PageModel
{
    private readonly IWebHostEnvironment _env;

    public UploadImageModel(IWebHostEnvironment env) => _env = env;

    // GET wird nicht benötigt
    public IActionResult OnGet() => NotFound();

    public async Task<IActionResult> OnPostAsync(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Keine Datei empfangen." });

        // Erlaubte Bildformate
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg" };

        var ext = Path.GetExtension(file.FileName);
        if (!allowed.Contains(ext))
            return BadRequest(new { error = $"Dateityp '{ext}' nicht erlaubt." });

        // Max 10 MB
        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { error = "Datei zu gross (max. 10 MB)." });

        var uploadDir = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadDir);

        // Eindeutiger Dateiname: Datum + GUID-Kürzel + Original-Endung
        var shortId  = Guid.NewGuid().ToString("N")[..8];
        var safeName = $"{DateTime.Now:yyyyMMdd}_{shortId}{ext}";
        var filePath = Path.Combine(uploadDir, safeName);

        await using var stream = System.IO.File.Create(filePath);
        await file.CopyToAsync(stream);

        return new JsonResult(new { url = $"/uploads/{safeName}" });
    }
}
