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
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BazarSanj", CurrentVersion()));
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        c.Timeout = TimeSpan.FromMinutes(30);   // ✨ v3.2.2: ۱۰ دقیقه برای اینترنت ایران کم بود — دانلود نیمه‌کاره Timeout می‌شد
        return c;
    }

    /// <summary>
    /// ✨ v3.2.2: نوع نصب فعلی — اگر hostfxr.dll کنار exe باشد یعنی self-contained (پرتابل).
    /// آپدیتر قبلاً کورکورانه اولین zip (Lite) را برمی‌داشت — برای نصب پرتابل باید Portable بردارد.
    /// </summary>
    public static bool IsSelfContainedInstall()
        => File.Exists(Path.Combine(AppContext.BaseDirectory, "hostfxr.dll"));

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

        // ✨ v3.2.2: انتخاب هوشمند asset — Portable برای نصب پرتابل، Lite برای نصب سبک
        // (قبلاً همیشه اولین zip برداشته می‌شد و نصب پرتابل با Lite جایگزین ناقص می‌شد)
        string dlUrl = "", assetName = "";
        long assetSize = 0;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            bool selfContained = IsSelfContainedInstall();
            string want = selfContained ? "Portable" : "Lite";
            string? fallbackUrl = null, fallbackName = null;
            long fallbackSize = 0;
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.GetProperty("name").GetString() ?? "";
                if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                var url = a.GetProperty("browser_download_url").GetString() ?? "";
                var size = a.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0;

                if (name.Contains(want, StringComparison.OrdinalIgnoreCase))
                {
                    dlUrl = url; assetName = name; assetSize = size;
                    break;
                }
                fallbackUrl ??= url; fallbackName ??= name; fallbackSize = fallbackSize == 0 ? size : fallbackSize;
            }
            // اگر asset هم‌نام نوع نصب پیدا نشد → اولین zip موجود
            if (string.IsNullOrEmpty(dlUrl) && fallbackUrl != null)
            {
                dlUrl = fallbackUrl; assetName = fallbackName!; assetSize = fallbackSize;
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

    /// <summary>
    /// دانلود فایل بروزرسانی با گزارش پیشرفت (درصد) + ✨ v3.2.2: یک تلاش مجدد خودکار
    /// (اینترنت ناپایدار → دانلود نیمه‌کاره zip خراب می‌شد و استخراج استثنا می‌انداخت)
    /// </summary>
    public async Task<string> DownloadUpdateAsync(UpdateInfo info, IProgress<int>? progress, CancellationToken ct = default)
    {
        Exception? lastError = null;
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                return await DownloadOnceAsync(info, progress, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
                Logger.Error("Update/Download", info.AssetName, $"attempt {attempt} failed: {ex.Message}");
                if (attempt < 2) await Task.Delay(1500, ct);
            }
        }
        throw lastError!;
    }

    private async Task<string> DownloadOnceAsync(UpdateInfo info, IProgress<int>? progress, CancellationToken ct)
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
    /// ✨ v3.2.2 بازنویسی ضدباگ:
    ///  ۱) پروسسه‌های زامبی (node/chrome باقی‌مانده از اسکن) که از پوشه‌ی برنامه اجرا شده‌اند
    ///     قبل از کپی بسته می‌شوند — قفل فایل = دلیل اصلی «نه آپدیت می‌شود نه باز می‌شود»
    ///  ۲) شکست robocopy دیگر روی pause مخفی گیر نمی‌کند — لاگ می‌نویسد و برنامه را دوباره باز می‌کند
    ///  ۳) اسکریپت UTF-8 بدون BOM — BOM خط اول (@echo off) را خراب می‌کرد
    ///  ۴) sleep با ping — timeout در پنجره‌ی مخفی/بدون ورودی ممکن است شکست بخورد
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
title BazarSanj - Updating...
echo BazarSanj update in progress...
:waitloop
tasklist /FI ""PID eq {pid}"" 2>nul | find ""{pid}"" >nul
if not errorlevel 1 (
    ping -n 2 127.0.0.1 >nul
    goto waitloop
)
rem تاخیر اطمینان برای آزاد شدن دستگیره‌ی فایل‌ها
ping -n 3 127.0.0.1 >nul
rem بستن پروسسه‌های زامبی‌ای که از پوشه‌ی برنامه اجرا شده‌اند (node / chrome)
powershell -NoProfile -ExecutionPolicy Bypass -Command ""Get-Process | ForEach-Object {{ try {{ if ($_.Path -like '{appDir}*') {{ Stop-Process -Id $_.Id -Force }} }} catch {{}} }}"" 2>nul
robocopy ""{sourceDir}"" ""{appDir}"" /E /XF *.db *.db-wal *.db-shm error_log*.txt /R:5 /W:2 >nul
if errorlevel 8 (
    echo %date% %time% robocopy error >> ""{appDir}\update_error.log""
    start """" ""{exePath}""
    exit /b 1
)
start """" ""{exePath}""
del ""%~f0""
";
        // ✨ بدون BOM — BOM ابتدای اسکریپت cmd، خط اول را به فرمان نامعتبر تبدیل می‌کرد
        File.WriteAllText(script, cmd, new System.Text.UTF8Encoding(false));

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
