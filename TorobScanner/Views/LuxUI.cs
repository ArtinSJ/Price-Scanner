using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace TorobScanner.Views;

/// <summary>
/// ✨ جعبه‌ابزار لوکس (v2.8) — زبان طراحی مشترک همه‌ی پنجره‌ها
/// ✨ v2.8: هر تم یک «سبک طراحی» کامل است — علاوه بر رنگ‌ها، شعاع گوشه‌ها،
/// ضخامت خط دور، نوع سایه و دکور پس‌زمینه هم با SetPalette عوض می‌شوند.
/// همه‌ی براش‌ها Freeze می‌شوند (پرفورمنس).
/// </summary>
public static class LuxUI
{
    // ═══════════ فونت ═══════════
    public static readonly FontFamily Font =
        new(new Uri("pack://application:,,,/"), "./Fonts/#Vazirmatn, Segoe UI");

    // ═══════════ پالت رنگ (متغیر — با SetPalette عوض می‌شود) ═══════════
    public static Brush TextPrimary   { get; private set; } = B("#F3F6FB");
    public static Brush TextSecondary { get; private set; } = B("#A9B2C1");
    public static Brush TextDim       { get; private set; } = B("#6E7889");
    public static Brush Platinum      { get; private set; } = B("#F4F7FB");
    public static Brush Accent        { get; private set; } = B("#8FB8FF");
    public static Brush Success       { get; private set; } = B("#71E6A8");
    public static Brush Danger        { get; private set; } = B("#FF9191");
    public static Brush Warning       { get; private set; } = B("#FFCB7D");

    public static Brush GlassFill       { get; private set; } = B("#12FFFFFF");
    public static Brush GlassFillStrong { get; private set; } = B("#1AFFFFFF");
    public static Brush GlassStroke     { get; private set; } = B("#22FFFFFF");
    public static Brush FocusStroke     { get; private set; } = B("#738FB8FF");

    public static Brush WindowBg       { get; private set; } = GB("#0B0D12", "#11141B", "#0D1016");
    public static Brush Iridescent     { get; private set; } = GB3("#A9C7FF", "#D8C6FF", "#F1D9FF");
    public static Brush PlatinumMetal  { get; private set; } = GB("#F8FAFD", "#DDE4EE", "#C9D3E1");

    /// <summary>رنگ‌های گرادیان ایریدسنت — برای متن‌های گرادیانی (IridescentText)</summary>
    public static Color IriA { get; private set; } = Color.FromRgb(0xA9, 0xC7, 0xFF);
    public static Color IriB { get; private set; } = Color.FromRgb(0xD8, 0xC6, 0xFF);
    public static Color IriC { get; private set; } = Color.FromRgb(0xF1, 0xD9, 0xFF);

    // ═══════════ ✨ v2.8: توکن‌های سبک طراحی ═══════════
    public static double WinRadius   { get; private set; } = 16;
    public static double SideRadius  { get; private set; } = 18;
    public static double CardRadius  { get; private set; } = 14;
    public static double BtnRadius   { get; private set; } = 11;
    public static double InputRadius { get; private set; } = 10;
    public static double PillRadius  { get; private set; } = 8;
    public static double ChipRadius  { get; private set; } = 10;
    public static double LogoRadius  { get; private set; } = 14;

    public static Thickness CardBorderThick { get; private set; } = new(1);
    public static Thickness ChipBorderThick { get; private set; } = new(1);

    public static Brush SidebarFill  { get; private set; } = B("#1EFFFFFF");
    public static Brush ChipFill     { get; private set; } = B("#18FFFFFF");
    public static Brush GhostFill    { get; private set; } = B("#08FFFFFF");
    public static Brush HoverFill    { get; private set; } = B("#12FFFFFF");
    public static Brush SelectedFill { get; private set; } = B("#2B8FB8FF");
    public static Brush PopupBg      { get; private set; } = B("#F20E1117");

