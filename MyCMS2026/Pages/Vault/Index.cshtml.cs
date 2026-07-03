using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Vault;

[Authorize]
public class VaultIndexModel : PageModel
{
    private readonly VaultService _vault;
    private readonly NavigationService _nav;

    public VaultIndexModel(VaultService vault, NavigationService nav)
    {
        _vault = vault;
        _nav   = nav;
    }

    public List<VaultFolder> Folders { get; private set; } = new();
    public string? Gruppe  { get; private set; }
    public string? Search  { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? gruppe, string? search)
    {
        if (!await CanAccessAsync(gruppe)) return Forbid();

        Gruppe = gruppe;
        Search = search;
        await LoadAsync();
        return Page();
    }

    // ── Datei hochladen ──────────────────────────────────────────────────

    public async Task<IActionResult> OnPostUploadAsync(IFormFile uploadFile, string? description, string? subfolder, string? gruppe)
    {
        // Hochladen erfordert erweiterte Rechte auf der zugehörigen Vault-Seite
        if (!await CanAccessAsync(gruppe, requireExtended: true)) return Forbid();

        if (uploadFile != null && uploadFile.Length > 0)
            await _vault.UploadAsync(gruppe ?? "", uploadFile, description ?? "", User.Identity?.Name ?? "", subfolder ?? "");
        return RedirectToPage(new { gruppe = NullIfEmpty(gruppe) });
    }

    // ── Datei löschen ────────────────────────────────────────────────────

    public IActionResult OnPostDelete(string fileName, string? subfolder, string? gruppe)
    {
        if (!User.IsInRole("Administrator")) return Forbid();
        _vault.DeleteFile(gruppe ?? "", fileName, subfolder ?? "");
        return RedirectToPage(new { gruppe = NullIfEmpty(gruppe) });
    }

    // ── Datei umbenennen ─────────────────────────────────────────────────

    public IActionResult OnPostRename(string fileName, string newName, string? subfolder, string? gruppe)
    {
        if (!User.IsInRole("Administrator")) return Forbid();
        _vault.RenameFile(gruppe ?? "", fileName, newName ?? fileName, subfolder ?? "");
        return RedirectToPage(new { gruppe = NullIfEmpty(gruppe) });
    }

    // ── Beschreibung aktualisieren ───────────────────────────────────────

    public IActionResult OnPostUpdateDescription(string fileName, string? description, string? subfolder, string? gruppe)
    {
        if (!User.IsInRole("Administrator")) return Forbid();
        _vault.UpdateDescription(gruppe ?? "", fileName, description ?? "", subfolder ?? "");
        return RedirectToPage(new { gruppe = NullIfEmpty(gruppe) });
    }

    // ── Ordner erstellen ─────────────────────────────────────────────────

    public IActionResult OnPostCreateFolder(string folderName, string? gruppe)
    {
        if (!User.IsInRole("Administrator")) return Forbid();
        _vault.CreateFolder(gruppe ?? "", folderName ?? "");
        return RedirectToPage(new { gruppe = NullIfEmpty(gruppe) });
    }

    // ── Ordner löschen ───────────────────────────────────────────────────

    public IActionResult OnPostDeleteFolder(string folderName, string? gruppe)
    {
        if (!User.IsInRole("Administrator")) return Forbid();
        _vault.DeleteFolder(gruppe ?? "", folderName ?? "");
        return RedirectToPage(new { gruppe = NullIfEmpty(gruppe) });
    }

    // ── intern ───────────────────────────────────────────────────────────

    /// <summary>Gruppenzugriff anhand der Nav-Berechtigungen (wVault-Einträge) prüfen.</summary>
    private Task<bool> CanAccessAsync(string? gruppe, bool requireExtended = false)
    {
        var roles = User.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value);
        return _nav.CanAccessVaultGruppeAsync(gruppe, roles, requireExtended);
    }

    private async Task LoadAsync()
    {
        await Task.CompletedTask;
        var folders = _vault.GetFolders(Gruppe ?? "");

        if (!string.IsNullOrWhiteSpace(Search))
        {
            foreach (var f in folders)
                f.Files = f.Files.Where(fi =>
                    fi.FileName.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
                    fi.Description.Contains(Search, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        Folders = folders;
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
