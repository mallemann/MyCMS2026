using System.Text.Json;
using MyCMS2026.Models;

namespace MyCMS2026.Services;

public class SiteService
{
    private readonly string _siteFile;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private SiteConfig? _cache;

    public SiteService(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _siteFile = Path.Combine(dataDir, "site.json");
        EnsureDefaults();
    }

    private void EnsureDefaults()
    {
        if (!File.Exists(_siteFile))
        {
            var defaults = new SiteConfig { Title = "MyCMS", Status = "Active" };
            var json = JsonSerializer.Serialize(defaults, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_siteFile, json);
        }
    }

    public async Task<SiteConfig> GetAsync()
    {
        if (_cache != null) return _cache;
        await _lock.WaitAsync();
        try
        {
            if (_cache != null) return _cache;
            var json = await File.ReadAllTextAsync(_siteFile);
            _cache = JsonSerializer.Deserialize<SiteConfig>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            return _cache;
        }
        finally { _lock.Release(); }
    }

    public async Task SaveAsync(SiteConfig config)
    {
        await _lock.WaitAsync();
        try
        {
            _cache = config;
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_siteFile, json);
        }
        finally { _lock.Release(); }
    }

    public void InvalidateCache() => _cache = null;
}
