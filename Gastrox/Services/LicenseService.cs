using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Gastrox.Services;

public record LicenseInfo(
    bool IsValid,
    string? Type,
    string? Expires,
    string? Customer,
    string? Error);

/// <summary>
/// Služba pro validaci licence proti serveru gastrox.helptech.app.
/// Výsledek se cachuje v DB (Nastaveni) pro offline provoz.
/// </summary>
public static class LicenseService
{
    private const string ApiUrl = "https://gastrox.helptech.app/app/verify.php";
    public const int DemoMaxKaret = 20;

    /// <summary>Aktuální stav licence (z cache). Null = ještě neověřeno.</summary>
    public static LicenseInfo? Current { get; private set; }

    /// <summary>True pokud je licence platná a aktivní.</summary>
    public static bool IsLicensed => Current?.IsValid == true;

    /// <summary>
    /// Načte uložený stav licence z DB (pro offline provoz).
    /// Volá se při startu aplikace.
    /// </summary>
    public static void LoadCachedState()
    {
        var n = DatabaseService.LoadNastaveni();
        var valid = n.TryGetValue("license.valid", out var v) && v == "1";
        var type = n.TryGetValue("license.type", out var t) ? t : null;
        var expires = n.TryGetValue("license.expires", out var ex) ? ex : null;
        var customer = n.TryGetValue("license.customer", out var c) ? c : null;

        // Kontrola expirace i offline
        if (valid && !string.IsNullOrEmpty(expires))
        {
            if (DateTime.TryParse(expires, out var expDate) && expDate < DateTime.Now)
                valid = false;
        }

        Current = new LicenseInfo(valid, type, expires, customer, null);
    }

    /// <summary>
    /// Ověří licenční klíč proti serveru a uloží výsledek do DB.
    /// </summary>
    public static async Task<LicenseInfo> ValidateAsync(string licenseKey)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("Gastrox", UpdateService.CurrentVersion));

            var body = JsonSerializer.Serialize(new { key = licenseKey });
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            var response = await http.PostAsync(ApiUrl, content);
            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var isValid = root.TryGetProperty("valid", out var vp) && vp.GetBoolean();
            var type = root.TryGetProperty("type", out var tp) ? tp.GetString() : null;
            var expires = root.TryGetProperty("expires", out var ep) ? ep.GetString() : null;
            var customer = root.TryGetProperty("customer", out var cp) ? cp.GetString() : null;
            var error = root.TryGetProperty("error", out var errp) ? errp.GetString() : null;

            var info = new LicenseInfo(isValid, type, expires, customer, error);
            SaveToCache(licenseKey, info);
            Current = info;
            return info;
        }
        catch (Exception ex)
        {
            return new LicenseInfo(false, null, null, null, $"Nelze se spojit se serverem: {ex.Message}");
        }
    }

    /// <summary>Odstraní licenci (deaktivace).</summary>
    public static void ClearLicense()
    {
        Current = new LicenseInfo(false, null, null, null, null);
        DatabaseService.SaveNastaveniBulk(new System.Collections.Generic.Dictionary<string, string?>
        {
            ["license.key"]      = null,
            ["license.valid"]    = "0",
            ["license.type"]     = null,
            ["license.expires"]  = null,
            ["license.customer"] = null,
        });
    }

    private static void SaveToCache(string key, LicenseInfo info)
    {
        DatabaseService.SaveNastaveniBulk(new System.Collections.Generic.Dictionary<string, string?>
        {
            ["license.key"]      = key,
            ["license.valid"]    = info.IsValid ? "1" : "0",
            ["license.type"]     = info.Type,
            ["license.expires"]  = info.Expires,
            ["license.customer"] = info.Customer,
        });
    }
}
