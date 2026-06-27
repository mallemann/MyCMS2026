using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Meetings;

[Authorize]
public class MeetingFileModel : PageModel
{
    private readonly MeetingService _meetings;
    public MeetingFileModel(MeetingService meetings) => _meetings = meetings;

    public async Task<IActionResult> OnGetAsync(string id, string fileId, string? fileName = null)
    {
        var meeting = await _meetings.GetByIdAsync(id);
        if (meeting == null) return NotFound();

        var file = meeting.Files.FirstOrDefault(f => f.Id == fileId);
        if (file == null) return NotFound();

        var path = _meetings.GetFilePath(meeting.MeetingNr, file.StoredName);
        if (!System.IO.File.Exists(path)) return NotFound();

        var ext = Path.GetExtension(file.StoredName).ToLowerInvariant();
        FileHelper.SetContentDisposition(Response, file.OriginalName, ext);
        return PhysicalFile(path, _meetings.GetMimeType(file.StoredName));
    }
}
