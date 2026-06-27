using Microsoft.AspNetCore.Http;

namespace MyCMS2026.Pages;

/// <summary>
/// Setzt den Content-Disposition Header für File-Downloads.
/// Inline für Bilder (öffnet direkt in Safari), Attachment für PDFs und Office-Dokumente.
/// PDFs als Attachment: iOS öffnet via QuickLook, beim Teilen kommt nur die Datei (kein URL) in die Mail.
/// Beide Varianten bekommen filename= (ASCII-Fallback) und filename*= (RFC 5987, Umlaute).
/// OnStarting stellt sicher, dass unser Header zuletzt gesetzt wird.
/// </summary>
public static class FileHelper
{
    private static readonly HashSet<string> InlineTypes =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".txt" };

    public static void SetContentDisposition(HttpResponse response, string originalName, string ext)
    {
        var disposition = InlineTypes.Contains(ext) ? "inline" : "attachment";

        // ASCII-sicherer Fallback-Name (für ältere Clients / iOS Share-Sheet)
        var asciiName = string.Concat(originalName
            .Select(c => c < 128 && c != '"' && c != '\\' ? c : '_'));

        // RFC 5987 encoded (für korrekte Umlaute in modernen Browsern)
        var encodedName = Uri.EscapeDataString(originalName);

        var headerValue = $"{disposition}; filename=\"{asciiName}\"; filename*=UTF-8''{encodedName}";

        // OnStarting: wird direkt vor dem Senden des Response aufgerufen,
        // nach PhysicalFileResult — so wird unser Header nicht überschrieben.
        response.OnStarting(() =>
        {
            response.Headers["Content-Disposition"] = headerValue;
            return Task.CompletedTask;
        });
    }
}
