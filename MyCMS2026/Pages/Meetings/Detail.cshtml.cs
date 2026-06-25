using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Meetings;

[Authorize]
public class MeetingDetailModel : PageModel
{
    private readonly MeetingService _meetings;
    public MeetingDetailModel(MeetingService meetings) => _meetings = meetings;

    public Meeting? Meeting { get; set; }
    public string? ReturnPageId { get; set; }
    public string? ReturnProjectId { get; set; }
    public string? Gruppe { get; set; }

    public async Task<IActionResult> OnGetAsync(string? id, string? returnPageId, string? returnProjectId, string? gruppe)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        Meeting = await _meetings.GetByIdAsync(id);
        if (Meeting == null) return NotFound();
        ReturnPageId = returnPageId;
        ReturnProjectId = returnProjectId;
        Gruppe = gruppe;
        return Page();
    }
}
