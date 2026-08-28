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
using Microsoft.Win32;
using TorobScanner.Data;
using TorobScanner.Models;
using TorobScanner.Scrapers;
using TorobScanner.Services;
using TorobScanner.Views;

namespace TorobScanner;

/// <summary>
/// پنجره اصلی — UI آورورا + هماهنگی سرویس‌ها:
/// ✅ رفع باگ ۲: همه محصولات نمایش داده می‌شوند (حتی بدون قیمت)
/// ✅ بروزرسانی از کل دیتابیس (نه فقط فیلتر جاری)
/// ✅ دکمه توقف برای همه عملیات طولانی
/// ✅ محافظ عملیات همزمان + try/catch کامل (رفع باگ ۱ و ۹)
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

    private StackPanel _cardsContainer = null!;
    private TextBlock _statusText = null!;
    private ProgressBar _progressBar = null!;
    private TextBlock _txtTotalProducts = null!;
    private TextBlock _txtCheapestPrice = null!;
    private TextBlock _txtAvgPrice = null!;
    private StackPanel _categoryPillsPanel = null!;
    private ComboBox _sortCombo = null!;
    private Button _stopBtn = null!;

    private HashSet<string> _recentlyScannedNewUrls = new();
    private CancellationTokenSource? _cts;
    private bool _isBusy;

    private readonly SolidColorBrush _auroraCyan = new(Color.FromRgb(0, 240, 255));
    private readonly SolidColorBrush _auroraPurple = new(Color.FromRgb(123, 97, 255));
    private readonly SolidColorBrush _obsidianBg = new(Color.FromRgb(10, 12, 16));
    private readonly SolidColorBrush _glassPanelBg = new(Color.FromArgb(180, 20, 24, 32));

    public MainWindow()
    {
        InitializeComponent();
        ThemeHelper.ApplyObsidianTheme(this);
        _db = new DatabaseManager();
        _scraper = new TorobProductScraper(_db);
        _scraperFactory = new ScraperFactory();
        _importService = new ImportExportService();

        InitializeUltraUI();
        LoadProducts();
    }

    private void InitializeUltraUI()
    {
        Title = "Torob Intelligence Pro";
        Width = 1280; Height = 820;
        FlowDirection = FlowDirection.RightToLeft;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        FontFamily = new FontFamily("Segoe UI");

        var outerBorder = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = _obsidianBg,
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect { BlurRadius = 40, Color = Colors.Black, Opacity = 0.8, ShadowDepth = 0 }
        };

        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(45) });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var titleBar = new Border { Background = new SolidColorBrush(Color.FromRgb(8, 10, 14)) };
        titleBar.MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };

        var titleBarGrid = new Grid();
        titleBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(15, 0, 0, 0) };
        var liveDot = new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(Color.FromRgb(0, 255, 127)), Margin = new Thickness(0, 0, 12, 0) };
        var blinkAnim = new DoubleAnimation(0.3, 1, TimeSpan.FromSeconds(1)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
        liveDot.BeginAnimation(OpacityProperty, blinkAnim);

        titleStack.Children.Add(liveDot);
        titleStack.Children.Add(new TextBlock { Text = "TOROB INTELLIGENCE", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White });
        titleBarGrid.Children.Add(titleStack);

        var controlsStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 15, 0) };
        var btnMinimize = new Button { Content = "—", Width = 30, Height = 30, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.White, FontSize = 14, Cursor = Cursors.Hand };
        btnMinimize.Click += (s, e) => WindowState = WindowState.Minimized;
        var btnClose = new Button { Content = "✕", Width = 30, Height = 30, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.White, FontSize = 14, Cursor = Cursors.Hand };
        btnClose.Click += (s, e) => Close();

        controlsStack.Children.Add(btnMinimize);
        controlsStack.Children.Add(btnClose);
        titleBarGrid.Children.Add(controlsStack);
        Grid.SetColumn(controlsStack, 1);

        titleBar.Child = titleBarGrid;
        rootGrid.Children.Add(titleBar);

        var mainContent = new Grid { Margin = new Thickness(0, 1, 0, 0) };
        mainContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
        mainContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(mainContent, 1);

        var sidebar = new Border { Background = new SolidColorBrush(Color.FromArgb(120, 15, 18, 24)), BorderBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), BorderThickness = new Thickness(0, 0, 1, 0) };
        var sidebarStack = new StackPanel { Margin = new Thickness(20, 30, 20, 20) };

        var logoStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 40), HorizontalAlignment = HorizontalAlignment.Center };
        var logoIcon = new TextBlock { Text = "⚡", FontSize = 26, Foreground = _auroraCyan, Margin = new Thickness(0, 0, 10, 0), FontFamily = new FontFamily("Segoe UI Emoji") };
        var logoTitle = new TextBlock { FontSize = 18, FontWeight = FontWeights.Bold };
        logoTitle.Text = "دستیار قیمت";
        var gradientBrush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        gradientBrush.GradientStops.Add(new GradientStop(_auroraCyan.Color, 0));
        gradientBrush.GradientStops.Add(new GradientStop(_auroraPurple.Color, 1));
        logoTitle.Foreground = gradientBrush;

        logoStack.Children.Add(logoIcon);
        logoStack.Children.Add(logoTitle);
        sidebarStack.Children.Add(logoStack);

        sidebarStack.Children.Add(CreateAuroraButton("🔄 بروزرسانی لیست", RefreshAll_Click));
        sidebarStack.Children.Add(CreateAuroraButton("📥 وارد کردن لینک", ImportLinks_Click));
        sidebarStack.Children.Add(CreateAuroraButton("🌐 اسکن سایت‌های دیگر", ScanExternalSite_Click));
        sidebarStack.Children.Add(CreateAuroraButton("📤 خروجی اکسل", ExportLinks_Click));
        sidebarStack.Children.Add(CreateAuroraButton("⚙️ مدیریت دسته‌بندی‌ها", ManageCategories_Click));

        sidebar.Child = sidebarStack;
        mainContent.Children.Add(sidebar);

        var workspace = new Grid { Margin = new Thickness(25) };
        workspace.RowDefinitions.Add(new RowDefinition { Height = new GridLength(110) });
        workspace.RowDefinitions.Add(new RowDefinition { Height = new GridLength(50) });
        workspace.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
        workspace.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var statsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
        _txtTotalProducts = CreateAuroraStatCard(statsPanel, "محصولات ردیابی شده", "0");
        _txtCheapestPrice = CreateAuroraStatCard(statsPanel, "ارزان‌ترین قیمت", "0 تومان");
        _txtAvgPrice = CreateAuroraStatCard(statsPanel, "میانگین بازار", "0 تومان");
        workspace.Children.Add(statsPanel);

        var filterSortPanel = new Grid();
        filterSortPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        filterSortPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _categoryPillsPanel = new StackPanel { Orientation = Orientation.Horizontal };
        Grid.SetColumn(_categoryPillsPanel, 0);
        filterSortPanel.Children.Add(_categoryPillsPanel);

        _sortCombo = new ComboBox { Width = 150, Height = 32, Margin = new Thickness(0,0,10,0), FontSize = 12 };
        _sortCombo.Items.Add("مرتب‌سازی: پیش‌فرض");
        _sortCombo.Items.Add("ارزان‌ترین");
        _sortCombo.Items.Add("گران‌ترین");
        _sortCombo.Items.Add("جدیدترین");
        _sortCombo.SelectedIndex = 0;
        _sortCombo.SelectionChanged += (s, e) => LoadProducts();
        Grid.SetColumn(_sortCombo, 1);
        filterSortPanel.Children.Add(_sortCombo);

        Grid.SetRow(filterSortPanel, 1);
        workspace.Children.Add(filterSortPanel);

        // --- پنل وضعیت + دکمه توقف ---
        var statusGrid = new Grid();
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var statusPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _statusText = new TextBlock { Text = "سیستم آماده است.", Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 150)), FontSize = 12, Margin = new Thickness(5, 0, 0, 3), FontWeight = FontWeights.Light };
        _progressBar = new ProgressBar { Height = 3, Background = new SolidColorBrush(Color.FromRgb(30, 34, 42)), Foreground = _auroraCyan, BorderThickness = new Thickness(0), Visibility = Visibility.Collapsed };
        statusPanel.Children.Add(_statusText);
        statusPanel.Children.Add(_progressBar);
        statusGrid.Children.Add(statusPanel);
        Grid.SetColumn(statusPanel, 0);

        _stopBtn = new Button {
            Content = "⏹ توقف", Height = 26, Padding = new Thickness(14,0,14,0),
            Background = new SolidColorBrush(Color.FromRgb(200, 50, 45)), Foreground = Brushes.White,
            BorderThickness = new Thickness(0), FontSize = 12, FontWeight = FontWeights.Bold,
            Cursor = Cursors.Hand, Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10,0,0,0)
        };
        _stopBtn.Click += (s, e) => {
            try { _cts?.Cancel(); } catch { }
            _statusText.Text = "در حال توقف عملیات...";
        };
        statusGrid.Children.Add(_stopBtn);
        Grid.SetColumn(_stopBtn, 1);

        Grid.SetRow(statusGrid, 2);
        workspace.Children.Add(statusGrid);

        var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.Transparent, Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(0, 0, 10, 0) };
        _cardsContainer = new StackPanel();
        scrollViewer.Content = _cardsContainer;
        Grid.SetRow(scrollViewer, 3);
        workspace.Children.Add(scrollViewer);

        mainContent.Children.Add(workspace);
        Grid.SetColumn(workspace, 1);

        rootGrid.Children.Add(mainContent);
        outerBorder.Child = rootGrid;
        Content = outerBorder;
    }

    private void RenderCategoryPills()
    {
        _categoryPillsPanel.Children.Clear();
        var categories = _db.GetAllCategories();
        categories.Insert(0, "همه");

        foreach (var cat in categories)
        {
            var pill = new Button
            {
                Content = cat,
                Height = 32,
                Margin = new Thickness(0, 0, 10, 0),
                Padding = new Thickness(15, 0, 15, 0),
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Cursor = Cursors.Hand,
                Tag = cat
            };

            if (cat == _currentFilter)
            {
                pill.Background = _auroraCyan;
                pill.Foreground = Brushes.Black;
                pill.FontWeight = FontWeights.Bold;
                pill.Effect = new DropShadowEffect { BlurRadius = 10, Color = _auroraCyan.Color, Opacity = 0.5, ShadowDepth = 0 };
            }
            else
            {
                pill.Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
                pill.Foreground = new SolidColorBrush(Color.FromRgb(180, 190, 210));
                pill.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                pill.BorderThickness = new Thickness(1);
            }

            pill.Click += (s, e) => {
                _currentFilter = (string)((Button)s).Tag;
                LoadProducts();
            };
            _categoryPillsPanel.Children.Add(pill);
        }
    }

    private Button CreateAuroraButton(string text, RoutedEventHandler click)
    {
        var btn = new Button
        {
            Content = text,
            Height = 44,
            Margin = new Thickness(0, 0, 0, 12),
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            FontSize = 13,
            FontWeight = FontWeights.Medium,
            Cursor = Cursors.Hand
        };
        btn.MouseEnter += (s, e) => {
            btn.Background = new SolidColorBrush(Color.FromArgb(30, 0, 240, 255));
            btn.BorderBrush = _auroraCyan;
            btn.Effect = new DropShadowEffect { BlurRadius = 15, Color = _auroraCyan.Color, Opacity = 0.5, ShadowDepth = 0 };
        };
        btn.MouseLeave += (s, e) => {
            btn.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
            btn.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            btn.Effect = null;
        };
        btn.Click += click;
        return btn;
    }

    private TextBlock CreateAuroraStatCard(Panel parent, string title, string value)
    {
        var card = new Border
        {
            Background = _glassPanelBg,
            CornerRadius = new CornerRadius(10),
            Width = 200, Height = 100, Margin = new Thickness(0, 0, 15, 0),
            BorderBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect { BlurRadius = 20, Color = Colors.Black, Opacity = 0.3, ShadowDepth = 2 }
        };
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var titleTxt = new TextBlock { Text = title, Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 130)), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, FontWeight = FontWeights.Light };
        var valueTxt = new TextBlock { Text = value, FontSize = 21, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 8, 0, 0) };
        var valBrush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        valBrush.GradientStops.Add(new GradientStop(_auroraCyan.Color, 0));
        valBrush.GradientStops.Add(new GradientStop(_auroraPurple.Color, 1));
        valueTxt.Foreground = valBrush;
        grid.Children.Add(titleTxt);
        grid.Children.Add(valueTxt);
        Grid.SetRow(valueTxt, 1);
        card.Child = grid;
        parent.Children.Add(card);
        return valueTxt;
    }

    private void LoadProducts()
    {
        // ✅ رفع باگ ۲: همه محصولات نمایش داده می‌شوند — محصولات تازه‌وارد «شبح» نمی‌شوند
        _allProducts = new ObservableCollection<SavedProduct>(_db.GetAllProducts());

        if (_currentFilter == "همه") _filteredProducts = _allProducts.ToList();
        else _filteredProducts = _allProducts.Where(p => p.CategoryName == _currentFilter).ToList();

        if (_sortCombo != null && _sortCombo.SelectedIndex > 0)
        {
            switch (_sortCombo.SelectedIndex)
            {
                case 1: _filteredProducts = _filteredProducts.OrderBy(p => p.LastPrice).ToList(); break;
                case 2: _filteredProducts = _filteredProducts.OrderByDescending(p => p.LastPrice).ToList(); break;
                case 3: _filteredProducts = _filteredProducts.OrderByDescending(p => p.LastUpdate).ToList(); break;
            }
        }

        RenderCategoryPills();

        _cardsContainer.Children.Clear();
        foreach (var product in _filteredProducts) _cardsContainer.Children.Add(CreateAuroraProductCard(product));

        _txtTotalProducts.Text = _filteredProducts.Count.ToString();
        if (_filteredProducts.Any(p => p.LastPrice > 0))
        {
            _txtCheapestPrice.Text = $"{_filteredProducts.Where(p => p.LastPrice > 0).Min(p => p.LastPrice):N0} تومان";
            _txtAvgPrice.Text = $"{_filteredProducts.Where(p => p.LastPrice > 0).Average(p => p.LastPrice):N0} تومان";
        }
        else { _txtCheapestPrice.Text = "0 تومان"; _txtAvgPrice.Text = "0 تومان"; }
    }

    private Border CreateAuroraProductCard(SavedProduct product)
    {
        bool isAlertNew = _recentlyScannedNewUrls.Contains(product.TorobUrl);

        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(160, 18, 22, 28)),
            CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 0, 12), Padding = new Thickness(25, 15, 25, 15),
            BorderBrush = isAlertNew ? new SolidColorBrush(Color.FromRgb(0, 255, 127)) : new SolidColorBrush(Color.FromArgb(15, 255, 255, 255)),
            BorderThickness = isAlertNew ? new Thickness(2) : new Thickness(1),
            Effect = new DropShadowEffect {
                BlurRadius = isAlertNew ? 20 : 10,
                Color = isAlertNew ? Color.FromRgb(0, 255, 127) : Colors.Black,
                Opacity = isAlertNew ? 0.6 : 0.2,
                ShadowDepth = isAlertNew ? 0 : 1
            }
        };

        if (!isAlertNew) {
            card.MouseEnter += (s, e) => {
                card.RenderTransform = new TranslateTransform(0, 0);
                card.RenderTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-2, TimeSpan.FromMilliseconds(150)) { EasingFunction = new QuadraticEase() });
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(60, 0, 240, 255));
            };
            card.MouseLeave += (s, e) => {
                card.RenderTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(150)));
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255));
            };
        }

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });

        var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        infoStack.Children.Add(new TextBlock { Text = product.ProductName, Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.Medium, TextTrimming = TextTrimming.CharacterEllipsis });
        var storeStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        storeStack.Children.Add(new TextBlock { Text = "●", Foreground = _auroraPurple, FontSize = 10, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center });
        storeStack.Children.Add(new TextBlock { Text = $"{product.StoreName} | {product.CategoryName}", Foreground = new SolidColorBrush(Color.FromRgb(160, 170, 190)), FontSize = 12, FontWeight = FontWeights.Light });
        infoStack.Children.Add(storeStack);
        grid.Children.Add(infoStack);

        var priceStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        priceStack.Children.Add(new TextBlock { Text = $"{product.LastPrice:N0}", Foreground = Brushes.White, FontSize = 18, FontWeight = FontWeights.SemiBold });
        priceStack.Children.Add(new TextBlock { Text = "تومان", Foreground = new SolidColorBrush(Color.FromRgb(100, 110, 130)), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0) });
        grid.Children.Add(priceStack);
        Grid.SetColumn(priceStack, 1);

        var actionStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };

        string changeText;
        if (isAlertNew) changeText = "✅ محصول جدید";
        else if (product.LastPrice == 0) changeText = "در انتظار قیمت";
        else if (product.PreviousPrice == 0) changeText = "بروزرسانی شد";
        else if (product.LastPrice < product.PreviousPrice) changeText = $"▼ {product.PreviousPrice - product.LastPrice:N0} کاهش";
        else if (product.LastPrice > product.PreviousPrice) changeText = $"▲ {product.LastPrice - product.PreviousPrice:N0} افزایش";
        else changeText = "ثابت";

        var changeTxt = new TextBlock { Text = changeText, FontSize = 10, Margin = new Thickness(0, 0, 10, 0), FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(8, 4, 8, 4) };

        if (isAlertNew) { changeTxt.Foreground = Brushes.Black; changeTxt.Background = new SolidColorBrush(Color.FromRgb(0, 255, 127)); }
        else if (product.PreviousPrice > 0 && product.LastPrice < product.PreviousPrice) { changeTxt.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 127)); changeTxt.Background = new SolidColorBrush(Color.FromArgb(30, 0, 255, 127)); }
        else if (product.PreviousPrice > 0 && product.LastPrice > product.PreviousPrice) { changeTxt.Foreground = new SolidColorBrush(Color.FromRgb(255, 69, 58)); changeTxt.Background = new SolidColorBrush(Color.FromArgb(30, 255, 69, 58)); }
        else { changeTxt.Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 145)); changeTxt.Background = new SolidColorBrush(Color.FromArgb(20, 120, 130, 145)); }

        actionStack.Children.Add(changeTxt);

        var openBtn = new Button { Content = "↗", Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = 16, Cursor = Cursors.Hand, Margin = new Thickness(5), Foreground = new SolidColorBrush(Color.FromRgb(150, 160, 180)) };
        openBtn.MouseEnter += (s, e) => openBtn.Foreground = _auroraCyan;
        openBtn.MouseLeave += (s, e) => openBtn.Foreground = new SolidColorBrush(Color.FromRgb(150, 160, 180));
        openBtn.Click += (s, e) => {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(product.TorobUrl) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, $"بازکردن لینک ناموفق بود:\n{ex.Message}"); }
        };
        actionStack.Children.Add(openBtn);
        grid.Children.Add(actionStack);
        Grid.SetColumn(actionStack, 2);

        card.Child = grid;
        return card;
    }

    // ═══════════════ Event Handlers ═══════════════

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
        var progress = new Progress<(int current, int total, string status)>(p => {
            _statusText.Text = $"{p.status} ({p.current}/{p.total})"; _progressBar.Value = p.current;
        });

        try
        {
            await Task.Run(() => _scraper.RefreshProductsAsync(productsToUpdate, progress, _cts.Token));
            _statusText.Text = "بروزرسانی با موفقیت کامل شد.";
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
            LoadProducts();
        }
    }

    private void ImportLinks_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "فایل متنی|*.txt;*.csv" };
        if (dialog.ShowDialog() == true)
        {
            List<SavedProduct> imported;
            try { imported = _importService.ImportLinks(dialog.FileName); }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"خطا در خواندن فایل:\n{ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!imported.Any()) { MessageBox.Show(this, "هیچ لینک معتبری در فایل پیدا نشد."); return; }

            var settingsWin = new ImportSettingsWindow(_db);
            if (settingsWin.ShowDialog() == true)
            {
                var targetCat = settingsWin.SelectedCategory;
                foreach (var p in imported)
                {
                    p.CategoryName = targetCat;
                    try { _db.SaveProduct(p); }
                    catch (Exception ex) { Logger.Error("Import", p.TorobUrl, ex.Message); }
                }
                LoadProducts();
                MessageBox.Show(this, $"{imported.Count} محصول با موفقیت در دسته '{targetCat}' اضافه شد.");
            }
        }
    }

    private async void ScanExternalSite_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) { MessageBox.Show(this, "یک عملیات در حال اجراست. لطفاً منتظر بمانید یا آن را متوقف کنید."); return; }

        var scanWin = new ExternalScanWindow(_db.GetAllCategories(), _scraperFactory.GetRegisteredSites());
        if (scanWin.ShowDialog() == true)
        {
            _isBusy = true;
            _cts = new CancellationTokenSource();

            _progressBar.Visibility = Visibility.Visible;
            _progressBar.IsIndeterminate = true;
            _stopBtn.Visibility = Visibility.Visible;

            var scraper = _scraperFactory.GetScraper(scanWin.CategoryUrl);
            _statusText.Text = $"استفاده از {scraper.SiteName} - در حال اسکن...";

            var progress = new Progress<(int current, int total, string status)>(p => {
                _statusText.Text = $"{scraper.SiteName}: {p.status} ({p.current}/{p.total})";
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
                _statusText.Text = "اسکن لغو شد.";
                _progressBar.Visibility = Visibility.Collapsed;
                _progressBar.IsIndeterminate = false;
                _stopBtn.Visibility = Visibility.Collapsed;
                _cts.Dispose(); _cts = null;
                _isBusy = false;
                LoadProducts();
                return;
            }
            catch (Exception ex)
            {
                _statusText.Text = $"خطا در اسکن: {ex.Message}";
                _progressBar.Visibility = Visibility.Collapsed;
                _progressBar.IsIndeterminate = false;
                _stopBtn.Visibility = Visibility.Collapsed;
                _cts.Dispose(); _cts = null;
                _isBusy = false;
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

            _progressBar.Visibility = Visibility.Collapsed;
            _progressBar.IsIndeterminate = false;
            _stopBtn.Visibility = Visibility.Collapsed;
            _cts.Dispose(); _cts = null;
            _isBusy = false;

            _statusText.Text = $"اسکن کامل شد. {scannedProducts.Count} محصول بررسی شد.";
            LoadProducts();

            if (newProducts.Any())
            {
                var newNames = string.Join("\n", newProducts.Select(p => "• " + p.ProductName).Take(15));
                MessageBox.Show(this, $"اسکن کامل شد!\n{newProducts.Count} محصول جدید در سایت پیدا شد:\n\n{newNames}{(newProducts.Count > 15 ? "\n..." : "")}", "محصولات جدید", MessageBoxButton.OK, MessageBoxImage.Information);

                _ = Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(_ => {
                    Dispatcher.Invoke(() => {
                        _recentlyScannedNewUrls.Clear();
                        LoadProducts();
                    });
                });
            }
            else
            {
                MessageBox.Show(this, $"اسکن کامل شد.\n{scannedProducts.Count} محصول بررسی شد.\nهیچ محصول جدیدی در سایت پیدا نشد.", "اسکن سایت", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void ExportLinks_Click(object sender, RoutedEventArgs e)
    {
        if (!_filteredProducts.Any()) { MessageBox.Show(this, "محصولی برای خروجی گرفتن نیست."); return; }
        var dialog = new SaveFileDialog { Filter = "فایل اکسل|*.xlsx", FileName = $"گزارش_قیمت_{_currentFilter}.xlsx" };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                _importService.ExportToExcel(_filteredProducts.ToList(), dialog.FileName);
                MessageBox.Show(this, "فایل اکسل با موفقیت ساخته شد.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"خطا در ساخت فایل اکسل:\n{ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void ManageCategories_Click(object sender, RoutedEventArgs e)
    {
        var mgmtWindow = new ManagementWindow(_db, _scraper);
        mgmtWindow.ShowDialog();
        LoadProducts();
    }
}
