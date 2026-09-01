using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using TorobScanner.Services;

namespace TorobScanner.Views;

/// <summary>
/// ✨ v2.7: پنجره‌ی تنظیمات بازارسنج
///   ۱) ظاهر و تم — ۴ تم لوکس ترند ۲۰۲۵ با پیش‌نمایش زنده و تعویض بدون ری‌استارت
///   ۲) بروزرسانی خودکار — بررسی هنگام شروع + نصب خودکار بدون سوال + بررسی فوری
/// هر تغییر بلافاصله در settings.json ذخیره می‌شود.
/// </summary>
public class SettingsWindow : Window
{
    private readonly Border[] _themeCards = new Border[ThemeService.All.Count];
    private ToggleSwitch _autoCheckToggle = null!;
    private ToggleSwitch _autoInstallToggle = null!;
    private TextBlock _versionText = null!;

    public SettingsWindow()
    {
        Title = "تنظیمات";
        Width = 566;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 760;
        FlowDirection = FlowDirection.RightToLeft;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = Application.Current?.MainWindow;
        WindowStyle = WindowStyle.None; AllowsTransparency = true;
        ResizeMode = ResizeMode.NoResize;
        Background = Brushes.Transparent;
        ThemeHelper.ApplyObsidianTheme(this);

        // ✨ bugfix 24: با تعویض تم، خود پنجره‌ی تنظیمات هم زنده بازسازی شود
        // (قبلاً فقط بعد از بستن و بازکردن، ظاهر جدید می‌گرفت)
        ThemeService.ThemeChanged += RebuildForTheme;
        Closed += (s, e) => ThemeService.ThemeChanged -= RebuildForTheme;   // جلوگیری از نشت حافظه (رخداد استاتیک)
        KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };

