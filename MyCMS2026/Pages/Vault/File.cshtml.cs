using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Vault;

[Authorize]
public class VaultFileModel : PageModel
{
    private readonly VaultService _vault;
    private readonly NavigationService _nav;

    public VaultFileModel(VaultService vault, NavigationService nav)
    {
        _vault = vault;
        _nav   = nav;
    }

    public async Task<IActionResult> OnGetAsync(string gruppe, string fileName, string? subfolder)
    {
        if (string.IsNullOrEmpty(fileName)) return NotFound();

        // Gruppenzugriff anhand der Nav-Berechtigungen (wVault-Einträge) prüfen
        var roles = User.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value);
        if (!await _nav.CanAccessVaultGruppeAsync(gruppe, roles)) return Forbid();

        var path = _vault.GetFilePath(gruppe ?? "", fileName, subfolder ?? "");
        if (!System.IO.File.Exists(path)) return NotFound();

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        FileHelper.SetContentDisposition(Response, fileName, ext);
        return PhysicalFile(path, _vault.GetMimeType(fileName));
    }
}
