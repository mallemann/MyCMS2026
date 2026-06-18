using System.Text.Json;
using MyCMS2026.Models;

namespace MyCMS2026.Services;

public class RoleService
{
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<Role>? _cache;

    // Standardrollen die beim ersten Start angelegt werden
    private static readonly List<Role> DefaultRoles =
    [
        new() { Name = "Member",        Description = "Normales Mitglied – hat Lesezugriff auf alle freigegebenen Seiten.",                        SortOrder = 10 },
        new() { Name = "Administrator", Description = "Systemadministrator – darf Benutzer, Navigation und Site-Konfiguration verwalten.",          SortOrder = 99 },
    ];

    public RoleService(IWebHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "roles.json");
    }

    private async Task<List<Role>> LoadAsync()
    {
        if (_cache is not null) return _cache;

        if (!File.Exists(_path))
        {
            _cache = DefaultRoles.Select(r => new Role
            {
                Id          = Guid.NewGuid().ToString(),
                Name        = r.Name,
                Description = r.Description,
                SortOrder   = r.SortOrder
            }).ToList();
            await SaveAsync(_cache);
            return _cache;
        }

        var json = await File.ReadAllTextAsync(_path);
        _cache = JsonSerializer.Deserialize<List<Role>>(json) ?? [];
        return _cache;
    }

    private async Task SaveAsync(List<Role> roles)
    {
        var json = JsonSerializer.Serialize(roles, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_path, json);
        _cache = roles;
    }

    public async Task<List<Role>> GetAllAsync()
    {
        await _lock.WaitAsync();
        try   { return (await LoadAsync()).OrderBy(r => r.SortOrder).ThenBy(r => r.Name).ToList(); }
        finally { _lock.Release(); }
    }

    public async Task<List<string>> GetNamesAsync()
        => (await GetAllAsync()).Select(r => r.Name).ToList();

    public async Task CreateAsync(Role role)
    {
        await _lock.WaitAsync();
        try
        {
            var list = await LoadAsync();
            role.Id = Guid.NewGuid().ToString();
            list.Add(role);
            await SaveAsync(list);
        }
        finally { _lock.Release(); }
    }

    public async Task UpdateAsync(Role updated)
    {
        await _lock.WaitAsync();
        try
        {
            var list = await LoadAsync();
            var idx  = list.FindIndex(r => r.Id == updated.Id);
            if (idx >= 0) list[idx] = updated;
            await SaveAsync(list);
        }
        finally { _lock.Release(); }
    }

    public async Task DeleteAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            var list = await LoadAsync();
            list.RemoveAll(r => r.Id == id);
            await SaveAsync(list);
        }
        finally { _lock.Release(); }
    }
}
