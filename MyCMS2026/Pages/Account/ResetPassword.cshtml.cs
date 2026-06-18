using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Account;

public class ResetPasswordModel : PageModel
{
    private readonly UserService _users;
    private readonly SiteService _site;

    public ResetPasswordModel(UserService users, SiteService site)
    {
        _users = users;
        _site  = site;
    }

    [BindProperty(SupportsGet = true)] public string Token { get; set; } = "";
    [BindProperty] public string NewPassword { get; set; } = "";
    [BindProperty] public string ConfirmPassword { get; set; } = "";

    public string? Error { get; set; }
    public bool Success { get; set; }
    public bool TokenValid { get; set; }
    public string SiteTitle { get; set; } = "MyCMS";

    public async Task<IActionResult> OnGetAsync()
    {
        var cfg = await _site.GetAsync();
        SiteTitle = cfg.Title;

        if (string.IsNullOrEmpty(Token)) return RedirectToPage("/Account/Login");
        var user = await _users.GetByResetTokenAsync(Token);
        TokenValid = user != null;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var cfg = await _site.GetAsync();
        SiteTitle = cfg.Title;

        if (string.IsNullOrEmpty(Token))
            return RedirectToPage("/Account/Login");

        var user = await _users.GetByResetTokenAsync(Token);
        if (user == null)
        {
            Error = "Der Reset-Link ist ungültig oder abgelaufen.";
            TokenValid = false;
            return Page();
        }

        TokenValid = true;

        if (NewPassword.Length < 8)
        {
            Error = "Das Passwort muss mindestens 8 Zeichen lang sein.";
            return Page();
        }
        if (NewPassword != ConfirmPassword)
        {
            Error = "Die Passwörter stimmen nicht überein.";
            return Page();
        }

        var ok = await _users.ResetPasswordWithTokenAsync(Token, NewPassword);
        if (!ok)
        {
            Error = "Fehler beim Zurücksetzen des Passworts. Bitte erneut anfordern.";
            return Page();
        }

        Success = true;
        return Page();
    }
}
