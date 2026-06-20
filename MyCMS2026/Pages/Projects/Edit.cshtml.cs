using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Projects;

[Authorize]
public class ProjectEditModel : PageModel
{
    private readonly ProjectService _projects;
    private readonly UserService _users;
    private readonly RoleService _roles;
    private readonly GruppenService _gruppen;

    public ProjectEditModel(ProjectService projects, UserService users, RoleService roles, GruppenService gruppen)
    {
        _projects = projects;
        _users    = users;
        _roles    = roles;
        _gruppen  = gruppen;
    }

    [BindProperty] public Project Input { get; set; } = new();
    public bool IsNew => string.IsNullOrEmpty(Input.Id);
    public bool IsAdmin { get; private set; }
    public List<string> UserNames { get; private set; } = new();
    public List<string> RoleNames { get; private set; } = new();
    public List<string> AvailableGruppen { get; private set; } = new();
    public string? Error { get; set; }

    private List<string> UserRoles => User.Claims
        .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
        .Select(c => c.Value).ToList();

    private async Task LoadUsersAsync()
    {
        var users = await _users.GetAllAsync();
        UserNames = users
            .Where(u => u.IsActive)
            .Select(u => u.UserName)
            .OrderBy(n => n)
            .ToList();
        var allRoles = await _roles.GetAllAsync();
        RoleNames = allRoles.Select(r => r.Name).OrderBy(n => n).ToList();
        AvailableGruppen = await _gruppen.GetAllAsync();
    }

    public async Task<IActionResult> OnGetAsync(string? id)
    {
        IsAdmin = User.IsInRole("Administrator");
        var roles = UserRoles;
        await LoadUsersAsync();

        if (string.IsNullOrEmpty(id))
        {
            // Neues Projekt: nur Admin
            if (!IsAdmin) return Forbid();
        }
        else
        {
            var existing = await _projects.GetByIdAsync(id);
            if (existing == null) return NotFound();
            if (!_projects.CanEdit(existing, IsAdmin, roles)) return Forbid();
            Input = existing;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        IsAdmin = User.IsInRole("Administrator");
        var roles = UserRoles;

        if (string.IsNullOrWhiteSpace(Input.Name))
        {
            Error = "Projektname ist ein Pflichtfeld.";
            await LoadUsersAsync();
            return Page();
        }

        var userName = User.Identity?.Name ?? "";

        if (string.IsNullOrEmpty(Input.Id))
        {
            // Neues Projekt: nur Admin
            if (!IsAdmin) return Forbid();
            var created = await _projects.CreateAsync(Input, userName);
            return RedirectToPage("Detail", new { id = created.Id });
        }
        else
        {
            var existing = await _projects.GetByIdAsync(Input.Id);
            if (existing == null) return NotFound();
            if (!_projects.CanEdit(existing, IsAdmin, roles)) return Forbid();
            await _projects.UpdateAsync(Input, userName);
            return RedirectToPage("Detail", new { id = Input.Id });
        }
    }
}
