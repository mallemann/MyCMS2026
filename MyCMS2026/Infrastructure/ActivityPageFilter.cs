using Microsoft.AspNetCore.Mvc.Filters;
using MyCMS2026.Pages.Page;
using MyCMS2026.Services;

namespace MyCMS2026.Infrastructure;

/// <summary>
/// Razor-Page-Filter: trackt Besuche der Content-Seiten pro Benutzer und Tag.
/// - Nur authentifizierte Nicht-Administratoren
/// - Nur GET-Requests
/// - Nur Inhaltsseiten (PageIndexModel), keine Admin-/Account-Unterseiten
/// - Seitenname = NavItem.Title (direkt aus dem PageModel nach Handler-Ausführung)
/// </summary>
public class ActivityPageFilter : IAsyncPageFilter
{
    private readonly ActivityService _activity;

    public ActivityPageFilter(ActivityService activity) => _activity = activity;

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext ctx) =>
        Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext ctx,
        PageHandlerExecutionDelegate next)
    {
        await next();   // Seite zuerst ausführen, dann tracken

        // Nur GET-Requests tracken
        if (!string.Equals(ctx.HttpContext.Request.Method, "GET",
                StringComparison.OrdinalIgnoreCase)) return;

        var user = ctx.HttpContext.User?.Identity?.Name;
        if (string.IsNullOrEmpty(user)) return;

        // Administratoren werden nicht getrackt
        if (ctx.HttpContext.User.IsInRole("Administrator")) return;

        // Nur Content-Seiten (PageIndexModel) tracken.
        // NavItem.Title wird in OnGetAsync gesetzt – nach await next() direkt verfügbar.
        if (ctx.HandlerInstance is PageIndexModel indexModel &&
            !string.IsNullOrEmpty(indexModel.NavItem?.Title))
        {
            await _activity.RecordPageAsync(user, indexModel.NavItem.Title);
        }
    }
}
