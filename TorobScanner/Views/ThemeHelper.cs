using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace TorobScanner.Views;

/// <summary>
/// ✨ تم لوکس Platinum-Glass (v2.5):
/// استایل‌های کنترل‌ها حالا سراسری‌اند (Themes/LuxTheme.xaml در App.xaml).
/// این کلاس فقط فونت باندل‌شده Vazirmatn را ست می‌کند + انیمیشن ورود نرم به هر پنجره.
///
/// ✅ رفع باگ ۲۰ (v2.5.2) — «Transform is not valid for Window»:
/// کلاس Window در WPF (چون یک HWND واقعی است) هیچ RenderTransform غیر همانی را
/// قبول نمی‌کند و همان لحظه InvalidOperationException می‌دهد. قبلاً
/// window.RenderTransform = TranslateTransform(...) می‌نوشتیم → کرش استارتاپ.
/// حالا انیمیشن ورود دو بخش شده:
///   • Fade (شفافیت) روی خود پنجره — مجاز و امن
///   • Rise (حرکت رو به بالا) روی محتوای ریشه‌ی پنجره — همان جلوه بصری، بدون خطا
/// </summary>
public static class ThemeHelper
{
    public static void ApplyObsidianTheme(Window window)
    {
        // ✨ v2.6: آیکون لوکس بازارسنج — تسک‌بار، Alt+Tab و تیتر همه‌ی پنجره‌ها
        try
        {
            window.Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/BazarSanjIcon.png"));
        }
        catch { /* آیکون حیاتی نیست — هرگز استارت را نیندازد */ }

        // فونت فارسی باندل‌شده (وزن‌ها: Light/Regular/Medium/SemiBold/Bold)
        window.FontFamily = LuxUI.Font;
        window.FontSize = 13;

        // شروع نامرئی — fade روی خود پنجره (Opacity روی Window مجاز است)
        window.Opacity = 0;

        RoutedEventHandler? onLoaded = null;
        onLoaded = (s, e) =>
        {
            window.Loaded -= onLoaded;

            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(190))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            window.BeginAnimation(UIElement.OpacityProperty, fade);

            // ✅ Rise روی محتوای ریشه (نه خود Window!) — RenderTransform روی
            // UIElement معمولی کاملاً مجاز است؛ فقط روی Window ممنوع است.
            if (window.Content is UIElement root)
            {
                var rise = new TranslateTransform(0, 12);
                root.RenderTransform = rise;
                var riseAnim = new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(230))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                rise.BeginAnimation(TranslateTransform.YProperty, riseAnim);
            }
        };
        window.Loaded += onLoaded;
    }
}
