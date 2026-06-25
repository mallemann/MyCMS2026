using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Projects;

[Authorize]
public class ProjectDetailModel : PageModel
{
    private readonly ProjectService _projects;
    private readonly TodoService _todos;
    private readonly MeetingService _meetings;

    public ProjectDetailModel(ProjectService projects, TodoService todos, MeetingService meetings)
    {
        _projects = projects;
        _todos    = todos;
        _meetings = meetings;
    }

    public Project Project { get; private set; } = new();
    public List<TodoItem> Todos { get; private set; } = new();
    public List<Meeting> Meetings { get; private set; } = new();
    public bool IsAdmin { get; private set; }
    public bool CanEdit { get; private set; }
    public bool CanComment { get; private set; }
    public string? ActiveTab { get; private set; }

    // Journal-Prompt nach Zuweisung
    public string? PromptJournalId { get; private set; }
    public string? PromptJournalType { get; private set; }
    public string? PromptJournalTitle { get; private set; }

    // Auto-öffnen eines Eintrags nach Erstellung
    public string? OpenEntryId { get; private set; }

    private async Task<bool> LoadProjectAsync(string id)
    {
        IsAdmin = User.IsInRole("Administrator");
        var userRoles = User.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value).ToList();

        var project = await _projects.GetByIdAsync(id);
        if (project == null) return false;
        if (!_projects.CanRead(project, IsAdmin, userRoles)) return false;

        Project    = project;
        CanEdit    = _projects.CanEdit(project, IsAdmin, userRoles);
        CanComment = true;

        var allTodos    = await _todos.GetAllAsync();
        var allMeetings = await _meetings.GetAllAsync();

