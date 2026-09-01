using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Win32;
using TorobScanner.Data;
using TorobScanner.Models;
using TorobScanner.Scrapers;
using TorobScanner.Services;
using TorobScanner.Views;
using Button = System.Windows.Controls.Button;
using Path = System.IO.Path;

namespace TorobScanner;

/// <summary>
/// پنجره اصلی — تم لوکس Platinum-Glass (v2.5):
/// ✨ گلس‌مورفیسم پلاتینیومی: شیشه‌های مات روی پس‌زمینه‌ی ابیس + سحابی‌های نرم
/// ✅ WindowChrome: تغییر اندازه واقعی + حداکثر + Aero Snap (قبلاً پنجره اصلاً resize نمی‌شد)
/// ✅ رفع باگ ۲: همه محصولات نمایش داده می‌شوند (حتی بدون قیمت)
/// ✅ بروزرسانی از کل دیتابیس (نه فقط فیلتر جاری)
/// ✅ دکمه توقف برای همه عملیات طولانی
/// ✅ محافظ عملیات همزمان + try/catch کامل (رفع باگ ۱ و ۹)
/// ✅ اعداد فارسی + زمان نسبی («۲ ساعت پیش») در کارت‌ها
/// ✅ پرفورمنس: بدون سایه‌ی ثابت روی هر کارت — گلو فقط هنگام hover
/// </summary>
public partial class MainWindow : Window
{
    private readonly DatabaseManager _db;
    private readonly TorobProductScraper _scraper;
    private readonly ScraperFactory _scraperFactory;
    private readonly ImportExportService _importService;

    private ObservableCollection<SavedProduct> _allProducts = new();
    private List<SavedProduct> _filteredProducts = new();
    private string _currentFilter = "همه";
    private string _searchText = "";                 // ✨ v3.1.4: جستجوی زنده‌ی محصولات
    private TextBox _searchBox = null!;
    private TextBlock _searchPlaceholder = null!;
    private DispatcherTimer _searchDebounce = null!;

    private StackPanel _cardsContainer = null!;
    private TextBlock _statusText = null!;
    private ProgressBar _progressBar = null!;
    private TextBlock _txtTotalProducts = null!;
    private TextBlock _txtCheapestPrice = null!;
    private TextBlock _txtAvgPrice = null!;
    private Panel _categoryPillsPanel = null!;
    private ComboBox _sortCombo = null!;
    private Button _stopBtn = null!;

    private Border _outerBorder = null!;
    private Grid _clipGrid = null!;
    private Button _maxBtn = null!;
    private ScrollViewer _listScroll = null!;
    private Grid _toastHost = null!;   // ✨ v2.6: میزبان اعلان‌های شیشه‌ای

    private HashSet<string> _recentlyScannedNewUrls = new();
    private CancellationTokenSource? _cts;
    private bool _isBusy;

    public MainWindow()
    {
        InitializeComponent();

        // ✅ رفع باگ ۱۹ (v2.5.1): گارد استارت‌آپ — اگر هر بخش از راه‌اندازی (دیتابیس/UI/بارگذاری)
        // خطا بدهد، به‌جای XamlParseException انگلیسیِ بی‌فایده و پروسه‌ی شبحِ بدون پنجره،
        // پیام فارسیِ گویا با «علت واقعی» نمایش داده می‌شود و برنامه تمیز بسته می‌شود.
        try
        {
            // ✨ v2.7: اعمال تم ذخیره‌شده قبل از ساخت UI + تعویض زنده‌ی تم از تنظیمات
            ThemeService.ApplySaved();
            ThemeService.ThemeChanged += OnThemeChanged;

            ThemeHelper.ApplyObsidianTheme(this);
            _db = new DatabaseManager();
            _scraper = new TorobProductScraper(_db);
            _scraperFactory = new ScraperFactory();
            _importService = new ImportExportService();

            InitializeLuxUI();
            StateChanged += (s, e) => ApplyMaximizedChrome();
            _ = LoadProductsAsync();
        }
        catch (Exception ex)
        {
            Services.Logger.Error("Startup", "", ex.ToString());

            var root = App.RootMessage(ex);
            MessageBox.Show(
                "راه‌اندازی برنامه با مشکل مواجه شد:\n\n" + root + "\n\n" +
                "راهنمای سریع:\n" +
                "• همه‌ی فایل‌های کنار exe (مخصوصاً e_sqlite3.dll) باید همراه برنامه باشند — کل ZIP را استخراج کنید.\n" +
                "• پوشه‌ی برنامه باید قابل نوشتن باشد (از Program Files خارج کنید).\n\n" +
                $"جزئیات فنی کامل:\n{App.LogPath}",
                "خطای راه‌اندازی", MessageBoxButton.OK, MessageBoxImage.Error);

            try { Application.Current?.Shutdown(1); } catch { }
            Environment.Exit(1);
        }
    }

    // ═══════════════════════ پوسته پنجره ═══════════════════════

    private bool _chromeReady;   // ✨ bugfix 21: کروم پنجره فقط یک بار تنظیم می‌شود

