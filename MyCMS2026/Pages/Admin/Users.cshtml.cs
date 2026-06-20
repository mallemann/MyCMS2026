using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class UsersModel : PageModel
{
    private readonly UserService _users;
    private readonly EmailService _email;
    private readonly SiteService _site;

    public UsersModel(UserService users, EmailService email, SiteService site)
    {
        _users = users;
        _email = email;
        _site  = site;
    }

    public List<AppUser> Users { get; private set; } = new();
    public string Message { get; private set; } = "";
    public bool IsError { get; private set; }

    public async Task OnGetAsync()
    {
        Users = await _users.GetAllAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync(
        string newUserName, string newKuerzel, string newEmail,
        string newPassword, string newRoles)
    {
        var roles = newRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var ok = await _users.CreateAsync(newUserName, newEmail, newPassword, roles, newKuerzel);
        Message = ok ? $"Benutzer '{newUserName}' erstellt." : $"Benutzername '{newUserName}' bereits vergeben.";
        IsError = !ok;
        Users = await _users.GetAllAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAsync(
        string originalUserName, string updUserName, string updKuerzel,
        string updEmail, string? updPassword, string? updRoles, bool updActive)
    {
        var roles = (updRoles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var (ok, error) = await _users.UpdateUserAsync(originalUserName, updUserName, updEmail, updPassword, roles, updActive, updKuerzel);
        Message = ok ? $"Benutzer '{updUserName}' gespeichert." : error;
        IsError = !ok;
        Users = await _users.GetAllAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string userName)
    {
        if (userName == User.Identity!.Name)
        {
            Message = "Sie können sich selbst nicht löschen.";
            IsError = true;
        }
        else
        {
            var ok = await _users.DeleteAsync(userName);
            Message = ok ? $"Benutzer '{userName}' gelöscht." : "Benutzer nicht gefunden.";
            IsError = !ok;
        }
        Users = await _users.GetAllAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSetPasswordAsync(string setUserName, string setNewPassword)
    {
        if (setNewPassword.Length < 8)
        {
            Message = "Passwort muss mindestens 8 Zeichen lang sein.";
            IsError = true;
            Users = await _users.GetAllAsync();
            return Page();
        }

        // Erst User laden, dann nur Passwort setzen — Rollen/E-Mail bleiben unverändert
        var user = (await _users.GetAllAsync()).FirstOrDefault(u =>
            u.UserName.Equals(setUserName, StringComparison.OrdinalIgnoreCase));

        if (user == null)
        {
            Message = $"Benutzer '{setUserName}' nicht gefunden.";
            IsError = true;
            Users = await _users.GetAllAsync();
            return Page();
        }

        var setOk = await _users.SetPasswordAsync(setUserName, setNewPassword);

        if (!setOk)
        {
            Message = $"Passwort für '{setUserName}' konnte nicht gesetzt werden.";
            IsError = true;
        }
        else
        {
            Message = $"Passwort für '{setUserName}' gesetzt.";

            // E-Mail-Benachrichtigung falls E-Mail vorhanden
            if (!string.IsNullOrEmpty(user.Email))
            {
                var cfg = await _site.GetAsync();
                var body = $"""
                    <p>Hallo {user.UserName},</p>
                    <p>Ein Administrator hat Ihr Passwort auf <strong>{cfg.Title}</strong> zurückgesetzt.</p>
                    <p>Ihr neues Passwort lautet: <strong style="font-size:1.1em">{setNewPassword}</strong></p>
                    <p>
                        <a href="{Request.Scheme}://{Request.Host}/Account/Login"
                           style="background:#4361ee;color:#fff;padding:10px 20px;
                                  border-radius:6px;text-decoration:none;display:inline-block;">
                           Jetzt anmelden
                        </a>
                    </p>
                    <p>Wir empfehlen, das Passwort nach der Anmeldung zu ändern.</p>
                    <hr/><small style="color:#888">{cfg.Title}</small>
                    """;

                await _email.SendAsync(user.Email, $"Ihr Passwort wurde zurückgesetzt – {cfg.Title}", body);
                Message += " E-Mail-Benachrichtigung versandt.";
            }
            else
            {
                Message += " (Keine E-Mail-Adresse hinterlegt – keine Benachrichtigung möglich.)";
            }
        }

        Users = await _users.GetAllAsync();
        return Page();
    }
}
