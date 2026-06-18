using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class GruppenModel : PageModel
{
    private readonly GruppenService _gruppen;
    public GruppenModel(GruppenService gruppen) => _gruppen = gruppen;

    public List<string> Gruppen { get; set; } = new();
    public string? Message { get; set; }
    public bool IsError { get; set; }

    public async Task OnGetAsync()
        => Gruppen = await _gruppen.GetAllAsync();

    public async Task<IActionResult> OnPostAddAsync(string name)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrEmpty(name))
        {
            Message = "Gruppenname darf nicht leer sein.";
            IsError = true;
            Gruppen = await _gruppen.GetAllAsync();
            return Page();
        }
        await _gruppen.AddAsync(name);
        Message = $"Gruppe «{name}» wurde hinzugefügt.";
        Gruppen = await _gruppen.GetAllAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string name)
    {
        await _gruppen.DeleteAsync(name);
        Message = $"Gruppe «{name}» wurde gelöscht.";
        Gruppen = await _gruppen.GetAllAsync();
        return Page();
    }
}
