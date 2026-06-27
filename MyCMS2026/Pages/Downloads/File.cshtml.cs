using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Downloads;

[Authorize]
public class DownloadFileModel : PageModel
{
    private readonly DownloadService _downloads;
    public DownloadFileModel(DownloadService downloads) => _downloads = downloads;

    public async Task<IActionResult> OnGetAsync(string storedName, string? fileName = null)
    {
        if (string.IsNullOrEmpty(storedName)) return NotFound();

        var item = await _downloads.GetByStoredNameAsync(storedName);
        if (item == null) return NotFound();

        var path = _downloads.GetFilePath(storedName);
        if (!System.IO.File.Exists(path)) return NotFound();

        var ext = Path.GetExtension(storedName).ToLowerInvariant();
        FileHelper.SetContentDisposition(Response, item.OriginalName, ext);
        return PhysicalFile(path, _downloads.GetMimeType(storedName));
    }
}
