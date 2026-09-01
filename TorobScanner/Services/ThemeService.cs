using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace TorobScanner.Services;

/// <summary>نوع دکور پس‌زمینه — هر سبک طراحی، فضای متفاوتی دارد</summary>
public enum ThemeDecor
{
    Nebulas,  // سحابی‌های نرم زنده (گلس‌مورفیسم)
    Plain,    // بوم کاملاً تمیز و مات (بنتو گرید اپل)
    Aurora,   // سه هاله‌ی آرورای بنفش-فیروزه‌ای-صورتی (Linear/Stripe)
    Couture   // پرده‌ی طلای شامپاینی بسیار کم‌جان (کوتور نویر)
}

/// <summary>
/// ✨ v2.8 — هر تم یک «زبان طراحی» کامل است، نه فقط یک پالت رنگ:
/// شعاع گوشه‌ها، ضخامت خط دور، جنس سایه، دکور پس‌زمینه و وزن تیترها
/// همه برای هر سبک متفاوت‌اند.
/// </summary>
public sealed record LuxStyle(
    double WinRadius, double SideRadius, double CardRadius, double BtnRadius,
    double InputRadius, double PillRadius, double ChipRadius, double LogoRadius,
    double CardBorderThick, double BtnBorderThick, double ChipBorderThick,
    string CardFill, string CardFillStrong, string CardStroke, string SidebarFill,
    string ChipFill, string ChipFillHover,
    string PrimaryBtnStroke, string PrimaryBtnText,
    string GhostFill, string HoverFill, string SelectedFill, string SelectedText,
    string PopupBg, string Separator,
    string ScrollThumb, string ScrollThumbHover, string ScrollThumbDrag,
    string GridLine, string HoverLayer, string Hairline,
    string LogoText, string LogoGlow,
    string SuccessText, string DangerText,
    ThemeDecor Decor, bool IsLight, FontWeightsHolder HeaderWeight,
    string CardShadowColor, double CardShadowBlur, double CardShadowDepth,
    double CardShadowDirection, double CardShadowOpacity);

/// <summary>تعریف یک تم لوکس — رنگ‌ها + سبک طراحی</summary>
public sealed record LuxThemeDef(
    string Id, string NameFa, string NameEn, string Desc,
    string BgA, string BgB, string BgC,
    string TextPrimary, string TextSecondary, string TextDim,
    string Platinum, string Accent, string Success, string Danger, string Warning,
    string IriA, string IriB, string IriC,
    string NebulaA, string NebulaB,
    string MetalA, string MetalB, string MetalC,
    LuxStyle Style);

/// <summary>نگه‌دارنده‌ی وزن فونت برای رکورد (FontWeight ساخت‌پذیر نیست)</summary>
public sealed class FontWeightsHolder
{
    public FontWeight Value { get; init; } = FontWeights.SemiBold;
    public static implicit operator FontWeight(FontWeightsHolder h) => h.Value;
    public static FontWeightsHolder Of(FontWeight w) => new() { Value = w };
}

/// <summary>
/// ✨ v2.9 — ۴ تم برنده‌ی جوایز طراحی، هر کدام یک «زبان طراحی» کاملاً مستقل:
///
///   ۱) پلاتینیوم گلس — گلس‌مورفیسم اپل ویژن‌پرو؛ شیشه‌های مات، گوشه‌های بزرگ،
///      سحابی‌های زنده روی ابسیدین
///   ۲) بنتو سفید — بوم سفید خالص اپل (apple.com)؛ کاشی‌های سفید بدون سایه
///      که فقط با خط مویی #D2D2D7 جدا می‌شوند، چیپ‌های خاکستری روشن،
///      تایپ درشت سیاه و آبی کلاسیک اپل — انتخاب عاشقان لایت‌مود
///   ۳) آرورا گلو — امضای Linear/Stripe برنده‌ی Awwwards؛ شب سرمه‌ای عمیق
///      با هاله‌های زنده‌ی بنفش-فیروزه‌ای-صورتی، متن‌های گرادیانی نورانی و
///      هاله‌ی بنفش دور کارت‌ها
///   ۴) کوتور نویر — لوکس خانه‌های مد (Gucci/Dior برنده‌ی FWA)؛ ابسیدین
///      گرم و طلای شامپاینی، خطوط مویی تیز و کم‌شعاع، متن طلایی گرادیانی،
///      دکمه‌ی طلایی با متن مشکی و تیترهای لطیف Light
///
/// مکانیزم تعویض زنده: پالت + توکن‌های سبک در دیکشنری جدا هستند و استایل‌ها
/// با DynamicResource به آن‌ها نگاه می‌کنند؛ Apply() همین دیکشنری را با
/// دیکشنری تم جدید جایگزین می‌کند → همه‌ی کنترل‌ها فوراً عوض می‌شوند.
/// پنجره‌ی اصلی هم رخداد ThemeChanged را می‌گیرد و محتوای خود را بازسازی
/// می‌کند (شکل‌ها، سایه‌ها و دکور پس‌زمینه هم از نو ساخته می‌شوند).
/// </summary>
public static class ThemeService
{
    /// <summary>پس از هر تعویض تم (نه در استارت) صدا زده می‌شود</summary>
    public static event Action? ThemeChanged;

