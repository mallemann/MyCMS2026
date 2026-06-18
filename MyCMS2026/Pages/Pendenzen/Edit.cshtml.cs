using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Pendenzen;

[Authorize]
public class PendenzEditModel : PageModel
{
    private readonly PendenzService _pendenzSvc;

    public PendenzEditModel(PendenzService pendenzSvc)
        => _pendenzSvc = pendenzSvc;

    // Query-Parameter
    [BindProperty(SupportsGet = true)] public string? PendenzId { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnId { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string? ConfigString { get; set; } = "";

    [BindProperty] public Pendenz Input { get; set; } = new();

    public bool IsNew => string.IsNullOrEmpty(PendenzId);
    public bool IsPersonalMode => string.IsNullOrEmpty(Input.ConfigString);
    public bool CanDelete { get; private set; }
    public string Message { get; private set; } = "";
    public bool IsError { get; private set; }

    private string CurrentUser => User.Identity?.Name ?? "?";
    private bool IsAdmin => User.IsInRole("Administrator");

    public async Task<IActionResult> OnGetAsync()
    {
        if (IsNew)
        {
            // Neue Pendenz vorbereiten
            Input = new Pendenz
            {
                ConfigString = ConfigString,
                Verantwortlich = CurrentUser
            };
        }
        else
        {
            var p = await _pendenzSvc.GetByIdAsync(PendenzId!);
            if (p == null) return NotFound();

            // Zugriff prüfen: nur eigene oder ExtendedAccess
            if (!IsAdmin && p.Verantwortlich != CurrentUser)
                return Forbid();

            Input = p;
        }

        CanDelete = Input.Erledigt && (IsAdmin || Input.Verantwortlich == CurrentUser);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Kein [Required] auf dem Modell → ModelState-Check weglassen,
        // da z.B. DateTime?-Bindungsfehler sonst lautlos scheitern.
        ModelState.Clear();

        // Im persönlichen Modus: Verantwortlich immer auf currentUser sperren
        if (string.IsNullOrEmpty(Input.ConfigString))
            Input.Verantwortlich = CurrentUser;

        if (IsNew)
        {
            // Input.ConfigString kommt bereits aus dem Hidden-Field im Formular
            await _pendenzSvc.CreateAsync(Input, CurrentUser);
            return Redirect($"/Page/Index?id={ReturnId}");
        }
        else
        {
            var ok = await _pendenzSvc.UpdateAsync(Input, CurrentUser);
            if (!ok)
            {
                Message = "Pendenz konnte nicht gespeichert werden.";
                IsError = true;
                return Page();
            }
            return Redirect($"/Page/Index?id={ReturnId}");
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        ModelState.Clear();
        if (string.IsNullOrEmpty(Input.Id))
            return BadRequest();

        var (ok, msg) = await _pendenzSvc.DeleteAsync(Input.Id, CurrentUser, IsAdmin);
        if (!ok)
        {
            Message = msg;
            IsError = true;
            // Reload
            var p = await _pendenzSvc.GetByIdAsync(Input.Id);
            if (p != null) Input = p;
            return Page();
        }

        return Redirect($"/Page/Index?id={ReturnId}");
    }
}
