using System.Security.Cryptography;
using System.Text;

namespace MyCMS2026.Services;

/// <summary>
/// Erzeugt zeitlich begrenzte, HMAC-SHA256-signierte Links zum PCC-Statistik-Dashboard
/// (myalbatros.ch/Dashboards), damit eingeloggte MyCMS-Nutzer sich dort nicht
/// nochmals anmelden müssen.
///
/// Konfiguration in appsettings.Production.json (nicht in Git):
///   "PccLink": {
///     "BaseUrl": "https://myalbatros.ch/Dashboards/",
///     "Secret":  "SHARED_SECRET_HIER"
///   }
///
/// Das Secret muss identisch in Dashboard/auth.php (PHP-Konstante PCC_LINK_SECRET)
/// hinterlegt sein. Ohne konfiguriertes Secret (Entwicklung) erscheint die normale
/// Login-Maske des Dashboards.
///
/// Das Token ist ca. 2 Minuten gültig und wird nach erfolgreichem Login sofort
/// aus der URL entfernt, damit es nicht im Browser-Verlauf landet.
/// </summary>
public class PccLinkService
{
    private readonly IConfiguration _config;

    public PccLinkService(IConfiguration config) => _config = config;

    public string BuildLink()
    {
        var baseUrl = _config["PccLink:BaseUrl"] ?? "https://myalbatros.ch/Dashboards/";
        var secret  = _config["PccLink:Secret"];

        if (string.IsNullOrWhiteSpace(secret))
            return baseUrl; // kein Secret konfiguriert → normale Login-Maske

        var ts    = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var token = ComputeToken(ts, secret);
        var sep   = baseUrl.Contains('?') ? "&" : "?";
        return $"{baseUrl}{sep}pcc_ts={ts}&pcc_token={token}";
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_config["PccLink:Secret"]);

    private static string ComputeToken(long ts, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(ts.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
