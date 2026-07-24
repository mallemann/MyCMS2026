using System.Text;
using System.Text.Json;
using MyCMS2026.Models;

namespace MyCMS2026.Services;

public class WeeklyMailService
{
    private readonly string _configFile;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private WeeklyMailConfig? _cache;

    private readonly TodoService _todos;
    private readonly MeetingService _meetings;
    private readonly ProjectService _projects;
    private readonly UserService _users;
    private readonly EmailService _email;
    private readonly SiteService _site;
    private readonly ILogger<WeeklyMailService> _log;

    private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };

    public WeeklyMailService(
        IWebHostEnvironment env,
        TodoService todos,
        MeetingService meetings,
        ProjectService projects,
        UserService users,
        EmailService email,
        SiteService site,
        ILogger<WeeklyMailService> log)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _configFile = Path.Combine(dataDir, "weeklymail.json");
        _todos    = todos;
        _meetings = meetings;
        _projects = projects;
        _users    = users;
        _email    = email;
        _site     = site;
        _log      = log;
    }

    // ── Config laden / speichern ─────────────────────────────────────────────

    private async Task<WeeklyMailConfig> LoadAsync()
    {
        if (_cache is not null) return _cache;
        await _lock.WaitAsync();
        try
        {
            if (_cache is not null) return _cache;
            if (!File.Exists(_configFile))
            {
                _cache = new WeeklyMailConfig();
                return _cache;
            }
            var json = await File.ReadAllTextAsync(_configFile);
            _cache = JsonSerializer.Deserialize<WeeklyMailConfig>(json, _jsonOpts) ?? new();
            return _cache;
        }
        finally { _lock.Release(); }
    }

    private async Task SaveAsync(WeeklyMailConfig cfg)
    {
        await _lock.WaitAsync();
        try
        {
            _cache = cfg;
            var json = JsonSerializer.Serialize(cfg, _jsonOpts);
            await File.WriteAllTextAsync(_configFile, json);
        }
        finally { _lock.Release(); }
    }

    public async Task<WeeklyMailConfig> GetConfigAsync() => await LoadAsync();

    /// <summary>Vom Admin zugeordnete Gruppen (Baseline, ohne Kontext-Deaktivierung).</summary>
    public async Task<List<string>> GetBaselineGruppenAsync(string userName)
    {
        var cfg = await LoadAsync();
        var r = cfg.Recipients.FirstOrDefault(x => string.Equals(x.UserId, userName, StringComparison.OrdinalIgnoreCase));
        return r?.AllowedGruppen ?? new List<string>();
    }

    /// <summary>
    /// Effektive Access-Gruppen = Baseline minus die vom User deaktivierten Kontexte (Gruppen).
    /// Diese Methode ist die zentrale Quelle für die Sichtbarkeit in allen Widgets.
    /// </summary>
    public async Task<List<string>> GetAllowedGruppenAsync(string userName)
    {
        var baseline = await GetBaselineGruppenAsync(userName);
        if (baseline.Count == 0) return baseline;
        var user = await _users.GetByNameAsync(userName);
        var deaktiviert = user?.DeaktivierteKontexte ?? new List<string>();
        if (deaktiviert.Count == 0) return baseline;
        return baseline.Where(g => !deaktiviert.Contains(g, StringComparer.OrdinalIgnoreCase)).ToList();
    }

    public async Task SaveConfigAsync(WeeklyMailConfig cfg)
    {
        var current = await LoadAsync();
        cfg.LastSentAt = current.LastSentAt;   // LastSentAt nicht überschreiben
        await SaveAsync(cfg);
    }

    public void InvalidateCache() => _cache = null;

    // ── Versand ──────────────────────────────────────────────────────────────

    /// <summary>Sendet das Weekly Mail an alle konfigurierten Empfänger.</summary>
    public async Task SendWeeklyAsync()
    {
        var cfg = await LoadAsync();
        if (!cfg.Recipients.Any())
        {
            _log.LogInformation("Weekly Mail: keine Empfänger konfiguriert.");
            return;
        }

        var siteConfig  = await _site.GetAsync();
        var baseUrl     = siteConfig.BaseUrl.TrimEnd('/');

        var allTodos    = await _todos.GetAllAsync();
        var allMeetings = await _meetings.GetAllAsync();
        var allProjects = await _projects.GetAllAsync();

        var cutoff = DateTime.Today.AddDays(-7);

        foreach (var recipient in cfg.Recipients)
        {
            if (string.IsNullOrEmpty(recipient.Email)) continue;

            var user = await _users.GetByNameAsync(recipient.UserId);
            var userRoles = user?.Roles ?? new List<string>();
            var isAdmin = userRoles.Contains("Administrator");

            try
            {
                var effektiveGruppen = await GetAllowedGruppenAsync(recipient.UserId);
                var html = BuildMail(recipient, userRoles, isAdmin, effektiveGruppen,
                    allTodos, allMeetings, allProjects, cutoff, baseUrl);

                await _email.SendAsync(recipient.Email,
                    $"Weekly Update – {DateTime.Today:dd.MM.yyyy}", html);

                _log.LogInformation("Weekly Mail gesendet an {Email}", recipient.Email);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Weekly Mail Fehler für {Email}", recipient.Email);
            }
        }

        cfg.LastSentAt = DateTime.Now;
        await SaveAsync(cfg);
    }

    /// <summary>
    /// Erzeugt das Weekly Mail eines Users (dessen Access-Gruppen/Einstellungen) und schickt es
    /// als Vorschau/Test an <paramref name="sendTo"/> (i.d.R. den auslösenden Administrator).
    /// </summary>
    public async Task<string> SendWeeklyToUserAsync(string userId, string sendTo)
    {
        if (string.IsNullOrWhiteSpace(sendTo))
            return "Keine Ziel-E-Mail-Adresse (Admin) vorhanden.";

        var cfg = await LoadAsync();
        var recipient = cfg.Recipients.FirstOrDefault(r =>
            string.Equals(r.UserId, userId, StringComparison.OrdinalIgnoreCase));
        if (recipient == null)
            return $"Kein Empfänger-Eintrag für '{userId}' vorhanden.";

        var siteConfig  = await _site.GetAsync();
        var baseUrl     = siteConfig.BaseUrl.TrimEnd('/');
        var allTodos    = await _todos.GetAllAsync();
        var allMeetings = await _meetings.GetAllAsync();
        var allProjects = await _projects.GetAllAsync();
        var cutoff      = DateTime.Today.AddDays(-7);

        // Inhalt aus Sicht des Ziel-Users erzeugen
        var user      = await _users.GetByNameAsync(recipient.UserId);
        var userRoles = user?.Roles ?? new List<string>();
        var isAdmin   = userRoles.Contains("Administrator");

        var effektiveGruppen = await GetAllowedGruppenAsync(recipient.UserId);
        var html = BuildMail(recipient, userRoles, isAdmin, effektiveGruppen,
            allTodos, allMeetings, allProjects, cutoff, baseUrl);

        // ... aber an den Admin senden (Vorschau), Betreff als Test markiert
        var subject  = $"[TEST – {userId}] Weekly Update – {DateTime.Today:dd.MM.yyyy}";
        var response = await _email.SendTestAsync(sendTo, subject, html);
        _log.LogInformation("Weekly-Test für {User} an Admin {Admin}: {Response}", userId, sendTo, response);
        return string.IsNullOrWhiteSpace(response)
            ? $"Test-Weekly für '{userId}' an {sendTo} übergeben."
            : $"Test-Weekly für '{userId}' an {sendTo} gesendet. Serverantwort: {response}";
    }

    // ── HTML-Builder ─────────────────────────────────────────────────────────

    private string BuildMail(
        WeeklyMailRecipient recipient,
        List<string> userRoles,
        bool isAdmin,
        List<string> effektiveGruppen,
        List<TodoItem> allTodos,
        List<Meeting> allMeetings,
        List<Project> allProjects,
        DateTime cutoff,
        string baseUrl)
    {
        var sb = new StringBuilder();
        sb.Append(@"<!DOCTYPE html>
<html><head><meta charset='utf-8'>
<style>
  body { font-family: Arial, sans-serif; font-size: 14px; color: #333; margin: 0; padding: 0; }
  .wrap { max-width: 700px; margin: 0 auto; padding: 24px; }
  h1 { font-size: 20px; color: #1a1a2e; border-bottom: 2px solid #4a90d9; padding-bottom: 8px; }
  h2 { font-size: 16px; color: #4a90d9; margin-top: 28px; margin-bottom: 8px; }
  table { width: 100%; border-collapse: collapse; margin-bottom: 12px; }
  th { background: #f0f4f8; text-align: left; padding: 6px 10px; font-size: 12px; color: #555; }
  td { padding: 6px 10px; border-bottom: 1px solid #eee; vertical-align: top; }
  .badge { display: inline-block; padding: 2px 7px; border-radius: 4px; font-size: 11px; font-weight: bold; }
  .badge-open   { background: #fff3cd; color: #856404; }
  .badge-done   { background: #d1e7dd; color: #0a3622; }
  .badge-overdue{ background: #f8d7da; color: #842029; }
  .row-done   { background-color: #d1e7dd; }
  .row-overdue{ background-color: #f8d7da; }
  .row-soon   { background-color: #fff3cd; }
  a.link { color: #1a6bbf; text-decoration: none; }
  a.link:hover { text-decoration: underline; }
  .empty { color: #999; font-size: 13px; font-style: italic; padding: 8px 0; }
  .footer { margin-top: 36px; font-size: 11px; color: #aaa; border-top: 1px solid #eee; padding-top: 12px; }
</style>
</head><body><div class='wrap'>");

        sb.Append($"<h1>Weekly Update – {DateTime.Today:dddd, dd. MMMM yyyy}</h1>");
        sb.Append($"<p>Guten Tag {recipient.UserId},<br>hier dein wöchentlicher Überblick.</p>");

        // ── Todos ─────────────────────────────────────────────────────────────
        if (recipient.ReceiveTodos)
        {
            var todos = FilterTodos(allTodos, allProjects, effektiveGruppen)
                .Where(t => !t.Erledigt)
                .OrderBy(t => t.ErledigenBis)
                .ToList();

            sb.Append("<h2>📋 Offene Aufgaben</h2>");
            if (!todos.Any())
            {
                sb.Append("<p class='empty'>Keine offenen Aufgaben.</p>");
            }
            else
            {
                sb.Append("<table><thead><tr><th>#</th><th>Thema</th><th>Verantwortlich</th><th>Fällig</th><th>Klasse / Gruppe</th></tr></thead><tbody>");
                foreach (var t in todos)
                {
                    var diff    = (t.ErledigenBis - DateTime.Today).Days;
                    var overdue = diff < 0;
                    var soon    = !overdue && diff <= 7;

                    var rowClass = overdue ? "row-overdue" : (soon ? "row-soon" : "");
                    var badge    = overdue
                        ? "<span class='badge badge-overdue'>Überfällig</span>"
                        : (soon ? "<span class='badge badge-open'>Bald fällig</span>" : "");

                    var due          = t.ErledigenBis.ToString("dd.MM.yyyy");
                    var klasseGruppe = string.Join(" / ", new[] { t.Klasse, t.Gruppe }.Where(s => !string.IsNullOrEmpty(s)));
                    var thema        = Esc(t.Thema);

                    if (!string.IsNullOrEmpty(baseUrl))
                        thema = $"<a class='link' href='{baseUrl}/Todos/Detail?id={t.Id}'>{thema}</a>";

                    sb.Append($"<tr class='{rowClass}'><td>#{t.TaskNr}</td><td>{thema} {badge}</td><td>{Esc(t.Verantwortlich)}</td><td>{due}</td><td>{Esc(klasseGruppe)}</td></tr>");
                }
                sb.Append("</tbody></table>");
            }
        }

        // ── Meetings ─────────────────────────────────────────────────────────
        if (recipient.ReceiveMeetings)
        {
            var meetings = FilterMeetings(allMeetings, allProjects, effektiveGruppen)
                .Where(m => m.Datum >= cutoff)
                .OrderBy(m => m.Datum)
                .ToList();

            sb.Append("<h2>📅 Meetings</h2>");
            if (!meetings.Any())
            {
                sb.Append("<p class='empty'>Keine Meetings im Zeitraum.</p>");
            }
            else
            {
                sb.Append("<table><thead><tr><th>#</th><th>Thema</th><th>Leitung</th><th>Datum</th><th>Status</th><th>Klasse / Gruppe</th></tr></thead><tbody>");
                foreach (var m in meetings)
                {
                    var isPast       = m.Datum < DateTime.Today;
                    var rowClass     = isPast ? "row-done" : "";
                    var datum        = m.Datum.ToString("dd.MM.yyyy");
                    var klasseGruppe = string.Join(" / ", new[] { m.Klasse, m.Gruppe }.Where(s => !string.IsNullOrEmpty(s)));
                    var thema        = Esc(m.Thema);

                    if (!string.IsNullOrEmpty(baseUrl))
                        thema = $"<a class='link' href='{baseUrl}/Meetings/Detail?id={m.Id}'>{thema}</a>";

                    sb.Append($"<tr class='{rowClass}'><td>#{m.MeetingNr}</td><td>{thema}</td><td>{Esc(m.Leitung)}</td><td>{datum}</td><td>{Esc(m.Status)}</td><td>{Esc(klasseGruppe)}</td></tr>");
                }
                sb.Append("</tbody></table>");
            }
        }

        // ── Journal ──────────────────────────────────────────────────────────
        if (recipient.ReceiveJournal)
        {
            var journalItems = new List<(Project project, JournalEntry entry)>();
            foreach (var project in allProjects)
            {
                // Report ist gruppen-scoped für ALLE (auch Admins): nur Journale der effektiven Gruppen.
                if (!effektiveGruppen.Contains(project.Gruppe, StringComparer.OrdinalIgnoreCase)) continue;
                foreach (var entry in project.Journal.Where(e => e.CreatedAt >= cutoff))
                    journalItems.Add((project, entry));
            }
            journalItems = journalItems.OrderByDescending(x => x.entry.CreatedAt).ToList();

            sb.Append("<h2>📓 Journal-Einträge (letzte 7 Tage)</h2>");
            if (!journalItems.Any())
            {
                sb.Append("<p class='empty'>Keine neuen Journal-Einträge.</p>");
            }
            else
            {
                foreach (var (project, entry) in journalItems)
                {
                    var projectName = Esc(project.Name);
                    if (!string.IsNullOrEmpty(baseUrl))
                        projectName = $"<a class='link' href='{baseUrl}/Projects/Detail/{project.Id}'>{projectName}</a>";

                    sb.Append($"<div style='margin-bottom:16px;border-left:3px solid #4a90d9;padding-left:12px;'>");
                    sb.Append($"<strong>{projectName}</strong> &mdash; {Esc(entry.Titel)}");
                    sb.Append($"<div style='font-size:11px;color:#888;margin:2px 0 6px'>{entry.CreatedAt:dd.MM.yyyy HH:mm} &middot; {Esc(entry.CreatedBy)}</div>");
                    sb.Append($"<div style='font-size:13px'>{entry.Content}</div>");
                    sb.Append("</div>");
                }
            }
        }

        sb.Append($"<div class='footer'>Automatisch generiert von MyCMS &middot; {DateTime.Now:dd.MM.yyyy HH:mm}</div>");
        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    // ── Filterlogik ───────────────────────────────────────────────────────────

    private List<TodoItem> FilterTodos(
        List<TodoItem> all, List<Project> projects, List<string> effektiveGruppen)
    {
        return all.Where(t =>
        {
            if (!string.IsNullOrEmpty(t.ProjectId))
            {
                var proj = projects.FirstOrDefault(p => p.Id == t.ProjectId);
                return proj is not null && effektiveGruppen.Contains(proj.Gruppe, StringComparer.OrdinalIgnoreCase);
            }
            if (string.IsNullOrEmpty(t.Gruppe))
                return true;
            return effektiveGruppen.Contains(t.Gruppe, StringComparer.OrdinalIgnoreCase);
        }).ToList();
    }

    private List<Meeting> FilterMeetings(
        List<Meeting> all, List<Project> projects, List<string> effektiveGruppen)
    {
        return all.Where(m =>
        {
            if (!string.IsNullOrEmpty(m.ProjectId))
            {
                var proj = projects.FirstOrDefault(p => p.Id == m.ProjectId);
                return proj is not null && effektiveGruppen.Contains(proj.Gruppe, StringComparer.OrdinalIgnoreCase);
            }
            if (string.IsNullOrEmpty(m.Gruppe))
                return true;
            return effektiveGruppen.Contains(m.Gruppe, StringComparer.OrdinalIgnoreCase);
        }).ToList();
    }

    private static string Esc(string? s) =>
        System.Net.WebUtility.HtmlEncode(s ?? "");
}
