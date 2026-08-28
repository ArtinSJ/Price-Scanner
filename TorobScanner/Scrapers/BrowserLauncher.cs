using System;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace TorobScanner.Scrapers;

/// <summary>ابزارهای مشترک راه‌اندازی مرورگر Playwright</summary>
internal static class BrowserLauncher
{
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    /// <summary>
    /// راه‌اندازی مرورگر با ۳ لایه دفاعی — مخصوص ایران که CDN مایکروسافت مسدود است:
    ///
    /// لایه ۱: کرومیوم باندل‌شده Playwright (اگر قبلاً نصب شده باشد — سازگارترین)
    /// لایه ۲: Microsoft Edge — روی همه ویندوز ۱۰/۱۱ از قبل نصب است → بدون هیچ دانلودی کار می‌کند!
    /// لایه ۳: Google Chrome — اگر کاربر نصب کرده باشد
    /// آخرین گزینه: نصب خودکار کرومیوم (URL اصلی Playwright 1.60 از Google Storage است که
    ///              معمولاً در ایران باز است؛ اگر نه، پیام راهنما نمایش داده می‌شود)
    /// </summary>
    public static async Task<(IPlaywright Playwright, IBrowser Browser)> LaunchAsync()
    {
        Exception? lastError = null;

        // ═══ لایه ۱ و ۲ و ۳: کرومیوم → Edge → Chrome ═══
        foreach (var channel in new string?[] { null, "msedge", "chrome" })
        {
            try
            {
                return await TryLaunchAsync(channel);
            }
            catch (Exception ex) when (IsBrowserMissing(ex))
            {
                lastError = ex; // مرورگر بعدی را امتحان کن
            }
        }

        // ═══ هیچ مرورگری پیدا نشد → تلاش برای نصب خودکار کرومیوم ═══
        // (URL دانلود از Google Storage می‌گذرد که معمولاً در ایران در دسترس است)
        try
        {
            Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
            return await TryLaunchAsync(null);
        }
        catch (Exception ex)
        {
            lastError = ex;
        }

        throw new InvalidOperationException(BuildHelpMessage(lastError), lastError);
    }

    private static async Task<(IPlaywright Playwright, IBrowser Browser)> TryLaunchAsync(string? channel)
    {
        IPlaywright? playwright = null;
        try
        {
            playwright = await Playwright.CreateAsync();
            var options = new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[]
                {
                    "--disable-blink-features=AutomationControlled",
                    "--disable-gpu",
                    "--disable-dev-shm-usage",
                    "--ignore-certificate-errors",
                    "--ssl-version-min=tls1"
                }
            };
            if (channel != null) options.Channel = channel;

            var browser = await playwright.Chromium.LaunchAsync(options);
            return (playwright, browser);
        }
        catch
        {
            playwright?.Dispose();
            throw;
        }
    }

    /// <summary>تشخیص «مرورگر نصب نیست» — هر دو عبارت رسمی و غیررسمی</summary>
    private static bool IsBrowserMissing(Exception ex)
    {
        // variance های مختلف پیام Playwright برای مرورگر غایب
        return ex.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("executable does not exist", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("playwright install", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("Failed to launch", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("because it's not installed", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>پیام راهنمای کامل فارسی — با راه‌حل‌های دانلود جایگزین</summary>
    private static string BuildHelpMessage(Exception? ex) =>
        "راه‌اندازی مرورگر ناموفق بود. راه‌حل‌ها به ترتیب:\n\n" +
        "۱) Microsoft Edge را نصب کنید (روی ویندوز ۱۰/۱۱ معمولاً از قبل هست):\n" +
        "   https://www.microsoft.com/edge\n\n" +
        "۲) کرومیوم را دستی دانلود کنید (لینک‌های مستقیم از Google Storage — در ایران معمولاً باز است):\n" +
        "   a) chrome-win64.zip (حدود ۱۹۰MB):\n" +
        "   https://storage.googleapis.com/chrome-for-testing-public/148.0.7778.96/win64/chrome-win64.zip\n\n" +
        "   b) chrome-headless-shell-win64.zip (حدود ۱۱۸MB):\n" +
        "   https://storage.googleapis.com/chrome-for-testing-public/148.0.7778.96/win64/chrome-headless-shell-win64.zip\n\n" +
        "   سپس در پوشه زیر (اگر نیست بسازید):\n" +
        "   %LOCALAPPDATA%\\ms-playwright\\\n" +
        "   یک پوشه به نام chromium-1223 بسازید و محتوای فایل (a) را داخلش استخراج کنید؛\n" +
        "   و یک پوشه به نام chromium_headless_shell-1223 بسازید و محتوای فایل (b) را داخلش استخراج کنید.\n" +
        "   (راه میانبر: در File Explorer این را در نوار آدرس بزنید: %LOCALAPPDATA%\\ms-playwright)\n\n" +
        "۳) اگر VPN دارید، فعالش کنید و برنامه را دوباره باز کنید تا خودکار نصب شود.\n\n" +
        $"جزئیات فنی: {ex?.Message ?? "نامشخص"}";

    /// <summary>کانتکست با UA مرورگر واقعی + بلاک کردن تصاویر/فونت‌ها (سرعت ~۳ برابر)</summary>
    public static async Task<IBrowserContext> CreateContextAsync(IBrowser browser, bool blockResources = true)
    {
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = UserAgent,
            Locale = "fa-IR",
            ViewportSize = new ViewportSize { Width = 1366, Height = 900 }
        });

        if (blockResources)
        {
            await context.RouteAsync("**/*.{png,jpg,jpeg,gif,svg,ico,woff,woff2,ttf,mp4,webm,webp}",
                route => route.AbortAsync());
        }
        return context;
    }

    /// <summary>ناوبری با یک تلاش مجدد برای خطاهای گذرای شبکه</summary>
    public static async Task GotoWithRetryAsync(IPage page, string url, int timeoutMs = 45000)
    {
        try
        {
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = timeoutMs
            });
        }
        catch (PlaywrightException)
        {
            await Task.Delay(2000);
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = timeoutMs
            });
        }
    }
}
