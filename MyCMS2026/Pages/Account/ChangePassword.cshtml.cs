using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Account;

[Authorize]
public class ChangePasswordModel : PageModel
{
    private readonly UserService _users;
    public ChangePasswordModel(UserService users) => _users = users;

    [BindProperty] public string CurrentPassword { get; set; } = "";
    [BindProperty] public string NewPassword { get; set; } = "";
    [BindProperty] public string ConfirmPassword { get; set; } = "";

    public string ErrorMessage { get; set; } = "";
    public string SuccessMessage { get; set; } = "";

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (NewPassword != ConfirmPassword)
        {
            ErrorMessage = "Die Passwörter stimmen nicht überein.";
            return Page();
        }
        if (NewPassword.Length < 8)
        {
            ErrorMessage = "Das Passwort muss mindestens 8 Zeichen lang sein.";
            return Page();
        }

        var ok = await _users.ChangePasswordAsync(User.Identity!.Name!, CurrentPassword, NewPassword);
        if (!ok)
        {
            ErrorMessage = "Das aktuelle Passwort ist falsch.";
            return Page();
        }

        SuccessMessage = "Passwort wurde erfolgreich geändert.";
        return Page();
    }
}
