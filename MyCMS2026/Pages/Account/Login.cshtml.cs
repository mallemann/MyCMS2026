using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Account;

public class LoginModel : PageModel
{
    private readonly UserService _users;
    private readonly SiteService _site;

    public LoginModel(UserService users, SiteService site)
    {
        _users = users;
        _site = site;
    }

    [BindProperty] public string UserName { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public bool RememberMe { get; set; }

    public string ErrorMessage { get; set; } = "";
    public string SiteTitle { get; set; } = "MyCMS";

    public async Task OnGetAsync()
    {
        var cfg = await _site.GetAsync();
        SiteTitle = cfg.Title;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var cfg = await _site.GetAsync();
        SiteTitle = cfg.Title;

        var user = await _users.ValidateAsync(UserName, Password);
        if (user == null)
        {
            ErrorMessage = "Benutzername oder Passwort ungültig.";
            return Page();
        }

        var principal = _users.BuildPrincipal(user);
        var props = new AuthenticationProperties
        {
            IsPersistent = RememberMe,
            ExpiresUtc = RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : null
        };

        await HttpContext.SignInAsync("MyCMSCookies", principal, props);
        return RedirectToPage("/Index");
    }
}
