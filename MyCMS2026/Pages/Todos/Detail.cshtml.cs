using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Todos;

[Authorize]
public class TodoDetailModel : PageModel
{
    private readonly TodoService _todos;
    public TodoDetailModel(TodoService todos) => _todos = todos;

    public TodoItem? Todo { get; set; }
    public string? ReturnPageId { get; set; }
    public string? ReturnProjectId { get; set; }
    public string? Gruppe { get; set; }

    public async Task<IActionResult> OnGetAsync(string? id, string? returnPageId, string? returnProjectId, string? gruppe)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        Todo = await _todos.GetByIdAsync(id);
        if (Todo == null) return NotFound();
        ReturnPageId = returnPageId;
        ReturnProjectId = returnProjectId;
        Gruppe = gruppe;
        return Page();
    }
}
