using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TorobScanner.Models;
using TorobScanner.Services;

namespace TorobScanner.Scrapers;

/// <summary>
/// موتور اسکن تطبیقی چنداستراتژی — هسته‌ی نسخه ۲.
/// هر سایت با SiteProfile خودش پیکربندی می‌شود؛ برای سایت‌های ناشناخته
/// موتور Universal خودش بهترین استراتژی (JSON-LD → Microdata → DOM) را انتخاب می‌کند.
///
/// ✅ رفع باگ ۸: CancellationToken در تمام مراحل + محافظ حلقه بی‌نهایت صفحه‌بندی
/// ✅ رفع باگ ۱۲: همه Wait ها Timeout دارند
/// ✅ رفع باگ ۱۰: لاگ Thread-Safe
/// </summary>
public class AdaptiveScraper : ISiteScraper
{
    private readonly SiteProfile _profile;

    /// <summary>آخرین استراتژی برنده — برای نمایش در UI</summary>
    public string LastStrategy { get; private set; } = "";

    public AdaptiveScraper(SiteProfile profile)
    {
        _profile = profile;
    }

    public string SiteName => _profile.Name;
    public string[] SupportedDomains =>
        string.IsNullOrEmpty(_profile.HostContains) ? new[] { "*" } : new[] { _profile.HostContains };

    public bool CanHandle(string url) => SiteProfile.Match(url) == _profile || string.IsNullOrEmpty(_profile.HostContains);

