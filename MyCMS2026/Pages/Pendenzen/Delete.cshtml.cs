using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Pendenzen;

[Authorize]
public class PendenzDeleteModel : PageModel
{
    private readonly PendenzService _pendenzSvc;
    public PendenzDeleteModel(PendenzService pendenzSvc) => _pendenzSvc = pendenzSvc;

    public async Task<IActionResult> OnPostAsync(string id, string returnId)
    {
        if (!string.IsNullOrEmpty(id))
        {
            var currentUser = User.Identity?.Name ?? "?";
            var isAdmin = User.IsInRole("Administrator");
            await _pendenzSvc.DeleteAsync(id, currentUser, isAdmin);
        }

        return Redirect($"/Page/Index?id={returnId}");
    }
}
