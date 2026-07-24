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
    private readonly WeeklyMailService _weeklyMail;
    public JournalFileModel(ProjectService projects, WeeklyMailService weeklyMail)
    {
        _projects   = projects;
        _weeklyMail = weeklyMail;
    }

    public async Task<IActionResult> OnGetAsync(string projectId, string entryId, string fileId, string? fileName = null)
    {
        var project = await _projects.GetByIdAsync(projectId);
        if (project == null) return NotFound();

        var isAdmin        = User.IsInRole("Administrator");
        var allowedGruppen = await _weeklyMail.GetAllowedGruppenAsync(User.Identity?.Name ?? "");
        if (!_projects.CanRead(project, isAdmin, allowedGruppen)) return Forbid();

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
