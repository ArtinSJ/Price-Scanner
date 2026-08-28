using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using TorobScanner.Data;
using TorobScanner.Models;
using TorobScanner.Services;

namespace TorobScanner.Scrapers;

/// <summary>
/// اسکرپر محصولات تکی (ترب و لینک‌های ذخیره‌شده):
/// ✅ JSON-LD اول (دقیق‌ترین) → DOM هیوریستیک بعد از آن
/// ✅ رفع باگ ۱: راه‌اندازی مرورگر + نوشتن DB داخل try/catch
/// ✅ رفع باگ ۵: قیمت بدون جداکننده هزارگان هم پشتیبانی می‌شود
/// ✅ پشتیبانی CancellationToken
/// </summary>
public class TorobProductScraper
{
    private readonly SemaphoreSlim _semaphore = new(3);
    private readonly DatabaseManager _db;

    public TorobProductScraper(DatabaseManager db)
    {
        _db = db;
    }

    public async Task RefreshProductsAsync(List<SavedProduct> products,
        IProgress<(int current, int total, string status)> progress,
        CancellationToken ct = default)
    {
        int total = products.Count;
        int current = 0;

        var (playwright, browser) = await BrowserLauncher.LaunchAsync();
        try
        {
            var tasks = products.Select(async product =>
            {
                await _semaphore.WaitAsync(ct);
                try
                {
                    if ((DateTime.Now - product.LastUpdate).TotalHours < 6 &&
                        product.ProductName != "محصول جدید")
                    {
                        Interlocked.Increment(ref current);
                        progress?.Report((current, total, $"کش شده: {product.ProductName}"));
                        return;
                    }

                    progress?.Report((current, total, $"در حال بررسی: {product.ProductName}"));

                    var context = await BrowserLauncher.CreateContextAsync(browser);
                    var page = await context.NewPageAsync();

                    var (price, store, title) = await ScrapeProductPage(page, product.TorobUrl);
                    await page.CloseAsync();

                    if (price > 0)
                    {
                        product.LastPrice = price; // PreviousPrice توسط SQL هوشمند مدیریت می‌شود (رفع باگ ۷)
                        product.StoreName = store;
                        if (!string.IsNullOrWhiteSpace(title) && title != "نامشخص") product.ProductName = title;
                        product.LastUpdate = DateTime.Now;
                        try { _db.SaveProduct(product); }
                        catch (Exception ex) { Logger.Error("RefreshProducts", product.TorobUrl, ex.Message); }
                    }
                    Interlocked.Increment(ref current);
                    progress?.Report((current, total, $"بروزرسانی شد: {product.ProductName}"));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { Logger.Error("RefreshProducts", product.TorobUrl, ex.Message); }
                finally { _semaphore.Release(); }
            }).ToList();

            await Task.WhenAll(tasks);
        }
        finally
        {
            await browser.DisposeAsync();
            playwright.Dispose();
        }
    }

    private async Task<(decimal price, string store, string title)> ScrapeProductPage(IPage page, string url)
    {
        try
        {
            await BrowserLauncher.GotoWithRetryAsync(page, url);
            await page.WaitForTimeoutAsync(2000);

            // ۱) JSON-LD صفحه محصول — دقیق‌ترین روش
            try
            {
                var ld = await page.EvaluateAsync<JsonElement>(ExtractorScripts.ProductPageJsonLd);
                if (ld.ValueKind == JsonValueKind.Object)
                {
                    decimal ldPrice = 0;
                    if (ld.TryGetProperty("price", out var priceEl) && priceEl.ValueKind == JsonValueKind.Number)
                        ldPrice = Convert.ToDecimal(priceEl.GetDouble());

                    if (ldPrice > 0)
                    {
                        string store = ld.TryGetProperty("store", out var storeEl) ? storeEl.GetString() ?? "" : "";
                        string title = ld.TryGetProperty("title", out var titleEl) ? titleEl.GetString() ?? "" : "";
                        return (ldPrice,
                            string.IsNullOrWhiteSpace(store) ? "نامشخص" : store,
                            string.IsNullOrWhiteSpace(title) ? "" : title);
                    }
                }
            }
            catch { /* JSON-LD نبود یا خراب بود — ادامه با DOM */ }

            // ۲) Fallback: هیوریستیک DOM
            var scrapeData = await page.EvaluateAsync<JsonElement>(ExtractorScripts.ProductPageDom);
            if (scrapeData.ValueKind == JsonValueKind.Object)
            {
                decimal price = 0;
                if (scrapeData.TryGetProperty("price", out var pEl))
                {
                    if (pEl.ValueKind == JsonValueKind.Number) price = Convert.ToDecimal(pEl.GetDouble());
                    else price = AdaptiveScraper.ParsePrice(pEl.GetString() ?? "0");
                }
                string store = scrapeData.TryGetProperty("store", out var sEl) ? sEl.GetString() ?? "نامشخص" : "نامشخص";
                string title = scrapeData.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? "" : "";
                return (price, store, title);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("ProductPage", url, ex.Message);
        }
        return (0, "نامشخص", "");
    }
}