    private void InitializeLuxUI()
    {
        // ✨ bugfix 21 — «Cannot change AllowsTransparency after a Window has been shown»:
        // OnThemeChanged این متد را دوباره صدا می‌زند؛ AllowsTransparency/WindowStyle را
        // نمی‌شود روی پنجره‌ی باز عوض کرد → ارور + اعمال‌نشدن تم تا ری‌استارت.
        // راه‌حل: این بلوک فقط بار اول اجرا شود؛ تعویض تم فقط محتوا را بازسازی می‌کند.
        if (!_chromeReady)
        {
            Title = "بازارسنج | BazarSanj";
            Width = 1280; Height = 830;
            MinWidth = 1060; MinHeight = 660;
            FlowDirection = FlowDirection.RightToLeft;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.CanResize;

            // ✨ تغییر اندازه واقعی + حداکثر + Aero Snap برای پنجره‌ی بدون کادر
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 54,
                ResizeBorderThickness = new Thickness(10),
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                UseAeroCaptionButtons = false
            });
            _chromeReady = true;
        }

        _outerBorder = new Border
        {
            CornerRadius = new CornerRadius(LuxUI.WinRadius),
            Background = LuxUI.WindowBg,
            BorderBrush = LuxUI.GlassStroke,
            BorderThickness = LuxUI.CardBorderThick,
            Margin = new Thickness(12),
            Effect = LuxUI.ShadowWindow
        };

        _clipGrid = new Grid();
        ClipRounded(_clipGrid, LuxUI.WinRadius);

        // ✨ v2.9: دکور پس‌زمینه — مختص هر زبان طراحی
        //   گلس: سحابی‌های زنده | بنتو: بوم تمیز و مات
        //   آرورا: سه هاله‌ی نورانی | کوتور: پرده‌ی طلای کم‌جان
        BuildBackgroundDecor(_clipGrid);

        var mainGrid = new Grid();
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(54) });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _clipGrid.Children.Add(mainGrid);

        BuildTitleBar(mainGrid);
        BuildContent(mainGrid);

        // ✨ v2.6: لایه اعلان‌های شیشه‌ای (Toast) — بالاترین لایه، بدون گیر دسترس
        _toastHost = new Grid
        {
            IsHitTestVisible = false,
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 34)
        };
        _clipGrid.Children.Add(_toastHost);

        _outerBorder.Child = _clipGrid;
        Content = _outerBorder;
    }

    private void ClipRounded(Grid grid, double radius)
    {
        void Apply(Size s) =>
            grid.Clip = new RectangleGeometry(new Rect(0, 0, s.Width, s.Height), radius, radius);
        grid.SizeChanged += (s, e) => Apply(e.NewSize);
        _applyClipRadius = r => { grid.Clip = new RectangleGeometry(new Rect(0, 0, grid.ActualWidth, grid.ActualHeight), r, r); };
    }
    private Action<double>? _applyClipRadius;

    /// <summary>
    /// ✨ v2.9: دکور پس‌زمینه بر اساس زبان طراحی تم فعال —
    ///   گلس: سحابی‌های تنفس‌دار | بنتو: هیچ — بوم مات تمیز اپل
    ///   آرورا: سه هاله‌ی بنفش/فیروزه‌ای/صورتی | کوتور: پرده‌ی طلای گرم
    /// همه‌ی عناصر ۲۰فریم‌اند (پرفورمنس — Slow).
    /// </summary>
    private void BuildBackgroundDecor(Grid clipGrid)
    {
        switch (LuxUI.Decor)
        {
            case Services.ThemeDecor.Nebulas:
            {
                // سحابی‌های تنفس‌دار — امضای گلس‌مورفیسم
                // ✨ bugfix 23: کوچک‌تر و کم‌جان‌تر — قبلاً بزرگ و زننده بودند
                var nebula1 = new Ellipse
                {
                    Width = 440, Height = 370,
                    Fill = (Brush)Application.Current.Resources["LuxNebulaBlue"],
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(-110, -140, 0, 0),
                    IsHitTestVisible = false,
                    CacheMode = new BitmapCache()
                };
                var nebula2 = new Ellipse
                {
                    Width = 480, Height = 390,
                    Fill = (Brush)Application.Current.Resources["LuxNebulaLavender"],
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, -130, -160),
                    IsHitTestVisible = false,
                    CacheMode = new BitmapCache()
                };
                var neb1Anim = new DoubleAnimation(0.38, 0.62, TimeSpan.FromSeconds(13))
                { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
                var neb2Anim = new DoubleAnimation(0.30, 0.54, TimeSpan.FromSeconds(17))
                { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
                Slow(neb1Anim); Slow(neb2Anim);
                nebula1.BeginAnimation(OpacityProperty, neb1Anim);
                nebula2.BeginAnimation(OpacityProperty, neb2Anim);
                clipGrid.Children.Add(nebula1);
                clipGrid.Children.Add(nebula2);
                break;
            }

            case Services.ThemeDecor.Plain:
                // بنتو گرید: هیچ — پاکیزگی بوم اپل خودش امضاست
                break;

            case Services.ThemeDecor.Aurora:
            {
                // سه هاله‌ی آرورا — بنفش/فیروزه‌ای/صورتی؛ امضای Linear/Stripe
                // ✨ bugfix 23: کوچک‌تر و لطیف‌تر
                var orbViolet = new Ellipse
                {
                    Width = 470, Height = 390,
                    Fill = (Brush)Application.Current.Resources["LuxNebulaBlue"],
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(-130, -160, 0, 0),
                    IsHitTestVisible = false,
                    CacheMode = new BitmapCache()
                };
                var orbCyan = new Ellipse
                {
                    Width = 500, Height = 420,
                    Fill = (Brush)Application.Current.Resources["LuxNebulaLavender"],
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, -150, -180),
                    IsHitTestVisible = false,
                    CacheMode = new BitmapCache()
                };
                // هاله‌ی سوم (صورتی) از ته رنگ گرادیان ایریدسنت تم ساخته می‌شود
                var pk = LuxUI.IriC;
                var pinkBrush = Frozen(new RadialGradientBrush
                {
                    GradientStops =
                    {
                        new GradientStop(Color.FromArgb(0x52, pk.R, pk.G, pk.B), 0),
                        new GradientStop(Color.FromArgb(0x00, pk.R, pk.G, pk.B), 1)
                    }
                });
                var orbPink = new Ellipse
                {
                    Width = 400, Height = 340,
                    Fill = pinkBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 60, 140, 0),
                    IsHitTestVisible = false,
                    CacheMode = new BitmapCache()
                };
                var a1 = new DoubleAnimation(0.40, 0.70, TimeSpan.FromSeconds(11))
                { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
                var a2 = new DoubleAnimation(0.36, 0.64, TimeSpan.FromSeconds(15))
                { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
                var a3 = new DoubleAnimation(0.28, 0.52, TimeSpan.FromSeconds(19))
                { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
                Slow(a1); Slow(a2); Slow(a3);
                orbViolet.BeginAnimation(OpacityProperty, a1);
                orbCyan.BeginAnimation(OpacityProperty, a2);
                orbPink.BeginAnimation(OpacityProperty, a3);
                clipGrid.Children.Add(orbViolet);
                clipGrid.Children.Add(orbCyan);
                clipGrid.Children.Add(orbPink);
                break;
            }

            case Services.ThemeDecor.Couture:
            {
                // پرده‌ی طلای شامپاینی — هاله‌ی گرم کم‌جان از بالا + برنز از پایین
                // ✨ bugfix 23: کوچک‌تر و لطیف‌تر
                var goldOrb = new Ellipse
                {
                    Width = 560, Height = 420,
                    Fill = (Brush)Application.Current.Resources["LuxNebulaBlue"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, -210, 0, 0),
                    IsHitTestVisible = false,
                    CacheMode = new BitmapCache()
                };
                var bronzeOrb = new Ellipse
                {
                    Width = 470, Height = 370,
                    Fill = (Brush)Application.Current.Resources["LuxNebulaLavender"],
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(-140, 0, 0, -170),
                    IsHitTestVisible = false,
                    CacheMode = new BitmapCache()
                };
                var g1 = new DoubleAnimation(0.30, 0.52, TimeSpan.FromSeconds(16))
                { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
                var g2 = new DoubleAnimation(0.22, 0.40, TimeSpan.FromSeconds(20))
                { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
                Slow(g1); Slow(g2);
                goldOrb.BeginAnimation(OpacityProperty, g1);
                bronzeOrb.BeginAnimation(OpacityProperty, g2);
                clipGrid.Children.Add(goldOrb);
                clipGrid.Children.Add(bronzeOrb);
                break;
            }
        }
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        if (freezable.CanFreeze) freezable.Freeze();
        return freezable;
    }

    private void ApplyMaximizedChrome()
    {
        bool max = WindowState == WindowState.Maximized;
        _outerBorder.CornerRadius = max ? new CornerRadius(0) : new CornerRadius(LuxUI.WinRadius);
        _outerBorder.Margin = max ? new Thickness(0) : new Thickness(12);
        _outerBorder.BorderThickness = max ? new Thickness(0) : LuxUI.CardBorderThick;
        _outerBorder.Effect = max ? null : LuxUI.ShadowWindow;
        _applyClipRadius?.Invoke(max ? 0 : LuxUI.WinRadius);
        if (_maxBtn != null)
            _maxBtn.Content = max ? "❐" : "▢";
    }

    private void BuildTitleBar(Grid mainGrid)
    {
        var titleBar = new Grid { Background = Brushes.Transparent };
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // ─── برند (سمت راست در RTL) ───
        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 18, 0) };
        var logoChip = LuxUI.IconChip("✦", LuxUI.PlatinumMetal, 30, 13);
        logoChip.Child = new TextBlock
        {
            Text = "✦", FontSize = 14, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(LuxUI.LogoTextColor),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        brand.Children.Add(logoChip);
        var brandTitle = new TextBlock
        {
            Text = "BAZARSANJ", FontSize = 12, FontWeight = LuxUI.HeaderWeight,
            Foreground = LuxUI.TextPrimary, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };
        brand.Children.Add(brandTitle);
        var verChip = LuxUI.Pill("۳.۲.۰", new SolidColorBrush(Tint(LuxUI.Accent, 0x24)),
            LuxUI.Accent, 9.5);
        verChip.Margin = new Thickness(10, 0, 0, 0);
        verChip.VerticalAlignment = VerticalAlignment.Center;
        brand.Children.Add(verChip);

        var liveDot = new Ellipse
        {
            Width = 7, Height = 7,
            Fill = LuxUI.Success,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        var blink = new DoubleAnimation(0.25, 1, TimeSpan.FromSeconds(1.6)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
        Slow(blink);
        liveDot.BeginAnimation(OpacityProperty, blink);
        brand.Children.Add(liveDot);

        titleBar.Children.Add(brand);

        // ─── دکمه‌های پنجره ───
        var winBtns = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };

        var btnClose = new Button { Content = "✕", Style = (Style)Application.Current.Resources["LuxTitleCloseButton"], FontSize = 12 };
        btnClose.Click += (s, e) => Close();

        _maxBtn = new Button { Content = "▢", Style = (Style)Application.Current.Resources["LuxTitleButton"] };
        _maxBtn.Click += (s, e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        var btnMin = new Button { Content = "—", Style = (Style)Application.Current.Resources["LuxTitleButton"] };
        btnMin.Click += (s, e) => WindowState = WindowState.Minimized;

        // RTL: اولین فرزند = راست‌ترین → ترتیب بصری چپ→راست: — ▢ ✕
        winBtns.Children.Add(btnClose);
        winBtns.Children.Add(_maxBtn);
        winBtns.Children.Add(btnMin);

        WindowChrome.SetIsHitTestVisibleInChrome(btnClose, true);
        WindowChrome.SetIsHitTestVisibleInChrome(_maxBtn, true);
        WindowChrome.SetIsHitTestVisibleInChrome(btnMin, true);

        Grid.SetColumn(winBtns, 2);
        titleBar.Children.Add(winBtns);

        mainGrid.Children.Add(titleBar);

        // ✨ v2.6: خط مویی زیر نوار عنوان — رنگ/گرادیان از سبک تم فعال
        var hairline = new Border
        {
            Height = 1,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(18, 0, 18, 0),
            IsHitTestVisible = false,
            Background = (Brush)Application.Current.Resources["LuxHairline"]
        };
        Grid.SetRow(hairline, 0);
        mainGrid.Children.Add(hairline);
    }

    private void BuildContent(Grid mainGrid)
    {
        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(252) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(content, 1);

        BuildSidebar(content);
        BuildWorkspace(content);

        mainGrid.Children.Add(content);
    }

    // ═══════════════════════ سایدبار ═══════════════════════

    private void BuildSidebar(Grid content)
    {
        var sidebar = new Border
        {
            Background = LuxUI.SidebarFill,
            BorderBrush = LuxUI.GlassStroke,
            BorderThickness = LuxUI.CardBorderThick,
            CornerRadius = new CornerRadius(LuxUI.SideRadius),
            Margin = new Thickness(16, 0, 12, 16),
            Effect = LuxUI.CardShadow   // بروتال: سایه سخت | کلود: سایه نرم | گلس: بدون سایه
        };
        var stack = new StackPanel { Margin = new Thickness(16, 22, 16, 16) };

        // ─── لوگو ───
        var logoRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) };
        var logoGlowColor = (Color)ColorConverter.ConvertFromString(ThemeService.Current.Style.LogoGlow);
        var logoGlow = new DropShadowEffect
        { BlurRadius = 22, ShadowDepth = 0, Color = logoGlowColor, Opacity = 0.7 };
        var logoChip = new Border
        {
            Width = 46, Height = 46,
            Background = LuxUI.PlatinumMetal,
            CornerRadius = new CornerRadius(LuxUI.LogoRadius),
            Effect = logoGlow,
            Child = new TextBlock
            {
                Text = "✦", FontSize = 21, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(LuxUI.LogoTextColor),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        };
        logoRow.Children.Add(logoChip);

        // ✨ v2.6: نبض ملایم هاله‌ی لوگو — حس زنده بودن برند (v2.7: روی ۲۰ فریم)
        var logoPulse = new DoubleAnimation(0.5, 0.85, TimeSpan.FromSeconds(3.4))
        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        Slow(logoPulse);
        logoGlow.BeginAnimation(DropShadowEffect.OpacityProperty, logoPulse);

        var logoTitle = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(11, 0, 0, 0) };
        logoTitle.Children.Add(new TextBlock { Text = "بازارسنج", FontSize = 17.5, FontWeight = LuxUI.HeaderWeight, Foreground = LuxUI.TextPrimary });
        logoTitle.Children.Add(new TextBlock { Text = "هوش قیمت‌گذاری و رصد رقبا", FontSize = 9, Foreground = LuxUI.TextDim, Margin = new Thickness(0, 3, 0, 0) });
        logoRow.Children.Add(logoTitle);
        stack.Children.Add(logoRow);

        stack.Children.Add(new TextBlock
        {
            Text = "ردیاب هوشمند قیمت بازار",
            FontSize = 10.5, Foreground = LuxUI.TextDim,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 24)
        });

        // ─── دکمه‌های ناوبری ───
        stack.Children.Add(SideButton("⟳", "بروزرسانی لیست", RefreshAll_Click));
        stack.Children.Add(SideButton("◈", "اسکن سایت‌های دیگر", ScanExternalSite_Click));
        stack.Children.Add(SideButton("⇄", "مقایسه محصولات", CompareHub_Click));
        stack.Children.Add(SideButton("↥", "خروجی اکسل", ExportLinks_Click));
        stack.Children.Add(SideButton("▦", "مدیریت دسته‌بندی‌ها", ManageCategories_Click));
        stack.Children.Add(SideButton("⚙", "تنظیمات", OpenSettings_Click));

        // ─── کارت وضعیت موتور ───
        var engineCard = new Border
        {
            Background = LuxUI.GlassFill,
            BorderBrush = LuxUI.GlassStroke, BorderThickness = LuxUI.CardBorderThick,
            CornerRadius = new CornerRadius(LuxUI.CardRadius),
            Padding = new Thickness(14, 12, 14, 12),
            VerticalAlignment = VerticalAlignment.Bottom,
            Effect = LuxUI.CardShadow
        };
        var engineStack = new StackPanel();
        engineStack.Children.Add(new TextBlock { Text = "موتور اسکن تطبیقی", FontSize = 11.5, FontWeight = FontWeights.Medium, Foreground = LuxUI.TextSecondary });
        var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        statusRow.Children.Add(new Ellipse { Width = 6, Height = 6, Fill = LuxUI.Success, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) });
        statusRow.Children.Add(new TextBlock { Text = "فعال — آماده اسکن", FontSize = 10.5, Foreground = LuxUI.TextDim });
        engineStack.Children.Add(statusRow);
        engineCard.Child = engineStack;
        stack.Children.Add(engineCard);

        sidebar.Child = stack;
        Grid.SetColumn(sidebar, 0);
        content.Children.Add(sidebar);
    }

    private Button SideButton(string icon, string label, RoutedEventHandler click)
    {
        var btn = new Button
        {
            Margin = new Thickness(0, 0, 0, 9),
            Height = 46,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(4, 0, 6, 0)
        };

        // ✨ v2.6: اکسنت‌بار لغزنده — هنگام hover روشن می‌شود (گذار انیمیشنی، نه آنی)
        var accentBar = new Border
        {
            Width = 2.5,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(6, 8, 0, 8),
            CornerRadius = new CornerRadius(1),
            Background = LuxUI.Iridescent,
            Opacity = 0
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var chipBrush = new SolidColorBrush(LuxUI.ChipFillColor);
        var chip = new Border
        {
            Width = 30, Height = 30,
            Background = chipBrush,
            CornerRadius = new CornerRadius(LuxUI.ChipRadius),
            BorderBrush = LuxUI.GlassStroke, BorderThickness = LuxUI.ChipBorderThick,
            Child = new TextBlock
            {
                Text = icon, FontSize = 13.5, Foreground = LuxUI.Accent,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        };
        row.Children.Add(chip);
        row.Children.Add(new TextBlock
        {
            Text = label, FontSize = 13, Foreground = LuxUI.TextPrimary,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0)
        });

        var host = new Grid { Background = Brushes.Transparent };
        host.Children.Add(accentBar);
        host.Children.Add(row);
        btn.Content = host;

        var chipHover = new ColorAnimation
        {
            To = LuxUI.ChipFillHoverColor,   // ✨ v2.8: هم‌گام با سبک فعال
            Duration = TimeSpan.FromMilliseconds(150),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        var chipRest = new ColorAnimation
        {
            To = LuxUI.ChipFillColor,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        btn.MouseEnter += (s, e) =>
        {
            accentBar.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(160)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
            chipBrush.BeginAnimation(SolidColorBrush.ColorProperty, chipHover);
        };
        btn.MouseLeave += (s, e) =>
        {
            accentBar.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(220)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
            chipBrush.BeginAnimation(SolidColorBrush.ColorProperty, chipRest);
        };

        btn.Click += click;
        return btn;
    }

    // ═══════════════════════ فضای کاری ═══════════════════════

    private void BuildWorkspace(Grid content)
    {
        var workspace = new Grid { Margin = new Thickness(6, 0, 18, 16) };
        workspace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                      // ✨ v3.1: نوار تب‌ها
        workspace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                      // وضعیت عملیات (مشترک بین تب‌ها)
        workspace.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // صفحه‌ی فعال
        Grid.SetColumn(workspace, 1);

        // ─── ✨ v3.1: نوار تب‌ها — «همه محصولات» + تب‌های مقایسه‌ی قابل بستن ───
        var tabsHost = new Border
        {
            Background = LuxUI.GhostFill,
            BorderBrush = LuxUI.GlassStroke, BorderThickness = LuxUI.ChipBorderThick,
            CornerRadius = new CornerRadius(LuxUI.PillRadius + 4),
            Padding = new Thickness(6, 5, 6, 5),
            Margin = new Thickness(0, 2, 0, 10)
        };
        _tabsPanel = new WrapPanel();
        tabsHost.Child = _tabsPanel;
        workspace.Children.Add(tabsHost);
        Grid.SetRow(tabsHost, 0);

        // ─── وضعیت + توقف (مشترک — از هر تبی دیده می‌شود) ───
        var statusRow = new Grid { Margin = new Thickness(2, 0, 0, 10) };
        statusRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statusRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var statusStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _statusText = new TextBlock
        {
            Text = "سیستم آماده است.",
            Foreground = LuxUI.TextSecondary, FontSize = 11.5,
            Margin = new Thickness(0, 0, 0, 7)
        };
        _progressBar = new ProgressBar { Height = 4, Visibility = Visibility.Collapsed };
        statusStack.Children.Add(_statusText);
        statusStack.Children.Add(_progressBar);
        statusRow.Children.Add(statusStack);
        Grid.SetColumn(statusStack, 0);

        _stopBtn = new Button
        {
            Content = "⏹ توقف",
            Style = (Style)Application.Current.Resources["LuxBtnStop"],
            Height = 30, Padding = new Thickness(16, 0, 16, 0),
            FontSize = 11.5, Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        _stopBtn.Click += (s, e) =>
        {
            try { _cts?.Cancel(); } catch { }
            _statusText.Text = "در حال توقف عملیات...";
        };
        statusRow.Children.Add(_stopBtn);
        Grid.SetColumn(_stopBtn, 1);

        workspace.Children.Add(statusRow);
        Grid.SetRow(statusRow, 1);

        // ─── میزبان صفحه‌ی فعال ───
        _pageHost = new Grid();
        workspace.Children.Add(_pageHost);
        Grid.SetRow(_pageHost, 2);

        // ─── صفحه‌ی «همه محصولات» ───
        var productsPage = BuildProductsPage();
        InitTabs(productsPage);

        content.Children.Add(workspace);
    }

    /// <summary>✨ v3.1: صفحه‌ی لیست محصولات (تب اول) — آمار + جستجو + فیلتر + کارت‌ها</summary>
    private FrameworkElement BuildProductsPage()
    {
        var page = new Grid();
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // ─── کارت‌های آماری (بنتو) ───
        var statsPanel = new Grid { Margin = new Thickness(0, 2, 0, 16) };
        for (int i = 0; i < 3; i++) statsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _txtTotalProducts = StatCard(statsPanel, 0, "◈", "محصولات ردیابی شده", "۰");
        _txtCheapestPrice = StatCard(statsPanel, 1, "↓", "ارزان‌ترین قیمت", "۰ تومان");
        _txtAvgPrice = StatCard(statsPanel, 2, "≈", "میانگین بازار", "۰ تومان");
        page.Children.Add(statsPanel);
        Grid.SetRow(statsPanel, 0);

        // ─── ✨ v3.1.4: جستجوی زنده‌ی محصولات (نام / فروشگاه / دسته) ───
        var searchHost = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        _searchBox = new TextBox
        {
            MinHeight = 40, VerticalContentAlignment = VerticalAlignment.Center, FontSize = 13
        };
        _searchPlaceholder = new TextBlock
        {
            Text = "🔍  جستجوی محصول، فروشگاه یا دسته…",
            FontSize = 13, Foreground = LuxUI.TextDim,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 14, 0), IsHitTestVisible = false
        };
        searchHost.Children.Add(_searchBox);
        searchHost.Children.Add(_searchPlaceholder);

        _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
        _searchDebounce.Tick += (s, e) =>
        {
            _searchDebounce.Stop();
            var q = _searchBox.Text ?? "";
            if (q == _searchText) return;
            _searchText = q;
            _ = LoadProductsAsync(animate: false);
        };
        _searchBox.TextChanged += (s, e) =>
        {
            _searchPlaceholder.Visibility = string.IsNullOrEmpty(_searchBox.Text)
                ? Visibility.Visible : Visibility.Collapsed;
            _searchDebounce.Stop();
            _searchDebounce.Start();
        };
        _searchBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape)   // پاک‌سازی سریع با ESC
                _searchBox.Text = "";
        };
        page.Children.Add(searchHost);
        Grid.SetRow(searchHost, 1);

        // ─── فیلتر دسته‌ها + مرتب‌سازی ───
        var filterRow = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        filterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        filterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // WrapPanel: با زیاد شدن دسته‌ها به خط بعدی می‌شکند (قبلاً StackPanel سرریز می‌شد)
        _categoryPillsPanel = new WrapPanel();
        Grid.SetColumn(_categoryPillsPanel, 0);
        filterRow.Children.Add(_categoryPillsPanel);

        _sortCombo = new ComboBox { Width = 168, Height = 36, Margin = new Thickness(12, 0, 0, 0) };
        _sortCombo.Items.Add("مرتب‌سازی: پیش‌فرض");
        _sortCombo.Items.Add("ارزان‌ترین");
        _sortCombo.Items.Add("گران‌ترین");
        _sortCombo.Items.Add("جدیدترین");
        _sortCombo.SelectedIndex = 0;
        _sortCombo.SelectionChanged += (s, e) => _ = LoadProductsAsync();
        Grid.SetColumn(_sortCombo, 1);
        filterRow.Children.Add(_sortCombo);

        page.Children.Add(filterRow);
        Grid.SetRow(filterRow, 2);

        // ─── لیست کارت‌ها ───
        _listScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = Brushes.Transparent,
            Margin = new Thickness(0, 2, 6, 0)
        };
        _cardsContainer = new StackPanel();
        _listScroll.Content = _cardsContainer;
        page.Children.Add(_listScroll);
        Grid.SetRow(_listScroll, 3);

        return page;
    }

    private TextBlock StatCard(Grid parent, int col, string icon, string title, string value)
    {
        var card = new Border
        {
            Background = LuxUI.GlassFill,
            BorderBrush = LuxUI.GlassStroke, BorderThickness = LuxUI.CardBorderThick,
            CornerRadius = new CornerRadius(LuxUI.CardRadius),
            Padding = new Thickness(18, 14, 18, 15),
            Margin = new Thickness(col == 2 ? 0 : 0, 0, col == 0 ? 14 : 9, 0),
            Effect = LuxUI.CardShadow
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var top = new StackPanel { Orientation = Orientation.Horizontal };
        var chip = new Border
        {
            Width = 32, Height = 32,
            Background = LuxUI.ChipFill,
            CornerRadius = new CornerRadius(LuxUI.ChipRadius),
            BorderBrush = LuxUI.GlassStroke, BorderThickness = LuxUI.ChipBorderThick,
            Child = new TextBlock
            {
                Text = icon, FontSize = 14, Foreground = LuxUI.Accent,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        };
        top.Children.Add(chip);
        top.Children.Add(new TextBlock
        {
            Text = title, FontSize = 11.5, Foreground = LuxUI.TextSecondary,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0)
        });
        grid.Children.Add(top);

        var valueTxt = LuxUI.IridescentText(value, 19);
        valueTxt.HorizontalAlignment = HorizontalAlignment.Left;
        valueTxt.Margin = new Thickness(2, 10, 0, 0);
        valueTxt.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(valueTxt);
        Grid.SetRow(valueTxt, 1);

        card.Child = grid;
        Grid.SetColumn(card, col);
        parent.Children.Add(card);
        return valueTxt;
    }

    // ═══════════════════════ بارگذاری و رندر ═══════════════════════

    private int _loadGeneration;   // ✨ v2.7: نگهبان بارگذاری‌های همزمان

    /// <summary>✨ v2.7: انیمیشن‌های همیشگیِ محیطی روی ۲۰ فریم محدود می‌شوند.
    /// پنجره‌ی لایه‌ی شفاف (AllowsTransparency) هر فریم را از نو ترکیب می‌کند؛
    /// سحابی/چشمک/گلو با ۶۰fps فقط CPU/GPU را گرم نگه می‌دارد و حس فریز می‌سازد.</summary>
    private static void Slow(Timeline anim, int fps = 20) => Timeline.SetDesiredFrameRate(anim, fps);

    /// <summary>رنگ لهجه با آلفای دلخواه — هم‌گام با تم فعال (✨ v2.7)</summary>
    private static Color Tint(Brush accent, byte alpha)
    {
        var c = accent is SolidColorBrush sc ? sc.Color : Color.FromRgb(0x8F, 0xB8, 0xFF);
        return Color.FromArgb(alpha, c.R, c.G, c.B);
    }

    /// <summary>رنگ خالص یک براش تک‌رنگ (با حفظ آلفا) — برای کلون‌های قابل‌انیمیشن</summary>
    private static Color ColorOf(Brush b)
    {
        return b is SolidColorBrush sc ? sc.Color : Color.FromRgb(0x8F, 0xB8, 0xFF);
    }

    /// <summary>
    /// ✨ v2.7 رفع فریز — بارگذاری غیرهمزمان + رندر تکه‌ای:
    /// قبلاً: کوئری دیتابیس + ساخت «همه‌ی» کارت‌ها در یک قدم روی UI Thread؛
    /// با چند صد محصول پنجره چند ثانیه قفل می‌شد (همان «فریز»).
    /// حالا: دیتا در Task.Run می‌آید و کارت‌ها در دسته‌های ۱۴تایی با اولویت
    /// Background ساخته می‌شوند → UI بین دسته‌ها نفس می‌کشد؛ فیلتر/مرتب‌سازی
    /// حین عملیات هم پاسخ‌گو می‌ماند. نگهبان Generation بارگذاری‌های
    /// همزمان را خنثی می‌کند (کلیک‌های پشت‌سرهم pill).
    /// </summary>
    private async Task LoadProductsAsync(bool animate = true)
    {
        int gen = ++_loadGeneration;
        try
        {
            var (products, categories) = await Task.Run(() => (_db.GetAllProducts(), _db.GetAllCategories()));
            if (gen != _loadGeneration) return;   // بارگذاری جدیدتری شروع شده

            _allProducts = new ObservableCollection<SavedProduct>(products);

            if (_currentFilter == "همه") _filteredProducts = _allProducts.ToList();
            else _filteredProducts = _allProducts.Where(p => p.CategoryName == _currentFilter).ToList();

            // ✨ v3.1.4: جستجوی زنده — نام محصول / فروشگاه / دسته (با نرمال‌سازی فارسی:
            // ي→ی و ك→ک و نیم‌فاصله→فاصله تا جستجوی کاربر در هر حالتی بخورد)
            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                var q = NormalizeFa(_searchText);
                _filteredProducts = _filteredProducts.Where(p =>
                    NormalizeFa(p.ProductName).Contains(q) ||
                    NormalizeFa(p.StoreName).Contains(q) ||
                    NormalizeFa(p.CategoryName).Contains(q)).ToList();
            }

            if (_sortCombo != null && _sortCombo.SelectedIndex > 0)
            {
                switch (_sortCombo.SelectedIndex)
                {
                    case 1: _filteredProducts = _filteredProducts.OrderBy(p => p.LastPrice).ToList(); break;
                    case 2: _filteredProducts = _filteredProducts.OrderByDescending(p => p.LastPrice).ToList(); break;
                    case 3: _filteredProducts = _filteredProducts.OrderByDescending(p => p.LastUpdate).ToList(); break;
                }
            }

            RenderCategoryPills(categories);

            _cardsContainer.Children.Clear();
            if (_filteredProducts.Count == 0)
            {
                var empty = string.IsNullOrWhiteSpace(_searchText)
                    ? BuildEmptyState()
                    : BuildNoSearchState();   // ✨ v3.1.4: پیام اختصاصی «نتیجه‌ای برای جستجو نبود»
                if (animate) AnimateCardEntrance(empty, 0);
                _cardsContainer.Children.Add(empty);
            }
            else
            {
                for (int i = 0; i < _filteredProducts.Count; i++)
                {
                    var card = CreateLuxProductCard(_filteredProducts[i]);
                    if (animate && i < 24) AnimateCardEntrance(card, i);   // ✨ فقط ۲۴ کارت اول انیمیشن ورود — لیست‌های بزرگ فوری کامل می‌شوند
                    _cardsContainer.Children.Add(card);

                    if (i % 14 == 13)
                    {
                        await Dispatcher.Yield(DispatcherPriority.Background);   // نفس کشیدن UI بین دسته‌ها
                        if (gen != _loadGeneration) return;
                    }
                }
            }

            UpdateStats();
        }
        catch (Exception ex)
        {
            Logger.Error("LoadProducts", "", ex.ToString());
            if (_statusText != null) _statusText.Text = "خطا در بارگذاری لیست: " + ex.Message;
        }
    }

    private void UpdateStats()
    {
        _txtTotalProducts.Text = LuxUI.Fa(_filteredProducts.Count);
        if (_filteredProducts.Any(p => p.LastPrice > 0))
        {
            _txtCheapestPrice.Text = LuxUI.FaPrice(_filteredProducts.Where(p => p.LastPrice > 0).Min(p => p.LastPrice)) + " تومان";
            _txtAvgPrice.Text = LuxUI.FaPrice(_filteredProducts.Where(p => p.LastPrice > 0).Average(p => p.LastPrice)) + " تومان";
        }
        else { _txtCheapestPrice.Text = "۰ تومان"; _txtAvgPrice.Text = "۰ تومان"; }
    }

    private void RenderCategoryPills(List<string> categories)
    {
        _categoryPillsPanel.Children.Clear();
        categories.Insert(0, "همه");

        foreach (var cat in categories)
        {
            var pill = new Button
            {
                Content = cat,
                Height = 33,
                Margin = new Thickness(0, 0, 8, 6),
                Padding = new Thickness(16, 0, 16, 0),
                FontSize = 12,
                Tag = cat
            };

            if (cat == _currentFilter)
            {
                // ✨ v2.8: pill فعال با هویت دکمه‌ی اصلی تم (زرد بروتال / متالیک گلس / ...)
                pill.Background = LuxUI.PlatinumMetal;
                pill.Foreground = new SolidColorBrush(LuxUI.LogoTextColor);
                pill.FontWeight = FontWeights.SemiBold;
                pill.BorderBrush = new SolidColorBrush(LuxUI.LogoTextColor);
            }

            pill.Click += (s, e) =>
            {
                _currentFilter = (string)((Button)s).Tag!;
                _ = LoadProductsAsync();
            };
            _categoryPillsPanel.Children.Add(pill);
        }
    }

    /// <summary>
    /// ✨ v2.6: ورود پلکانی کارت‌ها — هر کارت با تاخیر کوتاه fade+rise می‌شود؛
    /// حس روونی و زنده بودن لیست (سقف تاخیر ۴۵۰ms تا لیست‌های بزرگ کند نشوند)
    /// </summary>
    private void AnimateCardEntrance(FrameworkElement card, int index)
    {
        var translate = new TranslateTransform(0, 14);
        card.RenderTransform = translate;
        card.Opacity = 0;

        var delay = TimeSpan.FromMilliseconds(Math.Min(index * 28, 450));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease, BeginTime = delay };
        var rise = new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(300)) { EasingFunction = ease, BeginTime = delay };
        card.BeginAnimation(OpacityProperty, fade);
        translate.BeginAnimation(TranslateTransform.YProperty, rise);
    }

    /// <summary>
    /// ✨ v3.1.4: نرمال‌سازی متن فارسی برای جستجو — عربی‌به‌فارسی (ي→ی، ك→ک) +
    /// نیم‌فاصله→فاصله + لاتین کوچک؛ تا جستجوی کاربر در هر شیوه‌ی تایپی بخورد
    /// </summary>
    private static string NormalizeFa(string? s) => (s ?? "")
        .Replace('ي', 'ی')
        .Replace('ك', 'ک')
        .Replace('\u200c', ' ')
        .ToLowerInvariant();

    /// <summary>
    /// ✨ v3.1.4: خالی‌استیت مخصوص جستجوی بی‌نتیجه — با نمایش خود عبارت جستجو
    /// </summary>
    private FrameworkElement BuildNoSearchState()
    {
        var panel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 60, 0, 0)
        };

        var chip = new Border
        {
            Width = 64, Height = 64,
            Background = LuxUI.ChipFill,
            BorderBrush = LuxUI.GlassStroke, BorderThickness = LuxUI.ChipBorderThick,
            CornerRadius = new CornerRadius(LuxUI.LogoRadius),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock
            {
                Text = "🔍", FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        };
        panel.Children.Add(chip);
        panel.Children.Add(new TextBlock
        {
            Text = $"نتیجه‌ای برای «{_searchText.Trim()}» پیدا نشد",
            FontSize = 16.5, FontWeight = FontWeights.SemiBold,
            Foreground = LuxUI.TextPrimary, HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 18, 0, 0)
        });
        panel.Children.Add(new TextBlock
        {
            Text = "بخشی از نام محصول، فروشگاه یا دسته را امتحان کنید یا عبارت را پاک کنید (ESC).",
            FontSize = 12, Foreground = LuxUI.TextSecondary,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        });
        return panel;
    }

    private FrameworkElement BuildEmptyState()
    {
        var panel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 60, 0, 0)
        };

        var chip = new Border
        {
            Width = 64, Height = 64,
            Background = LuxUI.ChipFill,
            BorderBrush = LuxUI.GlassStroke, BorderThickness = LuxUI.ChipBorderThick,
            CornerRadius = new CornerRadius(LuxUI.LogoRadius),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock
            {
                Text = "✦", FontSize = 26, Foreground = LuxUI.Accent,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        };
        // ✨ v2.6: شنای آهسته آیکون خالی‌استیت — فضای خالی هم زنده است (v2.7: روی ۲۰ فریم)
        var floatT = new TranslateTransform(0, 0);
        chip.RenderTransform = floatT;
        var floatAnim = new DoubleAnimation(0, -6, TimeSpan.FromSeconds(2.6))
        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        Slow(floatAnim);
        floatT.BeginAnimation(TranslateTransform.YProperty, floatAnim);
        panel.Children.Add(chip);
        panel.Children.Add(new TextBlock
        {
            Text = "هنوز محصولی ردیابی نشده",
            FontSize = 16.5, FontWeight = FontWeights.SemiBold,
            Foreground = LuxUI.TextPrimary, HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 18, 0, 0)
        });
        panel.Children.Add(new TextBlock
        {
            Text = "سایت رقبا را اسکن کنید یا اولین محصول را اضافه کنید تا قیمت‌ها را لحظه‌ای رصد کنیم.",
            FontSize = 12, Foreground = LuxUI.TextSecondary,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var cta = LuxUI.PrimaryButton("افزودن اولین محصول");
        cta.Margin = new Thickness(0, 22, 0, 0);
        cta.Padding = new Thickness(24, 0, 24, 0);
        cta.HorizontalAlignment = HorizontalAlignment.Center;
        cta.Click += (s, e) =>
        {
            var dialog = new AddProductWindow(_db.GetAllCategories()) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                _db.SaveProduct(new SavedProduct
                {
                    ProductName = dialog.ProductName,
                    TorobUrl = dialog.ProductUrl,
                    CategoryName = dialog.SelectedCategory
                });
                LoadProductsAsyncReload();
                ShowToast("محصول اضافه شد و ردیابی آغاز شد", "✦");
            }
        };
        panel.Children.Add(cta);

        var wrap = new Border { Child = panel };
        return wrap;
    }

    private FrameworkElement CreateLuxProductCard(SavedProduct product)
    {
        bool isNew = _recentlyScannedNewUrls.Contains(product.TorobUrl);

        var card = new Border
        {
            Background = isNew
                ? new SolidColorBrush(Tint(LuxUI.Success, 0x14))
                : LuxUI.GlassFill,
            CornerRadius = new CornerRadius(LuxUI.CardRadius),
            Margin = new Thickness(0, 0, 0, 10),
            BorderThickness = LuxUI.CardBorderThick,
            Effect = LuxUI.CardShadow
        };

        var grid = new Grid { Margin = new Thickness(18, 14, 16, 14) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // ─── اطلاعات محصول + نوار لهجه ───
        var info = new Grid();
        info.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        info.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var accent = new Border
        {
            Width = 3.5,
            Background = LuxUI.Iridescent,
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 3, 12, 3),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        info.Children.Add(accent);

        var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        infoStack.Children.Add(new TextBlock
        {
            Text = product.ProductName, Foreground = LuxUI.TextPrimary,
            FontSize = 14, FontWeight = FontWeights.Medium,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        var metaRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 7, 0, 0) };
        metaRow.Children.Add(new TextBlock
        {
            Text = product.StoreName, Foreground = LuxUI.Accent, FontSize = 11
        });
        metaRow.Children.Add(new TextBlock
        {
            Text = $"  •  {product.CategoryName}  •  بروزرسانی: {LuxUI.RelativeTimeFa(product.LastUpdate)}",
            Foreground = LuxUI.TextDim, FontSize = 11
        });
        infoStack.Children.Add(metaRow);
        info.Children.Add(infoStack);
        Grid.SetColumn(infoStack, 1);
        grid.Children.Add(info);

        // ─── قیمت ───
        var priceStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        priceStack.Children.Add(new TextBlock
        {
            Text = product.LastPrice > 0 ? LuxUI.FaPrice(product.LastPrice) : "—",
            Foreground = LuxUI.Platinum, FontSize = 17.5, FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        priceStack.Children.Add(new TextBlock
        {
            Text = product.LastPrice > 0 ? "تومان" : "قیمت ثبت نشده",
            Foreground = LuxUI.TextDim, FontSize = 10.5,
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0)
        });
        grid.Children.Add(priceStack);
        Grid.SetColumn(priceStack, 1);

        // ─── نشانگر تغییر قیمت + بازکردن لینک ───
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };

        actions.Children.Add(BuildChangeBadge(product, isNew));
        actions.Children.Add(LuxUI.OpenLinkButton(this, product.TorobUrl));

        grid.Children.Add(actions);
        Grid.SetColumn(actions, 2);

        card.Child = grid;

        // ✨ v3.1: کلیک روی کارت → تب مقایسه‌ی این محصول باز می‌شود (لینک فروشگاه‌های دیگر)
        card.Cursor = Cursors.Hand;
        card.ToolTip = "کلیک: مقایسه‌ی این محصول بین فروشگاه‌ها";
        card.MouseLeftButtonUp += (s, e) =>
        {
            e.Handled = true;
            OpenCompareTabForProduct(product);
        };

        // ─── hover لوکس v2.7: بدون سایه‌ی دائمی ✨ پرفورمنس ───
        // قبلاً هر کارت یک DropShadowEffect داشت (حتی با Opacity=۰) — چند صد
        // افکت بلور، رندر اسکرول را سنگین می‌کرد (عامل دوم «فریز»). حالا افکت
        // فقط هنگام hover ساخته و بعد از خروج ماوس حذف می‌شود؛ BitmapCache هم
        // کارتِ بی‌حرکت را روی GPU رستر می‌کند → اسکرول روان.
        card.CacheMode = new BitmapCache();
        {
            var glowColor = isNew ? ((SolidColorBrush)LuxUI.Success).Color : ((SolidColorBrush)LuxUI.Accent).Color;
            var restBorder = isNew ? Tint(LuxUI.Success, 0x55) : ColorOf(LuxUI.GlassStroke);
            var hoverBorder = isNew ? Tint(LuxUI.Success, 0x85) : Tint(LuxUI.Accent, 0x45);
            card.BorderBrush = new SolidColorBrush(restBorder);   // کلون قابل‌انیمیشن (براش منجمد انیمیت نمی‌شود)
            DropShadowEffect? hoverGlow = null;

            // ✨ v2.8: در سبک‌های سایه‌دار (بروتال/کلود/سایبر) سایه‌ی پایه نباید هنگام
            // hover با گلو جایگزین شود — فقط گلس که سایه‌ی پایه ندارد گلو می‌گیرد.
            bool hasBaseShadow = LuxUI.CardShadow != null;

            card.MouseEnter += (s, e) =>
            {
                if (!hasBaseShadow)
                {
                    hoverGlow ??= new DropShadowEffect { BlurRadius = 22, ShadowDepth = 0, Color = glowColor, Opacity = 0 };
                    card.Effect = hoverGlow;
                }
                var t = card.RenderTransform as TranslateTransform;
                if (t == null) { t = new TranslateTransform(); card.RenderTransform = t; }
                t.BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(t.Y, -2, TimeSpan.FromMilliseconds(150)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
                if (card.BorderBrush is SolidColorBrush bb)
                    bb.BeginAnimation(SolidColorBrush.ColorProperty,
                        new ColorAnimation(hoverBorder, TimeSpan.FromMilliseconds(150)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
                hoverGlow?.BeginAnimation(DropShadowEffect.OpacityProperty,
                    new DoubleAnimation(0.26, TimeSpan.FromMilliseconds(160)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
            };
            card.MouseLeave += (s, e) =>
            {
                if (card.RenderTransform is TranslateTransform t)
                    t.BeginAnimation(TranslateTransform.YProperty,
                        new DoubleAnimation(t.Y, 0, TimeSpan.FromMilliseconds(190)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
                if (card.BorderBrush is SolidColorBrush bb)
                    bb.BeginAnimation(SolidColorBrush.ColorProperty,
                        new ColorAnimation(restBorder, TimeSpan.FromMilliseconds(200)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
                if (hoverGlow == null) return;
                var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(210)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                fadeOut.Completed += (s2, e2) => { if (card.Effect == hoverGlow && hoverGlow.Opacity < 0.01) card.Effect = hasBaseShadow ? LuxUI.CardShadow : null; };
                hoverGlow.BeginAnimation(DropShadowEffect.OpacityProperty, fadeOut);
            };
        }

        return card;
    }

    private FrameworkElement BuildChangeBadge(SavedProduct product, bool isNew)
    {
        string text; Brush fg, bg;
        if (isNew) { text = "✦ محصول جدید"; fg = (Brush)Application.Current.Resources["LuxSuccessText"]; bg = new SolidColorBrush(Tint(LuxUI.Success, 0x20)); }
        else if (product.LastPrice == 0) { text = "در انتظار قیمت"; fg = LuxUI.Warning; bg = new SolidColorBrush(Tint(LuxUI.Warning, 0x20)); }
        else if (product.PreviousPrice == 0) { text = "بروزرسانی شد"; fg = LuxUI.TextSecondary; bg = new SolidColorBrush(Tint(LuxUI.TextSecondary, 0x14)); }
        else if (product.LastPrice < product.PreviousPrice)
        {
            text = $"▼ {LuxUI.FaPrice(product.PreviousPrice - product.LastPrice)}";
            fg = LuxUI.Success; bg = new SolidColorBrush(Tint(LuxUI.Success, 0x20));
        }
        else if (product.LastPrice > product.PreviousPrice)
        {
            text = $"▲ {LuxUI.FaPrice(product.LastPrice - product.PreviousPrice)}";
            fg = LuxUI.Danger; bg = new SolidColorBrush(Tint(LuxUI.Danger, 0x20));
        }
        else { text = "ثابت"; fg = LuxUI.TextDim; bg = new SolidColorBrush(Tint(LuxUI.TextDim, 0x10)); }

        return new Border
        {
            Background = bg,
            CornerRadius = new CornerRadius(LuxUI.PillRadius),
            Padding = new Thickness(9, 4, 9, 5),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = text, Foreground = fg, FontSize = 10,
                FontWeight = FontWeights.Medium
            }
        };
    }

    // ═══════════════════════ رویدادها ═══════════════════════

    private async void RefreshAll_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) { MessageBox.Show(this, "یک عملیات در حال اجراست. لطفاً منتظر بمانید یا آن را متوقف کنید."); return; }

        // ✅ رفع باگ ۲: بروزرسانی از کل دیتابیس — محصولات بدون قیمت هم شانس قیمت‌گرفتن پیدا می‌کنند
        var productsToUpdate = _db.GetAllProducts();
        if (!productsToUpdate.Any()) { MessageBox.Show(this, "هیچ محصولی برای بروزرسانی وجود ندارد."); return; }

        _isBusy = true;
        _cts = new CancellationTokenSource();
        _recentlyScannedNewUrls.Clear();

        _progressBar.Visibility = Visibility.Visible; _progressBar.Value = 0; _progressBar.Maximum = productsToUpdate.Count;
        _stopBtn.Visibility = Visibility.Visible;
        var progress = new Progress<(int current, int total, string status)>(p =>
        {
            _statusText.Text = LuxUI.Fa($"{p.status} ({p.current}/{p.total})");
            _progressBar.Value = p.current;
        });

        try
        {
            await Task.Run(() => _scraper.RefreshProductsAsync(productsToUpdate, progress, _cts.Token));
            _statusText.Text = "بروزرسانی با موفقیت کامل شد.";
            ShowToast("بروزرسانی کامل شد", "⟳");
        }
        catch (OperationCanceledException)
        {
            _statusText.Text = "بروزرسانی متوقف شد.";
        }
        catch (Exception ex)
        {
            // ✅ رفع باگ ۱: دیگر کرش خاموش نداریم
            _statusText.Text = $"خطا در بروزرسانی: {ex.Message}";
            MessageBox.Show(this, $"خطا در بروزرسانی:\n{ex.Message}", "خطا",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _progressBar.Visibility = Visibility.Collapsed;
            _stopBtn.Visibility = Visibility.Collapsed;
            _cts.Dispose(); _cts = null;
            _isBusy = false;
            _ = LoadProductsAsync(animate: false);
            RefreshActiveCompareTab();   // ✨ v3.1: تب مقایسه‌ی فعال هم با قیمت‌های تازه بازسازی شود
        }
    }

    private async void ScanExternalSite_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) { MessageBox.Show(this, "یک عملیات در حال اجراست. لطفاً منتظر بمانید یا آن را متوقف کنید."); return; }

        var scanWin = new ExternalScanWindow(_db.GetAllCategories(), _scraperFactory.GetRegisteredSites()) { Owner = this };
        if (scanWin.ShowDialog() == true)
        {
            _isBusy = true;
            _cts = new CancellationTokenSource();

            _progressBar.Visibility = Visibility.Visible;
            _progressBar.IsIndeterminate = true;
            _stopBtn.Visibility = Visibility.Visible;

            var scraper = _scraperFactory.GetScraper(scanWin.CategoryUrl);
            _statusText.Text = $"استفاده از {scraper.SiteName} — در حال اسکن...";

            var progress = new Progress<(int current, int total, string status)>(p =>
            {
                _statusText.Text = LuxUI.Fa($"{scraper.SiteName}: {p.status} ({p.current}/{p.total})");
            });

            List<SavedProduct> scannedProducts;
            try
            {
                scannedProducts = await Task.Run(() =>
                    scraper.ScanCategoryAsync(
                        scanWin.CategoryUrl,
                        scanWin.TargetCategory,
                        scanWin.StoreName,
                        progress,
                        _cts.Token));
            }
            catch (OperationCanceledException)
            {
                EndBusyState();
                _statusText.Text = "اسکن لغو شد.";
                _ = LoadProductsAsync(animate: false);
                RefreshActiveCompareTab();
                return;
            }
            catch (Exception ex)
            {
                EndBusyState();
                _statusText.Text = $"خطا در اسکن: {ex.Message}";
                MessageBox.Show(this, $"خطا در اسکن:\n{ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var existingUrls = _db.GetAllProducts().Select(p => p.TorobUrl).ToHashSet();
            var newProducts = new List<SavedProduct>();

            _recentlyScannedNewUrls.Clear();

            foreach (var p in scannedProducts)
            {
                if (p.LastPrice > 0)
                {
                    if (!existingUrls.Contains(p.TorobUrl))
                    {
                        newProducts.Add(p);
                        _recentlyScannedNewUrls.Add(p.TorobUrl);
                    }
                    try { _db.SaveProduct(p); }
                    catch (Exception ex) { Logger.Error("ExternalScan", p.TorobUrl, ex.Message); }
                }
            }

            EndBusyState();

            _statusText.Text = LuxUI.Fa($"اسکن کامل شد. {scannedProducts.Count} محصول بررسی شد.");
            ShowToast(LuxUI.Fa($"اسکن کامل شد — {scannedProducts.Count} محصول بررسی شد"), "◈");
            _ = LoadProductsAsync(animate: false);
            RefreshActiveCompareTab();

            if (newProducts.Any())
            {
                var newNames = string.Join("\n", newProducts.Select(p => "• " + p.ProductName).Take(15));
                MessageBox.Show(this,
                    $"اسکن کامل شد!\n{LuxUI.Fa(newProducts.Count)} محصول جدید در سایت پیدا شد:\n\n{newNames}{(newProducts.Count > 15 ? "\n..." : "")}",
                    "محصولات جدید", MessageBoxButton.OK, MessageBoxImage.Information);

                _ = Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _recentlyScannedNewUrls.Clear();
                        _ = LoadProductsAsync(animate: false);
                        RefreshActiveCompareTab();
                    });
                });
            }
            else
            {
                ShowToast(LuxUI.Fa($"هیچ محصول جدیدی پیدا نشد — {scannedProducts.Count} محصول بررسی شد"), "◈");
            }
        }
    }

    private void EndBusyState()
    {
        _progressBar.Visibility = Visibility.Collapsed;
        _progressBar.IsIndeterminate = false;
        _stopBtn.Visibility = Visibility.Collapsed;
        _cts?.Dispose(); _cts = null;
        _isBusy = false;
    }

    /// <summary>
    /// ✨ v2.6: اعلان شیشه‌ای (Toast) — جای پیام‌باکس‌های موفقیت؛
    /// ورود fade+rise، خروج خودکار پس از ۲/۸ ثانیه — بدون قطع کردن جریان کار
    /// </summary>
    private void ShowToast(string message, string icon = "✓")
    {
        _toastHost.Children.Clear();   // هر لحظه یک اعلان — آخرین پیام مهم‌تر است

        var toast = new Border
        {
            Background = LuxUI.PopupBg,
            BorderBrush = LuxUI.GlassStroke,
            BorderThickness = LuxUI.CardBorderThick,
            CornerRadius = new CornerRadius(LuxUI.CardRadius),
            Padding = new Thickness(18, 11, 20, 12),
            Effect = LuxUI.ShadowDialog,
            Child = new StackPanel { Orientation = Orientation.Horizontal }
        };
        var iconText = new TextBlock
        {
            Text = icon, FontSize = 13.5, Foreground = LuxUI.Accent,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0)
        };
        var msgText = new TextBlock
        {
            Text = message, FontSize = 12.5, Foreground = LuxUI.TextPrimary,
            VerticalAlignment = VerticalAlignment.Center, MaxWidth = 420,
            TextWrapping = TextWrapping.Wrap
        };
        ((StackPanel)toast.Child).Children.Add(iconText);
        ((StackPanel)toast.Child).Children.Add(msgText);

        toast.Opacity = 0;
        var slide = new TranslateTransform(0, 16);
        toast.RenderTransform = slide;
        _toastHost.Children.Add(toast);

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        toast.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(190)) { EasingFunction = ease });
        slide.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(16, 0, TimeSpan.FromMilliseconds(230)) { EasingFunction = ease });

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.8) };
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(280)) { EasingFunction = ease };
            fadeOut.Completed += (s2, e2) => _toastHost.Children.Remove(toast);
            toast.BeginAnimation(OpacityProperty, fadeOut);
            slide.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(0, 12, TimeSpan.FromMilliseconds(290)) { EasingFunction = ease });
        };
        timer.Start();
    }

    private async void ExportLinks_Click(object sender, RoutedEventArgs e)
    {
        if (!_filteredProducts.Any()) { MessageBox.Show(this, "محصولی برای خروجی گرفتن نیست."); return; }
        var dialog = new SaveFileDialog { Filter = "فایل اکسل|*.xlsx", FileName = $"گزارش_قیمت_{_currentFilter}.xlsx" };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                // ✨ v2.7 پرفورمنس: ساخت اکسل روی رشته‌ی کارگر — لیست‌های بزرگ UI را قفل نمی‌کنند
                _statusText.Text = "در حال ساخت فایل اکسل...";
                var products = _filteredProducts.ToList();
                var filePath = dialog.FileName;
                await Task.Run(() => _importService.ExportToExcel(products, filePath));
                _statusText.Text = "سیستم آماده است.";
                ShowToast("فایل اکسل با موفقیت ساخته شد", "↥");
            }
            catch (Exception ex)
            {
                _statusText.Text = "سیستم آماده است.";
                MessageBox.Show(this, $"خطا در ساخت فایل اکسل:\n{ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void ManageCategories_Click(object sender, RoutedEventArgs e)
    {
        var mgmtWindow = new ManagementWindow(_db, _scraper) { Owner = this };
        mgmtWindow.ShowDialog();
        _ = LoadProductsAsync(animate: false);
        RefreshActiveCompareTab();   // ✨ v3.1: ممکن است محصولی حذف شده باشد → تب مقایسه هم تازه شود
    }

    // ═════════════ تنظیمات و تم — v2.7 ═════════════

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow { Owner = this };
        win.ShowDialog();
    }

    /// <summary>
    /// ✨ v2.7: تعویض زنده‌ی تم — ThemeService پالت سراسری را عوض می‌کند؛
    /// اینجا محتوای پنجره با پالت جدید بازسازی می‌شود (بدون ری‌استارت).
    /// ✨ v3.1: تب‌های مقایسه‌ی باز هم بعد از بازسازی دوباره باز می‌شوند
    /// (و اگر تب مقایسه فعال بوده، همان تب دوباره فعال می‌شود).
    /// </summary>
    private void OnThemeChanged()
    {
        bool wasMaximized = WindowState == WindowState.Maximized;
        var reopenAnchors = _tabs.Where(t => !t.IsProductsTab).Select(t => t.Anchor).ToList();
        bool compareWasActive = false;
        bool activeWasHub = false;
        int? activeAnchorId = null;
        if (_activeTab != null && !_activeTab.IsProductsTab)
        {
            compareWasActive = true;
            activeWasHub = _activeTab.Anchor == null;
            activeAnchorId = _activeTab.Anchor?.Id;
        }

        InitializeLuxUI();
        if (wasMaximized) ApplyMaximizedChrome();
        _ = LoadProductsAsync(animate: false);

        CompareTabRecord? restore = null;
        foreach (var a in reopenAnchors)
        {
            var rec = AddCompareTab(a, activate: false);
            if (compareWasActive && ((a == null && activeWasHub) || (a != null && a.Id == activeAnchorId)))
                restore = rec;
        }
        if (restore != null) ActivateTab(restore);

        ShowToast("تم «" + ThemeService.Current.NameFa + "» اعمال شد", "✦");
    }

    /// <summary>helper داخلی برای reload بدون انیمیشن ورود</summary>
    private void LoadProductsAsyncReload() => _ = LoadProductsAsync(animate: false);
}
