using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Downloads;

[Authorize]
public class DownloadsIndexModel : PageModel
{
    private readonly DownloadService _downloads;
    private readonly NavigationService _nav;

    public DownloadsIndexModel(DownloadService downloads, NavigationService nav)
    {
        _downloads = downloads;
        _nav = nav;
    }

    public List<Download> Downloads { get; set; } = new();
    public string? Search { get; set; }
    public string? KlasseFilter { get; set; }
    public string? Gruppe { get; set; }
    public string? UploadMessage => TempData["UploadMessage"] as string;

    // Gibt true zurück wenn der User Administrator ist ODER die erweiterte Rolle für den NavItem hat
    private async Task<bool> CanEditAsync(string? navItemId)
    {
        if (User.IsInRole("Administrator")) return true;
        if (string.IsNullOrEmpty(navItemId)) return false;
        var userRoles = User.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value);
        return await _nav.HasExtendedAccessAsync(navItemId, userRoles);
    }

    public async Task OnGetAsync(string? search, string? klasse, string? gruppe)
    {
        Search       = search;
        KlasseFilter = klasse;
        Gruppe       = gruppe;

        var all = await _downloads.GetAllAsync();

        if (!string.IsNullOrWhiteSpace(gruppe))
            all = all.Where(d => d.Gruppe == gruppe).ToList();

        if (!string.IsNullOrWhiteSpace(search))
            all = all.Where(d =>
                d.Beschreibung.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                d.OriginalName.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(klasse))
            all = all.Where(d => d.Klasse == klasse).ToList();

        Downloads = all;
    }

    public async Task<IActionResult> OnPostUploadAsync(
        IFormFile uploadFile, string beschreibung, string klasse, string? gruppe, string? navItemId)
    {
        if (!await CanEditAsync(navItemId)) return Forbid();
        if (uploadFile == null || uploadFile.Length == 0)
            return RedirectToPage(new { gruppe });

        // Gruppe serverseitig bestimmen: Config-String-Seite erzwingt die Gruppe,
        // sonst muss zwingend eine Gruppe gewählt worden sein.
        var navItem    = string.IsNullOrEmpty(navItemId) ? null : await _nav.GetByIdAsync(navItemId);
        var pageGruppe = navItem?.ConfigString ?? "";
        var effektiveGruppe = !string.IsNullOrWhiteSpace(pageGruppe) ? pageGruppe : (gruppe ?? "");
        if (string.IsNullOrWhiteSpace(effektiveGruppe))
        {
            TempData["UploadMessage"] = "Bitte eine Gruppe auswählen.";
            return RedirectToPage(new { gruppe = string.IsNullOrWhiteSpace(gruppe) ? null : gruppe });
        }

        var item = new Download
        {
            Beschreibung = beschreibung ?? "",
            Klasse       = klasse ?? "",
            Gruppe       = effektiveGruppe,
            CreatedBy    = User.Identity?.Name ?? ""
        };
        await _downloads.CreateAsync(item, uploadFile);
        TempData["UploadMessage"] = $"'{uploadFile.FileName}' erfolgreich hochgeladen.";
        return RedirectToPage(new { gruppe = string.IsNullOrWhiteSpace(gruppe) ? null : gruppe });
    }

    public async Task<IActionResult> OnPostUpdateAsync(
        string id, string beschreibung, string klasse, string? gruppe, string? navItemId)
    {
        if (!await CanEditAsync(navItemId)) return Forbid();
        await _downloads.UpdateAsync(id, beschreibung ?? "", klasse ?? "");
        return RedirectToPage(new { gruppe = string.IsNullOrWhiteSpace(gruppe) ? null : gruppe });
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id, string? gruppe, string? navItemId)
    {
        if (!await CanEditAsync(navItemId)) return Forbid();
        await _downloads.DeleteAsync(id);
        return RedirectToPage(new { gruppe = string.IsNullOrWhiteSpace(gruppe) ? null : gruppe });
    }
}
