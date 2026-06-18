using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Pendenzen;

[Authorize]
public class PendenzToggleModel : PageModel
{
    private readonly PendenzService _pendenzSvc;
    public PendenzToggleModel(PendenzService pendenzSvc) => _pendenzSvc = pendenzSvc;

    public async Task<IActionResult> OnPostAsync(string id, string returnId)
    {
        if (!string.IsNullOrEmpty(id))
            await _pendenzSvc.ToggleErledigtAsync(id, User.Identity?.Name ?? "?");

        return Redirect($"/Page/Index?id={returnId}");
    }
}
