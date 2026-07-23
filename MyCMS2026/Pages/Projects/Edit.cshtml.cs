using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Projects;

[Authorize]
public class ProjectEditModel : PageModel
{
    private readonly ProjectService _projects;
    private readonly UserService _users;
    private readonly RoleService _roles;
    private readonly GruppenService _gruppen;
    private readonly NavigationService _nav;

    public ProjectEditModel(ProjectService projects, UserService users, RoleService roles, GruppenService gruppen, NavigationService nav)
    {
        _projects = projects;
        _users    = users;
        _roles    = roles;
        _gruppen  = gruppen;
        _nav      = nav;
    }

    [BindProperty] public Project Input { get; set; } = new();
    /// <summary>Nav-Seite, von der aus angelegt wird (bestimmt Gruppen-Zwang + Anlege-Recht).</summary>
    [BindProperty(SupportsGet = true)] public string? ReturnPageId { get; set; }
    public bool IsNew => string.IsNullOrEmpty(Input.Id);
    public bool IsAdmin { get; private set; }
    /// <summary>True, wenn die Gruppe durch den ConfigString der Seite vorgegeben (gesperrt) ist.</summary>
    public bool GruppeLocked { get; private set; }
    public string? LockedGruppe { get; private set; }
    public List<string> UserNames { get; private set; } = new();
    public List<string> RoleNames { get; private set; } = new();
    public List<string> AvailableGruppen { get; private set; } = new();
    public string? Error { get; set; }

    private List<string> UserRoles => User.Claims
        .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
        .Select(c => c.Value).ToList();

    private async Task LoadUsersAsync()
    {
        var users = await _users.GetAllAsync();
        UserNames = users
            .Where(u => u.IsActive)
            .Select(u => u.UserName)
            .OrderBy(n => n)
            .ToList();
        var allRoles = await _roles.GetAllAsync();
        RoleNames = allRoles.Select(r => r.Name).OrderBy(n => n).ToList();
        AvailableGruppen = await _gruppen.GetAllAsync();
    }

    public async Task<IActionResult> OnGetAsync(string? id)
    {
        IsAdmin = User.IsInRole("Administrator");
        var roles = UserRoles;
        await LoadUsersAsync();

        if (string.IsNullOrEmpty(id))
        {
            // Neues Projekt: Admin ODER Bearbeiten-Recht (ExtendedAccess) auf der Ursprungsseite
            var navItem = string.IsNullOrEmpty(ReturnPageId) ? null : await _nav.GetByIdAsync(ReturnPageId);
            var canCreate = IsAdmin
                || (!string.IsNullOrEmpty(ReturnPageId) && await _nav.HasExtendedAccessAsync(ReturnPageId, roles));
            if (!canCreate) return Forbid();

            // Gruppen-Zwang: wenn die Seite einen Gruppen-ConfigString trägt, Gruppe sperren
            // und Lese-/Bearbeiten-Rolle aus der Seite vorbelegen (für Admin editierbarer Default).
            if (navItem != null && !string.IsNullOrWhiteSpace(navItem.ConfigString))
            {
                GruppeLocked = true;
                LockedGruppe = navItem.ConfigString;
                Input.Gruppe = navItem.ConfigString;
                Input.LeseRolle       = navItem.BasicAccessRole;
                Input.BearbeitenRolle = navItem.ExtendedAccessRole;
            }
        }
        else
        {
            var existing = await _projects.GetByIdAsync(id);
            if (existing == null) return NotFound();
            if (!_projects.CanEdit(existing, IsAdmin, roles)) return Forbid();
            Input = existing;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        IsAdmin = User.IsInRole("Administrator");
        var roles = UserRoles;
        var userName = User.Identity?.Name ?? "";
        var isNew = string.IsNullOrEmpty(Input.Id);

        // Ursprungsseite (für Gruppen-Zwang + Rechte) serverseitig auflösen
        var navItem = string.IsNullOrEmpty(ReturnPageId) ? null : await _nav.GetByIdAsync(ReturnPageId);
        if (navItem != null && !string.IsNullOrWhiteSpace(navItem.ConfigString))
        {
            GruppeLocked = true;
            LockedGruppe = navItem.ConfigString;
        }

        if (string.IsNullOrWhiteSpace(Input.Name))
        {
            Error = "Projektname ist ein Pflichtfeld.";
            await LoadUsersAsync();
            return Page();
        }

        if (isNew)
        {
            // Anlege-Recht: Admin ODER ExtendedAccess auf der Ursprungsseite
            var canCreate = IsAdmin
                || (!string.IsNullOrEmpty(ReturnPageId) && await _nav.HasExtendedAccessAsync(ReturnPageId, roles));
            if (!canCreate) return Forbid();

            // Gruppe serverseitig erzwingen, wenn die Seite gruppen-scoped ist
            if (GruppeLocked)
                Input.Gruppe = LockedGruppe!;

            // Nicht-Admin-Ersteller (Projektleiter): Lese-/Bearbeiten-Rolle von der Seite ableiten,
            // damit er sein Projekt danach verwalten kann und die Gruppe es lesen kann.
            if (!IsAdmin && navItem != null)
            {
                Input.BearbeitenRolle = navItem.ExtendedAccessRole;
                Input.LeseRolle       = navItem.BasicAccessRole;
            }

            var created = await _projects.CreateAsync(Input, userName);
            return RedirectToPage("Detail", new { id = created.Id });
        }
        else
        {
            var existing = await _projects.GetByIdAsync(Input.Id);
            if (existing == null) return NotFound();
            if (!_projects.CanEdit(existing, IsAdmin, roles)) return Forbid();
            await _projects.UpdateAsync(Input, userName);
            return RedirectToPage("Detail", new { id = Input.Id });
        }
    }
}
