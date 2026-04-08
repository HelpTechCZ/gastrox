using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Gastrox.Services;

public record UpdateInfo(
    string Version,
    string TagName,
    string ReleaseUrl,
    string DownloadUrl,
    string ReleaseNotes);

/// <summary>
/// Služba pro kontrolu aktualizací proti GitHub Releases API.
/// Endpoint: https://api.github.com/repos/HelpTechCZ/gastrox/releases/latest
/// </summary>
public static class UpdateService
{
    private const string Owner = "HelpTechCZ";
    private const string Repo  = "gastrox";
    private const string ApiUrl = "https://api.github.com/repos/" + Owner + "/" + Repo + "/releases/latest";

    public static string CurrentVersion
    {
        get
        {
            var asm = Assembly.GetExecutingAssembly();
            var ver = asm.GetName().Version;
            return ver is null ? "0.0.0" : $"{ver.Major}.{ver.Minor}.{ver.Build}";
        }
    }

    public static async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("Gastrox", CurrentVersion));
            http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            var json = await http.GetStringAsync(ApiUrl);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString() ?? "";
            var htmlUrl = root.GetProperty("html_url").GetString() ?? "";
            var body    = root.TryGetProperty("body", out var b) ? (b.GetString() ?? "") : "";

            var cleanVer = tagName.TrimStart('v', 'V');
            if (!IsNewer(cleanVer, CurrentVersion))
                return null;

            // Najdi první ZIP asset
            string? downloadUrl = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
                return null;

            return new UpdateInfo(cleanVer, tagName, htmlUrl, downloadUrl, body);
        }
        catch
        {
            // Kontrola aktualizací nesmí shodit aplikaci – offline, rate limit atd.
            return null;
        }
    }

    /// <summary>
    /// Stáhne ZIP do temp, rozbalí vedle .exe do podsložky "update",
    /// a vygeneruje updater .bat, který po ukončení hlavní aplikace
    /// překopíruje nové soubory přes staré a spustí novou verzi.
    /// </summary>
    public static async Task<string> DownloadAndPrepareAsync(UpdateInfo info)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("Gastrox", CurrentVersion));

        var tempZip = Path.Combine(Path.GetTempPath(), $"gastrox-update-{info.Version}.zip");
        var tempDir = Path.Combine(Path.GetTempPath(), $"gastrox-update-{info.Version}");
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);

        var bytes = await http.GetByteArrayAsync(info.DownloadUrl);
        File.WriteAllBytes(tempZip, bytes);
        ZipFile.ExtractToDirectory(tempZip, tempDir);

        return tempDir;
    }

    /// <summary>
    /// Vygeneruje .bat, který počká na ukončení Gastrox.exe, překopíruje
    /// soubory z temp složky do instalačního adresáře a aplikaci znovu spustí.
    /// </summary>
    public static void LaunchUpdaterAndExit(string extractedUpdateDir)
    {
        var installDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
        var exeName = Path.GetFileName(Process.GetCurrentProcess().MainModule?.FileName ?? "Gastrox.exe");
        var batPath = Path.Combine(Path.GetTempPath(), $"gastrox-updater-{Guid.NewGuid():N}.bat");

        var bat = $@"@echo off
echo Aktualizace Gastroxu...
timeout /t 2 /nobreak >nul
:wait
tasklist /fi ""imagename eq {exeName}"" 2>nul | find /i ""{exeName}"" >nul
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto wait
)
xcopy /E /Y /I ""{extractedUpdateDir}\*"" ""{installDir}\""
start """" ""{installDir}\{exeName}""
del ""%~f0""
";
        File.WriteAllText(batPath, bat);
        Process.Start(new ProcessStartInfo
        {
            FileName = batPath,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    private static bool IsNewer(string remote, string local)
    {
        if (Version.TryParse(NormalizeVersion(remote), out var r) &&
            Version.TryParse(NormalizeVersion(local),  out var l))
            return r > l;
        return false;
    }

    private static string NormalizeVersion(string s)
    {
        var parts = s.Split('.').Select(p => p.Trim()).ToArray();
        // Version vyžaduje min. 2 části, doplnit patch/build na 0
        if (parts.Length == 1) return $"{parts[0]}.0.0";
        if (parts.Length == 2) return $"{parts[0]}.{parts[1]}.0";
        return string.Join('.', parts.Take(4));
    }
}
