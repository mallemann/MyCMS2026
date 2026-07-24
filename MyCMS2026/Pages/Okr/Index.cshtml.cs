using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Okr;

[Authorize]
public class OkrIndexModel : PageModel
{
    private readonly OkrService _okr;
    private readonly WeeklyMailService _weeklyMail;
    public OkrIndexModel(OkrService okr, WeeklyMailService weeklyMail)
    {
        _okr        = okr;
        _weeklyMail = weeklyMail;
    }

    public List<OkrObjective> Objectives { get; set; } = new();
    public List<int> Years { get; set; } = new();
    public int? SelectedYear { get; set; }
    public bool ShowAll { get; set; }
    public List<string> AllowedGruppen { get; set; } = new();

    public async Task OnGetAsync(int? year, bool showAll = false)
    {
        SelectedYear = year;
        ShowAll      = showAll;
        Years        = await _okr.GetYearsAsync();

        var all = year.HasValue
            ? await _okr.GetByYearAsync(year.Value)
            : await _okr.GetAllAsync();

        // Sichtbarkeit nach Reporting-Gruppen (analog Dashboard/Timeline/ToDos/Meetings)
        var userName  = User.Identity?.Name ?? "";
        var isAdmin   = User.IsInRole("Administrator");
        var config    = await _weeklyMail.GetConfigAsync();
        var recipient = config.Recipients.FirstOrDefault(r =>
            string.Equals(r.UserId, userName, StringComparison.OrdinalIgnoreCase));
        AllowedGruppen = recipient?.AllowedGruppen ?? new List<string>();
        var filterByGroup = AllowedGruppen.Any();

        if (isAdmin && !filterByGroup)
        {
            // Admin ohne Gruppen-Zuordnung → alles
        }
        else if (filterByGroup)
        {
            all = all.Where(o =>
                string.IsNullOrEmpty(o.Gruppe) ||
                AllowedGruppen.Contains(o.Gruppe, StringComparer.OrdinalIgnoreCase)).ToList();
        }
        else
        {
            all = new List<OkrObjective>();
        }

        Objectives = showAll ? all : all.Where(o => o.Status == "aktiv").ToList();
    }

    // Hilfsmethode: zurück zum Widget oder zur OKR-Seite
    // Wichtig: RedirectToPage statt Redirect($"/...") — respektiert PathBase!
    private IActionResult RedirectBack(string? returnPageId, int? year)
    {
        if (!string.IsNullOrEmpty(returnPageId))
            return RedirectToPage("/Page/Index", new { Id = returnPageId, okrYear = year });
        return RedirectToPage(new { year });
    }

    public async Task<IActionResult> OnPostAddObjectiveAsync(string text, int year, string? gruppe, string? returnPageId)
    {
        if (!User.IsInRole("Administrator")) return Forbid();
        // Gruppe ist Pflicht (scoped Widget liefert sie, unscoped muss gewählt werden)
        if (string.IsNullOrWhiteSpace(gruppe)) return RedirectBack(returnPageId, year);
        await _okr.CreateObjectiveAsync(new OkrObjective { Text = text, Year = year, Status = "aktiv", Gruppe = gruppe });
        return RedirectBack(returnPageId, year);
    }

    public async Task<IActionResult> OnPostEditObjectiveAsync(string objectiveId, string text, string status, int year, string? gruppe, string? returnPageId, int? okrYear)
    {
        if (!User.IsInRole("Administrator")) return Forbid();
        await _okr.UpdateObjectiveAsync(objectiveId, text, status, year, gruppe ?? "");
        return RedirectBack(returnPageId, okrYear ?? year);
    }

    public async Task<IActionResult> OnPostDeleteObjectiveAsync(string objectiveId, string? returnPageId, int? okrYear)
    {
        if (!User.IsInRole("Administrator")) return Forbid();
        await _okr.DeleteObjectiveAsync(objectiveId);
        return RedirectBack(returnPageId, okrYear);
    }

    public async Task<IActionResult> OnPostAddKrAsync(string objectiveId, string krText, double target, int? year, string? returnPageId, int? okrYear)
    {
        if (!User.IsInRole("Administrator")) return Forbid();
        await _okr.AddKeyResultAsync(objectiveId, new OkrKeyResult { Text = krText, TargetValue = target });
        return RedirectBack(returnPageId, okrYear ?? year);
    }

    public async Task<IActionResult> OnPostUpdateProgressAsync(string objectiveId, string krId, double current, int? year, string? returnPageId, int? okrYear)
    {
        if (!User.IsInRole("Administrator")) return Forbid();
        await _okr.UpdateProgressAsync(objectiveId, krId, current);
        return RedirectBack(returnPageId, okrYear ?? year);
    }

    public async Task<IActionResult> OnPostDeleteKrAsync(string objectiveId, string krId, int? year, string? returnPageId, int? okrYear)
    {
        if (!User.IsInRole("Administrator")) return Forbid();
        await _okr.DeleteKeyResultAsync(objectiveId, krId);
        return RedirectBack(returnPageId, okrYear ?? year);
    }
}
