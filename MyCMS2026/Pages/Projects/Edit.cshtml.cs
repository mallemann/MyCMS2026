using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Projects;

[Authorize(Roles = "Administrator")]
public class ProjectEditModel : PageModel
{
    private readonly ProjectService _projects;
    private readonly UserService _users;

    public ProjectEditModel(ProjectService projects, UserService users)
    {
        _projects = projects;
        _users    = users;
    }

    [BindProperty] public Project Input { get; set; } = new();
    public bool IsNew => string.IsNullOrEmpty(Input.Id);
    public List<string> UserNames { get; private set; } = new();
    public string? Error { get; set; }

    private async Task LoadUsersAsync()
    {
        var users = await _users.GetAllAsync();
        UserNames = users
            .Where(u => u.IsActive)
            .Select(u => u.UserName)
            .OrderBy(n => n)
            .ToList();
    }

    public async Task<IActionResult> OnGetAsync(string? id)
    {
        await LoadUsersAsync();
        if (!string.IsNullOrEmpty(id))
        {
            var existing = await _projects.GetByIdAsync(id);
            if (existing == null) return NotFound();
            Input = existing;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Input.Name))
        {
            Error = "Projektname ist ein Pflichtfeld.";
            await LoadUsersAsync();
            return Page();
        }

        var userName = User.Identity?.Name ?? "";

        if (string.IsNullOrEmpty(Input.Id))
        {
            var created = await _projects.CreateAsync(Input, userName);
            return RedirectToPage("Detail", new { id = created.Id });
        }
        else
        {
            await _projects.UpdateAsync(Input, userName);
            return RedirectToPage("Detail", new { id = Input.Id });
        }
    }
}
