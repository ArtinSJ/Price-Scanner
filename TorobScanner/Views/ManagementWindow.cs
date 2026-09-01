using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;
using TorobScanner.Data;
using TorobScanner.Models;
using TorobScanner.Scrapers;
using TorobScanner.Services;
using Button = System.Windows.Controls.Button;

namespace TorobScanner.Views;

/// <summary>
/// پنجره مدیریت دسته‌بندی‌ها و محصولات — تم لوکس Platinum-Glass (v2.5)
/// ✅ رفع باگ ۴: دکمه حذف از DataContext ردیف خودش عمل می‌کند (نه SelectedItem)
/// ✅ دکمه توقف بروزرسانی + محافظ عملیات همزمان
/// ✅ حفظ انتخاب فیلتر دسته بعد از هر بارگذاری
/// ✨ پوسته شیشه‌ای resizable با WindowChrome (تغییر اندازه + حداکثر)
/// </summary>
public class ManagementWindow : Window
{
    private readonly DatabaseManager _db;
    private readonly TorobProductScraper _scraper;
    private DataGrid _dataGrid = null!;
    private ComboBox _catFilterCombo = null!;
    private ProgressBar _progressBar = null!;
    private TextBlock _statusText = null!;
    private ListBox _catListBox = null!;
    private List<SavedProduct> _allProducts = new();
    private Button _stopBtn = null!;
    private Button _maxBtn = null!;
    private Border _outerBorder = null!;
    private CancellationTokenSource? _cts;
    private bool _isBusy;