    public async Task<List<SavedProduct>> ScanCategoryAsync(
        string url,
        string targetCategory,
        string storeName,
        IProgress<(int current, int total, string status)> progress,
        CancellationToken cancellationToken = default)
    {
        var allProducts = new List<SavedProduct>();
        var seenPageUrls = new HashSet<string>();
        var seenProductUrls = new HashSet<string>();   // ✅ محافظ محصول تکراری بین صفحات
        string currentUrl = url;
        int pageCount = 0;

        var (playwright, browser) = await BrowserLauncher.LaunchAsync();
        try
        {
            var context = await BrowserLauncher.CreateContextAsync(browser);
            var page = await context.NewPageAsync();
            var extractorJs = ExtractorScripts.BuildCategoryExtractor(_profile);

            while (!string.IsNullOrEmpty(currentUrl) && pageCount < _profile.MaxPages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // ✅ محافظ حلقه بی‌نهایت: URL تکراری → خاتمه
                // ⚠️ فقط fragment (#) حذف می‌شود — query string باید بماند وگرنه
                // صفحه‌بندی ?paged=2 با صفحه ۱ یکسان تلقی و اسکن بعد از صفحه اول قطع می‌شد
                var normalizedUrl = currentUrl.Split('#')[0].TrimEnd('/');
                if (!seenPageUrls.Add(normalizedUrl)) break;

                pageCount++;
                progress?.Report((pageCount, _profile.MaxPages,
                    $"[{_profile.Name}] صفحه {pageCount} — {allProducts.Count} محصول"));

                try
                {
                    await BrowserLauncher.GotoWithRetryAsync(page, currentUrl);

                    // ✅ رفع باگ ۲۸ (v3.1.4): محافظ ریدایرکت — اگر صفحه‌ی بعدیِ زنجیره (۲ به بعد)
                    //    به آدرس دیگری پرت شد (مثل ریدایرکت 301 افزونه‌های سئو — Rank Math در
                    //    coffekala: /page/N/ → صفحه اصلی)، محتوای مقصد به این دسته تعلق ندارد؛
                    //    بدون استخراج قطع می‌شود تا ویجت‌های صفحه‌ی مقصد به‌عنوان محصول اسکن نشوند.
                    if (pageCount > 1 && !SameLandingUrl(page.Url, currentUrl))
                    {
                        Logger.Error("ExternalScan", currentUrl,
                            $"ریدایرکت به آدرس دیگر → قطع تمیز زنجیره: {page.Url}");
                        break;
                    }

                    await page.WaitForTimeoutAsync(_profile.SettleMs);

                    // --- رفتار صفحه بر اساس پروفایل (نه کورکورانه برای همه) ---
                    if (_profile.NeedsLoadMore)
                    {
                        // ✅ رفع باگ ۳۳ (v3.2.0): جای NetworkIdle کورکورانه (۱۰s تایم‌اوت به‌ازای هر
                        //    کلیک ≈ ۳ دقیقه معطلی)، شمارش کارت‌های محصول پول می‌شود و تا وقتی عدد
                        //    بزرگ نشده (حداکثر ۱۵s) صبر می‌کنیم — سریع‌تر و مطمئن‌تر؛ اگر دو کلیک
                        //    پشت‌سرهم هیچ محصول جدیدی نیاورد (پایان لیست یا AJAX خراب) تمیز قطع می‌شود.
                        int baseline = await CountProductsAsync(page);
                        int stallRounds = 0;
                        for (int i = 0; i < _profile.MaxLoadMoreClicks; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            try
                            {
                                var loadMoreBtn = await FindLoadMoreButtonAsync(page);
                                if (loadMoreBtn == null || !await loadMoreBtn.IsVisibleAsync() || !await loadMoreBtn.IsEnabledAsync())
                                    break;
                                await loadMoreBtn.ScrollIntoViewIfNeededAsync();
                                await loadMoreBtn.ClickAsync(new ElementHandleClickOptions { Timeout = 5000 });

                                bool grew = false;
                                for (int t = 0; t < 15; t++)
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                    await page.WaitForTimeoutAsync(1000);
                                    int now = await CountProductsAsync(page);
                                    if (now > baseline) { baseline = now; grew = true; break; }
                                }
                                if (!grew)
                                {
                                    // شاید لود کند بود — یک فرصت دیگر با مکث طولانی‌تر
                                    await page.WaitForTimeoutAsync(2500);
                                    int now = await CountProductsAsync(page);
                                    if (now > baseline) { baseline = now; stallRounds = 0; }
                                    else if (++stallRounds >= 2) break;
                                }
                                else stallRounds = 0;
                            }
                            catch (OperationCanceledException) { throw; }
                            catch { break; }
                        }
                    }

                    if (_profile.NeedsInfiniteScroll)
                    {
                        for (int i = 0; i < _profile.MaxScrollRounds; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            await page.EvaluateAsync("() => window.scrollTo(0, document.body.scrollHeight)");
                            await page.WaitForTimeoutAsync(900);
                        }
                        // ✅ رفع باگ ۱۲: NetworkIdle با Timeout
                        try { await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 12000 }); }
                        catch { }
                    }

                    // --- اجرای موتور استخراج چنداستراتژی ---
                    var result = await page.EvaluateAsync<JsonElement>(extractorJs);
                    if (result.ValueKind == JsonValueKind.Null || result.ValueKind == JsonValueKind.Undefined) break;

                    LastStrategy = result.TryGetProperty("strategy", out var stratEl)
                        ? stratEl.GetString() ?? "unknown" : "unknown";

                    var items = result.GetProperty("products");
                    if (items.GetArrayLength() == 0 && pageCount > 1) break;

                    // ✅ محافظ محصول تکراری: صفحه‌ای که هیچ محصول «جدیدی» نداشت → پایان صفحه‌بندی
                    // (سایت‌هایی که /page/N/ را ignore می‌کنند و همان محتوا را می‌دهند)
                    int newCount = 0;
                    foreach (var p in items.EnumerateArray())
                    {
                        var product = new SavedProduct
                        {
                            ProductName = p.GetProperty("name").GetString() ?? "",
                            TorobUrl = p.GetProperty("url").GetString() ?? "",
                            LastPrice = ParsePrice(p.GetProperty("price").GetString() ?? "0"),
                            StoreName = string.IsNullOrWhiteSpace(storeName) ? _profile.Name : storeName,
                            CategoryName = targetCategory,
                            LastUpdate = DateTime.Now
                        };
                        if (seenProductUrls.Add(product.TorobUrl))
                        {
                            allProducts.Add(product);
                            newCount++;
                        }
                    }
                    if (newCount == 0 && pageCount > 1) break;

                    var nextUrlElement = result.GetProperty("nextUrl");
                    currentUrl = nextUrlElement.ValueKind == JsonValueKind.Null
                        ? "" : nextUrlElement.GetString() ?? "";
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Logger.Error("ExternalScan", currentUrl, ex.Message);
                    break;
                }
            }
        }
        finally
        {
            await browser.DisposeAsync();
            playwright.Dispose();
        }

        return allProducts;
    }

    /// <summary>
    /// ✨ v3.2.0: پیدا کردن دکمه «بارگیری بیشتر» — اول سلکتورهای اختصاصی پروفایل، بعد لیست عمومی
    /// </summary>
    private async Task<IElementHandle?> FindLoadMoreButtonAsync(IPage page)
    {
        foreach (var sel in _profile.LoadMoreSelectors)
        {
            try
            {
                var btn = await page.QuerySelectorAsync(sel);
                if (btn != null) return btn;
            }
            catch { }
        }
        return await page.QuerySelectorAsync(
            "a.load-more, button.load-more, .elementor-button-load-more, .wd-load-more, .woocommerce-load-more, .yith-wcan-load-more, .alm-load-more-btn, button.btn-load-more, a.yith-wcan-infinite-scroll");
    }

    /// <summary>
    /// ✨ v3.2.0: شمارش کارت‌های محصول صفحه (برای تشخیص رشد بعد از کلیک LoadMore)
    /// </summary>
    private async Task<int> CountProductsAsync(IPage page)
    {
        try
        {
            var selectors = _profile.ContainerSelectors.Length > 0
                ? _profile.ContainerSelectors
                : new[] { "li.product", ".product-grid-item", "div.product", "[class*='product-card']", "[class*='product-item']" };
            var js = "() => { const sels = " + System.Text.Json.JsonSerializer.Serialize(selectors) + ";" +
                     " for (const s of sels) { try { const n = document.querySelectorAll(s).length; if (n > 0) return n; } catch (e) {} }" +
                     " return 0; }";
            return await page.EvaluateAsync<int>(js);
        }
        catch { return 0; }
    }

    /// <summary>
    /// ✨ v3.1.4: کلید فرود URL — فقط host + path (بدون scheme/query/اسلش انتهایی؛
    /// path هم decode می‌شود تا انکودینگ متفاوتِ فارسی ریدایرکت حساب نشود)
    /// </summary>
    private static string LandingKey(string? url)
    {
        try
        {
            var u = new Uri(url!);
            var path = Uri.UnescapeDataString(u.AbsolutePath).TrimEnd('/').ToLowerInvariant();
            return u.Host.ToLowerInvariant().TrimEnd('.') + path;
        }
        catch { return (url ?? "").Trim().ToLowerInvariant(); }
    }

    /// <summary>✨ v3.1.4: آیا صفحه‌ی فرود همان آدرس درخواستی است؟ (ریدایرکت واقعی = false)</summary>
    private static bool SameLandingUrl(string landed, string requested)
        => string.Equals(LandingKey(landed), LandingKey(requested), StringComparison.Ordinal);

    /// <summary>پارسی قیمت فارسی/انگلیسی با هر نوع جداکننده</summary>
    public static decimal ParsePrice(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        string cleanText = text.Trim().Replace(",", "").Replace("٬", "").Replace("٫", "")
            .Replace("تومان", "").Replace("ریال", "").Trim();
        cleanText = cleanText.Replace("۰", "0").Replace("۱", "1").Replace("۲", "2")
            .Replace("۳", "3").Replace("۴", "4").Replace("۵", "5").Replace("۶", "6")
            .Replace("۷", "7").Replace("۸", "8").Replace("۹", "9");
        // نقطه اعشار احتمالی (قیمت ایرانی اعشار ندارد)
        int dot = cleanText.IndexOf('.');
        if (dot >= 0) cleanText = cleanText.Substring(0, dot);
        return decimal.TryParse(cleanText, out var price) ? price : 0;
    }
}
