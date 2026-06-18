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

    private string GetFilePath() =>
        System.IO.Path.Combine(_env.ContentRootPath, "App_Data", "pages", FileName);

    public IActionResult OnGet()
    {
        if (string.IsNullOrEmpty(FileName))
            return BadRequest("Kein Dateiname angegeben.");

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

        if (string.IsNullOrEmpty(FileName))
        {
            Message = "Kein Dateiname angegeben.";
            IsError = true;
            return Page();
        }

        var dir = System.IO.Path.Combine(_env.ContentRootPath, "App_Data", "pages");
        System.IO.Directory.CreateDirectory(dir);
        var path = System.IO.Path.Combine(dir, FileName);

        System.IO.File.WriteAllText(path, content ?? "");
        Content = content ?? "";
        Message = $"Datei '{FileName}' gespeichert.";
        return Page();
    }
}
