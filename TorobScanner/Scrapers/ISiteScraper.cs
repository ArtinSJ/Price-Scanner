using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TorobScanner.Models;

namespace TorobScanner.Scrapers;

/// <summary>
/// قرارداد اسکرپر هر سایت — الگوی Strategy پروژه حفظ شده است.
/// </summary>
public interface ISiteScraper
{
    string SiteName { get; }
    string[] SupportedDomains { get; }
    bool CanHandle(string url);
    Task<List<SavedProduct>> ScanCategoryAsync(
        string url,
        string targetCategory,
        string storeName,
        IProgress<(int current, int total, string status)> progress,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// کارخانه اسکرپرها — از رجیستری SiteProfile تغذیه می‌شود (منبع واحد حقیقت).
/// برای سایت‌های خاص که نیاز به منطق اختصاصی دارند (مثلاً API دیجی‌کالا)
/// کلاس جدید ISiteScraper بسازید و در سازنده ثبت کنید.
/// </summary>
public class ScraperFactory
{
    private readonly List<ISiteScraper> _customScrapers;

    public ScraperFactory(params ISiteScraper[] customScrapers)
    {
        _customScrapers = new List<ISiteScraper>(customScrapers);
    }

    public ISiteScraper GetScraper(string url)
    {
        // اول اسکرپرهای اختصاصی ثبت‌شده
        var custom = _customScrapers.FirstOrDefault(s => s.CanHandle(url));
        if (custom != null) return custom;

        // بعد پروفایل‌های رجیستری → موتور تطبیقی
        return new AdaptiveScraper(SiteProfile.Match(url));
    }

    public void RegisterScraper(ISiteScraper scraper) => _customScrapers.Add(scraper);

    public List<string> GetRegisteredSites()
    {
        var fromProfiles = SiteProfile.Known
            .Where(p => !string.IsNullOrEmpty(p.HostContains))
            .Select(p => $"✅ {p.Name}");
        var fromCustom = _customScrapers.Select(s => $"✅ {s.SiteName}");
        return fromProfiles.Concat(fromCustom)
            .Append($"🌐 Universal (همه سایت‌های دیگر)")
            .ToList();
    }
}
