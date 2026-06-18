using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Projects;

[Authorize]
public class ProjectIndexModel : PageModel
{
    private readonly ProjectService _projects;

    public ProjectIndexModel(ProjectService projects) => _projects = projects;

    public List<Project> Projects { get; private set; } = new();
    public bool IsAdmin { get; private set; }

    public async Task OnGetAsync()
    {
        IsAdmin = User.IsInRole("Administrator");
        var userRoles = User.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value);
        Projects = await _projects.GetVisibleAsync(IsAdmin, userRoles);
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        if (!User.IsInRole("Administrator")) return Forbid();
        await _projects.DeleteAsync(id);
        return RedirectToPage();
    }
}
