using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Meetings;

[Authorize]
public class MeetingsIndexModel : PageModel
{
    private readonly MeetingService _meetings;
    private readonly ProjectService _projects;
    public MeetingsIndexModel(MeetingService meetings, ProjectService projects)
    {
        _meetings = meetings;
        _projects = projects;
    }

    public List<Meeting> Meetings { get; set; } = new();
    public string? Search { get; set; }
    public string? StatusFilter { get; set; }
    public string? KlasseFilter { get; set; }
    public string? Gruppe { get; set; }

    public async Task OnGetAsync(string? search, string? status, string? klasse, string? gruppe)
    {
        Search       = search;
        StatusFilter = status;
        KlasseFilter = klasse;
        Gruppe       = gruppe;

        var all = await _meetings.GetAllAsync();

        // Gruppen-Filter
        if (!string.IsNullOrWhiteSpace(gruppe))
            all = all.Where(m => m.Gruppe == gruppe).ToList();

        if (!string.IsNullOrWhiteSpace(search))
            all = all.Where(m =>
                m.Thema.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                m.Beschreibung.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                m.Leitung.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(status))
            all = all.Where(m => m.Status == status).ToList();

        if (!string.IsNullOrWhiteSpace(klasse))
            all = all.Where(m => m.Klasse == klasse).ToList();

        Meetings = all;
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id, string? gruppe)
    {
        if (!User.IsInRole("Administrator")) return Forbid();
        // Verknüpfte Journal-Einträge in allen Projekten entfernen
        await _projects.RemoveJournalEntriesByLinkedItemAsync(id);
        await _meetings.DeleteAsync(id);
        return RedirectToPage(new { gruppe });
    }
}