    private static LuxStyle S(
        double win, double side, double card, double btn, double input, double pill, double chip, double logo,
        double cardBt, double btnBt, double chipBt,
        string cardFill, string cardFillStrong, string cardStroke, string sidebarFill,
        string chipFill, string chipHover,
        string primaryStroke, string primaryText,
        string ghost, string hover, string selected, string selectedText,
        string popupBg, string separator,
        string thumb, string thumbHover, string thumbDrag,
        string gridLine, string hoverLayer, string hairline,
        string logoText, string logoGlow, string successText, string dangerText,
        ThemeDecor decor, bool isLight, FontWeight weight,
        string shColor, double shBlur, double shDepth, double shDir, double shOpacity)
        => new(win, side, card, btn, input, pill, chip, logo, cardBt, btnBt, chipBt,
               cardFill, cardFillStrong, cardStroke, sidebarFill, chipFill, chipHover,
               primaryStroke, primaryText, ghost, hover, selected, selectedText,
               popupBg, separator, thumb, thumbHover, thumbDrag, gridLine, hoverLayer,
               hairline, logoText, logoGlow, successText, dangerText, decor, isLight,
               FontWeightsHolder.Of(weight), shColor, shBlur, shDepth, shDir, shOpacity);

    /// <summary>۴ تم لوکس — به ترتیب نمایش در تنظیمات</summary>
    public static readonly List<LuxThemeDef> All = new()
    {
        // ═══ ۱) پلاتینیوم گلس — گلس‌مورفیسم ویژن‌پرو ═══
        new LuxThemeDef(
            "platinum", "پلاتینیوم گلس", "Platinum Glass",
            "گلس‌مورفیسم ویژن‌پرو — شیشه‌های مات و هاله‌های نرم روی ابسیدین، با سحابی‌های زنده",
            "#0B0D12", "#11141B", "#0D1016",
            "#F3F6FB", "#A9B2C1", "#6E7889",
            "#F4F7FB", "#8FB8FF", "#71E6A8", "#FF9191", "#FFCB7D",
            "#A9C7FF", "#D8C6FF", "#F1D9FF",
            "#265B7DB8", "#2B8E7CC3",
            "#F8FAFD", "#DDE4EE", "#C9D3E1",
            S(16, 18, 14, 11, 10, 8, 10, 14,
              1, 1, 1,
              "#12FFFFFF", "#1AFFFFFF", "#22FFFFFF", "#1EFFFFFF",
              "#18FFFFFF", "#32FFFFFF",
              "#40FFFFFF", "#0B0D12",
              "#08FFFFFF", "#12FFFFFF", "#2B8FB8FF", "#F3F6FB",
              "#F20E1117", "#16FFFFFF",
              "#24FFFFFF", "#3DFFFFFF", "#52FFFFFF",
              "#0FFFFFFF", "#FFFFFF", "#2CFFFFFF",
              "#0B0D12", "#C9D8EE", "#BFF3D9", "#FFC9C9",
              ThemeDecor.Nebulas, false, FontWeights.SemiBold,
              "#000000", 0, 0, 315, 0)),

        // ═══ ۲) بنتو سفید — بوم سفید خالص اپل؛ خط مویی به‌جای سایه ═══
        // برای عاشقان لایت‌مود: هیچ رنگی روی بوم نیست — کاشی‌های سفید فقط با
        // خط مویی دقیق #D2D2D7 از هم جدا می‌شوند و چیپ‌های خاکستری روشن
        // (سوییچ‌ها و ورودی‌ها) روی سفید می‌نشینند؛ امضای apple.com
        new LuxThemeDef(
            "bento", "بنتو سفید", "Pure White Bento",
            "بوم سفید خالص برای عاشقان لایت‌مود — کاشی‌های سفید با خط مویی دقیق اپل، بدون سایه و بدون رنگ اضافه؛ پاکیزگی محض مثل apple.com",
            "#FFFFFF", "#FDFDFE", "#FFFFFF",
            "#1D1D1F", "#6E6E73", "#AEAEB2",
            "#1D1D1F", "#0071E3", "#34C759", "#FF3B30", "#FF9500",
            "#0A84FF", "#5E5CE6", "#BF5AF2",
            "#00FFFFFF", "#00FFFFFF",
            "#0A84FF", "#0071E3", "#006EDB",
            S(20, 20, 20, 12, 12, 7, 12, 14,
              1, 1, 1,
              "#FFFFFF", "#FFFFFF", "#FFD2D2D7", "#FFFFFFFF",
              "#FFF5F5F7", "#FFE8E8ED",
              "#00FFFFFF", "#FFFFFFFF",
              "#FFF5F5F7", "#FFF5F5F7", "#FFE8F0FE", "#FF0071E3",
              "#FFFFFFFF", "#FFE5E5EA",
              "#FFC7C7CC", "#FFAEAEB2", "#FF8E8E93",
              "#FFE5E5EA", "#0A1D1D1F", "#FFD2D2D7",
              "#FFFFFFFF", "#00FFFFFF", "#FF1E8E3E", "#FFD70015",
              ThemeDecor.Plain, true, FontWeights.Bold,
              "#00000000", 0, 0, 270, 0)),

        // ═══ ۳) آرورا گلو — امضای Linear/Stripe برنده‌ی Awwwards ═══
        // شب سرمه‌ای عمیق + هاله‌های زنده‌ی بنفش/فیروزه‌ای/صورتی + متن گرادیانی نورانی
        new LuxThemeDef(
            "aurora", "آرورا گلو", "Aurora Glow",
            "شب‌زاه نور — هاله‌های زنده‌ی بنفش-فیروزه‌ای-صورتی روی شب سرمه‌ای، امضای Linear و Stripe برنده‌ی Awwwards",
            "#05060F", "#0A0D1F", "#070A16",
            "#EEF1FF", "#9AA3C9", "#5C6488",
            "#F1F3FF", "#8B76FF", "#4ADE80", "#F87171", "#FBBF24",
            "#8B76FF", "#38BDF8", "#F472B6",
            "#598B76FF", "#4D38BDF8",
            "#9D8BFF", "#7C5CFF", "#38BDF8",
            S(16, 18, 12, 9, 8, 6, 8, 12,
              1, 1, 1,
              "#B3131630", "#CC171B38", "#338B76FF", "#99111428",
              "#1A8B76FF", "#2E8B76FF",
              "#00FFFFFF", "#FFFFFFFF",
              "#0F8B76FF", "#1A8B76FF", "#2E8B76FF", "#FFFFFFFF",
              "#F20C0F20", "#268B76FF",
              "#2E8B76FF", "#4D8B76FF", "#668B76FF",
              "#148B76FF", "#0F8B76FF", "#668B76FF",
              "#FFE9EDFF", "#B7A6FF", "#FF86EFAC", "#FFFDA4AF",
              ThemeDecor.Aurora, false, FontWeights.SemiBold,
              "#8B76FF", 16, 0, 315, 0.12)),

        // ═══ ۴) کوتور نویر — لوکس خانه‌های مد (Gucci/Dior برنده‌ی FWA) ═══
        // ابسیدین گرم + طلای شامپاینی + خطوط مویی تیز + تیتر Light
        new LuxThemeDef(
            "couture", "کوتور نویر", "Couture Noir",
            "لوکس مد روز — ابسیدین گرم و طلای شامپاینی با خطوط مویی تیز، به سبک خانه‌های مد Gucci و Dior برنده‌ی FWA",
            "#0A0806", "#141009", "#0D0A07",
            "#F5EFE2", "#B3A88E", "#7C7460",
            "#F5EFE2", "#D9B45B", "#A8C686", "#E07B6C", "#E5C15C",
            "#F6E27A", "#D4AF37", "#9C7A2E",
            "#3DD4AF37", "#386E4F1F",
            "#F6E27A", "#D4AF37", "#8F6D24",
            S(10, 12, 8, 5, 5, 3, 5, 8,
              1, 1, 1,
              "#F217130D", "#F81B1710", "#40D4AF37", "#F2100E09",
              "#1AD4AF37", "#2ED4AF37",
              "#998F6D24", "#FF0A0806",
              "#0FD4AF37", "#1AD4AF37", "#2ED4AF37", "#FFF6E27A",
              "#F60F0D09", "#26D4AF37",
              "#3DD4AF37", "#52D4AF37", "#66D4AF37",
              "#14D4AF37", "#0AD4AF37", "#99D4AF37",
              "#FFF5EFE2", "#D4AF37", "#FFCBE5C0", "#FFF5B8AD",
              ThemeDecor.Couture, false, FontWeights.Light,
              "#00000000", 0, 0, 315, 0))
    };

