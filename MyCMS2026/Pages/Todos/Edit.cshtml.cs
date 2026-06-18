using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Todos;

[Authorize]
public class TodoEditModel : PageModel
{
    private readonly TodoService _todos;
    private readonly UserService _users;
    private readonly ProjectService _projects;
    private readonly NavigationService _nav;

    public TodoEditModel(TodoService todos, UserService users, ProjectService projects, NavigationService nav)
    {
        _todos    = todos;
        _users    = users;
        _projects = projects;
        _nav      = nav;
    }

    [BindProperty] public TodoItem Todo { get; set; } = new();
    [BindProperty] public List<IFormFile> UploadedFiles { get; set; } = new();
    [BindProperty] public string? ReturnProjectId { get; set; }
    [BindProperty] public string? ReturnPageId { get; set; }
    public string? Error { get; set; }
    public List<string> Kuerzel { get; set; } = new();
    public List<Project> Projects { get; set; } = new();

    private async Task LoadKuerzelAsync()
    {
        var users = await _users.GetAllAsync();
        Kuerzel = users
            .Where(u => u.IsActive && !string.IsNullOrWhiteSpace(u.Kuerzel))
            .Select(u => u.Kuerzel)
            .OrderBy(k => k)
            .ToList();
    }

    private async Task LoadProjectsAsync()
    {
        var isAdmin   = User.IsInRole("Administrator");
        var userRoles = User.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value);
        Projects = await _projects.GetVisibleAsync(isAdmin, userRoles);
    }

    /// <summary>Returns true if the current user has ExtendedAccess on a nav page.</summary>
    private async Task<bool> HasNavExtendedAccessAsync(string? navId)
    {
        if (string.IsNullOrEmpty(navId)) return false;
        var userRoles = User.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value);
        return await _nav.HasExtendedAccessAsync(navId, userRoles);
    }

    /// <summary>Returns true if the current user has BearbeitenRolle on a specific project.</summary>
    private async Task<bool> IsProjectEditorAsync(string? projectId)
    {
        if (string.IsNullOrEmpty(projectId)) return false;
        var project = await _projects.GetByIdAsync(projectId);
        if (project == null) return false;
        var isAdmin   = User.IsInRole("Administrator");
        var userRoles = User.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value);
        return _projects.CanEdit(project, isAdmin, userRoles);
    }

    public async Task<IActionResult> OnGetAsync(string? id, string? gruppe, string? projectId, string? returnPageId)
    {
        await LoadKuerzelAsync();
        await LoadProjectsAsync();
        var isAdmin = User.IsInRole("Administrator");

        if (!string.IsNullOrEmpty(id))
        {
            var existing = await _todos.GetByIdAsync(id);
            if (existing == null) return NotFound();
            var canEdit = isAdmin
                || existing.CreatedBy == User.Identity?.Name
                || await IsProjectEditorAsync(existing.ProjectId)
                || await HasNavExtendedAccessAsync(returnPageId);
            if (!canEdit) return Forbid();
            Todo = existing;
        }
        else
        {
            // Creating new: admin, nav extended access, or project editor
            var canCreate = isAdmin
                || await IsProjectEditorAsync(projectId)
                || await HasNavExtendedAccessAsync(returnPageId);
            if (!canCreate) return Forbid();
            Todo.Anlagedatum  = DateTime.Today;
            Todo.ErledigenBis = DateTime.Today.AddDays(30);
            Todo.Gruppe       = gruppe ?? "";
            if (!string.IsNullOrEmpty(projectId))
                Todo.ProjectId = projectId;
        }
        ReturnProjectId = projectId ?? Todo.ProjectId;
        ReturnPageId    = returnPageId;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Todo.Thema))
        {
            Error = "Thema ist ein Pflichtfeld.";
            await LoadKuerzelAsync();
            await LoadProjectsAsync();
            return Page();
        }

        var userName = User.Identity?.Name ?? "";
        var isAdmin  = User.IsInRole("Administrator");
        var gruppe   = Todo.Gruppe;
        var retProj  = ReturnProjectId;   // wohin nach dem Speichern zurückkehren

        if (string.IsNullOrEmpty(Todo.Id))
        {
            // New: admin, nav extended access, or project editor
            var canCreate = isAdmin
                || await IsProjectEditorAsync(Todo.ProjectId ?? retProj)
                || await HasNavExtendedAccessAsync(ReturnPageId);
            if (!canCreate) return Forbid();
            Todo.Anlagedatum = DateTime.Today;
            Todo.CreatedBy   = userName;
            Todo.UpdatedBy   = userName;
            await _todos.CreateAsync(Todo, UploadedFiles);
        }
        else
        {
            // Edit: admin, creator, or project editor
            var existing = await _todos.GetByIdAsync(Todo.Id);
            var canEdit  = isAdmin
                || existing?.CreatedBy == userName
                || await IsProjectEditorAsync(existing?.ProjectId)
                || await HasNavExtendedAccessAsync(ReturnPageId);
            if (!canEdit) return Forbid();
            Todo.UpdatedBy = userName;
            await _todos.UpdateAsync(Todo, UploadedFiles);
        }

        if (!string.IsNullOrEmpty(ReturnPageId))
            return RedirectToPage("/Page/Index", new { id = ReturnPageId });
        if (!string.IsNullOrEmpty(retProj))
            return RedirectToPage("/Projects/Detail", new { id = retProj, tab = "todos" });
        return RedirectToPage("Index", new { gruppe = string.IsNullOrWhiteSpace(gruppe) ? null : gruppe });
    }

    public async Task<IActionResult> OnPostDeleteFileAsync(string id, string fileId)
    {
        var existing = await _todos.GetByIdAsync(id);
        var gruppe   = existing?.Gruppe;
        var retProj  = existing?.ProjectId;
        await _todos.DeleteFileAsync(id, fileId);
        return RedirectToPage(new { id, gruppe = string.IsNullOrWhiteSpace(gruppe) ? null : gruppe });
    }
}
