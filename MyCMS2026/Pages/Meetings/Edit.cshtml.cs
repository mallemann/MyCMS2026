using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Meetings;

[Authorize]
public class MeetingEditModel : PageModel
{
    private readonly MeetingService _meetings;
    private readonly UserService _users;
    private readonly ProjectService _projects;
    private readonly NavigationService _nav;

    public MeetingEditModel(MeetingService meetings, UserService users, ProjectService projects, NavigationService nav)
    {
        _meetings = meetings;
        _users    = users;
        _projects = projects;
        _nav      = nav;
    }

    [BindProperty] public Meeting Meeting { get; set; } = new();
    [BindProperty] public List<IFormFile> UploadedFiles { get; set; } = new();
    [BindProperty] public string? ReturnProjectId { get; set; }
    [BindProperty] public string? ReturnPageId { get; set; }
    public string? Error { get; set; }
    public List<string> Kuerzel { get; set; } = new();
    public List<Project> Projects { get; set; } = new();
    /// <summary>Set when creating from a project — Gruppe is locked to the project's Gruppe.</summary>
    public string? ProjectGruppe { get; set; }

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
        var isAdmin = User.IsInRole("Administrator");
        await LoadKuerzelAsync();
        await LoadProjectsAsync();

        if (!string.IsNullOrEmpty(id))
        {
            var existing = await _meetings.GetByIdAsync(id);
            if (existing == null) return NotFound();
            var canEdit = isAdmin
                || await IsProjectEditorAsync(existing.ProjectId)
                || await HasNavExtendedAccessAsync(returnPageId);
            if (!canEdit) return Forbid();
            Meeting = existing;
            // Lock Gruppe if this Meeting belongs to a project
            if (!string.IsNullOrEmpty(existing.ProjectId))
            {
                var proj = await _projects.GetByIdAsync(existing.ProjectId);
                if (proj != null && !string.IsNullOrEmpty(proj.Gruppe))
                {
                    ProjectGruppe  = proj.Gruppe;
                    Meeting.Gruppe = proj.Gruppe;  // keep in sync with project
                }
            }
        }
        else
        {
            var canCreate = isAdmin
                || await IsProjectEditorAsync(projectId)
                || await HasNavExtendedAccessAsync(returnPageId);
            if (!canCreate) return Forbid();
            if (!string.IsNullOrEmpty(projectId))
            {
                Meeting.ProjectId = projectId;
                var proj = await _projects.GetByIdAsync(projectId);
                if (proj != null && !string.IsNullOrEmpty(proj.Gruppe))
                {
                    Meeting.Gruppe = proj.Gruppe;
                    ProjectGruppe  = proj.Gruppe;
                }
            }
            else
            {
                Meeting.Gruppe = gruppe ?? "";
            }
        }
        ReturnProjectId = projectId ?? Meeting.ProjectId;
        ReturnPageId    = returnPageId;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Meeting.Thema))
        {
            Error = "Thema ist ein Pflichtfeld.";
            await LoadKuerzelAsync();
            await LoadProjectsAsync();
            return Page();
        }

        var userName  = User.Identity?.Name ?? "";
        var isAdmin   = User.IsInRole("Administrator");
        var gruppe    = Meeting.Gruppe;
        var retProj   = ReturnProjectId;
        var isNewItem = string.IsNullOrEmpty(Meeting.Id);   // vor CreateAsync merken

        if (string.IsNullOrEmpty(Meeting.Id))
        {
            var canCreate = isAdmin
                || await IsProjectEditorAsync(Meeting.ProjectId ?? retProj)
                || await HasNavExtendedAccessAsync(ReturnPageId);
            if (!canCreate) return Forbid();
            Meeting.CreatedBy = userName;
            Meeting.UpdatedBy = userName;
            await _meetings.CreateAsync(Meeting, UploadedFiles);
        }
        else
        {
            var existing = await _meetings.GetByIdAsync(Meeting.Id);
            var canEdit  = isAdmin
                || await IsProjectEditorAsync(existing?.ProjectId)
                || await HasNavExtendedAccessAsync(ReturnPageId);
            if (!canEdit) return Forbid();
            Meeting.UpdatedBy = userName;
            await _meetings.UpdateAsync(Meeting, UploadedFiles);
        }

        if (!string.IsNullOrEmpty(ReturnPageId))
            return RedirectToPage("/Page/Index", new { id = ReturnPageId });
        if (!string.IsNullOrEmpty(retProj))
        {
            if (isNewItem)
                return RedirectToPage("/Projects/Detail",
                    new { id = retProj, tab = "meetings", promptJournal = Meeting.Id, promptType = "meeting" });
            return RedirectToPage("/Projects/Detail", new { id = retProj, tab = "meetings" });
        }
        return RedirectToPage("Index", new { gruppe = string.IsNullOrWhiteSpace(gruppe) ? null : gruppe });
    }

    public async Task<IActionResult> OnPostDeleteFileAsync(string id, string fileId)
    {
        var existing = await _meetings.GetByIdAsync(id);
        if (existing == null) return NotFound();
        var isAdmin = User.IsInRole("Administrator");
        if (!isAdmin && !await IsProjectEditorAsync(existing.ProjectId)) return Forbid();
        var gruppe = existing.Gruppe;
        await _meetings.DeleteFileAsync(id, fileId);
        return RedirectToPage(new { id, gruppe = string.IsNullOrWhiteSpace(gruppe) ? null : gruppe });
    }
}
