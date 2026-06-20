using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class SiteConfigModel : PageModel
{
    private readonly SiteService _site;
    public SiteConfigModel(SiteService site) => _site = site;

    [BindProperty] public SiteConfig Config { get; set; } = new();
    public string Message { get; private set; } = "";
    public bool IsError { get; private set; }

    public async Task OnGetAsync() => Config = await _site.GetAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        // Passwort-Feld leer = bestehendes Passwort beibehalten
        if (string.IsNullOrEmpty(Config.Smtp.Password))
        {
            var current = await _site.GetAsync();
            Config.Smtp.Password = current.Smtp.Password;
        }
        await _site.SaveAsync(Config);
        _site.InvalidateCache();
        Message = "Konfiguration gespeichert.";
        return Page();
    }
}
