using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyCMS2026.Models;
using MyCMS2026.Services;

namespace MyCMS2026.Pages.Admin;

[Authorize(Roles = "Administrator")]
public class NavigationModel : PageModel
{
    private readonly NavigationService _nav;
    private readonly RoleService _roles;
    private readonly GruppenService _gruppen;
    private readonly UserService _users;
    private readonly IConfiguration _config;
    public NavigationModel(NavigationService nav, RoleService roles, GruppenService gruppen, UserService users, IConfiguration config)
    { _nav = nav; _roles = roles; _gruppen = gruppen; _users = users; _config = config; }

    public List<NavItem> Items { get; private set; } = new();
    public List<string> AvailableWidgets { get; private set; } = new();
    public List<string> AvailableRoles { get; private set; } = new();
    public List<string> AvailableGruppen { get; private set; } = new();
    public List<string> AvailableUsernames { get; private set; } = new();
    public string Message { get; private set; } = "";
    public bool IsError { get; private set; }

    public async Task OnGetAsync() => await LoadPageDataAsync();

    private async Task LoadPageDataAsync()
    {
        Items = await _nav.GetAllAsync();
        AvailableWidgets = LoadWidgets(_config.GetValue<bool>("AlbatrosEnabled"));
        AvailableRoles = await _roles.GetNamesAsync();
        AvailableGruppen = await _gruppen.GetAllAsync();
        AvailableUsernames = (await _users.GetAllAsync())
            .Where(u => u.IsActive && !string.IsNullOrEmpty(u.UserName))
            .Select(u => u.UserName).OrderBy(u => u).ToList();
    }

    private static List<string> LoadWidgets(bool albatrosEnabled)
    {
        var widgets = new List<string>
        {
            "wDashboard",
            "wDownloads",
            "wHTMLPage",
            "wHome",
            "wMeetingTimeline",
            "wMeetings",
            "wOKR",
            "wPendenzen",
            "wProjects",
            "wSearch",
            "wToDo",
            "wVault",
        };
        if (albatrosEnabled)
            widgets.Add("wPccLink");
        widgets.Sort();
        return widgets;
    }

    private NavItem BuildItem(string? id, string? parentId, string? title, string? navText,
        string? visRole, string? basicRole, string? extRole, string? widget, string? configStr, int menuOrder) => new()
    {
        Id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString() : id,
        ParentId = string.IsNullOrEmpty(parentId) ? null : parentId,
        Title = title?.Trim() ?? "",
        NavigationText = navText?.Trim() ?? "",
        VisibilityRole = visRole?.Trim() ?? "Member",
        BasicAccessRole = basicRole?.Trim() ?? "Member",
        ExtendedAccessRole = extRole?.Trim() ?? "Administrator",
        Widget = widget?.Trim() ?? "",
        ConfigString = configStr?.Trim() ?? "",
        MenuOrder = menuOrder
    };

    public async Task<IActionResult> OnPostCreateAsync(
        string title, string navText, string? parentId, string visRole,
        string basicRole, string extRole, string widget, string configStr, int menuOrder)
    {
        var item = BuildItem(null, parentId, title, navText, visRole, basicRole, extRole, widget, configStr, menuOrder);
        await _nav.CreateAsync(item);
        Message = $"Eintrag '{title}' erstellt.";
        await LoadPageDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAsync(
        string id, string title, string navText, string? parentId, string visRole,
        string basicRole, string extRole, string widget, string configStr, int menuOrder)
    {
        var item = BuildItem(id, parentId, title, navText, visRole, basicRole, extRole, widget, configStr, menuOrder);
        var ok = await _nav.UpdateAsync(item);
        Message = ok ? $"Eintrag '{title}' gespeichert." : "Eintrag nicht gefunden.";
        IsError = !ok;
        await LoadPageDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        // HTML-Datei mitlöschen wenn wHTMLPage-Widget
        var item = (await _nav.GetAllAsync()).FirstOrDefault(i => i.Id == id);
        if (item?.Widget == "wHTMLPage")
        {
            var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
            var htmlPath = System.IO.Path.Combine(env.ContentRootPath, "App_Data", "pages", id + ".html");
            if (System.IO.File.Exists(htmlPath))
                System.IO.File.Delete(htmlPath);
        }

        var ok = await _nav.DeleteAsync(id);
        Message = ok ? "Eintrag gelöscht." : "Eintrag nicht gefunden.";
        IsError = !ok;
        await LoadPageDataAsync();
        return Page();
    }
}
