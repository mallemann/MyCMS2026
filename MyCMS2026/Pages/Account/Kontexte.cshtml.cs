using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Account;

[Authorize]
public class KontexteModel : PageModel
{
    private readonly UserService _users;
    private readonly WeeklyMailService _weeklyMail;

    public KontexteModel(UserService users, WeeklyMailService weeklyMail)
    {
        _users      = users;
        _weeklyMail = weeklyMail;
    }

    // Keine eigene Ansicht – die Toggles leben im User-Einstellungen-Widget.
    public IActionResult OnGet() => Redirect("~/");

    public async Task<IActionResult> OnPostSaveAsync([FromForm] List<string> aktiv, string? returnPageId)
    {
        var userName = User.Identity?.Name ?? "";
        var baseline = await _weeklyMail.GetBaselineGruppenAsync(userName);
        aktiv ??= new List<string>();

        // Deaktiviert = Baseline-Gruppen (Admin-Grant), die NICHT als aktiv gepostet wurden.
        // Dadurch nie mehr als der Admin vergeben hat; entzieht der Admin eine Gruppe, fällt sie
        // automatisch aus der Baseline und ist nicht mehr aktivierbar.
        var deaktiviert = baseline
            .Where(g => !aktiv.Contains(g, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var user = await _users.SetDeaktivierteKontexteAsync(userName, deaktiviert);
        if (user != null)
        {
            // Auth-Cookie mit effektiven Rollen neu ausstellen → Navigation greift sofort.
            var principal = await _users.BuildPrincipalAsync(user);
            await HttpContext.SignInAsync("MyCMSCookies", principal);
        }

        if (!string.IsNullOrEmpty(returnPageId))
            return RedirectToPage("/Page/Index", new { id = returnPageId });
        return Redirect("~/");
    }
}
