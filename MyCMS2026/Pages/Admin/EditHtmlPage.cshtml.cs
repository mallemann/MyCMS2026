using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MyCMS2026.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class EditHtmlPageModel : PageModel
{
    private readonly IWebHostEnvironment _env;

    public EditHtmlPageModel(IWebHostEnvironment env) => _env = env;

    [BindProperty(SupportsGet = true)] public string FileName { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string ReturnId { get; set; } = "";

    public string Content { get; set; } = "";
    public string Message { get; set; } = "";
    public bool IsError { get; set; }
    public bool FileNotFound { get; set; }

    /// <summary>
    /// Sicherheit: nur einfache Dateinamen mit Endung .html zulassen —
    /// verhindert Path Traversal (../, absolute Pfade) aus App_Data/pages heraus.
    /// </summary>
    private static bool IsValidFileName(string fileName) =>
        !string.IsNullOrEmpty(fileName)
        && fileName == System.IO.Path.GetFileName(fileName)
        && !fileName.Contains("..")
        && fileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
        && System.Text.RegularExpressions.Regex.IsMatch(fileName, @"^[\w\-\.]+$");

    private string GetFilePath() =>
        System.IO.Path.Combine(_env.ContentRootPath, "App_Data", "pages", FileName);

    public IActionResult OnGet()
    {
        if (!IsValidFileName(FileName))
            return BadRequest("Ungültiger Dateiname.");

        var path = GetFilePath();
        if (System.IO.File.Exists(path))
            Content = System.IO.File.ReadAllText(path);
        else
            FileNotFound = true;

        return Page();
    }

    public IActionResult OnPost(string fileName, string returnId, string content)
    {
        FileName = fileName;
        ReturnId = returnId;

        if (!IsValidFileName(FileName))
        {
            Message = "Ungültiger Dateiname.";
            IsError = true;
            return Page();
        }

        var dir = System.IO.Path.Combine(_env.ContentRootPath, "App_Data", "pages");
        System.IO.Directory.CreateDirectory(dir);
        var path = GetFilePath();

        System.IO.File.WriteAllText(path, content ?? "");
        Content = content ?? "";
        Message = $"Datei '{FileName}' gespeichert.";
        return Page();
    }
}
