using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TorobScanner.Services;

namespace TorobScanner.Views;

/// <summary>
/// پنجره بروزرسانی خودکار برنامه از GitHub Releases:
/// بررسی نسخه → نمایش تغییرات → دانلود با نوار پیشرفت → نصب و راه‌اندازی مجدد
/// </summary>
public class UpdateWindow : Window
{
    private readonly UpdateService _updateService = new();
    private UpdateInfo? _updateInfo;

    private TextBlock _statusText = null!;
    private TextBlock _versionText = null!;
    private TextBox _notesBox = null!;
    private ProgressBar _progressBar = null!;
    private Button _actionBtn = null!;
    private bool _busy;

    private readonly SolidColorBrush _auroraCyan = new(Color.FromRgb(0, 240, 255));
    private readonly SolidColorBrush _dimText = new(Color.FromRgb(120, 130, 150));

    public UpdateWindow()
    {
        Title = "بروزرسانی برنامه";
        Width = 480;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 620;
        FlowDirection = FlowDirection.RightToLeft;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = Application.Current?.MainWindow;
        WindowStyle = WindowStyle.None; AllowsTransparency = true;
        ResizeMode = ResizeMode.NoResize;
        FontFamily = new FontFamily("Segoe UI"); Background = Brushes.Transparent;
        ThemeHelper.ApplyObsidianTheme(this);

        var outerBorder = new Border {
            CornerRadius = new CornerRadius(10), Background = new SolidColorBrush(Color.FromRgb(12, 14, 18)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 0, 240, 255)), BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 30, Color = Colors.Black, Opacity = 0.6, ShadowDepth = 0 }
        };

        var stack = new StackPanel { Margin = new Thickness(25) };

        // ═══ سربرگ + دکمه بستن ═══
        var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.Children.Add(new TextBlock { Text = "⬇️ بروزرسانی برنامه", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });

        var closeBtn = new Button { Content = "✕", Width = 30, Height = 30, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = new SolidColorBrush(Color.FromRgb(150, 160, 180)), FontSize = 14, Cursor = Cursors.Hand };
        closeBtn.MouseEnter += (s, e) => closeBtn.Foreground = new SolidColorBrush(Color.FromRgb(255, 69, 58));
        closeBtn.MouseLeave += (s, e) => closeBtn.Foreground = new SolidColorBrush(Color.FromRgb(150, 160, 180));
        closeBtn.Click += (s, e) => { if (!_busy) Close(); };
        headerGrid.Children.Add(closeBtn);
        Grid.SetColumn(closeBtn, 1);
        stack.Children.Add(headerGrid);

        // ═══ نسخه فعلی / جدید ═══
        _versionText = new TextBlock {
            Text = $"نسخه فعلی: {UpdateService.CurrentVersion()}",
            Foreground = _dimText, FontSize = 12, Margin = new Thickness(0, 0, 0, 12)
        };
        stack.Children.Add(_versionText);

        // ═══ وضعیت ═══
        _statusText = new TextBlock {
            Text = "در حال بررسی آخرین نسخه از گیت‌هاب...",
            Foreground = _auroraCyan, FontSize = 13, Margin = new Thickness(0, 0, 0, 12),
            TextWrapping = TextWrapping.Wrap
        };
        stack.Children.Add(_statusText);

        // ═══ یادداشت‌های انتشار ═══
        _notesBox = new TextBox {
            Background = new SolidColorBrush(Color.FromRgb(18, 22, 28)),
            Foreground = new SolidColorBrush(Color.FromRgb(180, 190, 210)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            BorderThickness = new Thickness(1), Padding = new Thickness(10),
            FontSize = 11, Height = 140, IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 0, 0, 12)
        };
        stack.Children.Add(_notesBox);

        // ═══ نوار پیشرفت دانلود ═══
        _progressBar = new ProgressBar {
            Height = 6, Minimum = 0, Maximum = 100,
            Background = new SolidColorBrush(Color.FromRgb(30, 34, 42)),
            Foreground = _auroraCyan, BorderThickness = new Thickness(0),
            Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 12)
        };
        stack.Children.Add(_progressBar);

        // ═══ دکمه اصلی ═══
        _actionBtn = new Button {
            Content = "🔄 بررسی مجدد", Height = 44,
            Background = new SolidColorBrush(Color.FromRgb(123, 97, 255)),
            Foreground = Brushes.White, BorderThickness = new Thickness(0),
            FontSize = 13, FontWeight = FontWeights.Bold, Cursor = Cursors.Hand,
            IsEnabled = false
        };
        _actionBtn.Click += ActionBtn_Click;
        stack.Children.Add(_actionBtn);

        outerBorder.Child = stack;
        Content = outerBorder;

        KeyDown += (s, e) => { if (e.Key == Key.Escape && !_busy) Close(); };
        Loaded += async (s, e) => await CheckAsync();
    }

    private async Task CheckAsync()
    {
        _busy = true;
        _actionBtn.IsEnabled = false;
        _statusText.Text = "در حال بررسی آخرین نسخه از گیت‌هاب...";
        _statusText.Foreground = _auroraCyan;

        try
        {
            _updateInfo = await _updateService.CheckForUpdateAsync();

            if (_updateInfo.UpdateAvailable)
            {
                _versionText.Text = $"نسخه فعلی: {_updateInfo.CurrentVersion}   ⟵   نسخه جدید: {_updateInfo.LatestVersion}";
                _statusText.Text = $"🎉 نسخه جدید {_updateInfo.LatestVersion} در دسترس است!" +
                    (_updateInfo.AssetSize > 0 ? $" (حجم: {_updateInfo.AssetSize / 1024.0 / 1024.0:F1} MB)" : "");
                _statusText.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 127));

                if (!string.IsNullOrWhiteSpace(_updateInfo.ReleaseNotes))
                {
                    _notesBox.Text = _updateInfo.ReleaseNotes;
                    _notesBox.Visibility = Visibility.Visible;
                }

                _actionBtn.Content = "⬇️ دانلود و نصب بروزرسانی";
                _actionBtn.Background = new SolidColorBrush(Color.FromRgb(0, 240, 255));
                _actionBtn.Foreground = Brushes.Black;
            }
            else
            {
                _statusText.Text = string.IsNullOrEmpty(_updateInfo.DownloadUrl) && _updateInfo.LatestVersion == _updateInfo.CurrentVersion
                    ? "✅ شما آخرین نسخه را دارید."
                    : $"✅ شما آخرین نسخه را دارید. ({_updateInfo.ReleaseNotes})".TrimEnd('(', ')', ' ', '.') + ".";
                _statusText.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 127));
                _actionBtn.Content = "🔄 بررسی مجدد";
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Update/Check", "", ex.ToString());
            _statusText.Text = $"❌ خطا در بررسی بروزرسانی:\n{ex.Message}\n(اتصال اینترنت/فیلترشکن را بررسی کنید)";
            _statusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 69, 58));
            _actionBtn.Content = "🔄 تلاش مجدد";
        }
        finally
        {
            _busy = false;
            _actionBtn.IsEnabled = true;
        }
    }

    private async void ActionBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;

        // اگر آپدیتی موجود نیست → بررسی مجدد
        if (_updateInfo == null || !_updateInfo.UpdateAvailable)
        {
            await CheckAsync();
            return;
        }

        // ═══ دانلود و نصب ═══
        if (MessageBox.Show(this,
                $"نسخه {_updateInfo.LatestVersion} دانلود و نصب می‌شود.\nبرنامه بعد از دانلود بسته و دوباره باز خواهد شد.\n(دیتابیس و اطلاعات شما دست نمی‌خورد)\n\nادامه می‌دهید؟",
                "تایید بروزرسانی", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _busy = true;
        _actionBtn.IsEnabled = false;
        _progressBar.Visibility = Visibility.Visible;
        _progressBar.Value = 0;

        var progress = new Progress<int>(p => {
            _progressBar.Value = p;
            _statusText.Text = $"در حال دانلود... {p}%";
        });

        try
        {
            var payloadDir = await _updateService.DownloadUpdateAsync(_updateInfo, progress);
            _statusText.Text = "✅ دانلود کامل شد. در حال نصب و راه‌اندازی مجدد...";
            await Task.Delay(800);
            _updateService.ApplyUpdateAndRestart(payloadDir); // برنامه بسته می‌شود
        }
        catch (Exception ex)
        {
            Logger.Error("Update/Download", _updateInfo.DownloadUrl, ex.ToString());
            _statusText.Text = $"❌ خطا در دانلود/نصب:\n{ex.Message}";
            _statusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 69, 58));
            _progressBar.Visibility = Visibility.Collapsed;
            _busy = false;
            _actionBtn.IsEnabled = true;
        }
    }
}
