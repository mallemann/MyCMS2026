using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Vault;

[Authorize]
public class VaultFileModel : PageModel
{
    private readonly VaultService _vault;
    public VaultFileModel(VaultService vault) => _vault = vault;

    public IActionResult OnGet(string gruppe, string fileName, string? subfolder)
    {
        if (string.IsNullOrEmpty(fileName)) return NotFound();

        var path = _vault.GetFilePath(gruppe ?? "", fileName, subfolder ?? "");
        if (!System.IO.File.Exists(path)) return NotFound();

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        FileHelper.SetContentDisposition(Response, fileName, ext);
        return PhysicalFile(path, _vault.GetMimeType(fileName));
    }
}
