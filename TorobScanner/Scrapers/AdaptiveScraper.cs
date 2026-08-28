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
                var normalizedUrl = currentUrl.Split('#')[0].Split('?')[0];
                if (!seenPageUrls.Add(normalizedUrl)) break;

                pageCount++;
                progress?.Report((pageCount, _profile.MaxPages,
                    $"[{_profile.Name}] صفحه {pageCount} — {allProducts.Count} محصول"));

                try
                {
                    await BrowserLauncher.GotoWithRetryAsync(page, currentUrl);
                    await page.WaitForTimeoutAsync(_profile.SettleMs);

                    // --- رفتار صفحه بر اساس پروفایل (نه کورکورانه برای همه) ---
                    if (_profile.NeedsLoadMore)
                    {
                        for (int i = 0; i < _profile.MaxLoadMoreClicks; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            try
                            {
                                var loadMoreBtn = await page.QuerySelectorAsync(
                                    "a.load-more, button.load-more, .elementor-button-load-more, .wd-load-more, .woocommerce-load-more, .yith-wcan-load-more, .alm-load-more-btn, button.btn-load-more, a.yith-wcan-infinite-scroll");
                                if (loadMoreBtn != null && await loadMoreBtn.IsVisibleAsync() && await loadMoreBtn.IsEnabledAsync())
                                {
                                    await loadMoreBtn.ScrollIntoViewIfNeededAsync();
                                    await loadMoreBtn.ClickAsync(new ElementHandleClickOptions { Timeout = 5000 });
                                    try { await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 10000 }); } catch { }
                                    await page.WaitForTimeoutAsync(1200);
                                }
                                else break;
                            }
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
