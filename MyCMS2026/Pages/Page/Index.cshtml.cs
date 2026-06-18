using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Page;

[Authorize]
public class PageIndexModel : PageModel
{
    private readonly NavigationService _nav;

    public PageIndexModel(NavigationService nav) => _nav = nav;

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = "";

    public NavItem? NavItem { get; private set; }
    public bool HasAccess { get; private set; }
    public bool HasExtendedAccess { get; private set; }
    public IEnumerable<string> UserRoles { get; private set; } = Enumerable.Empty<string>();

    public async Task<IActionResult> OnGetAsync()
    {
        UserRoles = User.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value);

        NavItem = await _nav.GetByIdAsync(Id);

        if (NavItem == null)
            return Page();

        HasAccess = await _nav.CanAccessAsync(Id, UserRoles);
        HasExtendedAccess = await _nav.HasExtendedAccessAsync(Id, UserRoles);

        return Page();
    }
}
