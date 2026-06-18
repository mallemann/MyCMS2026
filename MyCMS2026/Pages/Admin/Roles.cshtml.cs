using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class RolesModel : PageModel
{
    private readonly RoleService _roleSvc;
    public RolesModel(RoleService roleSvc) => _roleSvc = roleSvc;

    public List<Role> Roles { get; set; } = [];
    public string? Message  { get; set; }
    public bool    IsError  { get; set; }

    public async Task OnGetAsync()
        => Roles = await _roleSvc.GetAllAsync();

    // Erstellen
    public async Task<IActionResult> OnPostCreateAsync(
        string name, string description, int sortOrder)
    {
        await _roleSvc.CreateAsync(new Role
        {
            Name        = name.Trim(),
            Description = description.Trim(),
            SortOrder   = sortOrder
        });
        TempData["Msg"] = $"Rolle «{name}» erstellt.";
        return RedirectToPage();
    }

    // Aktualisieren
    public async Task<IActionResult> OnPostUpdateAsync(
        string id, string name, string description, int sortOrder)
    {
        await _roleSvc.UpdateAsync(new Role
        {
            Id          = id,
            Name        = name.Trim(),
            Description = description.Trim(),
            SortOrder   = sortOrder
        });
        TempData["Msg"] = $"Rolle «{name}» gespeichert.";
        return RedirectToPage();
    }

    // Löschen
    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        await _roleSvc.DeleteAsync(id);
        TempData["Msg"] = "Rolle gelöscht.";
        return RedirectToPage();
    }
}