    /// <summary>تم فعال فعلی</summary>
    public static LuxThemeDef Current { get; private set; } = All[0];

    /// <summary>تم بر اساس شناسه</summary>
    public static LuxThemeDef GetById(string id) =>
        All.FirstOrDefault(t => t.Id == id) ?? All[0];

    /// <summary>اعمال تم ذخیره‌شده هنگام استارت — بدون شلیک رخداد (پنجره‌ها هنوز باز نشده‌اند)</summary>
    public static void ApplySaved()
    {
        var def = GetById(SettingsService.Current.ThemeId);
        ApplyCore(def);
    }

    /// <summary>تعویض تم + ذخیره + اعلام به پنجره‌ها (تعویض زنده)</summary>
    public static void Apply(LuxThemeDef def)
    {
        if (def.Id == Current.Id) return;
        ApplyCore(def);
        SettingsService.Current.ThemeId = def.Id;
        SettingsService.Save();
        ThemeChanged?.Invoke();
    }

    private static void ApplyCore(LuxThemeDef def)
    {
        Current = def;
        ReplaceThemeDictionary(def);
        Views.LuxUI.SetPalette(def);
    }

    // ═══════════ ساخت دیکشنری کدی ═══════════

    /// <summary>
    /// دیکشنری پالت + توکن‌های سبک تم را می‌سازد و جای MergedDictionaries[0]
    /// می‌نشیند؛ چون استایل‌های LuxTheme.xaml DynamicResource به این کلیدها
    /// دارند، همه‌ی کنترل‌های زنده بلافاصله رنگ و شکل جدید می‌گیرند.
    /// </summary>
    private static void ReplaceThemeDictionary(LuxThemeDef def)
    {
        var st = def.Style;
        var d = new ResourceDictionary();

        // ─── رنگ‌ها ───
        C(d, "LuxBgAColor", def.BgA);
        C(d, "LuxBgBColor", def.BgB);
        C(d, "LuxBgCColor", def.BgC);
        C(d, "LuxTextPrimaryColor", def.TextPrimary);
        C(d, "LuxTextSecondaryColor", def.TextSecondary);
        C(d, "LuxTextDimColor", def.TextDim);
        C(d, "LuxPlatinumColor", def.Platinum);
        C(d, "LuxAccentColor", def.Accent);
        C(d, "LuxSuccessColor", def.Success);
        C(d, "LuxDangerColor", def.Danger);
        C(d, "LuxWarningColor", def.Warning);

        // ─── براش‌های متنی ───
        B(d, "LuxTextPrimary", def.TextPrimary);
        B(d, "LuxTextSecondary", def.TextSecondary);
        B(d, "LuxTextDim", def.TextDim);
        B(d, "LuxPlatinum", def.Platinum);
        B(d, "LuxAccent", def.Accent);
        B(d, "LuxSuccess", def.Success);
        B(d, "LuxDanger", def.Danger);
        B(d, "LuxWarning", def.Warning);

        // ─── سطوح کارت/شیشه — مقدارشان کاملاً وابسته به سبک است ───
        // گلس: شیشه‌های نیمه‌شفاف | بروتال: کاغذ سفید | سایبر: تیره‌ی توپر | کلود: سفید
        B(d, "LuxGlassFill", st.CardFill);
        B(d, "LuxGlassFillStrong", st.CardFillStrong);
        B(d, "LuxGlassStroke", st.CardStroke);
        B(d, "LuxGlassStrokeSoft", st.CardStroke);
        B(d, "LuxFocusStroke", Alpha(def.Accent, st.IsLight ? 0x50 : 0x73));

        // ─── براش‌های سبکی ───
        B(d, "LuxSidebarFill", st.SidebarFill);
        B(d, "LuxChipFill", st.ChipFill);
        B(d, "LuxGhostFill", st.GhostFill);
        B(d, "LuxHoverFill", st.HoverFill);
        B(d, "LuxSelectedFill", st.SelectedFill);
        B(d, "LuxSelectedText", st.SelectedText);
        B(d, "LuxPopupBg", st.PopupBg);
        B(d, "LuxSeparator", st.Separator);
        B(d, "LuxScrollThumb", st.ScrollThumb);
        B(d, "LuxScrollThumbHover", st.ScrollThumbHover);
        B(d, "LuxScrollThumbDrag", st.ScrollThumbDrag);
        B(d, "LuxGridLine", st.GridLine);
        B(d, "LuxHoverLayer", st.HoverLayer);
        B(d, "LuxPrimaryBtnStroke", st.PrimaryBtnStroke);
        B(d, "LuxPrimaryBtnText", st.PrimaryBtnText);
        B(d, "LuxLogoText", st.LogoText);
        B(d, "LuxSuccessText", st.SuccessText);
        B(d, "LuxDangerText", st.DangerText);
        B(d, "LuxSelection", Alpha(def.Accent, st.IsLight ? 0x40 : 0x5A));

        // دکمه‌های خطر/موفق — پس‌زمینه‌ی نیمه‌شفاف از رنگ خودشان
        B(d, "LuxDangerFill", Alpha(def.Danger, 0x24));
        B(d, "LuxDangerBorder", Alpha(def.Danger, st.IsLight ? 0x8C : 0x3D));
        B(d, "LuxSuccessFill", Alpha(def.Success, 0x24));
        B(d, "LuxSuccessBorder", Alpha(def.Success, st.IsLight ? 0x8C : 0x3D));
        B(d, "LuxStopFill", Alpha(def.Danger, 0x2B));
        B(d, "LuxStopBorder", Alpha(def.Danger, st.IsLight ? 0xA6 : 0x59));

        // ─── گرادیان‌ها ───
        d["LuxWindowBg"] = Frozen(new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(P(def.BgA), 0),
                new GradientStop(P(def.BgB), 0.55),
                new GradientStop(P(def.BgC), 1)
            }
        });

        d["LuxIridescent"] = Frozen(new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(P(def.IriA), 0),
                new GradientStop(P(def.IriB), 0.52),
                new GradientStop(P(def.IriC), 1)
            }
        });

        d["LuxPlatinumMetal"] = Frozen(new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(0, 1),
            GradientStops =
            {
                new GradientStop(P(def.MetalA), 0),
                new GradientStop(P(def.MetalB), 0.5),
                new GradientStop(P(def.MetalC), 1)
            }
        });

        d["LuxNebulaBlue"] = Frozen(new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(P(def.NebulaA), 0),
                new GradientStop(Translucent(def.NebulaA), 1)
            }
        });

        d["LuxNebulaLavender"] = Frozen(new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(P(def.NebulaB), 0),
                new GradientStop(Translucent(def.NebulaB), 1)
            }
        });

        // خط مویی زیر نوار عنوان — رنگش از سبک می‌آید
        d["LuxHairline"] = Frozen(new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(1, 0),
            GradientStops =
            {
                new GradientStop(Transparent(st.Hairline), 0),
                new GradientStop(P(st.Hairline), 0.5),
                new GradientStop(Transparent(st.Hairline), 1)
            }
        });

        // ─── توکن‌های شکل — قلب تفاوت سبک‌ها ───
        d["LuxWinRadius"] = new CornerRadius(st.WinRadius);
        d["LuxSideRadius"] = new CornerRadius(st.SideRadius);
        d["LuxCardRadius"] = new CornerRadius(st.CardRadius);
        d["LuxBtnRadius"] = new CornerRadius(st.BtnRadius);
        d["LuxInputRadius"] = new CornerRadius(st.InputRadius);
        d["LuxPillRadius"] = new CornerRadius(st.PillRadius);
        d["LuxChipRadius"] = new CornerRadius(st.ChipRadius);
        d["LuxCardBorderThick"] = new Thickness(st.CardBorderThick);
        d["LuxBtnBorderThick"] = new Thickness(st.BtnBorderThick);
        d["LuxChipBorderThick"] = new Thickness(st.ChipBorderThick);

        // ─── سایه‌ها — امضای بصری هر سبک ───
        // گلس: هاله‌ی نرم | بروتال: آفست سخت بدون بلور | سایبر: گلو نئونی | کلود: پخش لطیف
        d["LuxShadowCard"] = Frozen(new DropShadowEffect
        {
            BlurRadius = st.CardShadowBlur,
            ShadowDepth = st.CardShadowDepth,
            Direction = st.CardShadowDirection,
            Color = P(st.CardShadowColor),
            Opacity = st.CardShadowOpacity
        });

        if (st.IsLight)
        {
            // تم‌های روشن: سایه‌ی واقعیِ جهت‌دار، نه هاله‌ی سیاه
            d["LuxShadowDialog"] = Frozen(new DropShadowEffect
            { BlurRadius = 30, ShadowDepth = 5, Direction = 270, Color = P(st.CardShadowColor), Opacity = 0.30 });
            d["LuxShadowWindow"] = Frozen(new DropShadowEffect
            { BlurRadius = 46, ShadowDepth = 8, Direction = 270, Color = P("#1B1B26"), Opacity = 0.38 });
        }
        else
        {
            d["LuxShadowDialog"] = Frozen(new DropShadowEffect
            { BlurRadius = 34, ShadowDepth = 0, Color = Color.FromArgb(0xC0, 0, 0, 0), Opacity = 0.85 });
            d["LuxShadowWindow"] = Frozen(new DropShadowEffect
            { BlurRadius = 55, ShadowDepth = 0, Color = Color.FromArgb(0xE0, 0, 0, 0), Opacity = 0.9 });
        }

        var app = Application.Current;
        if (app == null) return;

        if (app.Resources.MergedDictionaries.Count > 0)
            app.Resources.MergedDictionaries[0] = d;   // جایگزینی تم — استایل‌ها دست‌نخورده
        else
            app.Resources.MergedDictionaries.Add(d);
    }

    private static Color P(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    /// <summary>نسخه‌ی کاملاً شفاف همان رنگ — لبه‌ی سحابی
    /// ✨ bugfix 22: قبلاً Color.FromRgb بود که آلفا=۲۵۵ (کاملاً مات!) برمی‌گرداند —
    /// گرادیان سحابی برعکس می‌شد: مرکز شفاف و لبه‌ی مات با لبه‌ی تیز و زننده.
    /// حالا لبه واقعاً شفاف است → هاله‌ی نرم و بلورشده.</summary>
    private static Color Translucent(string hex)
    {
        var c = P(hex);
        return Color.FromArgb(0, c.R, c.G, c.B);
    }

    private static Color Transparent(string hex)
    {
        var c = P(hex);
        return Color.FromArgb(0, c.R, c.G, c.B);
    }

    /// <summary>همان رنگ با آلفای دلخواه (#AARRGGBB)</summary>
    private static string Alpha(string hex, int a)
    {
        var c = P(hex);
        return $"#{a:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    private static void C(ResourceDictionary d, string key, string hex) => d[key] = P(hex);

    private static void B(ResourceDictionary d, string key, string hex)
    {
        var brush = new SolidColorBrush(P(hex));
        brush.Freeze();   // ✨ پرفورمنس: براش منجمد = بدون سربار change-notification
        d[key] = brush;
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        if (freezable.CanFreeze) freezable.Freeze();
        return freezable;
    }
}
