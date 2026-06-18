using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Todos;

[Authorize]
public class TodosIndexModel : PageModel
{
    private readonly TodoService _todos;
    private readonly NavigationService _nav;
    public TodosIndexModel(TodoService todos, NavigationService nav) { _todos = todos; _nav = nav; }

    public List<TodoItem> Todos { get; set; } = new();
    public string? Search { get; set; }
    public string? StatusFilter { get; set; }
    public string? KlasseFilter { get; set; }
    public string? Gruppe { get; set; }

    public async Task OnGetAsync(string? search, string? status, string? klasse, string? gruppe)
    {
        Search       = search;
        StatusFilter = status;
        KlasseFilter = klasse;
        Gruppe       = gruppe;

        var all = await _todos.GetAllAsync();

        // Gruppen-Filter: zeigt nur Einträge dieser Gruppe
        if (!string.IsNullOrWhiteSpace(gruppe))
            all = all.Where(t => t.Gruppe == gruppe).ToList();

        if (!string.IsNullOrWhiteSpace(search))
            all = all.Where(t =>
                t.Thema.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                t.Verantwortlich.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                t.Beschreibung.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

        if (status == "offen")
            all = all.Where(t => !t.Erledigt).ToList();
        else if (status == "erledigt")
            all = all.Where(t => t.Erledigt).ToList();

        if (!string.IsNullOrWhiteSpace(klasse))
            all = all.Where(t => t.Klasse == klasse).ToList();

        Todos = all;
    }

    public async Task<IActionResult> OnPostToggleAsync(string id, string? gruppe, string? returnPageId)
    {
        var isAdmin   = User.IsInRole("Administrator");
        var userRoles = User.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value);
        // Verantwortlich speichert das Kürzel, nicht den UserName
        var userKuerzel = User.FindFirst("Kuerzel")?.Value ?? "";

        if (!isAdmin && !string.IsNullOrEmpty(returnPageId))
        {
            var hasExtended = await _nav.HasExtendedAccessAsync(returnPageId, userRoles);
            if (!hasExtended)
            {
                // Nur eigene Todos togglen mit Basic-Zugriff
                var todo = await _todos.GetByIdAsync(id);
                var hasBasic = await _nav.CanAccessAsync(returnPageId, userRoles);
                if (!hasBasic || string.IsNullOrEmpty(userKuerzel) || todo?.Verantwortlich != userKuerzel)
                    return Forbid();
            }
        }

        await _todos.ToggleErledigtAsync(id, User.Identity?.Name ?? "");

        if (!string.IsNullOrEmpty(returnPageId))
            return RedirectToPage("/Page/Index", new { id = returnPageId });
        return RedirectToPage(new { gruppe });
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id, string? gruppe)
    {
        if (!User.IsInRole("Administrator")) return Forbid();
        await _todos.DeleteAsync(id);
        return RedirectToPage(new { gruppe });
    }
}
