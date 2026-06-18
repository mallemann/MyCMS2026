using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Services;

namespace MyCMS2026.Pages;

[Authorize]
public class IndexModel : PageModel
{
    private readonly NavigationService _nav;

    public IndexModel(NavigationService nav) => _nav = nav;

    public async Task<IActionResult> OnGetAsync()
    {
        // Zur ersten zugänglichen Seite weiterleiten
        var roles = User.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value);

        var tree = await _nav.GetTreeAsync(roles);
        var first = tree.FirstOrDefault();

        if (first != null)
            return RedirectToPage("/Page/Index", new { id = first.Id });

        // Kein Nav-Eintrag vorhanden
        return Page();
    }
}
