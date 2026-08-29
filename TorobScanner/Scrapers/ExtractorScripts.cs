using System.Globalization;
using System.Text.Json;

namespace TorobScanner.Scrapers;

/// <summary>
/// اسکریپت‌های جاوااسکریپت استخراج — به صورت تابع یگانه (سازگار با Playwright).
/// موتور چنداستراتژی: JSON-LD → Microdata → DOM هوشمند (پروفایل‌محور).
/// تست‌شده روی HTML واقعی سام‌کیش: ۱۲/۱۲ محصول (نسخه قبلی ۹/۱۲).
/// </summary>
public static class ExtractorScripts
{
    /// <summary>ساخت اسکریپت استخراج دسته‌بندی با تزریق سلکتورهای پروفایل</summary>
    public static string BuildCategoryExtractor(SiteProfile profile)
    {
        return CategoryExtractorTemplate
            .Replace("__PROFILE_CONTAINERS__", JsonSerializer.Serialize(profile.ContainerSelectors))
            .Replace("__PROFILE_TITLES__", JsonSerializer.Serialize(profile.TitleSelectors))
            .Replace("__PROFILE_PRICES__", JsonSerializer.Serialize(profile.PriceSelectors))
            .Replace("__MAX_PRICE__", profile.MaxPrice.ToString("0", CultureInfo.InvariantCulture))
            .Replace("__FORCE_PAGINATION__", profile.ForceTryPagination ? "true" : "false");
    }

