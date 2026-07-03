using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Account;

public class LoginModel : PageModel
{
    private readonly UserService _users;
    private readonly SiteService _site;
    private readonly ActivityService _activity;

    public LoginModel(UserService users, SiteService site, ActivityService activity)
    {
        _users    = users;
        _site     = site;
        _activity = activity;
    }

    [BindProperty] public string UserName { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public bool RememberMe { get; set; }

    public string ErrorMessage { get; set; } = "";
    public string SiteTitle { get; set; } = "MyCMS";
    public string SiteLogoUrl { get; set; } = "";

    public async Task OnGetAsync()
    {
        var cfg = await _site.GetAsync();
        SiteTitle   = cfg.Title;
        SiteLogoUrl = cfg.LogoUrl;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var cfg = await _site.GetAsync();
        SiteTitle   = cfg.Title;
        SiteLogoUrl = cfg.LogoUrl;

        var user = await _users.ValidateAsync(UserName, Password);
        if (user == null)
        {
            ErrorMessage = "Benutzername oder Passwort ungültig.";
            return Page();
        }

        // Offline-Sperre: Nur Administratoren dürfen sich anmelden
        if (cfg.Status == "Offline" && !user.Roles.Contains("Administrator"))
        {
            ErrorMessage = "Die Anwendung ist momentan offline. Bitte versuchen Sie es später erneut.";
            return Page();
        }

        var principal = _users.BuildPrincipal(user);
        var props = new AuthenticationProperties
        {
            IsPersistent = RememberMe,
            ExpiresUtc = RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : null
        };

        await HttpContext.SignInAsync("MyCMSCookies", principal, props);

        // Activity tracken (kein Tracking für Administratoren)
        if (!user.Roles.Contains("Administrator"))
            await _activity.RecordLoginAsync(user.UserName);

        return RedirectToPage("/Index");
    }
}