        BuildUi();
    }

    private void RebuildForTheme() => BuildUi();

    private void BuildUi()
    {
        var content = LuxUI.BuildDialogShell(this, "⚙",
            "تنظیمات",
            new SolidColorBrush(Tint(LuxUI.Accent, 0x2E)),
            out _, out var outerBorder);

        // ═══════════════ بخش ۱: ظاهر و تم ═══════════════
        content.Children.Add(SectionHeader("✦", "ظاهر و تم",
            "یکی از تم‌های لوکس را انتخاب کنید — بلافاصله و بدون ری‌استارت اعمال می‌شود"));

        var themeGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        themeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        themeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int r = 0; r < 2; r++) themeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (int i = 0; i < ThemeService.All.Count; i++)
        {
            var def = ThemeService.All[i];
            var card = BuildThemeCard(def);

            Grid.SetRow(card, i / 2);
            Grid.SetColumn(card, i % 2);
            if (i % 2 == 0) card.Margin = new Thickness(0, 0, 8, 12);
            else card.Margin = new Thickness(8, 0, 0, 12);

            themeGrid.Children.Add(card);
            _themeCards[i] = card;
        }
        content.Children.Add(themeGrid);

        RefreshThemeCards();

        // ═══════════════ بخش ۲: بروزرسانی خودکار ═══════════════
        content.Children.Add(SectionHeader("⟳", "بروزرسانی خودکار",
            "برنامه می‌تواند خودش نسخه‌های جدید را از گیت‌هاب پیدا و نصب کند"));

        var updateCard = LuxUI.GlassPanel(14);
        updateCard.Padding = new Thickness(16, 6, 16, 14);
        var updateStack = new StackPanel();

        // ─── کلید ۱: بررسی هنگام شروع ───
        _autoCheckToggle = new ToggleSwitch("بررسی خودکار هنگام شروع",
            "هر بار برنامه باز می‌شود، بی‌صدا نسخه‌ی جدید را چک می‌کند",
            SettingsService.Current.AutoCheckUpdates);
        _autoCheckToggle.Toggled += on =>
        {
            SettingsService.Current.AutoCheckUpdates = on;
            SettingsService.Save();
        };
        updateStack.Children.Add(_autoCheckToggle.Root);

        updateStack.Children.Add(new Separator
        {
            Margin = new Thickness(0, 10, 0, 10),
            Background = new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)),
        });

        // ─── کلید ۲: نصب خودکار بدون سوال ───
        _autoInstallToggle = new ToggleSwitch("نصب خودکار بروزرسانی",
            "بعد از پیدا شدن نسخه‌ی جدید، دانلود و نصب بدون توقف انجام می‌شود",
            SettingsService.Current.AutoInstallUpdates);
        _autoInstallToggle.Toggled += on =>
        {
            SettingsService.Current.AutoInstallUpdates = on;
            SettingsService.Save();
        };
        updateStack.Children.Add(_autoInstallToggle.Root);

        // ─── نسخه فعلی + بررسی فوری ───
        var versionRow = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        versionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        versionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _versionText = new TextBlock
        {
            Text = "نسخه فعلی: " + LuxUI.Fa(UpdateService.CurrentVersion()),
            Foreground = LuxUI.TextDim, FontSize = 11.5,
            VerticalAlignment = VerticalAlignment.Center
        };
        versionRow.Children.Add(_versionText);

        var checkBtn = LuxUI.GhostButton("بررسی بروزرسانی الان");
        checkBtn.Height = 34;
        checkBtn.Padding = new Thickness(16, 0, 16, 0);
        checkBtn.FontSize = 12;
        checkBtn.Click += (s, e) =>
        {
            var win = new UpdateWindow { Owner = this };
            win.ShowDialog();
        };
        Grid.SetColumn(checkBtn, 1);
        versionRow.Children.Add(checkBtn);
        updateStack.Children.Add(versionRow);

        updateCard.Child = updateStack;
        content.Children.Add(updateCard);

        // ─── پانویس ───
        content.Children.Add(new TextBlock
        {
            Text = "تنظیمات به‌صورت خودکار ذخیره می‌شوند — نیازی به دکمه‌ی ذخیره نیست.",
            Foreground = LuxUI.TextDim, FontSize = 10.5,
            Margin = new Thickness(0, 14, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        });

        Content = outerBorder;
    }

    // ═══════════ کارت تم با پیش‌نمایش زنده — هر سبک، ظاهر واقعی خودش ═══════════

    private Border BuildThemeCard(LuxThemeDef def)
    {
        var selected = ThemeService.Current.Id == def.Id;
        var st = def.Style;

        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF)),
            BorderBrush = selected ? new SolidColorBrush(Tint(LuxUI.Accent, 0x66)) : LuxUI.GlassStroke,
            BorderThickness = new Thickness(selected ? 1.6 : 1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(11, 11, 11, 12),
            Cursor = Cursors.Hand,
            Tag = def.Id,
            CacheMode = new BitmapCache()
        };

        var stack = new StackPanel();

        // ─── پیش‌نمایش مینیاتوری: سبک طراحی واقعی هر تم (شکل + سایه + دکور) ───
        stack.Children.Add(BuildMiniPreview(def));

        // ─── نام تم + سبک + نشان انتخاب ───
        var nameRow = new Grid { Margin = new Thickness(2, 10, 0, 0) };
        nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var nameStack = new StackPanel();
        nameStack.Children.Add(new TextBlock
        {
            Text = def.NameFa, FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = LuxUI.TextPrimary
        });

        var subRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 0) };
        subRow.Children.Add(new TextBlock
        {
            Text = def.NameEn, FontSize = 9.5, Foreground = LuxUI.TextDim,
            VerticalAlignment = VerticalAlignment.Center
        });
        var styleChip = new Border
        {
            Background = new SolidColorBrush(FromHex(st.IsLight ? def.Accent : def.IriA, 0x22)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 1, 6, 2),
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = StyleTag(def.Id), FontSize = 8.5,
                Foreground = new SolidColorBrush(FromHex(st.IsLight ? def.Accent : def.IriA, 0xE6))
            }
        };
        subRow.Children.Add(styleChip);
        nameStack.Children.Add(subRow);
        nameRow.Children.Add(nameStack);

        var badge = new TextBlock
        {
            Text = "✓ فعال", FontSize = 10, FontWeight = FontWeights.SemiBold,
            Foreground = LuxUI.Accent,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = selected ? Visibility.Visible : Visibility.Collapsed,
            Tag = "badge"
        };
        nameRow.Children.Add(badge);
        stack.Children.Add(nameRow);

        stack.Children.Add(new TextBlock
        {
            Text = def.Desc, FontSize = 10, Foreground = LuxUI.TextSecondary,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 6, 2, 0),
            LineHeight = 15
        });

        card.Child = stack;

        // ─── تعویض تم با کلیک — زنده! ───
        card.MouseLeftButtonUp += (s, e) =>
        {
            if (ThemeService.Current.Id == def.Id) return;
            ThemeService.Apply(def);          // پالت + سبک + ذخیره + رخداد
            RefreshThemeCards();              // نشانگر انتخاب در همین پنجره
        };
        card.MouseEnter += (s, e) =>
        {
            if (ThemeService.Current.Id != def.Id)
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
        };
        card.MouseLeave += (s, e) => RefreshThemeCards();

        return card;
    }

    private static string StyleTag(string id) => id switch
    {
        "platinum" => "گلس‌مورفیسم",
        "bento" => "بنتو سفید",
        "aurora" => "آرورا گلو",
        "couture" => "کوتور نویر",
        _ => ""
    };

    /// <summary>
    /// ✨ v2.8: مینی‌پیش‌نمایش که «سبک طراحی» واقعی هر تم را نشان می‌دهد —
    /// نه فقط رنگ؛ شعاع گوشه، ضخامت خط دور، جنس سایه و دکور پس‌زمینه.
    /// </summary>
    private Border BuildMiniPreview(LuxThemeDef def)
    {
        var st = def.Style;
        var bgA = FromHex(def.BgA); var bgB = FromHex(def.BgB);

        var preview = new Border
        {
            Height = 64,
            CornerRadius = new CornerRadius(st.WinRadius > 18 ? 12 : st.WinRadius),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0), EndPoint = new Point(1, 1),
                GradientStops = { new GradientStop(bgA, 0), new GradientStop(bgB, 1) }
            },
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(FromHex(st.IsLight ? def.TextPrimary : "#FFFFFF", st.IsLight ? (byte)0x22 : (byte)0x20)),
            ClipToBounds = true
        };

        var host = new Grid { Margin = new Thickness(12, 10, 12, 10) };

        // ─── دکور پس‌زمینه‌ی مختص زبان طراحی ───
        UIElement? decor = st.Decor switch
        {
            ThemeDecor.Aurora => MiniOrbs(def),    // دو هاله‌ی بنفش/فیروزه‌ای
            ThemeDecor.Couture => MiniGold(def),   // هاله‌ی طلایی + قاب مویی
            _ => null                              // گلس/بنتو: بدون دکور اضافه
        };
        if (decor != null) host.Children.Add(decor);

        // ─── کارت نمونه با هویت واقعی سبک ───
        var miniCard = new Border
        {
            Width = 118,
            Height = 34,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            CornerRadius = new CornerRadius(st.CardRadius > 16 ? 10 : st.CardRadius),
            Background = new SolidColorBrush(FromHex(st.CardFill)),
            BorderBrush = new SolidColorBrush(FromHex(st.CardStroke)),
            BorderThickness = new Thickness(st.CardBorderThick)
        };

        if (st.CardShadowBlur > 0)
        {
            // آرورا: هاله‌ی بنفش دور کارت
            miniCard.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = st.CardShadowBlur,
                ShadowDepth = st.CardShadowDepth,
                Direction = st.CardShadowDirection,
                Color = FromHex(st.CardShadowColor),
                Opacity = st.CardShadowOpacity
            };
        }
        // بنتو/کوتور: سایه ندارند — مسطح + خط مویی، خودش امضای سبک است

        var miniStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0) };
        miniStack.Children.Add(new Border
        {
            Width = 44, Height = 5,
            CornerRadius = new CornerRadius(st.PillRadius > 6 ? 2.5 : 1),
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0), EndPoint = new Point(1, 0),
                GradientStops = { new GradientStop(FromHex(def.IriA), 0), new GradientStop(FromHex(def.IriC), 1) }
            }
        });
        miniStack.Children.Add(new Border
        {
            Width = 66, Height = 5,
            CornerRadius = new CornerRadius(st.PillRadius > 6 ? 2.5 : 1),
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 5, 0, 0),
            Background = new SolidColorBrush(FromHex(def.TextSecondary, 0x8C))
        });
        miniCard.Child = miniStack;
        host.Children.Add(miniCard);

        // ─── نوار CTA متالیک + دات‌های رنگی ───
        var cta = new Border
        {
            Width = 34, Height = 18,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(st.BtnRadius > 9 ? 6 : st.BtnRadius),
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0), EndPoint = new Point(0, 1),
                GradientStops = { new GradientStop(FromHex(def.MetalA), 0), new GradientStop(FromHex(def.MetalC), 1) }
            },
            BorderBrush = new SolidColorBrush(FromHex(st.PrimaryBtnStroke)),
            BorderThickness = new Thickness(Math.Min(st.BtnBorderThick, 1.5))
        };
        host.Children.Add(cta);

        var dotsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 0)
        };
        foreach (var hex in new[] { def.Accent, def.Success, def.Warning })
        {
            dotsPanel.Children.Add(new Ellipse
            {
                Width = 8, Height = 8, Margin = new Thickness(2.5, 0, 0, 0),
                Fill = new SolidColorBrush(FromHex(hex, 0xE6)),
                Stroke = st.IsLight ? new SolidColorBrush(FromHex(def.TextPrimary, 0x66)) : null,
                StrokeThickness = st.IsLight ? 1 : 0
            });
        }
        host.Children.Add(dotsPanel);

        preview.Child = host;
        return preview;
    }

    /// <summary>دو هاله‌ی مینی آرورا — بنفش بالا-چپ، فیروزه‌ای پایین-راست</summary>
    private static UIElement MiniOrbs(LuxThemeDef def)
    {
        var g = new Grid { IsHitTestVisible = false };
        g.Children.Add(new Ellipse
        {
            Width = 52, Height = 44,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(-14, -16, 0, 0),
            Fill = MiniOrbBrush(FromHex(def.IriA, 0x66))
        });
        g.Children.Add(new Ellipse
        {
            Width = 56, Height = 48,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, -16, -18),
            Fill = MiniOrbBrush(FromHex(def.IriB, 0x59))
        });
        g.Children.Add(new Ellipse
        {
            Width = 40, Height = 36,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(30, 0, 0, 4),
            Fill = MiniOrbBrush(FromHex(def.IriC, 0x4D))
        });
        return g;
    }

    /// <summary>هاله‌ی طلایی مینی + قاب مویی طلایی — امضای کوتور</summary>
    private static UIElement MiniGold(LuxThemeDef def)
    {
        var g = new Grid { IsHitTestVisible = false };
        g.Children.Add(new Ellipse
        {
            Width = 90, Height = 60,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -26, 0, 0),
            Fill = MiniOrbBrush(FromHex(def.IriB, 0x40))
        });
        g.Children.Add(new System.Windows.Shapes.Rectangle
        {
            Margin = new Thickness(6),
            Stroke = new SolidColorBrush(FromHex(def.Accent, 0x30)),
            StrokeThickness = 1,
            RadiusX = 4, RadiusY = 4
        });
        return g;
    }

    /// <summary>براش شعاعی هاله‌ی مینی</summary>
    private static Brush MiniOrbBrush(Color c)
    {
        var b = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(c, 0),
                new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1)
            }
        };
        if (b.CanFreeze) b.Freeze();
        return b;
    }

    private void RefreshThemeCards()
    {
        for (int i = 0; i < _themeCards.Length; i++)
        {
            var card = _themeCards[i];
            if (card == null) continue;
            bool selected = (string)card.Tag == ThemeService.Current.Id;

            card.BorderBrush = selected ? new SolidColorBrush(Tint(LuxUI.Accent, 0x66)) : LuxUI.GlassStroke;
            card.BorderThickness = new Thickness(selected ? 1.6 : 1);
            card.Background = selected
                ? new SolidColorBrush(Tint(LuxUI.Accent, 0x12))
                : new SolidColorBrush(Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF));

            if (card.Child is StackPanel st && st.Children.Count > 1 &&
                st.Children[1] is Grid nr)
            {
                foreach (var child in nr.Children)
                    if (child is TextBlock tb && tb.Tag?.ToString() == "badge")
                        tb.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    // ═══════════ اجزای کمکی ═══════════

    /// <summary>سربرگ بخش: چیپ + عنوان + توضیح کمکی</summary>
    private StackPanel SectionHeader(string icon, string title, string desc)
    {
        var p = new StackPanel { Margin = new Thickness(0, 4, 0, 12) };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        var chip = LuxUI.IconChip(icon, new SolidColorBrush(Tint(LuxUI.Accent, 0x20)), 26, 12);
        chip.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(chip);
        row.Children.Add(new TextBlock
        {
            Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold,
            Foreground = LuxUI.TextPrimary, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(9, 0, 0, 0)
        });
        p.Children.Add(row);

        p.Children.Add(new TextBlock
        {
            Text = desc, FontSize = 10.5, Foreground = LuxUI.TextDim,
            Margin = new Thickness(2, 0, 0, 0)
        });
        return p;
    }

    private static Color Tint(Brush accent, byte alpha)
    {
        var c = accent is SolidColorBrush sc ? sc.Color : Color.FromRgb(0x8F, 0xB8, 0xFF);
        return Color.FromArgb(alpha, c.R, c.G, c.B);
    }

    private static Color FromHex(string hex, byte alpha)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex);
        return Color.FromArgb(alpha, c.R, c.G, c.B);
    }

    private static Color FromHex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    /// <summary>
    /// ✨ کلید سوییچی لوکس (ساخته‌شده در کد): قرص ۴۴×۲۴ + دسته‌ی لغزنده با
    /// انیمیشن نرم + رنگ هم‌گام با تم. جهت چپ‌به‌راستِ داخلی برای کنترل
    /// دقیق حرکت دسته (FlowDirection پنجره RTL است).
    /// </summary>
    private class ToggleSwitch
    {
        public event Action<bool>? Toggled;

        public readonly Grid Root = new()
        {
            Margin = new Thickness(0, 4, 0, 4),
            FlowDirection = FlowDirection.LeftToRight
        };

        private readonly Border _pill;
        private readonly Ellipse _knob;
        private readonly TranslateTransform _knobX = new(0, 0);
        private readonly TextBlock _label;
        private readonly TextBlock _hint;
        private bool _on;

        private static readonly Brush OffPill = Frozen(new SolidColorBrush(Color.FromArgb(0x2E, 0x6E, 0x78, 0x89)));
        private static readonly Brush OnPill  = Frozen(new SolidColorBrush(Color.FromArgb(0xE6, 0x71, 0xE6, 0xA8)));
        private static readonly Brush KnobFill = Frozen(new SolidColorBrush(Colors.White));

        public ToggleSwitch(string label, string hint, bool initial)
        {
            _on = initial;

            Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _label = new TextBlock { Text = label, FontSize = 12.5, FontWeight = FontWeights.Medium, Foreground = LuxUI.TextPrimary };
            _hint = new TextBlock { Text = hint, FontSize = 10, Foreground = LuxUI.TextDim, Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap };
            textStack.Children.Add(_label);
            textStack.Children.Add(_hint);
            Grid.SetColumn(textStack, 0);
            Root.Children.Add(textStack);

            var hit = new Border
            {
                Width = 52, Height = 30,
                Background = Brushes.Transparent,   // ناحیه‌ی کلیک بزرگ‌تر از قرص
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new Grid
                {
                    Width = 46, Height = 24,
                    FlowDirection = FlowDirection.LeftToRight
                }
            };
            var pillHost = (Grid)hit.Child;

            _pill = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = _on ? OnPill : OffPill,
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1)
            };
            pillHost.Children.Add(_pill);

            _knob = new Ellipse
            {
                Width = 18, Height = 18,
                Fill = KnobFill,
                RenderTransform = _knobX,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(3, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            pillHost.Children.Add(_knob);

            hit.MouseLeftButtonUp += (s, e) => Set(!_on, animated: true);
            Grid.SetColumn(hit, 1);
            Root.Children.Add(hit);

            Apply(animated: false);
        }

        private void Set(bool on, bool animated)
        {
            _on = on;
            Apply(animated);
            Toggled?.Invoke(on);
        }

        private void Apply(bool animated)
        {
            // دسته: خاموش=چپ (X=0) | روشن=راست (X=22)
            var target = _on ? 22.0 : 0.0;
            if (animated)
            {
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                _knobX.BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(target, TimeSpan.FromMilliseconds(190)) { EasingFunction = ease });

                var color = new ColorAnimation(_on ? OnPillColor : OffPillColor, TimeSpan.FromMilliseconds(190)) { EasingFunction = ease };
                if (_pill.Background is SolidColorBrush pb) pb.BeginAnimation(SolidColorBrush.ColorProperty, color);
            }
            else
            {
                _knobX.X = target;
                _pill.Background = new SolidColorBrush(_on ? OnPillColor : OffPillColor);
            }

            _label.Foreground = _on ? LuxUI.TextPrimary : LuxUI.TextSecondary;
        }

        private static Color OnPillColor => LuxUI.Success is SolidColorBrush sc ? sc.Color : Color.FromRgb(0x71, 0xE6, 0xA8);
        private static Color OffPillColor => LuxUI.TextDim is SolidColorBrush sc2 ? sc2.Color : Color.FromRgb(0x6E, 0x78, 0x89);

        private static SolidColorBrush Frozen(SolidColorBrush b) { if (b.CanFreeze) b.Freeze(); return b; }
    }
}
