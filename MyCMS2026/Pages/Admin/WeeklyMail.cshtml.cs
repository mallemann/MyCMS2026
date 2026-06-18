using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class WeeklyMailModel : PageModel
{
    private readonly WeeklyMailService _mailSvc;
    private readonly UserService       _users;
    private readonly GruppenService    _gruppen;

    public WeeklyMailModel(WeeklyMailService mailSvc, UserService users, GruppenService gruppen)
    {
        _mailSvc = mailSvc;
        _users   = users;
        _gruppen = gruppen;
    }

    public WeeklyMailConfig Config { get; private set; } = new();
    public List<AppUser> AllUsers { get; private set; } = new();
    public List<string> AllGruppen { get; private set; } = new();
    public string Message { get; private set; } = "";
    public bool IsError { get; private set; }

    private async Task LoadAsync()
    {
        Config     = await _mailSvc.GetConfigAsync();
        AllUsers   = (await _users.GetAllAsync()).Where(u => u.IsActive).OrderBy(u => u.UserName).ToList();
        AllGruppen = await _gruppen.GetAllAsync();
    }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostSaveAsync(
        [FromForm] List<string> recipientUser,
        [FromForm] List<string> recipientEmail,
        [FromForm] List<string> recipientTodos,
        [FromForm] List<string> recipientMeetings,
        [FromForm] List<string> recipientJournal,
        [FromForm] List<string> recipientGruppen)
    {
        // recipientGruppen kommt als "index:gruppe" Werte
        var recipients = new List<WeeklyMailRecipient>();
        for (int i = 0; i < recipientUser.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(recipientUser[i])) continue;
            recipients.Add(new WeeklyMailRecipient
            {
                UserId          = recipientUser[i],
                Email           = recipientEmail.ElementAtOrDefault(i) ?? "",
                ReceiveTodos    = recipientTodos.Contains(i.ToString()),
                ReceiveMeetings = recipientMeetings.Contains(i.ToString()),
                ReceiveJournal  = recipientJournal.Contains(i.ToString()),
                AllowedGruppen  = recipientGruppen
                    .Where(g => g.StartsWith($"{i}:"))
                    .Select(g => g.Substring(g.IndexOf(':') + 1))
                    .ToList()
            });
        }

        var cfg = await _mailSvc.GetConfigAsync();
        cfg.Recipients = recipients;
        await _mailSvc.SaveConfigAsync(cfg);
        _mailSvc.InvalidateCache();

        Message = "Konfiguration gespeichert.";
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSendNowAsync()
    {
        try
        {
            await _mailSvc.SendWeeklyAsync();
            Message = $"Weekly Mail gesendet ({DateTime.Now:dd.MM.yyyy HH:mm}).";
        }
        catch (Exception ex)
        {
            Message = $"Fehler: {ex.Message}";
            IsError = true;
        }
        await LoadAsync();
        return Page();
    }
}
