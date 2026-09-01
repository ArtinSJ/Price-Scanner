# بازارسنج | BazarSanj

**English:** BazarSanj is a free, open-source Windows (WPF) desktop tool for scanning, tracking and comparing product prices across Iranian online stores — powered by an adaptive multi-strategy scraping engine (JSON-LD / Microdata / DOM) built on Playwright.

**فارسی:** بازارسنج ابزار دسکتاپ ویندوز برای اسکن، رصد و مقایسه‌ی قیمت محصولات فروشگاه‌های اینترنتی ایران است؛ با موتور اسکن تطبیقی چنداستراتژی (JSON-LD / Microdata / DOM) بر پایه‌ی Playwright، دیتابیس SQLite و رابط لوکس گلس‌مورفیسم با فونت فارسی Vazirmatn.

---

## 📥 دانلود

به صفحه‌ی [Releases](https://github.com/ArtinSJ/Price-Scanner/releases/latest) بروید و یکی از دو بسته را انتخاب کنید:

| بسته | مناسب برای | پیش‌نیاز |
|------|-----------|----------|
| `BazarSanj-*-Portable-win-x64.zip` | **اکثر کاربران** — بدون هیچ نصبی؛ node.exe و کرومیوم داخل بسته هستند | هیچ (ویندوز ۱۰/۱۱ x64) |
| `BazarSanj-*-Lite-win-x64.zip` | کاربران حرفه‌ای — حجم کم؛ درایور node.exe داخل بسته | .NET 9 Desktop Runtime + مرورگر سیستم (Edge/Chrome) |

### اجرای نسخه‌ی پرتابل
1. فایل ZIP را دانلود و در یک پوشه Extract کنید (مسیر فارسی نداشته باشد؛ مثلاً `C:\BazarSanj`)
2. `BazarSanj.exe` را اجرا کنید — بدون نصب .NET، بدون Node.js، بدون مرورگر
3. اگر ویندوز فایل را بلاک کرد: روی ZIP راست‌کلیک → Properties → تیک **Unblock** → OK، دوباره Extract کنید
   (یا در PowerShell: `Get-ChildItem -Recurse | Unblock-File`)
4. اگر آنتی‌ویروس فایل‌ها را قرنطینه کرد، پوشه را در استثناهای آنتی‌ویروس اضافه کنید — پیام خطای برنامه در این حالت راهنمای فارسی سه‌مرحله‌ای نمایش می‌دهد

## 🚀 بیلد از سورس

پیش‌نیاز: [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (ویندوز با Desktop Runtime WPF)

```bash
git clone https://github.com/ArtinSJ/Price-Scanner.git
cd Price-Scanner/TorobScanner
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

> **پیش‌نیاز مرورگر (فقط نسخه‌ی بیلدشده از سورس):** برنامه خودش کرومیوم → Edge → Chrome را به ترتیب امتحان می‌کند و در صورت نیاز نصب خودکار دارد. می‌توانید دستی هم نصب کنید: `npx playwright install chromium`

## ✨ امکانات

- **موتور اسکن تطبیقی** — به‌جای سلکتورهای ثابت، هر سایت پروفایل اختصاصی دارد و موتور جهانی استراتژی برنده را خودکار انتخاب می‌کند (JSON-LD → Microdata → DOM)
- **صفحه‌بندی هوشمند** — پشتیبانی از زنجیره‌ی `rel=next`، صفحه‌بندی کلاسیک، دکمه‌ی AJAX «بارگیری بیشتر» و تشخیص ریدایرکت‌های سئو (مثل 301 کردن Rank Math) تا محصولات گم نشوند و ویجت‌های مقصد به لیست تزریق نشوند
- **فیلتر فروش‌رفته/ناموجود** — سه‌لایه‌ای (JSON-LD availability → Microdata → برچسب DOM)؛ فقط محصولات قابل خرید قیمت‌گذاری می‌شوند
- **مقایسه‌ی گروهی محصولات** از فروشگاه‌های مختلف + رصد تغییر قیمت با حباب‌های رنگی
- **جستجوی زنده** در لیست محصولات با نرمال‌سازی فارسی (ی/ک عربی، نیم‌فاصله)
- **بروزرسانی خودکار درون‌برنامه‌ای** از GitHub Releases
- **امنیت ورودی‌ها** (SQL Parameterized) + لاگ ساختاریافته + ایمپورت/اکسپورت Excel

## 🌐 سایت‌های پشتیبانی‌شده (تحلیل‌شده و تست‌شده)

| سایت | تم | استراتژی برنده | نکات |
|------|-----|----------------|------|
| **سام‌کیش** (samkish.com) | Bakala سفارشی | DOM | صفحه‌بندی a.next |
| **سیلاین** (cylline.com) | Bakala | DOM | ۳ لایه صفحه‌بندی |
| **کافه‌کالا** (coffekala.com) | Woodmart | DOM + LoadMore | دکمه AJAX «بارگیری بیشتر» + محافظ ریدایرکت |
| **کافه‌این‌شاپ** (cafe-inshop.com) | Woodmart | DOM | همه محصولات در یک صفحه |
| **کافئین‌اکسپرس** (caffeinexpress.ir) | Elementor Loop | DOM | قیمت فارسی |
| **قهوه ننجون** (nanjuncoffee.com) | Woodmart + JSON-LD | **JSON-LD** | قیمت تخفیفی درست |
| سایت‌های دیگر | — | موتور Universal | خودکار |

سایت جدید؟ `Scrapers/SiteProfile.cs` یک پروفایل ۲۰-۳۰ خطی می‌خواهد؛ در بدترین حالت موتور Universal هم شانس خوبی دارد.

## 🏗️ معماری (خلاصه)

```
TorobScanner/
├── MainWindow.xaml(.cs)          UI اصلی + جستجوی زنده
├── MainWindow.Compare.cs         مقایسه‌ی گروهی
├── Scrapers/
│   ├── AdaptiveScraper.cs        موتور تطبیقی (استراتژی‌ها + LoadMore + محافظ ریدایرکت)
│   ├── SiteProfile.cs            پروفایل هر سایت (سلکتورها، استراتژی، LoadMoreSelectors)
│   ├── SiteScrapers.cs           پروفایل‌های اختصاصی سایت‌ها
│   ├── ExtractorScripts.cs       اسکریپت‌های استخراج داخل مرورگر (JSON-LD/Microdata/DOM + فیلتر OOS)
│   ├── BrowserLauncher.cs        کرومیوم→Edge→Chrome + حالت پرتابل (node.exe باندل)
│   └── TorobProductScraper.cs    اسکنر تخصصی Torob
├── Data/DatabaseManager.cs       SQLite (WAL + Connection Pooling)
├── Services/                     تم، تنظیمات، لاگ، ایمپورت/اکسپورت، بروزرسانی خودکار
└── Themes/                       LuxTheme + LuxPalette (گلس‌مورفیسم)
```

## 📋 گزارش تغییرات

تاریخچه‌ی کامل نسخه‌ها و ریشه‌یابی باگ‌ها در [BUGFIX-CHANGELOG.md](BUGFIX-CHANGELOG.md) آمده است.

## 📄 مجوز

این پروژه با مجوز [MIT](LICENSE) منتشر شده است.
