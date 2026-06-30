using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Setup;

[AllowAnonymous]
public class SetupIndexModel : PageModel
{
    private readonly IWebHostEnvironment _env;
    private readonly UserService         _users;
    private readonly SiteService         _site;

    public SetupIndexModel(IWebHostEnvironment env, UserService users, SiteService site)
    {
        _env   = env;
        _users = users;
        _site  = site;
    }

    private string SetupFlag =>
        Path.Combine(_env.ContentRootPath, "App_Data", "setup-complete");

    // ── Eingabefelder ────────────────────────────────────────────────────────

    [BindProperty] public string SiteName     { get; set; } = "MyCMS";
    [BindProperty] public string BaseUrl      { get; set; } = "";
    [BindProperty] public string AdminUser    { get; set; } = "admin";
    [BindProperty] public string AdminEmail   { get; set; } = "";
    [BindProperty] public string AdminPw      { get; set; } = "";
    [BindProperty] public string AdminPwConf  { get; set; } = "";
    [BindProperty] public bool   LoadDemoData { get; set; } = false;

    public string? Error { get; private set; }

    public IActionResult OnGet()
    {
        if (System.IO.File.Exists(SetupFlag))
            return RedirectToPage("/Account/Login");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (System.IO.File.Exists(SetupFlag))
            return RedirectToPage("/Account/Login");

        // Validierung
        if (string.IsNullOrWhiteSpace(SiteName))
            { Error = "Site-Name ist erforderlich."; return Page(); }
        if (string.IsNullOrWhiteSpace(AdminUser))
            { Error = "Benutzername ist erforderlich."; return Page(); }
        if (string.IsNullOrWhiteSpace(AdminEmail) || !AdminEmail.Contains('@'))
            { Error = "Gültige E-Mail-Adresse erforderlich."; return Page(); }
        // null-safe (Model-Binding kann defaults überschreiben)
        AdminPw     ??= "";
        AdminPwConf ??= "";
        AdminUser   ??= "";
        AdminEmail  ??= "";
        SiteName    ??= "MyCMS";
        BaseUrl     ??= "";

        if (AdminPw.Length < 8)
            { Error = "Passwort muss mindestens 8 Zeichen lang sein."; return Page(); }
        if (AdminPw != AdminPwConf)
            { Error = "Passwörter stimmen nicht überein."; return Page(); }

        // 1. Site-Konfiguration speichern
        var site = await _site.GetAsync();
        site.Title   = SiteName.Trim();
        site.BaseUrl = BaseUrl.Trim();
        await _site.SaveAsync(site);

        // 2. Admin-User anlegen: users.json löschen, Cache leeren, frisch erstellen
        var adminUserTrimmed = AdminUser.Trim();
        var kuerzel = adminUserTrimmed[..Math.Min(3, adminUserTrimmed.Length)].ToUpper();

        var usersFile = Path.Combine(_env.ContentRootPath, "App_Data", "users.json");
        await System.IO.File.WriteAllTextAsync(usersFile, "[]");
        _users.InvalidateCache();

        // EnsureDefaultAdmin läuft nicht mehr (nur im Konstruktor).
        // Direkt den gewünschten Admin anlegen.
        var ok = await _users.CreateAsync(
            userName: adminUserTrimmed,
            email:    AdminEmail.Trim(),
            password: AdminPw,
            roles:    new List<string> { "Administrator", "Member" },
            kuerzel:  kuerzel);

        if (!ok) { Error = "Fehler beim Anlegen des Administrators."; return Page(); }

        // 3. Demodaten kopieren (optional)
        if (LoadDemoData)
        {
            var demoDir    = Path.Combine(_env.ContentRootPath, "App_Data", "demo");
            var appDataDir = Path.Combine(_env.ContentRootPath, "App_Data");
            if (Directory.Exists(demoDir))
            {
                foreach (var src in Directory.GetFiles(demoDir, "*.json"))
                {
                    var dest = Path.Combine(appDataDir, Path.GetFileName(src));
                    if (!System.IO.File.Exists(dest))   // users.json + site.json nicht überschreiben
                        System.IO.File.Copy(src, dest);
                }
            }
        }

        // 4. Setup-Flag setzen
        await System.IO.File.WriteAllTextAsync(SetupFlag, DateTime.UtcNow.ToString("o"));

        return RedirectToPage("/Account/Login", new { setupDone = true });
    }
}
