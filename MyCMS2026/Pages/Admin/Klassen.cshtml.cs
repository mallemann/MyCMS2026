using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class KlassenModel : PageModel
{
    private readonly KlassenService _klassen;
    public KlassenModel(KlassenService klassen) => _klassen = klassen;

    public Dictionary<string, List<string>> AllKlassen { get; private set; } = new();
    public string Message { get; private set; } = "";
    public bool IsError { get; private set; }

    public async Task OnGetAsync()
    {
        foreach (var type in KlassenService.Types)
            AllKlassen[type] = await _klassen.GetKlassenAsync(type);
    }

    public async Task<IActionResult> OnPostSaveAsync(string type, string klassen)
    {
        if (!KlassenService.Types.Contains(type))
        {
            Message = "Unbekannter Typ.";
            IsError = true;
        }
        else
        {
            var list = (klassen ?? "")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
            await _klassen.SetKlassenAsync(type, list);
            Message = $"Klassen für '{type}' gespeichert.";
        }

        foreach (var t in KlassenService.Types)
            AllKlassen[t] = await _klassen.GetKlassenAsync(t);
        return Page();
    }
}
