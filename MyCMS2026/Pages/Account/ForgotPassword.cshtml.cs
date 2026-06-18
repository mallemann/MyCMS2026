using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Account;

public class ForgotPasswordModel : PageModel
{
    private readonly UserService _users;
    private readonly EmailService _email;
    private readonly SiteService _site;

    public ForgotPasswordModel(UserService users, EmailService email, SiteService site)
    {
        _users = users;
        _email = email;
        _site  = site;
    }

    [BindProperty] public string UserNameOrEmail { get; set; } = "";
    public string? Message { get; set; }
    public bool Sent { get; set; }
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

        var (token, email, userName) = await _users.GeneratePasswordResetTokenAsync(UserNameOrEmail);

        if (token != null && email != null)
        {
            var resetUrl = Url.Page(
                "/Account/ResetPassword",
                null,
                new { token },
                Request.Scheme)!;

            var body = $"""
                <p>Hallo {userName},</p>
                <p>Sie haben eine Passwortrücksetzung für <strong>{SiteTitle}</strong> angefordert.</p>
                <p>
                    <a href="{resetUrl}" style="background:#4361ee;color:#fff;padding:10px 20px;
                       border-radius:6px;text-decoration:none;display:inline-block;">
                       Passwort zurücksetzen
                    </a>
                </p>
                <p>Der Link ist <strong>1 Stunde</strong> gültig.</p>
                <p>Falls Sie diese Anforderung nicht gestellt haben, ignorieren Sie diese E-Mail.</p>
                <hr/>
                <small style="color:#888">{SiteTitle}</small>
                """;

            await _email.SendAsync(email, $"Passwort zurücksetzen – {SiteTitle}", body);
        }

        // Immer dieselbe Meldung (kein User-Enumeration-Leak)
        Sent = true;
        Message = "Falls ein Konto mit diesen Angaben existiert, wurde eine E-Mail versandt.";
        return Page();
    }
}
