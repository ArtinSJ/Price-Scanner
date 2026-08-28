using System;
using System.Collections.Generic;
using System.Linq;

namespace TorobScanner.Scrapers;

/// <summary>
/// پروفایل هر سایت: «بهترین ابزار» برای آن دامنه خاص.
/// سایت جدید = فقط یک instance در لیست Known اضافه کنید.
/// برای سایت‌های ناشناخته، موتور Universal خودش بهترین استراتژی را انتخاب می‌کند.
/// </summary>
public class SiteProfile
{
    public string Name { get; init; } = "Universal";
    /// <summary>بخشی از hostname برای تشخیص دامنه (مثلاً "samkish")</summary>
    public string HostContains { get; init; } = "";
    /// <summary>سلکتورهای اختصاصی کانتینر کارت محصول — اولویت با اینهاست</summary>
    public string[] ContainerSelectors { get; init; } = Array.Empty<string>();
    /// <summary>سلکتورهای اختصاصی عنوان محصول</summary>
    public string[] TitleSelectors { get; init; } = Array.Empty<string>();
    /// <summary>سلکتورهای اختصاصی قیمت</summary>
    public string[] PriceSelectors { get; init; } = Array.Empty<string>();
    /// <summary>سایت دکمه «بارگیری بیشتر» دارد؟ (کافه‌کالا و…)</summary>
    public bool NeedsLoadMore { get; init; } = false;
    /// <summary>سایت اسکرول بی‌نهایت دارد؟</summary>
    public bool NeedsInfiniteScroll { get; init; } = false;
    public int MaxScrollRounds { get; init; } = 12;
    public int MaxLoadMoreClicks { get; init; } = 15;
    /// <summary>سقف قیمت قابل قبول (تومان) — رفع باگ ۳: از ۵۰ میلیون به ۱۰۰ میلیارد</summary>
    public decimal MaxPrice { get; init; } = 100_000_000_000m;
    /// <summary>حداکثر صفحات اسکن</summary>
    public int MaxPages { get; init; } = 60;
    /// <summary>مکث بعد از Goto (میلی‌ثانیه)</summary>
    public int SettleMs { get; init; } = 1500;
    /// <summary>
    /// سایت لینک صفحه‌بندی ندارد اما /page/N/ کار می‌کند (مثل caffeinexpress).
    /// با این فلگ، موتور صفحه ۲ را آزمایشی امتحان می‌کند —
    /// محافظ «محصول تکراری = توقف» در C# از حلقه بی‌نهایت جلوگیری می‌کند.
    /// </summary>
    public bool ForceTryPagination { get; init; } = false;

