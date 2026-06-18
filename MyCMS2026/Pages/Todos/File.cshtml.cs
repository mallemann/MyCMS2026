using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Todos;

[Authorize]
public class TodoFileModel : PageModel
{
    private readonly TodoService _todos;
    public TodoFileModel(TodoService todos) => _todos = todos;

    public async Task<IActionResult> OnGetAsync(string id, string fileId)
    {
        var todo = await _todos.GetByIdAsync(id);
        if (todo == null) return NotFound();

        var file = todo.Files.FirstOrDefault(f => f.Id == fileId);
        if (file == null) return NotFound();

        var path = _todos.GetFilePath(todo.TaskNr, file.StoredName);
        if (!System.IO.File.Exists(path)) return NotFound();

        var ext = Path.GetExtension(file.StoredName).ToLowerInvariant();
        var mime = ext switch
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

        return PhysicalFile(path, mime, file.OriginalName);
    }
}
