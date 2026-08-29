using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TorobScanner.Services;

/// <summary>نتیجه بررسی بروزرسانی</summary>
public class UpdateInfo
{
    public bool UpdateAvailable { get; init; }
    public string LatestVersion { get; init; } = "";
    public string CurrentVersion { get; init; } = "";
    public string ReleaseNotes { get; init; } = "";
    public string DownloadUrl { get; init; } = "";
    public string AssetName { get; init; } = "";
    public long AssetSize { get; init; }
}

/// <summary>
/// سرویس بروزرسانی خودکار از GitHub Releases:
/// ۱) بررسی آخرین نسخه از API گیت‌هاب و مقایسه با نسخه فعلی برنامه
/// ۲) دانلود فایل zip انتشار (با گزارش پیشرفت)
/// ۳) استخراج + ساخت اسکریپت آپدیت که بعد از بسته‌شدن برنامه،
///    فایل‌های جدید را جایگزین و برنامه را دوباره اجرا می‌کند
/// </summary>
public class UpdateService
{
    private const string RepoOwner = "ArtinSJ";
    private const string RepoName = "Price-Scanner";
    private const string ApiLatestRelease = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

    private static readonly HttpClient _http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient(new HttpClientHandler
        {
            // سازگاری با شرایط شبکه ایران — همانند فلسفه BrowserLauncher
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TorobIntelligence", CurrentVersion()));
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        c.Timeout = TimeSpan.FromMinutes(10);
        return c;
    }

    /// <summary>نسخه فعلی برنامه (از AssemblyVersion در csproj)</summary>
    public static string CurrentVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
        return $"{v.Major}.{v.Minor}.{v.Build}";
    }

    /// <summary>بررسی آخرین Release گیت‌هاب و مقایسه نسخه</summary>
    public async Task<UpdateInfo> CheckForUpdateAsync(CancellationToken ct = default)
    {
        var current = CurrentVersion();

        using var resp = await _http.GetAsync(ApiLatestRelease, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return new UpdateInfo { UpdateAvailable = false, CurrentVersion = current, LatestVersion = current, ReleaseNotes = "هنوز هیچ نسخه‌ای منتشر نشده است." };
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
        var latestStr = tag.TrimStart('v', 'V');
        var notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";

        // پیدا کردن اولین asset از نوع zip
        string dlUrl = "", assetName = "";
        long assetSize = 0;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.GetProperty("name").GetString() ?? "";
                if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    dlUrl = a.GetProperty("browser_download_url").GetString() ?? "";
                    assetName = name;
                    assetSize = a.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0;
                    break;
                }
            }
        }

        bool newer = IsNewer(latestStr, current) && !string.IsNullOrEmpty(dlUrl);
        return new UpdateInfo
        {
            UpdateAvailable = newer,
            LatestVersion = string.IsNullOrEmpty(latestStr) ? current : latestStr,
            CurrentVersion = current,
            ReleaseNotes = notes,
            DownloadUrl = dlUrl,
            AssetName = assetName,
            AssetSize = assetSize
        };
    }

    private static bool IsNewer(string latest, string current)
    {
        return Version.TryParse(Normalize(latest), out var l)
            && Version.TryParse(Normalize(current), out var c)
            && l > c;

        static string Normalize(string s)
        {
            // "2.4" → "2.4.0" | حذف پسوندهای -beta و ...
            var core = s.Split('-', '+')[0].Trim();
            var parts = core.Split('.');
            return parts.Length switch
            {
                1 => core + ".0.0",
                2 => core + ".0",
                _ => string.Join('.', parts.Take(4))
            };
        }
    }

    /// <summary>دانلود فایل بروزرسانی با گزارش پیشرفت (درصد)</summary>
    public async Task<string> DownloadUpdateAsync(UpdateInfo info, IProgress<int>? progress, CancellationToken ct = default)
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "TorobScannerUpdate");
        if (Directory.Exists(tmpDir)) { try { Directory.Delete(tmpDir, true); } catch { } }
        Directory.CreateDirectory(tmpDir);

        var zipPath = Path.Combine(tmpDir, info.AssetName);

        using var resp = await _http.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? info.AssetSize;

        await using (var src = await resp.Content.ReadAsStreamAsync(ct))
        await using (var dst = File.Create(zipPath))
        {
            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await src.ReadAsync(buffer, ct)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, n), ct);
                read += n;
                if (total > 0) progress?.Report((int)(read * 100 / total));
            }
        }

        // استخراج
        var extractDir = Path.Combine(tmpDir, "extracted");
        ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

        // اگر zip یک پوشه ریشه دارد (مثلاً publish/) وارد آن شو
        var exeName = Path.GetFileName(Environment.ProcessPath ?? "TorobScanner.exe");
        var sourceDir = FindPayloadDir(extractDir, exeName) ?? extractDir;
        return sourceDir;
    }

    private static string? FindPayloadDir(string root, string exeName)
    {
        if (File.Exists(Path.Combine(root, exeName)) || Directory.GetFiles(root, "*.dll").Length > 0)
            return root;
        foreach (var d in Directory.GetDirectories(root))
        {
            var found = FindPayloadDir(d, exeName);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// اعمال بروزرسانی: اسکریپت cmd ساخته می‌شود که بعد از خروج برنامه،
    /// فایل‌های جدید را روی پوشه برنامه کپی و برنامه را دوباره اجرا می‌کند.
    /// (دیتابیس و لاگ‌ها دست نمی‌خورند — فقط فایل‌های برنامه جایگزین می‌شوند)
    /// این متد برنامه را می‌بندد.
    /// </summary>
    public void ApplyUpdateAndRestart(string sourceDir)
    {
        var appDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
        var exePath = Environment.ProcessPath ?? Path.Combine(appDir, "TorobScanner.exe");
        var pid = Environment.ProcessId;
        var script = Path.Combine(Path.GetTempPath(), "TorobScannerUpdate", "apply_update.cmd");

        // ⚠️ دیتابیس/لاگ عمداً exclude — کاربر دیتایش را از دست ندهد
        var cmd = $@"@echo off
chcp 65001 >nul
title Torob Intelligence - Updating...
echo در حال بروزرسانی Torob Intelligence...
:waitloop
tasklist /FI ""PID eq {pid}"" 2>nul | find ""{pid}"" >nul
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto waitloop
)
robocopy ""{sourceDir}"" ""{appDir}"" /E /XF *.db *.db-wal *.db-shm error_log*.txt /R:3 /W:1 >nul
if errorlevel 8 (
    echo خطا در کپی فایل‌ها!
    pause
    exit /b 1
)
start """" ""{exePath}""
del ""%~f0""
";
        File.WriteAllText(script, cmd, System.Text.Encoding.UTF8);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{script}\"",
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = true,
            CreateNoWindow = true
        });

        System.Windows.Application.Current.Shutdown();
    }
}
