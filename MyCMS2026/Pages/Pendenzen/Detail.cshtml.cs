using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Pendenzen;

[Authorize]
public class PendenzDetailModel : PageModel
{
    private readonly PendenzService _pendenzSvc;

    public PendenzDetailModel(PendenzService pendenzSvc) => _pendenzSvc = pendenzSvc;

    public Pendenz? Pendenz { get; private set; }
    public string ReturnId { get; private set; } = "";
    public bool CanEdit { get; private set; }
    public bool CanDelete { get; private set; }

    private bool IsAdmin => User.IsInRole("Administrator");
    private string CurrentUser => User.Identity?.Name ?? "";

    public async Task<IActionResult> OnGetAsync(string? id, string? returnId)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        Pendenz = await _pendenzSvc.GetByIdAsync(id);
        if (Pendenz == null) return NotFound();

        ReturnId = returnId ?? "";

        CanEdit   = IsAdmin || Pendenz.Verantwortlich == CurrentUser;
        CanDelete = CanEdit && IsAdmin;
        return Page();
    }
}