    // ═══════════ رجیستری سایت‌های تحلیل‌شده ═══════════
    // سایت جدید؟ اینجا اضافه کن. تحلیل دقیق DOM + تست عملی کنار هر پروفایل انجام شده.
    public static readonly List<SiteProfile> Known = new()
    {
        // ✅ تحلیل‌شده ۲۰۲۶-۰۸: WordPress/WooCommerce + تم Bakala سفارشی (products__item-*)
        //    صفحه‌بندی سمت سرور با a.next → بدون LoadMore و بدون اسکرول بی‌نهایت
        //    تست عملی: ۱۲/۱۲ محصول استخراج شد (نسخه قبلی ۹/۱۲ به‌خاطر سقف ۵۰M)
        new SiteProfile
        {
            Name = "سام‌کیش (samkish.com)",
            HostContains = "samkish",
            ContainerSelectors = new[] { "li.product.type-product", "li.product" },
            TitleSelectors = new[] { "p.products__item-fatitle a", "a[title]" },
            PriceSelectors = new[] { ".woocommerce-Price-amount", ".products__item-price" },
            NeedsLoadMore = false,
            NeedsInfiniteScroll = false,
            MaxPages = 60,
            SettleMs = 1200
        },

        // ✅ تحلیل‌شده ۲۰۲۶-۰۸: WordPress/WooCommerce + تم Bakala (همان تم سام‌کیش!)
        //    ساختار یکسان: li.product + products__item-fatitle + صفحه‌بندی a.next
        //    تست عملی: ۱۸ محصول استخراج شد + صفحه‌بندی /page/2/ فعال
        new SiteProfile
        {
            Name = "سیلاین (cylline.com)",
            HostContains = "cylline",
            ContainerSelectors = new[] { "li.product.type-product", "li.product" },
            TitleSelectors = new[] { "p.products__item-fatitle a", "a[title]" },
            PriceSelectors = new[] { ".woocommerce-Price-amount", ".products__item-price" },
            NeedsLoadMore = false,
            NeedsInfiniteScroll = false,
            MaxPages = 60,
            SettleMs = 1200
        },

        // ✅ تحلیل‌شده ۲۰۲۶-۰۸: WordPress/WooCommerce + تم Woodmart
        //    کانتینر: .product-grid-item | عنوان: h3.wd-entities-title | قیمت استاندارد
        //    صفحه‌بندی لینک ندارد اما /page/N/ فعال است (صفحه ۱: ۸ محصول، صفحه ۲: ۱۲ محصول)
        //    → ForceTryPagination + محافظ محصول تکراری
        new SiteProfile
        {
            Name = "کافه‌کالا (coffekala.com)",
            HostContains = "coffekala",
            ContainerSelectors = new[] { ".product-grid-item" },
            TitleSelectors = new[] { "h3.wd-entities-title a", "h3.wd-entities-title", "a[title]" },
            PriceSelectors = new[] { ".price .amount", ".woocommerce-Price-amount" },
            NeedsLoadMore = false,
            NeedsInfiniteScroll = false,
            ForceTryPagination = true,
            MaxPages = 40,
            SettleMs = 1500
        },

        // ✅ تحلیل‌شده ۲۰۲۶-۰۸: WordPress/WooCommerce + تم Woodmart (مثل کافه‌کالا)
        //    ۹۹ محصول در یک صفحه (بدون صفحه‌بندی عملی — page/2 خطای 403 می‌دهد)
        //    کانتینر: .product-grid-item | عنوان: h3.wd-entities-title
        new SiteProfile
        {
            Name = "کافه‌این‌شاپ (cafe-inshop.com)",
            HostContains = "cafe-inshop",
            ContainerSelectors = new[] { ".product-grid-item" },
            TitleSelectors = new[] { "h3.wd-entities-title a", "h3.wd-entities-title", "a[title]" },
            PriceSelectors = new[] { ".price .amount", ".woocommerce-Price-amount" },
            NeedsLoadMore = false,
            NeedsInfiniteScroll = false,
            MaxPages = 10,
            SettleMs = 1500
        },

        // ✅ تحلیل‌شده ۲۰۲۶-۰۸: WordPress + المنتور Loop Grid (e-loop-item)
        //    پیچیده‌ترین ساختار: قیمت‌های فارسی + لینک‌های eael-wrapper خالی
        //    عنوان: h2 | قیمت: .woocommerce-Price-amount | صفحه‌بندی بدون لینک ولی /page/N/ فعال
        //    → ForceTryPagination + محافظ محصول تکراری
        new SiteProfile
        {
            Name = "کافئین‌اکسپرس (caffeinexpress.ir)",
            HostContains = "caffeinexpress",
            ContainerSelectors = new[] { "[data-elementor-type=\"loop-item\"]", ".e-loop-item" },
            TitleSelectors = new[] { "h2.elementor-heading-title", "h2", "h3.elementor-heading-title" },
            PriceSelectors = new[] { ".woocommerce-Price-amount", ".price" },
            NeedsLoadMore = false,
            NeedsInfiniteScroll = false,
            ForceTryPagination = true,
            MaxPages = 15,
            SettleMs = 2000
        },

        // ✅ تحلیل‌شده ۲۰۲۶-۰۸: WordPress + المنتور + JSON-LD کامل ItemList
        //    بهترین حالت ممکن: ۱۲ محصول با قیمت تخفیفی درست در JSON-LD
        //    نکته: اسم‌ها پسوند «☕️ فروشگاه قهوه ننجون» دارند که موتور خودش پاک می‌کند
        new SiteProfile
        {
            Name = "قهوه ننجون (nanjuncoffee.com)",
            HostContains = "nanjun",
            ContainerSelectors = new[] { ".product-grid-item", ".product" },
            TitleSelectors = new[] { "h3.wd-entities-title", "h2", "h3" },
            PriceSelectors = new[] { "ins .amount", ".woocommerce-Price-amount" },
            NeedsLoadMore = false,
            NeedsInfiniteScroll = false,
            MaxPages = 10,
            SettleMs = 1500
        },

        // 🌐 حالت پیش‌فرض برای سایت‌های ناشناخته — موتور Universal
        new SiteProfile
        {
            Name = "Universal (همه سایت‌ها)",
            HostContains = "",
            NeedsLoadMore = true,
            NeedsInfiniteScroll = true,
            MaxScrollRounds = 10,
            MaxLoadMoreClicks = 12,
            MaxPages = 60
        }
    };

    /// <summary>پروفایل متناسب با URL (fallback: Universal)</summary>
    public static SiteProfile Match(string url)
    {
        try
        {
            var host = new Uri(url).Host.ToLowerInvariant();
            var profile = Known.FirstOrDefault(p =>
                !string.IsNullOrEmpty(p.HostContains) && host.Contains(p.HostContains));
            return profile ?? Known.Last();
        }
        catch { return Known.Last(); }
    }
}