    /// <summary>اسکریپت استخراج از صفحه محصول تکی (ترب و مشابه) — JSON-LD اول</summary>
    public const string ProductPageJsonLd = @"() => {
    let out = { price: 0, store: '', title: '' };
    document.querySelectorAll('script[type=""application/ld+json""]').forEach(s => {
        try {
            let data = JSON.parse(s.textContent);
            let collect = (item) => {
                if (!item || typeof item !== 'object') return;
                let types = Array.isArray(item['@type']) ? item['@type'] : [item['@type']];
                if (types.includes('Product')) {
                    if (item.name && !out.title) out.title = item.name;
                    let offers = item.offers || {};
                    let offersList = Array.isArray(offers) ? offers : [offers];
                    let prices = offersList.map(o => parseFloat(o.price || o.lowPrice || 0)).filter(p => p > 0);
                    if (prices.length && out.price === 0) out.price = Math.min(...prices);
                    let seller = offersList.find(o => o.seller && o.seller.name);
                    if (seller && !out.store) out.store = seller.seller.name;
                }
                if (types.includes('ItemList') && Array.isArray(item.itemListElement))
                    item.itemListElement.forEach(li => collect(li.item || li));
                if (Array.isArray(item['@graph'])) item['@graph'].forEach(collect);
            };
            collect(data);
        } catch (e) {}
    });
    return out;
}";

    /// <summary>اسکریپت استخراج DOM از صفحه محصول تکی — هیوریستیک نسخه بهبودیافته</summary>
    public const string ProductPageDom = @"() => {
    let title = document.querySelector('h1')?.innerText || document.title || '';
    title = title.replace(/\| ?ترب/g, '').replace(/خرید و قیمت/g, '').replace(/کد محصول/g, '').trim();

    const toEn = (str) => str.replace(/[۰-۹]/g, d => '۰۱۲۳۴۵۶۷۸۹'.indexOf(d));
    const priceRegex = /([\d۰-۹]{1,3}(?:[.,٬٫][\d۰-۹]{3})+|[\d۰-۹]{5,})/g;

    let firstPrice = null;
    let minNode = null;
    let elements = document.querySelectorAll('div, span, a, p, b, strong, h1, h2, h3');

    for (let el of elements) {
        if (el.children.length === 0) {
            let text = el.innerText?.trim();
            if (text && text.length < 40) {
                let match = text.match(priceRegex);
                if (match) {
                    let str = toEn(match[0].replace(/[.,٬٫]/g, ''));
                    let price = parseInt(str);
                    if (!isNaN(price) && price > 10000) { firstPrice = price; minNode = el; break; }
                }
            }
        }
    }

    if (firstPrice === null) return { price: 0, store: 'نامشخص', title: title };

    let storeName = 'نامشخص';
    let parent = minNode.parentElement;
    for (let i = 0; i < 6; i++) {
        if (!parent) break;
        const link = parent.querySelector('a[href*=shops], a[href*=""shop""]');
        if (link) {
            const img = link.querySelector('img[alt]');
            if (img?.alt?.length > 1) { storeName = img.alt; break; }
            if (link.innerText?.trim().length > 1) { storeName = link.innerText.trim(); break; }
        }
        const img = parent.querySelector('img[alt]');
        if (img?.alt?.length > 1 && !img.alt.includes('Torob') && !img.alt.includes('ترب')) { storeName = img.alt; break; }
        const spans = parent.querySelectorAll('a, span, div, p, h3, b');
        for (let span of spans) {
            if (span.children.length === 0) {
                const spanText = span.innerText?.trim();
                if (spanText && spanText.length > 1 && spanText.length < 25 && !spanText.includes('تومان') && !spanText.match(/\d/) && !spanText.includes('افزودن') && !spanText.includes('سبد')) { storeName = spanText; break; }
            }
        }
        if (storeName !== 'نامشخص') break;
        parent = parent.parentElement;
    }
    return { price: firstPrice, store: storeName, title: title };
}";

    // ═══════════════════════════════════════════════════════════════════
    //  موتور استخراج دسته‌بندی — تابع یگانه (Playwright سازگار)
    //  جای‌نگهدارها: __PROFILE_CONTAINERS__ / __PROFILE_TITLES__ /
    //               __PROFILE_PRICES__ / __MAX_PRICE__
    // ═══════════════════════════════════════════════════════════════════
    private const string CategoryExtractorTemplate = @"() => {
    const profileContainers = __PROFILE_CONTAINERS__;
    const profileTitles = __PROFILE_TITLES__;
    const profilePrices = __PROFILE_PRICES__;
    const MAX_PRICE = parseInt('__MAX_PRICE__');
    const FORCE_PAGINATION = __FORCE_PAGINATION__;

    const toEn = (str) => str.replace(/[۰-۹]/g, d => '۰۱۲۳۴۵۶۷۸۹'.indexOf(d));
    const cleanPrice = (str) => {
        if (!str) return 0;
        let clean = toEn(String(str)).replace(/[.,٬٫\s]|تومان|ریال/g, '');
        let num = parseInt(clean);
        return isNaN(num) ? 0 : num;
    };
    // پاک‌سازی اسم: حذف پسوند فروشگاه (آلودگی رایج JSON-LD مانند «... ☕️ فروشگاه قهوه ننجون»)
    const cleanName = (raw) => {
        let n = (raw || '').trim().replace(/\s+/g, ' ');
        let fk = n.indexOf('فروشگاه');
        if (fk > 10) n = n.substring(0, fk);
        // حذف ایموجی/جداکننده انتهایی — \uFE0F و \u200D بخشی از ایموجی‌های ترکیبی هستند
        n = n.replace(/[☕✨⭐|~\s\uFE0F\u200D]+$/g, '').trim();
        return n;
    };
    // قیمت با جداکننده هزارگان یا بدون آن (حداقل ۵ رقم)
    const priceRegex = /([\d۰-۹]{1,3}(?:[.,٬٫][\d۰-۹]{3})+|[\d۰-۹]{5,})/;
    const badKeywords = ['cart', 'checkout', 'my-account', 'login', 'register', 'wishlist', 'compare', 'add-to-cart', 'wp-admin'];

    let products = [];
    let seenUrls = new Set();

    const addProduct = (url, name, price) => {
        url = (url || '').split('#')[0].split('?')[0];
        name = cleanName(name);
        if (!url || !url.startsWith('http')) return;
        if (name.length < 3 || name.length > 250) return;
        if (!price || price < 1000 || price > MAX_PRICE) return;
        if (seenUrls.has(url)) return;
        seenUrls.add(url);
        products.push({ url: url, name: name.substring(0, 150), price: price.toString() });
    };

    // ═══ استراتژی ۱: JSON-LD (Schema.org) — معتبرترین منبع ═══
    let strategy = 'json-ld';
    document.querySelectorAll('script[type=""application/ld+json""]').forEach(s => {
        try {
            let data = JSON.parse(s.textContent);
            let collect = (item) => {
                if (!item || typeof item !== 'object') return;
                let types = Array.isArray(item['@type']) ? item['@type'] : [item['@type']];
                if (types.includes('Product')) {
                    let offers = item.offers || {};
                    let offersList = Array.isArray(offers) ? offers : [offers];
                    let prices = offersList.map(o => parseFloat(o.price || o.lowPrice || o.highPrice || 0)).filter(p => p > 0);
                    let price = prices.length ? Math.min(...prices) : 0;
                    let url = item.url || (offersList[0] && offersList[0].url) || '';
                    addProduct(url, item.name, cleanPrice(price));
                }
                if (types.includes('ItemList') && Array.isArray(item.itemListElement))
                    item.itemListElement.forEach(li => collect(li.item || li));
                if (Array.isArray(item['@graph'])) item['@graph'].forEach(collect);
            };
            collect(data);
        } catch (e) {}
    });
    if (products.length >= 3) return { products: products, nextUrl: findNext(), strategy: strategy };

    // ═══ استراتژی ۲: Microdata (itemprop) ═══
    products = []; seenUrls = new Set();
    strategy = 'microdata';
    document.querySelectorAll('[itemtype*=""schema.org/Product""]').forEach(card => {
        let name = (card.querySelector('[itemprop=""name""]')?.textContent || '').trim();
        let priceEl = card.querySelector('[itemprop=""price""], [itemprop=""lowPrice""]');
        let price = cleanPrice(priceEl?.getAttribute('content') || priceEl?.textContent || '0');
        let link = card.querySelector('a[itemprop=""url""], a[href]');
        let img = card.querySelector('img[itemprop=""image""], img');
        if (name.length < 3 && img?.alt) name = img.alt.trim();
        if (link?.href) addProduct(link.href, name, price);
    });
    if (products.length >= 3) return { products: products, nextUrl: findNext(), strategy: strategy };

    // ═══ استراتژی ۳: DOM هوشمند (پروفایل سایت + ووکامرس + هیوریستیک) ═══
    products = []; seenUrls = new Set();
    strategy = 'dom';

    let containerSelectors = [
        ...profileContainers,
        'li.product', 'div.product', '.product-item', '.wc-block-grid__product',
        'article.product', '.type-product', '.jet-engine-listing-item',
        '[class*=""product-card""]', '[class*=""product-box""]', '[class*=""product-item""]',
        '[class*=""products__item""]', '[data-product-id]', '.item-product', '.product-card'
    ];
    let containers = [];
    let usedSelector = '';
    for (let sel of containerSelectors) {
        try {
            let found = Array.from(document.querySelectorAll(sel))
                .filter(el => el.querySelector('a[href]') && el.textContent.match(priceRegex));
            if (found.length > containers.length) {
                containers = found;
                usedSelector = sel;
                if (found.length >= 4) break;
            }
        } catch (e) {}
    }

    if (containers.length === 0) {
        let links = document.querySelectorAll('a[href*=""/product/""], a[href*=""/p/""]');
        let parentMap = new Map();
        links.forEach(link => {
            let parent = link.closest('div, li, article, figure');
            while (parent && parent !== document.body) {
                // محدودیت اندازه: کارت محصول کوچک است؛ والدِ بزرگ = بخش نامرتبط صفحه (مثل ورود/ثبت‌نام)
                if (parent.textContent.match(priceRegex) &&
                    parent.querySelectorAll('a[href*=""/product/""], a[href*=""/p/""]').length <= 3 &&
                    parent.textContent.trim().length < 1500) {
                    parentMap.set(parent, true);
                    break;
                }
                parent = parent.parentElement;
            }
        });
        containers = Array.from(parentMap.keys());
    }

    containers.forEach(card => {
        // اسم محصول: پروفایل سایت → استانداردها → title attribute → img alt
        let name = '';
        let titleSelectors = [
            ...profileTitles,
            '.woocommerce-loop-product__title', '.product-title', '.wd-product-title',
            'h2 a', 'h3 a', 'h4 a', 'h2', 'h3', 'h4',
            '[itemprop=""name""]', '[class*=""-title""]', '[class*=""title""]',
            'p a', '.name'
        ];
        for (let sel of titleSelectors) {
            let el = card.querySelector(sel);
            if (el) {
                let t = (el.getAttribute('title') || el.textContent || '').trim().replace(/\s+/g, ' ');
                if (t.length >= 3 && t.length < 200) { name = t; break; }
            }
        }
        if (name.length < 3) {
            let link = card.querySelector('a[href*=""/product/""], a[href*=""/p/""]');
            if (link) name = (link.getAttribute('title') || link.textContent || '').trim();
        }
        if (name.length < 3) {
            let img = card.querySelector('img[alt]');
            if (img) name = img.alt.trim();
        }
        if (name.length < 3) return;

        // قیمت: پروفایل سایت → ins (تخفیف‌دار) → amount → microdata → بقیه
        let price = 0;
        let priceSelectors = [
            ...profilePrices,
            '.price ins .amount', '.price ins', '.price .amount',
            '.woocommerce-Price-amount', '[class*=""price""] .amount',
            '[itemprop=""price""]', '[class*=""final-price""]', '[class*=""current-price""]',
            '.price', 'bdi'
        ];
        for (let sel of priceSelectors) {
            let el = card.querySelector(sel);
            if (!el) continue;
            let content = el.getAttribute('content');
            if (content) {
                let p = cleanPrice(content);
                if (p >= 1000) { price = p; break; }
            }
            let match = el.textContent.match(priceRegex);
            if (match) {
                let p = cleanPrice(match[1]);
                if (p >= 1000) { price = p; break; }
            }
        }
        if (price < 1000) {
            let re = new RegExp(priceRegex.source, 'g');
            let matches = card.textContent.match(re);
            if (matches) {
                let candidates = matches.map(m => cleanPrice(m)).filter(p => p >= 1000);
                if (candidates.length) price = Math.min(...candidates);
            }
        }
        if (price < 1000) return;

        // لینک محصول
        let link = card.querySelector('a[href*=""/product/""], a.woocommerce-LoopProduct-link, a.product-item-link, h2 a, h3 a, h4 a, p a, [class*=""title""] a, a[href]');
        if (!link?.href) return;
        let href = link.href;
        let lower = href.toLowerCase();
        if (badKeywords.some(kw => lower.includes(kw))) return;
        if (link.closest('header, footer, nav, aside, .sidebar, .widget, .pagination, .woocommerce-pagination, .breadcrumb')) return;

        addProduct(href, name, price);
    });

    return { products: products, nextUrl: findNext(), strategy: strategy + (usedSelector ? ':' + usedSelector : '') };

    // ═══ پیدا کردن صفحه بعد ═══
    function findNext() {
        let nextSelectors = ['a.next.page-numbers', 'a.next', 'a[rel=""next""]',
            '.pagination .next', '.woocommerce-pagination a.next', 'a.pagination-next',
            '[class*=""next-page""]'];
        for (let sel of nextSelectors) {
            let el = document.querySelector(sel);
            if (el?.href && !el.href.endsWith('#')) return el.href;
        }
        try {
            let url = new URL(window.location.href);
            // ✅ رفع باگ: پارامتر صحیح (paged یا page) افزایش می‌یابد — قبلاً همیشه paged
            //    نوشته می‌شد و سایت‌های ?page=N در صفحه اول گیر می‌کردند
            let pagedParam = url.searchParams.has('paged') ? 'paged'
                           : (url.searchParams.has('page') ? 'page' : null);
            if (pagedParam) {
                let page = parseInt(url.searchParams.get(pagedParam));
                if (!isNaN(page)) {
                    url.searchParams.set(pagedParam, page + 1);
                    return url.toString();
                }
            }
            let match = url.pathname.match(/page\/(\d+)\/?$/);
            if (match) return url.origin + url.pathname.replace(/page\/\d+\/?$/, 'page/' + (parseInt(match[1]) + 1) + '/') + (url.search || '');
            let hasPaginationMarker = document.querySelector('.page-numbers a, .pagination a');
            // FORCE_PAGINATION: برای سایت‌هایی که لینک صفحه‌بندی ندارند اما /page/N/ کار می‌کند
            if ((hasPaginationMarker || FORCE_PAGINATION) && products.length > 0)
                return url.origin + url.pathname.replace(/\/$/, '') + '/page/2/' + (url.search || '');
        } catch (e) {}
        return null;
    }
}";
}
