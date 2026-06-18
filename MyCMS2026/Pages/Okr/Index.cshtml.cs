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
    public OkrIndexModel(OkrService okr) => _okr = okr;

    public List<OkrObjective> Objectives { get; set; } = new();
    public List<int> Years { get; set; } = new();
    public int? SelectedYear { get; set; }
    public bool ShowAll { get; set; }

    public async Task OnGetAsync(int? year, bool showAll = false)
    {
        SelectedYear = year;
        ShowAll      = showAll;
        Years        = await _okr.GetYearsAsync();

        var all = year.HasValue
            ? await _okr.GetByYearAsync(year.Value)
            : await _okr.GetAllAsync();

        Objectives = showAll ? all : all.Where(o => o.Status == "aktiv").ToList();
    }

    public async Task<IActionResult> OnPostAddObjectiveAsync(string text, int year)
    {
        if (!User.IsInRole("Administrator")) return Forbid();
        await _okr.CreateObjectiveAsync(new OkrObjective { Text = text, Year = year, Status = "aktiv" });
        return RedirectToPage(new { year });
    }

    public async Task<IActionResult> OnPostEditObjectiveAsync(string objectiveId, string text, string status, int year)
    {
        if (!User.IsInRole("Administrator")) return Forbid();
        await _okr.UpdateObjectiveAsync(objectiveId, text, status, year);
        return RedirectToPage(new { year });
    }

    public async Task<IActionResult> OnPostDeleteObjectiveAsync(string objectiveId)
    {
        if (!User.IsInRole("Administrator")) return Forbid();
        await _okr.DeleteObjectiveAsync(objectiveId);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddKrAsync(string objectiveId, string krText, double target, int? year)
    {
        if (!User.IsInRole("Administrator")) return Forbid();
        await _okr.AddKeyResultAsync(objectiveId, new OkrKeyResult { Text = krText, TargetValue = target });
        return RedirectToPage(new { year });
    }

    public async Task<IActionResult> OnPostUpdateProgressAsync(string objectiveId, string krId, double current, int? year)
    {
        if (!User.IsInRole("Administrator")) return Forbid();
        await _okr.UpdateProgressAsync(objectiveId, krId, current);
        return RedirectToPage(new { year });
    }

    public async Task<IActionResult> OnPostDeleteKrAsync(string objectiveId, string krId, int? year)
    {
        if (!User.IsInRole("Administrator")) return Forbid();
        await _okr.DeleteKeyResultAsync(objectiveId, krId);
        return RedirectToPage(new { year });
    }
}