    /// <summary>رنگ‌های قابل‌انیمیشن (برای ColorAnimation)</summary>
    public static Color ChipFillColor     { get; private set; } = Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF);
    public static Color ChipFillHoverColor { get; private set; } = Color.FromArgb(0x32, 0xFF, 0xFF, 0xFF);
    public static Color LogoTextColor     { get; private set; } = Color.FromRgb(0x0B, 0x0D, 0x12);

    public static FontWeight HeaderWeight { get; private set; } = FontWeights.SemiBold;

    /// <summary>دکور پس‌زمینه‌ی پنجره — مختص هر سبک</summary>
    public static Services.ThemeDecor Decor { get; private set; } = Services.ThemeDecor.Nebulas;
    public static bool IsLight { get; private set; } = false;

    public static Effect? CardShadow { get; private set; } = null;
    public static Effect ShadowDialog { get; private set; } = new DropShadowEffect
        { BlurRadius = 34, ShadowDepth = 0, Color = Color.FromArgb(0xC0, 0, 0, 0), Opacity = 0.85 };
    public static Effect ShadowWindow { get; private set; } = new DropShadowEffect
        { BlurRadius = 55, ShadowDepth = 0, Color = Color.FromArgb(0xE0, 0, 0, 0), Opacity = 0.9 };

    /// <summary>
    /// ✨ v2.8: تعویض کامل زبان طراحی (فراخوانی از ThemeService) —
    /// رنگ‌ها + شکل‌ها + سایه‌ها + دکور. همه‌ی مقادیر جدید Freeze می‌شوند.
    /// </summary>
    public static void SetPalette(Services.LuxThemeDef def)
    {
        var st = def.Style;

        TextPrimary   = FrozenB(def.TextPrimary);
        TextSecondary = FrozenB(def.TextSecondary);
        TextDim       = FrozenB(def.TextDim);
        Platinum      = FrozenB(def.Platinum);
        Accent        = FrozenB(def.Accent);
        Success       = FrozenB(def.Success);
        Danger        = FrozenB(def.Danger);
        Warning       = FrozenB(def.Warning);
        GlassFill       = FrozenB(st.CardFill);
        GlassFillStrong = FrozenB(st.CardFillStrong);
        GlassStroke     = FrozenB(st.CardStroke);
        FocusStroke     = FrozenB("#" + (st.IsLight ? 50 : 73) + def.Accent.TrimStart('#'));
        WindowBg      = FrozenG(def.BgA, def.BgB, def.BgC);
        Iridescent    = FrozenG3(def.IriA, def.IriB, def.IriC);
        PlatinumMetal = FrozenG(def.MetalA, def.MetalB, def.MetalC);
        IriA = (Color)ColorConverter.ConvertFromString(def.IriA);
        IriB = (Color)ColorConverter.ConvertFromString(def.IriB);
        IriC = (Color)ColorConverter.ConvertFromString(def.IriC);

        // ─── توکن‌های شکل ───
        WinRadius   = st.WinRadius;
        SideRadius  = st.SideRadius;
        CardRadius  = st.CardRadius;
        BtnRadius   = st.BtnRadius;
        InputRadius = st.InputRadius;
        PillRadius  = st.PillRadius;
        ChipRadius  = st.ChipRadius;
        LogoRadius  = st.LogoRadius;
        CardBorderThick = new Thickness(st.CardBorderThick);
        ChipBorderThick = new Thickness(st.ChipBorderThick);

        // ─── براش‌های سبکی ───
        SidebarFill = FrozenB(st.SidebarFill);
        ChipFill    = FrozenB(st.ChipFill);
        GhostFill   = FrozenB(st.GhostFill);
        HoverFill   = FrozenB(st.HoverFill);
        SelectedFill = FrozenB(st.SelectedFill);
        PopupBg     = FrozenB(st.PopupBg);
        ChipFillColor      = (Color)ColorConverter.ConvertFromString(st.ChipFill);
        ChipFillHoverColor = (Color)ColorConverter.ConvertFromString(st.ChipFillHover);
        LogoTextColor      = (Color)ColorConverter.ConvertFromString(st.LogoText);
        HeaderWeight = st.HeaderWeight;
        Decor  = st.Decor;
        IsLight = st.IsLight;

        // ─── سایه‌ها ───
        CardShadow = st.CardShadowOpacity <= 0 && st.CardShadowBlur <= 0 && st.CardShadowDepth <= 0
            ? null
            : Frozen(new DropShadowEffect
            {
                BlurRadius = st.CardShadowBlur,
                ShadowDepth = st.CardShadowDepth,
                Direction = st.CardShadowDirection,
                Color = (Color)ColorConverter.ConvertFromString(st.CardShadowColor),
                Opacity = st.CardShadowOpacity
            });
        if (st.IsLight)
        {
            ShadowDialog = Frozen(new DropShadowEffect
            { BlurRadius = 30, ShadowDepth = 5, Direction = 270, Color = Color.FromRgb(0x1B, 0x1B, 0x26), Opacity = 0.30 });
            ShadowWindow = Frozen(new DropShadowEffect
            { BlurRadius = 46, ShadowDepth = 8, Direction = 270, Color = Color.FromRgb(0x1B, 0x1B, 0x26), Opacity = 0.38 });
        }
        else
        {
            ShadowDialog = Frozen(new DropShadowEffect
            { BlurRadius = 34, ShadowDepth = 0, Color = Color.FromArgb(0xC0, 0, 0, 0), Opacity = 0.85 });
            ShadowWindow = Frozen(new DropShadowEffect
            { BlurRadius = 55, ShadowDepth = 0, Color = Color.FromArgb(0xE0, 0, 0, 0), Opacity = 0.9 });
        }
    }

    private static Brush FrozenB(string hex) { var b = B(hex); if (b.CanFreeze) b.Freeze(); return b; }
    private static Brush FrozenG(string a, string b, string c) { var g = GB(a, b, c); if (g.CanFreeze) g.Freeze(); return g; }
    private static Brush FrozenG3(string a, string b, string c) { var g = GB3(a, b, c); if (g.CanFreeze) g.Freeze(); return g; }
    private static T Frozen<T>(T f) where T : Freezable { if (f.CanFreeze) f.Freeze(); return f; }

    private static SolidColorBrush B(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    private static LinearGradientBrush GB(string a, string b, string c) => new()
    {
        StartPoint = new Point(0, 0), EndPoint = new Point(1, 1),
        GradientStops = { GS(a, 0), GS(b, 0.55), GS(c, 1) }
    };
    private static LinearGradientBrush GB3(string a, string b, string c) => new()
    {
        StartPoint = new Point(0, 0), EndPoint = new Point(1, 1),
        GradientStops = { GS(a, 0), GS(b, 0.52), GS(c, 1) }
    };
    private static GradientStop GS(string hex, double offset) =>
        new((Color)ColorConverter.ConvertFromString(hex), offset);

    // ═══════════ اعداد فارسی ═══════════
    private static readonly string[] FaDigits = { "۰", "۱", "۲", "۳", "۴", "۵", "۶", "۷", "۸", "۹" };

    /// <summary>تبدیل ارقام لاتین به فارسی برای نمایش لوکس‌تر</summary>
    public static string Fa(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (chars[i] >= '0' && chars[i] <= '9')
                chars[i] = (char)('۰' + (chars[i] - '0'));
        return new string(chars);
    }

    public static string Fa(long v) => Fa(v.ToString("N0", CultureInfo.InvariantCulture));
    public static string FaPrice(decimal v) => Fa(v.ToString("N0", CultureInfo.InvariantCulture));

    /// <summary>«۲ ساعت پیش» — زمان نسبی فارسی</summary>
    public static string RelativeTimeFa(DateTime time)
    {
        if (time == default) return "بدون بروزرسانی";
        var d = DateTime.Now - time;
        if (d.TotalMinutes < 1) return "همین حالا";
        if (d.TotalHours < 1) return $"{Fa((long)d.TotalMinutes)} دقیقه پیش";
        if (d.TotalHours < 24) return $"{Fa((long)d.TotalHours)} ساعت پیش";
        if (d.TotalDays < 30) return $"{Fa((long)d.TotalDays)} روز پیش";
        return $"{Fa(time.Day)} {time.Month switch { 1 => "فروردین", 2 => "اردیبهشت", 3 => "خرداد", 4 => "تیر", 5 => "مرداد", 6 => "شهریور", 7 => "مهر", 8 => "آبان", 9 => "آذر", 10 => "دی", 11 => "بهمن", _ => "اسفند" }}";
    }

    // ═══════════ استایل‌ها از دیکشنری ═══════════
    public static Style StyleOf(string key) =>
        (Style)Application.Current.Resources[key];

    public static Button PrimaryButton(string text) => new()
    { Content = text, Style = StyleOf("LuxBtnPrimary"), Height = 44 };
    public static Button GhostButton(string text) => new()
    { Content = text, Style = StyleOf("LuxBtnGhost"), Height = 38 };
    public static Button DangerButton(string text) => new()
    { Content = text, Style = StyleOf("LuxBtnDanger"), Height = 38 };
    public static Button SuccessButton(string text) => new()
    { Content = text, Style = StyleOf("LuxBtnSuccess"), Height = 38 };

    // ═══════════ اجزای مشترک ═══════════

    /// <summary>
    /// پنل کارت استاندارد — شعاع، خط دور و پرکنندگی از سبک تم فعال.
    /// (گلس: شیشه‌ی نیمه‌شفاف | بروتال: کاغذ با خط سیاه | سایبر: تیره‌ی نئونی | کلود: سفید)
    /// </summary>
    public static Border GlassPanel(double? radius = null, Brush? fill = null, Brush? stroke = null)
        => new()
        {
            Background = fill ?? GlassFill,
            BorderBrush = stroke ?? GlassStroke,
            BorderThickness = CardBorderThick,
            CornerRadius = new CornerRadius(radius ?? CardRadius)
        };

    /// <summary>چیپ آیکون — شعاع و خط دورش از سبک تم پیروی می‌کند</summary>
    public static Border IconChip(string emoji, Brush tintBg, double size = 36, double emojiSize = 16)
    {
        var chip = new Border
        {
            Width = size, Height = size,
            Background = tintBg,
            CornerRadius = new CornerRadius(Math.Min(ChipRadius, size * 0.32)),
            BorderBrush = GlassStroke, BorderThickness = ChipBorderThick,
            Child = new TextBlock
            {
                Text = emoji, FontSize = emojiSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        return chip;
    }

    /// <summary>لیبل کم‌رنگ</summary>
    public static TextBlock Caption(string text, double size = 11.5) => new()
    {
        Text = text, Foreground = TextSecondary, FontSize = size,
        FontWeight = FontWeights.Normal
    };

    /// <summary>متن با گرادیان ایریدسنت — برای اعداد لوکس (✨ v2.7: هم‌گام با تم فعال)</summary>
    public static TextBlock IridescentText(string text, double size = 21)
    {
        var tb = new TextBlock
        {
            Text = text, FontSize = size, FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var g = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        g.GradientStops.Add(new GradientStop(IriA, 0));
        g.GradientStops.Add(new GradientStop(IriB, 0.52));
        g.GradientStops.Add(new GradientStop(IriC, 1));
        tb.Foreground = g;
        return tb;
    }

    /// <summary>
    /// پوسته‌ی دیالوگ لوکس: عنوان + چیپ آیکون + دکمه بستن + کانتنت
    /// پنجره باید قبلاً WindowStyle=None و AllowsTransparency=true باشد.
    /// ✨ v2.8: شعاع و سایه از سبک تم فعال می‌آید.
    /// </summary>
    public static StackPanel BuildDialogShell(
        Window win, string icon, string title, Brush iconTint,
        out StackPanel content, out Border outerBorder)
    {
        outerBorder = new Border
        {
            CornerRadius = new CornerRadius(WinRadius),
            Background = WindowBg,
            BorderBrush = GlassStroke,
            BorderThickness = CardBorderThick,
            Margin = new Thickness(14), // جا برای سایه‌ی اطراف پنجره (وگرنه clip می‌شود)
            Effect = ShadowDialog
        };

        // نوار عنوان برای درگ — WindowStyle=None
        var titleBar = new Grid { Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Top };
        titleBar.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ClickCount == 1 && e.ButtonState == MouseButtonState.Pressed)
            { try { win.DragMove(); } catch { } }
        };

        var stack = new StackPanel { Margin = new Thickness(28, 20, 28, 26) };

        // سربرگ: چیپ + عنوان + بستن
        var header = new Grid { Margin = new Thickness(0, 0, 0, 22) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var chip = IconChip(icon, iconTint, 40, 18);
        chip.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(chip);

        var titleTxt = new TextBlock
        {
            Text = title, FontSize = 16.5, FontWeight = FontWeights.SemiBold,
            Foreground = TextPrimary, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        header.Children.Add(titleTxt);
        Grid.SetColumn(titleTxt, 1);

        var closeBtn = new Button
        {
            Content = "✕", Style = StyleOf("LuxTitleCloseButton"),
            VerticalAlignment = VerticalAlignment.Center
        };
        // ✨ v2.7: پنجره‌های non-modal (مثل UpdateWindow خودکار) DialogResult ندارند —
        // قبلاً InvalidOperationException می‌داد؛ حالا امن است.
        closeBtn.Click += (s, e) =>
        {
            try { win.DialogResult = false; } catch { } win.Close();
        };
        header.Children.Add(closeBtn);
        Grid.SetColumn(closeBtn, 2);

        // سربرگ باید بالای درگ‌زون باشد
        titleBar.Children.Add(header);

        content = stack;

        var root = new Grid();
        root.Children.Add(titleBar);
        root.Children.Add(stack);
        Grid.SetRow(stack, 0);
        stack.Margin = new Thickness(28, 74, 28, 26); // جا برای نوار عنوان
        outerBorder.Child = root;
        return stack;
    }

    /// <summary>کارتی که لینک ترب/سایت را باز می‌کند — دکمه دایره‌ای شیشه‌ای ↗</summary>
    public static Button OpenLinkButton(Window owner, string url)
    {
        var btn = new Button
        {
            Content = "↗",
            Style = StyleOf("LuxBtnGhost"),
            Width = 34, Height = 34, Padding = new Thickness(0),
            FontSize = 14,
            ToolTip = "بازکردن لینک در مرورگر"
        };
        btn.Click += (s, e) =>
        {
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, $"بازکردن لینک ناموفق بود:\n{ex.Message}");
            }
        };
        return btn;
    }

    /// <summary>حباب pill (برای لیست سایت‌ها) — شعاع از سبک تم</summary>
    public static Border Pill(string text, Brush tint, Brush foreground, double size = 10.5) => new()
    {
        Background = tint,
        CornerRadius = new CornerRadius(PillRadius),
        Padding = new Thickness(10, 4, 10, 4),
        Margin = new Thickness(0, 0, 6, 6),
        BorderBrush = GlassStroke, BorderThickness = CardBorderThick,
        Child = new TextBlock { Text = text, Foreground = foreground, FontSize = size }
    };
}
