using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class ActivityModel : PageModel
{
    private readonly ActivityService _activity;

    public ActivityModel(ActivityService activity) => _activity = activity;

    public List<ActivityEntry> Entries { get; set; } = new();

    public async Task OnGetAsync() =>
        Entries = await _activity.GetAllAsync();
}
