using System;
using System.IO;

namespace TorobScanner.Services;

/// <summary>
/// لاگ خطای Thread-Safe با چرخش فایل:
/// ✅ رفع باگ ۱۰: چند تسک همزمان می‌توانند لاگ بنویسند بدون IOException
/// ✅ چرخش خودکار: فایل از ۵۱۲ کیلوبایت بزرگ‌تر نشود
/// ✨ v3.5 (اصلاح P0-۵ بازبینی): اگر پوشه‌ی برنامه اجازه‌ی نوشتن نداشته باشد
///    (مثل Program Files یا Desktop همگام‌شونده)، تا امروز لاگ «بی‌صدا» دور ریخته می‌شد
///    و کاربر/پشتیبانی هیچ ردپایی از خطا نداشت. حالا خودکار به
///    %APPDATA%\TorobScanner (و در حالت extrem به %TEMP%) منتقل می‌شود و
///    Logger.LogFilePath همیشه مسیر «واقعی» فایل لاگ را می‌دهد.
/// </summary>
public static class Logger
{
    private static readonly object _lock = new();
    private const string LogFile = "error_log.txt";
    private const string OldLogFile = "error_log.old.txt";
    private const long MaxSizeBytes = 512 * 1024;

    private static string? _resolvedDir;

    /// <summary>مسیر واقعی فایل لاگ — اولویت: کنار exe → %APPDATA% → %TEMP%</summary>
    public static string LogFilePath
    {
        get
        {
            if (_resolvedDir == null)
            {
                lock (_lock) { _resolvedDir ??= ResolveLogDir(); }
            }
            return Path.Combine(_resolvedDir, LogFile);
        }
    }

    /// <summary>اولین پوشه‌ای که واقعاً قابل نوشتن است — تصمیم فقط یک بار گرفته می‌شود</summary>
    private static string ResolveLogDir()
    {
        // ۱) کنار برنامه (رفتار کلاسیک)
        var baseDir = AppContext.BaseDirectory;
        if (IsWritable(baseDir)) return baseDir;

        // ۲) APPDATA — همان پناهگاه دیتابیس (باگ ۱۸)
        try
        {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TorobScanner");
            Directory.CreateDirectory(appData);
            if (IsWritable(appData)) return appData;
        }
        catch { }

        // ۳) آخرین پناهگاه — TEMP سیستم
        return Path.GetTempPath();

        static bool IsWritable(string dir)
        {
            try
            {
                var probe = Path.Combine(dir, $".log_test_{Guid.NewGuid():N}.tmp");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch { return false; }
        }
    }

    public static void Error(string context, string url, string message)
    {
        Write("ERROR", context, url, message);
    }

    public static void Warn(string context, string message)
    {
        Write("WARN", context, "", message);
    }

    public static void Info(string context, string message)
    {
        Write("INFO", context, "", message);
    }

    private static void Write(string level, string context, string url, string message)
    {
        try
        {
            lock (_lock)
            {
                var dir = _resolvedDir ?? ResolveLogDir();
                _resolvedDir = dir;
                var path = Path.Combine(dir, LogFile);
                if (File.Exists(path) && new FileInfo(path).Length > MaxSizeBytes)
                {
                    var oldPath = Path.Combine(dir, OldLogFile);
                    try { if (File.Exists(oldPath)) File.Delete(oldPath); } catch { }
                    try { File.Move(path, oldPath); } catch { }
                }
                File.AppendAllText(path,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {level} | {context} | {url}\n{message}\n---\n");
            }
        }
        catch { /* لاگ هرگز نباید برنامه را بندازد */ }
    }
}
