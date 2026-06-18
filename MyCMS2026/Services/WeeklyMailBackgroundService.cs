namespace MyCMS2026.Services;

public class WeeklyMailBackgroundService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<WeeklyMailBackgroundService> _log;

    public WeeklyMailBackgroundService(IServiceProvider sp, ILogger<WeeklyMailBackgroundService> log)
    {
        _sp  = sp;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _log.LogInformation("WeeklyMailBackgroundService gestartet.");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await CheckAndSendAsync();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Fehler im WeeklyMailBackgroundService.");
            }

            // Nächste volle Stunde abwarten
            var now   = DateTime.Now;
            var next  = now.AddHours(1).Date.AddHours(now.AddHours(1).Hour);
            var delay = next - now;
            if (delay <= TimeSpan.Zero) delay = TimeSpan.FromMinutes(30);

            await Task.Delay(delay, ct);
        }
    }

    private async Task CheckAndSendAsync()
    {
        // Nur montags zwischen 07:00 und 07:59
        var now = DateTime.Now;
        if (now.DayOfWeek != DayOfWeek.Monday || now.Hour != 7)
            return;

        // Singletons direkt aus DI holen
        var siteSvc   = _sp.GetRequiredService<SiteService>();
        var mailSvc   = _sp.GetRequiredService<WeeklyMailService>();

        var config = await siteSvc.GetAsync();
        if (!config.WeeklyMailEnabled)
            return;

        var mailCfg = await mailSvc.GetConfigAsync();

        // Schon diese Woche gesendet?
        if (mailCfg.LastSentAt.HasValue)
        {
            var lastSentWeek = System.Globalization.ISOWeek.GetWeekOfYear(mailCfg.LastSentAt.Value);
            var currentWeek  = System.Globalization.ISOWeek.GetWeekOfYear(now);
            if (lastSentWeek == currentWeek && mailCfg.LastSentAt.Value.Year == now.Year)
            {
                _log.LogDebug("Weekly Mail diese Woche bereits gesendet ({LastSentAt}).", mailCfg.LastSentAt);
                return;
            }
        }

        _log.LogInformation("Weekly Mail wird gesendet...");
        await mailSvc.SendWeeklyAsync();
    }
}
