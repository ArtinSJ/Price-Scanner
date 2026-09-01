using System;
using System.IO;
using System.Text.Json;

namespace TorobScanner.Services;

/// <summary>مدل تنظیمات برنامه — به‌صورت JSON ذخیره می‌شود</summary>
public class AppSettings
{
    /// <summary>تم فعال: platinum | aurora | emerald | rosegold</summary>
    public string ThemeId { get; set; } = "platinum";

    /// <summary>بررسی خودکار بروزرسانی هنگام شروع برنامه (پیش‌فرض: روشن)</summary>
    public bool AutoCheckUpdates { get; set; } = true;

    /// <summary>نصب خودکار بروزرسانی بدون پرسش (پیش‌فرض: خاموش)</summary>
    public bool AutoInstallUpdates { get; set; } = false;
}

/// <summary>
/// ✨ v2.7: سرویس تنظیمات — فایل settings.json کنار برنامه؛
/// اگر پوشه‌ی برنامه فقط-خواندنی بود (Program Files)، مثل دیتابیس
/// به %APPDATA%\TorobScanner پناه می‌برد تا تنظیمات همیشه قابل ذخیره باشند.
/// </summary>
public static class SettingsService
{
    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static AppSettings Current { get; private set; } = new();

    /// <summary>مسیر فایل تنظیمات — با همان منطق fallback دیتابیس</summary>
    public static string SettingsPath
    {
        get
        {
            var primary = Path.Combine(AppContext.BaseDirectory, "settings.json");
            if (IsDirectoryWritable(AppContext.BaseDirectory)) return primary;
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TorobScanner");
            try { Directory.CreateDirectory(dir); } catch { }
            return Path.Combine(dir, "settings.json");
        }
    }

    /// <summary>خواندن تنظیمات — اگر فایل نبود یا خراب بود، پیش‌فرض</summary>
    public static void Load()
    {
        try
        {
            var path = SettingsPath;
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), _json);
                if (loaded != null) Current = loaded;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Settings/Load", SettingsPath, ex.Message);
            Current = new AppSettings();
        }
    }

    /// <summary>ذخیره تنظیمات — هرگز استثنا نمی‌اندازد (فقط لاگ)</summary>
    public static void Save()
    {
        try
        {
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Current, _json));
        }
        catch (Exception ex)
        {
            Logger.Error("Settings/Save", SettingsPath, ex.Message);
        }
    }

    private static bool IsDirectoryWritable(string dir)
    {
        try
        {
            var probe = Path.Combine(dir, $".write_test_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch { return false; }
    }
}