    public ManagementWindow(DatabaseManager db, TorobProductScraper scraper)
    {
        _db = db;
        _scraper = scraper;

        Title = "مدیریت دسته‌بندی‌ها و محصولات";
        Width = 1080; Height = 730;
        MinWidth = 940; MinHeight = 600;
        FlowDirection = FlowDirection.RightToLeft;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = Application.Current?.MainWindow;
        WindowStyle = WindowStyle.None; AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.CanResize;
        ThemeHelper.ApplyObsidianTheme(this);

        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 54,
            ResizeBorderThickness = new Thickness(10),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false
        });

        _outerBorder = new Border
        {
            CornerRadius = new CornerRadius(LuxUI.WinRadius),
            Background = LuxUI.WindowBg,
            BorderBrush = LuxUI.GlassStroke,
            BorderThickness = LuxUI.CardBorderThick,
            Margin = new Thickness(12),
            Effect = LuxUI.ShadowWindow
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(54) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        BuildTitleBar(root);
        BuildBody(root);

        _outerBorder.Child = root;
        Content = _outerBorder;

        StateChanged += (s, e) =>
        {
            bool max = WindowState == WindowState.Maximized;
            _outerBorder.CornerRadius = max ? new CornerRadius(0) : new CornerRadius(LuxUI.WinRadius);
            _outerBorder.Margin = max ? new Thickness(0) : new Thickness(12);
            _outerBorder.BorderThickness = max ? new Thickness(0) : LuxUI.CardBorderThick;
            _outerBorder.Effect = max ? null : LuxUI.ShadowWindow;
            if (_maxBtn != null) _maxBtn.Content = max ? "❐" : "▢";
        };

        Closed += (s, e) => { try { _cts?.Cancel(); } catch { } };

        LoadData();
    }

    private void BuildTitleBar(Grid root)
    {
        var titleBar = new Grid { Background = Brushes.Transparent };
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0) };
        var chip = new Border
        {
            Width = 30, Height = 30,
            Background = LuxUI.ChipFill,
            CornerRadius = new CornerRadius(LuxUI.ChipRadius),
            BorderBrush = LuxUI.GlassStroke, BorderThickness = LuxUI.ChipBorderThick,
            Child = new TextBlock
            {
                Text = "⚙", FontSize = 14, Foreground = LuxUI.Accent,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        };
        brand.Children.Add(chip);
        brand.Children.Add(new TextBlock
        {
            Text = "پنل مدیریت کامل", FontSize = 13.5, FontWeight = FontWeights.SemiBold,
            Foreground = LuxUI.TextPrimary, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(11, 0, 0, 0)
        });
        titleBar.Children.Add(brand);

        var winBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        var btnClose = new Button { Content = "✕", Style = (Style)Application.Current.Resources["LuxTitleCloseButton"], FontSize = 12 };
        btnClose.Click += (s, e) => Close();
        _maxBtn = new Button { Content = "▢", Style = (Style)Application.Current.Resources["LuxTitleButton"] };
        _maxBtn.Click += (s, e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        var btnMin = new Button { Content = "—", Style = (Style)Application.Current.Resources["LuxTitleButton"] };
        btnMin.Click += (s, e) => WindowState = WindowState.Minimized;

        winBtns.Children.Add(btnClose);
        winBtns.Children.Add(_maxBtn);
        winBtns.Children.Add(btnMin);

        WindowChrome.SetIsHitTestVisibleInChrome(btnClose, true);
        WindowChrome.SetIsHitTestVisibleInChrome(_maxBtn, true);
        WindowChrome.SetIsHitTestVisibleInChrome(btnMin, true);

        Grid.SetColumn(winBtns, 2);
        titleBar.Children.Add(winBtns);
        root.Children.Add(titleBar);
    }

    private void BuildBody(Grid root)
    {
        var body = new Grid { Margin = new Thickness(16, 4, 16, 16) };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(262) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(body, 1);

        // ═══════════ پنل چپ: دسته‌بندی‌ها + برنامه ═══════════
        var leftPanel = new Border
        {
            Background = LuxUI.SidebarFill,
            BorderBrush = LuxUI.GlassStroke, BorderThickness = LuxUI.CardBorderThick,
            CornerRadius = new CornerRadius(LuxUI.SideRadius),
            Margin = new Thickness(0, 0, 14, 0),
            Padding = new Thickness(16)
        };
        var leftStack = new StackPanel();

        leftStack.Children.Add(new TextBlock
        {
            Text = "دسته‌بندی‌ها", FontSize = 12.5, FontWeight = FontWeights.SemiBold,
            Foreground = LuxUI.TextSecondary, Margin = new Thickness(2, 0, 0, 10)
        });

        _catListBox = new ListBox { Height = 220 };
        leftStack.Children.Add(_catListBox);

        var addCatStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        var newCatTxt = new TextBox { MinHeight = 40, VerticalContentAlignment = VerticalAlignment.Center, Width = 148, FontSize = 12 };
        var addCatBtn = new Button
        {
            Content = "＋", Width = 40, Height = 40,
            Style = (Style)Application.Current.Resources["LuxBtnPrimary"],
            FontSize = 15, Padding = new Thickness(0), Margin = new Thickness(8, 0, 0, 0)
        };
        addCatStack.Children.Add(newCatTxt);
        addCatStack.Children.Add(addCatBtn);
        leftStack.Children.Add(addCatStack);

        var delCatBtn = LuxUI.DangerButton("حذف دسته انتخابی");
        delCatBtn.Height = 36;
        delCatBtn.Margin = new Thickness(0, 12, 0, 0);
        delCatBtn.HorizontalContentAlignment = HorizontalAlignment.Center;
        leftStack.Children.Add(delCatBtn);

        var delProdsBtn = new Button
        {
            Content = "حذف محصولات دسته",
            Style = (Style)Application.Current.Resources["LuxBtnGhost"],
            Height = 36, Margin = new Thickness(0, 8, 0, 0),
            Foreground = LuxUI.Warning
        };
        leftStack.Children.Add(delProdsBtn);

        // ═══ بروزرسانی برنامه ═══
        leftStack.Children.Add(new Separator { Margin = new Thickness(0, 18, 0, 12) });
        leftStack.Children.Add(new TextBlock
        {
            Text = $"نسخه برنامه: {UpdateService.CurrentVersion()}",
            Foreground = LuxUI.TextDim, FontSize = 10.5, Margin = new Thickness(2, 0, 0, 10)
        });
        var updateAppBtn = LuxUI.GhostButton("بررسی بروزرسانی برنامه");
        updateAppBtn.Height = 36;
        updateAppBtn.Foreground = LuxUI.Accent;
        updateAppBtn.ToolTip = "دانلود و نصب خودکار آخرین نسخه از گیت‌هاب";
        leftStack.Children.Add(updateAppBtn);

        leftPanel.Child = leftStack;
        Grid.SetColumn(leftPanel, 0);
        body.Children.Add(leftPanel);

        // ═══════════ پنل راست: ابزارها + جدول ═══════════
        var rightPanel = new Grid();
        rightPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rightPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        rightPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetColumn(rightPanel, 1);

        var actionsStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        var addLinkBtn = LuxUI.PrimaryButton("افزودن لینک");
        addLinkBtn.Height = 36;
        addLinkBtn.Padding = new Thickness(14, 0, 14, 0);
        addLinkBtn.FontSize = 12;
        addLinkBtn.Margin = new Thickness(0, 0, 8, 0);
        addLinkBtn.ToolTip = "افزودن لینک محصول از ترب یا سایت‌های دیگر";

        var updateAllBtn = LuxUI.SuccessButton("آپدیت کل لیست");
        updateAllBtn.Height = 36;
        updateAllBtn.Padding = new Thickness(14, 0, 14, 0);
        updateAllBtn.FontSize = 12;
        updateAllBtn.Margin = new Thickness(0, 0, 8, 0);

        _catFilterCombo = new ComboBox { Width = 150, Height = 36 };

        var updateCatBtn = new Button
        {
            Content = "آپدیت دسته انتخابی",
            Style = (Style)Application.Current.Resources["LuxBtnGhost"],
            Height = 36, FontSize = 12,
            Padding = new Thickness(14, 0, 14, 0),
            Foreground = LuxUI.Accent, Margin = new Thickness(8, 0, 0, 0)
        };

        actionsStack.Children.Add(addLinkBtn);
        actionsStack.Children.Add(updateAllBtn);
        actionsStack.Children.Add(_catFilterCombo);
        actionsStack.Children.Add(updateCatBtn);
        rightPanel.Children.Add(actionsStack);

        // ═══ دیتاگرید شیشه‌ای ═══
        _dataGrid = new DataGrid { IsReadOnly = false };

        _dataGrid.Columns.Add(new DataGridTextColumn { Header = "نام محصول", Binding = new System.Windows.Data.Binding("ProductName"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _dataGrid.Columns.Add(new DataGridTextColumn { Header = "لینک", Binding = new System.Windows.Data.Binding("TorobUrl"), Width = new DataGridLength(190), IsReadOnly = true });

        var catCol = new DataGridComboBoxColumn { Header = "دسته‌بندی", Width = 150 };
        catCol.ItemsSource = _db.GetAllCategories();
        catCol.SelectedItemBinding = new System.Windows.Data.Binding("CategoryName");
        _dataGrid.Columns.Add(catCol);

        _dataGrid.Columns.Add(new DataGridTextColumn { Header = "قیمت", Binding = new System.Windows.Data.Binding("LastPrice") { StringFormat = "{0:N0}" }, Width = 110, IsReadOnly = true });

        var delCol = new DataGridTemplateColumn { Header = "عملیات", Width = 74 };
        var delFactory = new FrameworkElementFactory(typeof(Button));
        delFactory.SetValue(Button.ContentProperty, "✕");
        delFactory.SetValue(Button.BackgroundProperty, (Brush)Application.Current.Resources["LuxDangerFill"]);
        delFactory.SetValue(Button.BorderThicknessProperty, new Thickness(0));
        delFactory.SetValue(Button.ForegroundProperty, (Brush)Application.Current.Resources["LuxDangerText"]);
        delFactory.SetValue(Button.FontSizeProperty, 11.5);
        delFactory.SetValue(Button.CursorProperty, Cursors.Hand);
        // ✅ رفع باگ ۴: DataContext ردیف خود دکمه — دیگر محصول اشتباهی حذف نمی‌شود
        delFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler((s, e) =>
        {
            var row = (e.OriginalSource as FrameworkElement)?.DataContext as SavedProduct
                      ?? (s as FrameworkElement)?.DataContext as SavedProduct;
            if (row != null)
            {
                if (MessageBox.Show(this, $"حذف «{row.ProductName}» ؟", "تایید حذف",
                        MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    _db.DeleteProduct(row.Id);
                    LoadData();
                }
            }
        }));
        delCol.CellTemplate = new DataTemplate { VisualTree = delFactory };
        _dataGrid.Columns.Add(delCol);

        _dataGrid.RowEditEnding += (s, e) =>
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                if (e.Row.Item is SavedProduct prod)
                {
                    try { _db.SaveProduct(prod); }
                    catch (Exception ex) { MessageBox.Show(this, $"خطا در ذخیره: {ex.Message}"); }
                }
            }
        };

        Grid.SetRow(_dataGrid, 1);
        rightPanel.Children.Add(_dataGrid);

        // ═══ نوار وضعیت ═══
        var statusGrid = new Grid { Margin = new Thickness(2, 12, 0, 0) };
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });

        _statusText = new TextBlock
        {
            Foreground = LuxUI.TextSecondary, FontSize = 11.5,
            VerticalAlignment = VerticalAlignment.Center
        };
        statusGrid.Children.Add(_statusText);

        _stopBtn = new Button
        {
            Content = "⏹ توقف",
            Style = (Style)Application.Current.Resources["LuxBtnStop"],
            Height = 30, Padding = new Thickness(14, 0, 14, 0),
            FontSize = 11.5, Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };
        _stopBtn.Click += (s, e) =>
        {
            try { _cts?.Cancel(); } catch { }
            _statusText.Text = "در حال توقف...";
        };
        statusGrid.Children.Add(_stopBtn);
        Grid.SetColumn(_stopBtn, 1);

        _progressBar = new ProgressBar
        {
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        statusGrid.Children.Add(_progressBar);
        Grid.SetColumn(_progressBar, 2);

        Grid.SetRow(statusGrid, 2);
        rightPanel.Children.Add(statusGrid);

        body.Children.Add(rightPanel);
        root.Children.Add(body);

        // ═══════════ رویدادها ═══════════
        addCatBtn.Click += (s, e) =>
        {
            var catName = newCatTxt.Text?.Trim() ?? "";
            if (catName.Length > 0) { _db.AddCategory(catName); newCatTxt.Text = ""; LoadData(); }
        };

        delCatBtn.Click += (s, e) =>
        {
            var selectedCat = _catListBox.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(selectedCat))
            {
                if (MessageBox.Show(this, $"آیا از حذف دسته '{selectedCat}' مطمئن هستید؟ محصولات این دسته به 'عمومی' منتقل می‌شوند.", "تایید حذف", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try { _db.DeleteCategory(selectedCat); } catch (Exception ex) { MessageBox.Show(this, $"خطا: {ex.Message}"); }
                    LoadData();
                }
            }
            else
            {
                MessageBox.Show(this, "لطفا ابتدا یک دسته را از لیست انتخاب کنید.");
            }
        };

        delProdsBtn.Click += (s, e) =>
        {
            var selectedCat = _catListBox.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(selectedCat))
            {
                if (MessageBox.Show(this, $"آیا از حذف تمام محصولات مربوط به دسته '{selectedCat}' مطمئن هستید؟ (دسته‌بندی باقی می‌ماند)", "تایید حذف محصولات", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    _db.DeleteProductsInCategory(selectedCat);
                    LoadData();
                }
            }
            else
            {
                MessageBox.Show(this, "لطفا ابتدا یک دسته را از لیست انتخاب کنید.");
            }
        };

        updateAppBtn.Click += (s, e) =>
        {
            if (_isBusy) { MessageBox.Show(this, "ابتدا عملیات جاری را متوقف کنید."); return; }
            var win = new UpdateWindow { Owner = this };
            win.ShowDialog();
        };

        addLinkBtn.Click += (s, e) =>
        {
            var dialog = new AddProductWindow(_db.GetAllCategories()) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                _db.SaveProduct(new SavedProduct { ProductName = dialog.ProductName, TorobUrl = dialog.ProductUrl, CategoryName = dialog.SelectedCategory });
                LoadData();
            }
        };

        updateAllBtn.Click += async (s, e) => await UpdateProducts(_allProducts);
        updateCatBtn.Click += async (s, e) =>
        {
            var catToUpdate = _catFilterCombo.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(catToUpdate)) return;
            await UpdateProducts(_allProducts.Where(p => p.CategoryName == catToUpdate).ToList());
        };
    }

    private async Task UpdateProducts(List<SavedProduct> products)
    {
        // ✅ رفع باگ ۹: محافظ عملیات همزمان
        if (_isBusy) { MessageBox.Show(this, "یک عملیات بروزرسانی در حال اجراست. لطفاً منتظر بمانید یا آن را متوقف کنید."); return; }
        if (!products.Any()) { MessageBox.Show(this, "محصولی برای بروزرسانی نیست."); return; }

        _isBusy = true;
        _cts = new CancellationTokenSource();
        _progressBar.Visibility = Visibility.Visible; _progressBar.Value = 0; _progressBar.Maximum = products.Count;
        _stopBtn.Visibility = Visibility.Visible;
        var progress = new Progress<(int current, int total, string status)>(p =>
        {
            _statusText.Text = LuxUI.Fa($"{p.status} ({p.current}/{p.total})");
            _progressBar.Value = p.current;
        });

        try
        {
            await Task.Run(() => _scraper.RefreshProductsAsync(products, progress, _cts.Token));
            _statusText.Text = "بروزرسانی کامل شد.";
        }
        catch (OperationCanceledException)
        {
            _statusText.Text = "بروزرسانی متوقف شد.";
        }
        catch (Exception ex)
        {
            _statusText.Text = $"خطا در بروزرسانی: {ex.Message}";
            MessageBox.Show(this, $"خطا در بروزرسانی:\n{ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _progressBar.Visibility = Visibility.Collapsed;
            _stopBtn.Visibility = Visibility.Collapsed;
            _cts.Dispose(); _cts = null;
            _isBusy = false;
            LoadData();
        }
    }

    private void LoadData()
    {
        _allProducts = _db.GetAllProducts();
        _dataGrid.ItemsSource = null;
        _dataGrid.ItemsSource = _allProducts;

        var cats = _db.GetAllCategories();
        // ✅ حفظ انتخاب کاربر بعد از بارگذاری مجدد
        var selectedCat = _catFilterCombo.SelectedItem?.ToString();
        _catFilterCombo.ItemsSource = cats;
        if (cats.Count > 0)
        {
            if (selectedCat != null && cats.Contains(selectedCat)) _catFilterCombo.SelectedItem = selectedCat;
            else _catFilterCombo.SelectedIndex = 0;
        }

        _catListBox.ItemsSource = null;
        _catListBox.ItemsSource = cats;
    }
}
