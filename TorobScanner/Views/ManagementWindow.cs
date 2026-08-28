using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using TorobScanner.Data;
using TorobScanner.Models;
using TorobScanner.Scrapers;

namespace TorobScanner.Views;

/// <summary>
/// پنجره مدیریت دسته‌بندی‌ها و محصولات:
/// ✅ رفع باگ ۴: دکمه حذف از DataContext ردیف خودش عمل می‌کند (نه SelectedItem)
/// ✅ دکمه توقف بروزرسانی + محافظ عملیات همزمان
/// ✅ حفظ انتخاب فیلتر دسته بعد از هر بارگذاری
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
    private CancellationTokenSource? _cts;
    private bool _isBusy;

    public ManagementWindow(DatabaseManager db, TorobProductScraper scraper)
    {
        _db = db;
        _scraper = scraper;

        Title = "مدیریت دسته‌بندی‌ها و محصولات";
        Width = 1000; Height = 700;
        FlowDirection = FlowDirection.RightToLeft;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = Application.Current?.MainWindow;
        Background = new SolidColorBrush(Color.FromRgb(14, 16, 20));
        FontFamily = new FontFamily("Segoe UI");
        ThemeHelper.ApplyObsidianTheme(this);

        var mainGrid = new Grid { Margin = new Thickness(20) };
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(50) });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });

        var header = new TextBlock { Text = "⚙️ پنل مدیریت کامل", FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
        mainGrid.Children.Add(header);
        Grid.SetColumnSpan(header, 2);

        var leftPanel = new StackPanel { Margin = new Thickness(0, 20, 15, 0) };
        leftPanel.Children.Add(new TextBlock { Text = "دسته‌بندی‌ها:", Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.Medium, Margin = new Thickness(0,0,0,10) });

        _catListBox = new ListBox { Background = new SolidColorBrush(Color.FromRgb(20, 24, 30)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Height = 200 };
        leftPanel.Children.Add(_catListBox);

        var addCatStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        var newCatTxt = new TextBox { Background = new SolidColorBrush(Color.FromRgb(20, 24, 30)), Foreground = Brushes.White, BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromArgb(60,255,255,255)), Height = 32, Width = 150 };
        var addCatBtn = new Button { Content = "➕", Width = 32, Height = 32, Background = new SolidColorBrush(Color.FromRgb(0, 240, 255)), Foreground = Brushes.Black, Margin = new Thickness(5, 0, 0, 0), Cursor = System.Windows.Input.Cursors.Hand };
        addCatStack.Children.Add(newCatTxt); addCatStack.Children.Add(addCatBtn);
        leftPanel.Children.Add(addCatStack);

        var delCatBtn = new Button { Content = "🗑️ حذف دسته انتخابی", Height = 32, Margin = new Thickness(0, 10, 0, 0), Background = new SolidColorBrush(Color.FromArgb(30, 255, 69, 58)), Foreground = Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        leftPanel.Children.Add(delCatBtn);

        var delProdsBtn = new Button { Content = "⚠️ حذف محصولات دسته", Height = 32, Margin = new Thickness(0, 10, 0, 0), Background = new SolidColorBrush(Color.FromArgb(30, 255, 165, 0)), Foreground = Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
        leftPanel.Children.Add(delProdsBtn);

        mainGrid.Children.Add(leftPanel);
        Grid.SetRow(leftPanel, 1);

        var rightPanel = new StackPanel { Margin = new Thickness(15, 20, 0, 0) };

        var actionsStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,10) };
        var addLinkBtn = new Button { Content = "➕ افزودن لینک", Height = 32, Padding = new Thickness(10,0,10,0), Background = new SolidColorBrush(Color.FromArgb(30, 0, 240, 255)), Foreground = Brushes.White, Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(0,0,10,0) };
        var updateAllBtn = new Button { Content = "🔄 آپدیت کل لیست", Height = 32, Padding = new Thickness(10,0,10,0), Background = new SolidColorBrush(Color.FromArgb(30, 0, 255, 127)), Foreground = Brushes.White, Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(0,0,10,0) };

        _catFilterCombo = new ComboBox { Width = 150, Height = 32 };
        var updateCatBtn = new Button { Content = "🔄 آپدیت دسته انتخابی", Height = 32, Padding = new Thickness(10,0,10,0), Background = new SolidColorBrush(Color.FromArgb(30, 123, 97, 255)), Foreground = Brushes.White, Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(10,0,0,0) };

        actionsStack.Children.Add(addLinkBtn);
        actionsStack.Children.Add(updateAllBtn);
        actionsStack.Children.Add(_catFilterCombo);
        actionsStack.Children.Add(updateCatBtn);
        rightPanel.Children.Add(actionsStack);

        _dataGrid = new DataGrid {
            Background = new SolidColorBrush(Color.FromRgb(18, 22, 28)),
            Foreground = Brushes.White,
            RowBackground = new SolidColorBrush(Color.FromRgb(20, 24, 30)),
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(25, 29, 35)),
            BorderThickness = new Thickness(0), GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
            HeadersVisibility = DataGridHeadersVisibility.Column,
            AutoGenerateColumns = false, IsReadOnly = false, CanUserAddRows = false
        };

        var headerStyle = new Style(typeof(DataGridColumnHeader));
        headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, new SolidColorBrush(Color.FromRgb(15, 18, 24))));
        headerStyle.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, Brushes.White));
        headerStyle.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.SemiBold));
        headerStyle.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(10)));
        headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(40, 255, 255, 255))));
        headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderThicknessProperty, new Thickness(0,0,1,1)));
        _dataGrid.ColumnHeaderStyle = headerStyle;

        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(DataGridCell.BackgroundProperty, Brushes.Transparent));
        cellStyle.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, Brushes.Transparent));
        cellStyle.Setters.Add(new Setter(DataGridCell.PaddingProperty, new Thickness(10)));
        cellStyle.Setters.Add(new Setter(DataGridCell.ForegroundProperty, Brushes.White));
        _dataGrid.CellStyle = cellStyle;

        _dataGrid.Columns.Add(new DataGridTextColumn { Header = "نام محصول", Binding = new System.Windows.Data.Binding("ProductName"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _dataGrid.Columns.Add(new DataGridTextColumn { Header = "لینک", Binding = new System.Windows.Data.Binding("TorobUrl"), Width = new DataGridLength(200), IsReadOnly = true });

        var catCol = new DataGridComboBoxColumn { Header = "دسته‌بندی", Width = 150 };
        catCol.ItemsSource = _db.GetAllCategories();
        catCol.SelectedItemBinding = new System.Windows.Data.Binding("CategoryName");
        _dataGrid.Columns.Add(catCol);

        _dataGrid.Columns.Add(new DataGridTextColumn { Header = "قیمت", Binding = new System.Windows.Data.Binding("LastPrice") { StringFormat = "{0:N0}" }, Width = 100, IsReadOnly = true });

        var delCol = new DataGridTemplateColumn { Header = "عملیات", Width = 80 };
        var delFactory = new FrameworkElementFactory(typeof(Button));
        delFactory.SetValue(Button.ContentProperty, "🗑️");
        delFactory.SetValue(Button.BackgroundProperty, Brushes.Transparent);
        delFactory.SetValue(Button.BorderThicknessProperty, new Thickness(0));
        delFactory.SetValue(Button.CursorProperty, System.Windows.Input.Cursors.Hand);
        // ✅ رفع باگ ۴: DataContext ردیف خود دکمه — دیگر محصول اشتباهی حذف نمی‌شود
        delFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler((s, e) => {
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

        _dataGrid.RowEditEnding += (s, e) => {
            if (e.EditAction == DataGridEditAction.Commit) {
                if (e.Row.Item is SavedProduct prod)
                {
                    try { _db.SaveProduct(prod); }
                    catch (Exception ex) { MessageBox.Show(this, $"خطا در ذخیره: {ex.Message}"); }
                }
            }
        };

        rightPanel.Children.Add(_dataGrid);
        mainGrid.Children.Add(rightPanel);
        Grid.SetRow(rightPanel, 1); Grid.SetColumn(rightPanel, 1);

        _statusText = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 150)), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        _progressBar = new ProgressBar { Height = 3, Background = new SolidColorBrush(Color.FromRgb(30, 34, 42)), Foreground = new SolidColorBrush(Color.FromRgb(0, 240, 255)), BorderThickness = new Thickness(0), Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center };

        _stopBtn = new Button {
            Content = "⏹ توقف", Height = 28, Padding = new Thickness(12,0,12,0),
            Background = new SolidColorBrush(Color.FromRgb(200, 50, 45)), Foreground = Brushes.White,
            BorderThickness = new Thickness(0), FontSize = 12, FontWeight = FontWeights.Bold,
            Cursor = System.Windows.Input.Cursors.Hand, Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8,0,0,0)
        };
        _stopBtn.Click += (s, e) => {
            try { _cts?.Cancel(); } catch { }
            _statusText.Text = "در حال توقف...";
        };

        var statusGrid = new Grid();
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        statusGrid.Children.Add(_statusText);
        statusGrid.Children.Add(_stopBtn); Grid.SetColumn(_stopBtn, 1);
        statusGrid.Children.Add(_progressBar); Grid.SetColumn(_progressBar, 2);
        mainGrid.Children.Add(statusGrid); Grid.SetRow(statusGrid, 2); Grid.SetColumnSpan(statusGrid, 2);

        Content = mainGrid;

        addCatBtn.Click += (s, e) => { if(!string.IsNullOrWhiteSpace(newCatTxt.Text)) { _db.AddCategory(newCatTxt.Text.Trim()); LoadData(); } };

        delCatBtn.Click += (s, e) => {
            var selectedCat = _catListBox.SelectedItem?.ToString();
            if(!string.IsNullOrEmpty(selectedCat)) {
                if(MessageBox.Show(this, $"آیا از حذف دسته '{selectedCat}' مطمئن هستید؟ محصولات این دسته به 'عمومی' منتقل می‌شوند.", "تایید حذف", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) {
                    try { _db.DeleteCategory(selectedCat); } catch (Exception ex) { MessageBox.Show(this, $"خطا: {ex.Message}"); }
                    LoadData();
                }
            } else {
                MessageBox.Show(this, "لطفا ابتدا یک دسته را از لیست انتخاب کنید.");
            }
        };

        delProdsBtn.Click += (s, e) => {
            var selectedCat = _catListBox.SelectedItem?.ToString();
            if(!string.IsNullOrEmpty(selectedCat)) {
                if(MessageBox.Show(this, $"آیا از حذف تمام محصولات مربوط به دسته '{selectedCat}' مطمئن هستید؟ (دسته‌بندی باقی می‌ماند)", "تایید حذف محصولات", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) {
                    _db.DeleteProductsInCategory(selectedCat); LoadData();
                }
            } else {
                MessageBox.Show(this, "لطفا ابتدا یک دسته را از لیست انتخاب کنید.");
            }
        };

        addLinkBtn.Click += (s, e) => {
            var dialog = new AddProductWindow(_db.GetAllCategories());
            if (dialog.ShowDialog() == true) {
                _db.SaveProduct(new SavedProduct { ProductName = dialog.ProductName, TorobUrl = dialog.ProductUrl, CategoryName = dialog.SelectedCategory });
                LoadData();
            }
        };
        updateAllBtn.Click += async (s, e) => await UpdateProducts(_allProducts);
        updateCatBtn.Click += async (s, e) => {
            var catToUpdate = _catFilterCombo.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(catToUpdate)) return;
            await UpdateProducts(_allProducts.Where(p => p.CategoryName == catToUpdate).ToList());
        };

        Closed += (s, e) => { try { _cts?.Cancel(); } catch { } };

        LoadData();
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
        var progress = new Progress<(int current, int total, string status)>(p => {
            _statusText.Text = $"{p.status} ({p.current}/{p.total})"; _progressBar.Value = p.current;
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
