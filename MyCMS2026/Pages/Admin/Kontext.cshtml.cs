using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class KontextModel : PageModel
{
    private readonly GruppenService _gruppen;
    private readonly KontextService _kontext;
    private readonly RoleService _roles;

    public KontextModel(GruppenService gruppen, KontextService kontext, RoleService roles)
    {
        _gruppen = gruppen;
        _kontext = kontext;
        _roles   = roles;
    }

    public List<string> AllGruppen { get; private set; } = new();
    public List<Kontext> Kontexte { get; private set; } = new();
    public List<string> AllRoles { get; private set; } = new();
    public string Message { get; private set; } = "";

    private async Task LoadAsync()
    {
        AllGruppen = await _gruppen.GetAllAsync();
        Kontexte   = await _kontext.GetAllAsync();
        AllRoles   = await _roles.GetNamesAsync();
    }

    public string RolleFor(string gruppe)
        => Kontexte.FirstOrDefault(k => string.Equals(k.Gruppe, gruppe, System.StringComparison.OrdinalIgnoreCase))?.Rolle ?? "";

    public string BeschreibungFor(string gruppe)
        => Kontexte.FirstOrDefault(k => string.Equals(k.Gruppe, gruppe, System.StringComparison.OrdinalIgnoreCase))?.Beschreibung ?? "";

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostSaveAsync(
        [FromForm] List<string> gruppe,
        [FromForm] List<string> rolle,
        [FromForm] List<string> beschreibung)
    {
        var list = new List<Kontext>();
        for (int i = 0; i < gruppe.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(gruppe[i])) continue;
            list.Add(new Kontext
            {
                Gruppe       = gruppe[i],
                Rolle        = rolle.ElementAtOrDefault(i) ?? "",
                Beschreibung = beschreibung.ElementAtOrDefault(i) ?? ""
            });
        }
        await _kontext.SaveAllAsync(list);
        Message = "Kontext-Konfiguration gespeichert.";
        await LoadAsync();
        return Page();
    }
}
