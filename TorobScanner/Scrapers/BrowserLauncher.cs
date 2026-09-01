using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace TorobScanner.Scrapers;

/// <summary>
/// ابزارهای مشترک راه‌اندازی مرورگر Playwright
///
/// ✅ رفع باگ ۳۲ (v3.2.0) — «ارور node.exe هنگام اسکن»:
/// مایکروسافت Playwright نسخه ۱.۶۰ هیچ چیزی در زمان اجرا extract نمی‌کند؛ صرفاً دنبال
/// «.playwright\node\win32_x64\node.exe» کنار Microsoft.Playwright.dll می‌گردد و اگر آنجا
/// نباشد (فایل‌های ZIP بلاک‌شده‌ی ویندوز، قرنطینه‌ی آنتی‌ویروس، کپی ناقص پوشه، ...
/// دقیقاً خطای «Driver not found: ...node.exe» داده می‌شود و اسکن خارجی هیچ‌وقت شروع نمی‌شود.
///
/// راه‌حل: حالت پرتابل — اگر کنار exe، پوشه‌های باندل‌شده وجود داشته باشند، مسیرها با
/// متغیرهای محیطی رسمی Playwright قفل می‌شوند:
///   PLAYWRIGHT_NODEJS_PATH       → node.exe باندل‌شده (پوشه‌ی visible «node» یا داخل .playwright)
///   PLAYWRIGHT_DRIVER_SEARCH_PATH → پوشه‌ی برنامه (جعبه‌ابزار .playwright باندل‌شده)
///   PLAYWRIGHT_BROWSERS_PATH     → پوشه‌ی «browsers» باندل‌شده (کرومیوم داخل بسته)
/// نتیجه: برنامه کاملاً مستقل از سیستم اجرا می‌شود — بدون نصب Node.js، بدون نصب مرورگر،
/// بدون دانلود CDN (که در ایران مسدود است).
/// </summary>
internal static class BrowserLauncher
{
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    static BrowserLauncher()
    {
        // ═══ حالت پرتابل (رفع باگ ۳۲): قفل‌کردن مسیرهای باندل‌شده قبل از هر اسکن ═══
        // اگر پوشه‌های باندل کنار exe باشند، Playwright دقیقاً از همان‌ها استفاده می‌کند؛
        // اگر نباشند (بیلد سورس)، هیچ متغیری ست نمی‌شود و رفتار پیش‌فرض حفظ می‌شود.
        try
        {
            string baseDir = AppContext.BaseDirectory;
            string visibleNode = Path.Combine(baseDir, "node", "node.exe");
            string bundledNode = Path.Combine(baseDir, ".playwright", "node", "win32_x64", "node.exe");
            string bundledCli = Path.Combine(baseDir, ".playwright", "package", "cli.js");
            string browsersDir = Path.Combine(baseDir, "browsers");

            if (File.Exists(visibleNode))
                Environment.SetEnvironmentVariable("PLAYWRIGHT_NODEJS_PATH", visibleNode);
            else if (File.Exists(bundledNode))
                Environment.SetEnvironmentVariable("PLAYWRIGHT_NODEJS_PATH", bundledNode);

            // SEARCH_PATH فقط وقتی ست می‌شود که درایور کامل باشد (node.exe + cli.js)
            if (File.Exists(bundledCli) && File.Exists(bundledNode))
                Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", baseDir);

            if (Directory.Exists(browsersDir))
                Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browsersDir);
        }
        catch { /* محیط غیرپرتابل — مسیرهای سیستمی */ }
    }

    /// <summary>آیا خطا مربوط به گم‌شدن درایور/node.exe است؟ (نه مرورگر)</summary>
    private static bool IsDriverMissing(Exception ex)
    {
        string m = ex.Message;
        return m.Contains("Driver not found", StringComparison.OrdinalIgnoreCase)
            || m.Contains("PLAYWRIGHT_DRIVER_SEARCH_PATH", StringComparison.OrdinalIgnoreCase)
            || m.Contains("node.exe", StringComparison.OrdinalIgnoreCase)
            || m.Contains("cli.js", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// راه‌اندازی مرورگر با ۳ لایه دفاعی — مخصوص ایران که CDN مایکروسافت مسدود است:
    ///
    /// لایه ۱: کرومیوم باندل‌شده (پوشه «browsers» کنار برنامه در بسته پرتابل)
    /// لایه ۲: Microsoft Edge — روی همه ویندوز ۱۰/۱۱ از قبل نصب است → بدون هیچ دانلودی کار می‌کند!
    /// لایه ۳: Google Chrome — اگر کاربر نصب کرده باشد
    /// آخرین گزینه: نصب خودکار کرومیوم (URL اصلی Playwright 1.60 از Google Storage است که
    ///              معمولاً در ایران باز است؛ اگر نه، پیام راهنما نمایش داده می‌شود)
    /// </summary>
    public static async Task<(IPlaywright Playwright, IBrowser Browser)> LaunchAsync()
    {
        Exception? lastError = null;

        // ✅ رفع باگ ۳۲: اگر درایور/node.exe گم باشد، امتحان مرورگرهای دیگر بی‌معنی است —
        //    خطا باید فوراً با پیام فارسیِ قابل‌فهم داده شود.
        try
        {
            return await TryLaunchAsync(null);
        }
        catch (Exception ex) when (IsDriverMissing(ex))
        {
            throw new InvalidOperationException(BuildDriverHelpMessage(ex), ex);
        }
        catch (Exception ex) when (IsBrowserMissing(ex))
        {
            lastError = ex;
        }

        // ═══ لایه ۲ و ۳: Edge → Chrome ═══
        foreach (var channel in new string?[] { "msedge", "chrome" })
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

    /// <summary>✨ v3.2.0: پیام فارسی برای گم‌شدن درایور node.exe</summary>
    private static string BuildDriverHelpMessage(Exception ex) =>
        "درایور اسکن (node.exe) کنار برنامه پیدا نشد.\n\n" +
        "این درایور داخل خود پوشه‌ی برنامه است و به نصب Node.js روی ویندوز نیازی نیست؛\n" +
        "فقط باید این دو شرط برقرار باشد:\n\n" +
        "۱) پوشه‌ی «node» و پوشه‌ی «.playwright» کنار فایل اجرایی برنامه (TorobScanner.exe) باشند.\n" +
        "   اگر برنامه را جابه‌جا کرده‌اید، همه‌ی پوشه‌ها را با هم منتقل کنید.\n\n" +
        "۲) آنتی‌ویروس فایل node.exe را قرنطینه یا بلاک نکرده باشد.\n" +
        "   اگر پوشه node یا .playwright خالی است، آنتی‌ویروس حذفش کرده:\n" +
        "   فایل node.exe را در لیست استثناهای آنتی‌ویروس قرار دهید و بسته را دوباره باز کنید.\n\n" +
        "۳) اگر ZIP را از اینترنت گرفته‌اید: روی فایل ZIP راست‌کلیک → Properties →\n" +
        "   تیک Unblock را بزنید، سپس Extract کنید.\n\n" +
        $"جزئیات فنی: {ex.Message}";

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
