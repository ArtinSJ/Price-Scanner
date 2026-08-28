using System;
using System.IO;

namespace TorobScanner.Services;

/// <summary>
/// لاگ خطای Thread-Safe با چرخش فایل:
/// ✅ رفع باگ ۱۰: چند تسک همزمان می‌توانند لاگ بنویسند بدون IOException
/// ✅ چرخش خودکار: فایل از ۵۱۲ کیلوبایت بزرگ‌تر نشود
/// </summary>
public static class Logger
{
    private static readonly object _lock = new();
    private const string LogFile = "error_log.txt";
    private const string OldLogFile = "error_log.old.txt";
    private const long MaxSizeBytes = 512 * 1024;

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
                var path = Path.Combine(AppContext.BaseDirectory, LogFile);
                if (File.Exists(path) && new FileInfo(path).Length > MaxSizeBytes)
                {
                    var oldPath = Path.Combine(AppContext.BaseDirectory, OldLogFile);
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
