using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Pages;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Projects;

[Authorize]
public class JournalFileModel : PageModel
{
    private readonly ProjectService _projects;
    public JournalFileModel(ProjectService projects) => _projects = projects;

    public async Task<IActionResult> OnGetAsync(string projectId, string entryId, string fileId, string? fileName = null)
    {
        var project = await _projects.GetByIdAsync(projectId);
        if (project == null) return NotFound();

        var isAdmin   = User.IsInRole("Administrator");
        var userRoles = User.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value).ToList();
        if (!_projects.CanRead(project, isAdmin, userRoles)) return Forbid();

        var entry = project.Journal.FirstOrDefault(e => e.Id == entryId);
        if (entry == null) return NotFound();

        var file = entry.Files.FirstOrDefault(f => f.Id == fileId);
        if (file == null) return NotFound();

        var path = _projects.GetJournalFilePath(project.ProjectNr, entry.JournalNr, file.StoredName);
        if (!System.IO.File.Exists(path)) return NotFound();

        var ext = Path.GetExtension(file.StoredName).ToLowerInvariant();
        FileHelper.SetContentDisposition(Response, file.OriginalName, ext);
        return PhysicalFile(path, _projects.GetMimeType(file.StoredName));
    }
}
