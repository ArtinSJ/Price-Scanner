using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TorobScanner.Models;

namespace TorobScanner.Scrapers;

/// <summary>
/// اسکرپر اختصاصی سام‌کیش — نمونه‌ی الگوی افزودن سایت جدید.
/// تمام منطق در موتور تطبیقی مشترک است؛ این کلاس فقط پروفایل را انتخاب می‌کند.
/// اگر روزی منطق کاملاً متفاوتی لازم شد، متد ScanCategoryAsync را override کنید.
/// </summary>
public class SamkishScraper : ISiteScraper
{
    private readonly AdaptiveScraper _engine;

    public SamkishScraper()
    {
        _engine = new AdaptiveScraper(SiteProfile.Known.First(p => p.HostContains == "samkish"));
    }

    public string SiteName => "سام‌کیش (SamKish)";
    public string[] SupportedDomains => new[] { "samkish.com" };

    public bool CanHandle(string url) => url.Contains("samkish.com", StringComparison.OrdinalIgnoreCase);

    public Task<List<SavedProduct>> ScanCategoryAsync(string url, string targetCategory, string storeName,
        IProgress<(int current, int total, string status)> progress, CancellationToken cancellationToken = default)
        => _engine.ScanCategoryAsync(url, targetCategory, storeName, progress, cancellationToken);
}

/// <summary>اسکرپر عمومی برای همه سایت‌های ناشناخته</summary>
public class GenericScraper : ISiteScraper
{
    private readonly AdaptiveScraper _engine;

    public GenericScraper()
    {
        _engine = new AdaptiveScraper(SiteProfile.Known.Last());
    }

    public string SiteName => "Universal (همه سایت‌ها)";
    public string[] SupportedDomains => new[] { "*" };
    public bool CanHandle(string url) => true;

    public Task<List<SavedProduct>> ScanCategoryAsync(string url, string targetCategory, string storeName,
        IProgress<(int current, int total, string status)> progress, CancellationToken cancellationToken = default)
        => _engine.ScanCategoryAsync(url, targetCategory, storeName, progress, cancellationToken);
}