        Todos    = allTodos.Where(t => t.ProjectId == id).ToList();
        Meetings = allMeetings.Where(m => m.ProjectId == id).ToList();
        return true;
    }

    public async Task<IActionResult> OnGetAsync(string id, string? tab,
        string? promptJournal, string? promptType, string? openEntry)
    {
        if (!await LoadProjectAsync(id)) return NotFound();
        ActiveTab   = tab ?? "journal";
        OpenEntryId = openEntry;

        // Journal-Prompt: Item-Titel für Anzeige im Modal ermitteln
        if (!string.IsNullOrEmpty(promptJournal) && CanEdit)
        {
            PromptJournalId   = promptJournal;
            PromptJournalType = promptType;
            if (promptType == "todo")
            {
                var t = await _todos.GetByIdAsync(promptJournal);
                PromptJournalTitle = t != null ? $"Aufgabe #{t.TaskNr}: {t.Thema}" : null;
            }
            else
            {
                var m = await _meetings.GetByIdAsync(promptJournal);
                PromptJournalTitle = m != null ? $"Sitzung #{m.MeetingNr}: {m.Thema}" : null;
            }
        }
        return Page();
    }

    // ── Journal ──────────────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostAddJournalAsync(string id, string titel, string content)
    {
        if (!await LoadProjectAsync(id)) return NotFound();
        if (!CanEdit) return Forbid();
        await _projects.AddJournalEntryAsync(id, titel, content, User.Identity?.Name ?? "");
        return RedirectToPage(new { id, tab = "journal" });
    }

    // Verknüpften Journal-Eintrag anlegen (aus Prompt-Modal)
    public async Task<IActionResult> OnPostAddLinkedJournalAsync(
        string id, string linkedItemId, string promptType)
    {
        if (!await LoadProjectAsync(id)) return NotFound();
        if (!CanEdit) return Forbid();

        string titel;
        string? linkedTodoId    = null;
        string? linkedMeetingId = null;

        if (promptType == "todo")
        {
            var t = await _todos.GetByIdAsync(linkedItemId);
            titel        = t != null ? $"Aufgabe #{t.TaskNr}: {t.Thema}" : "Aufgabe";
            linkedTodoId = linkedItemId;
        }
        else
        {
            var m = await _meetings.GetByIdAsync(linkedItemId);
            titel           = m != null ? $"Sitzung #{m.MeetingNr}: {m.Thema}" : "Sitzung";
            linkedMeetingId = linkedItemId;
        }

        var entry = await _projects.AddLinkedJournalEntryAsync(
            id, titel, User.Identity?.Name ?? "", linkedTodoId, linkedMeetingId);

        return RedirectToPage(new { id, tab = "journal", openEntry = entry?.Id });
    }

    public async Task<IActionResult> OnPostUpdateJournalAsync(string id, string entryId, string titel, string content)
    {
        if (!await LoadProjectAsync(id)) return NotFound();
        if (!CanEdit) return Forbid();
        await _projects.UpdateJournalEntryAsync(id, entryId, titel, content, User.Identity?.Name ?? "");
        return RedirectToPage(new { id, tab = "journal" });
    }

    public async Task<IActionResult> OnPostDeleteJournalAsync(string id, string entryId)
    {
        if (!await LoadProjectAsync(id)) return NotFound();
        if (!CanEdit && !IsAdmin) return Forbid();
        await _projects.DeleteJournalEntryAsync(id, entryId);
        return RedirectToPage(new { id, tab = "journal" });
    }

    // ── Comments ─────────────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostAddCommentAsync(string id, string entryId, string text)
    {
        if (!await LoadProjectAsync(id)) return NotFound();
        await _projects.AddCommentAsync(id, entryId, text, User.Identity?.Name ?? "");
        return RedirectToPage(new { id, tab = "journal" });
    }

    public async Task<IActionResult> OnPostDeleteCommentAsync(string id, string entryId, string commentId)
    {
        if (!await LoadProjectAsync(id)) return NotFound();
        if (!CanEdit && !IsAdmin) return Forbid();
        await _projects.DeleteCommentAsync(id, entryId, commentId);
        return RedirectToPage(new { id, tab = "journal" });
    }

    // ── Assign / Remove Todo ─────────────────────────────────────────────────

    public async Task<IActionResult> OnPostAssignTodoAsync(string id, string todoId)
    {
        if (!await LoadProjectAsync(id)) return NotFound();
        if (!CanEdit) return Forbid();
        var todo = await _todos.GetByIdAsync(todoId);
        if (todo != null)
        {
            todo.ProjectId = id;
            await _todos.UpdateAsync(todo, new List<IFormFile>());
        }
        return RedirectToPage(new { id, tab = "todos", promptJournal = todoId, promptType = "todo" });
    }

    public async Task<IActionResult> OnPostRemoveTodoAsync(string id, string todoId)
    {
        if (!await LoadProjectAsync(id)) return NotFound();
        if (!CanEdit) return Forbid();

        // Verknüpfte Journal-Einträge entfernen
        var toDelete = Project.Journal
            .Where(e => e.LinkedTodoId == todoId)
            .Select(e => e.Id).ToList();
        foreach (var entryId in toDelete)
            await _projects.DeleteJournalEntryAsync(id, entryId);

        var todo = await _todos.GetByIdAsync(todoId);
        if (todo != null)
        {
            todo.ProjectId = null;
            await _todos.UpdateAsync(todo, new List<IFormFile>());
        }
        return RedirectToPage(new { id, tab = "todos" });
    }

    // ── Toggle Erledigt (Todo) ────────────────────────────────────────────────

    public async Task<IActionResult> OnPostToggleTodoAsync(string id, string todoId)
    {
        if (!await LoadProjectAsync(id)) return NotFound();
        if (!CanEdit) return Forbid();
        await _todos.ToggleErledigtAsync(todoId, User.Identity?.Name ?? "");
        return RedirectToPage(new { id, tab = "todos" });
    }

    // ── Assign / Remove Meeting ───────────────────────────────────────────────

    public async Task<IActionResult> OnPostAssignMeetingAsync(string id, string meetingId)
    {
        if (!await LoadProjectAsync(id)) return NotFound();
        if (!CanEdit) return Forbid();
        var meeting = await _meetings.GetByIdAsync(meetingId);
        if (meeting != null)
        {
            meeting.ProjectId = id;
            await _meetings.UpdateAsync(meeting, new List<IFormFile>());
        }
        return RedirectToPage(new { id, tab = "meetings", promptJournal = meetingId, promptType = "meeting" });
    }

    public async Task<IActionResult> OnPostRemoveMeetingAsync(string id, string meetingId)
    {
        if (!await LoadProjectAsync(id)) return NotFound();
        if (!CanEdit) return Forbid();

        // Verknüpfte Journal-Einträge entfernen
        var toDelete = Project.Journal
            .Where(e => e.LinkedMeetingId == meetingId)
            .Select(e => e.Id).ToList();
        foreach (var entryId in toDelete)
            await _projects.DeleteJournalEntryAsync(id, entryId);

        var meeting = await _meetings.GetByIdAsync(meetingId);
        if (meeting != null)
        {
            meeting.ProjectId = null;
            await _meetings.UpdateAsync(meeting, new List<IFormFile>());
        }
        return RedirectToPage(new { id, tab = "meetings" });
    }
}
