using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TorobScanner.Services;

namespace TorobScanner.Views;

/// <summary>
/// پنجره بروزرسانی خودکار برنامه از GitHub Releases — تم لوکس Platinum-Glass (v2.5):
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
    private bool _downloadFailed;   // ✨ v3.2.2: پس از شکست دانلود، دکمه به لینک دستی تبدیل می‌شود

    /// <summary>
    /// ✨ v2.7: حالت خودکار — بعد از بررسی، اگر نسخه جدید بود بدون پرسش
    /// مستقیم دانلود و نصب می‌شود (برای «نصب خودکار بروزرسانی» در تنظیمات).
    /// </summary>
    public bool AutoMode { get; init; }

    public UpdateWindow()
    {
        Title = "بروزرسانی برنامه";
        Width = 500;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 640;
        FlowDirection = FlowDirection.RightToLeft;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = Application.Current?.MainWindow;
        WindowStyle = WindowStyle.None; AllowsTransparency = true;
        ResizeMode = ResizeMode.NoResize;
        Background = Brushes.Transparent;
        ThemeHelper.ApplyObsidianTheme(this);

        var content = LuxUI.BuildDialogShell(this, "⟳",
            "بروزرسانی برنامه",
            (Brush)Application.Current!.Resources["LuxFocusStroke"],
            out _, out var outerBorder);

        // ═══ نسخه فعلی / جدید ═══
        _versionText = new TextBlock
        {
            Text = $"نسخه فعلی: {UpdateService.CurrentVersion()}",
            Foreground = LuxUI.TextDim, FontSize = 12, Margin = new Thickness(0, 0, 0, 12)
        };
        content.Children.Add(_versionText);

        // ═══ وضعیت ═══
        _statusText = new TextBlock
        {
            Text = "در حال بررسی آخرین نسخه از گیت‌هاب...",
            Foreground = LuxUI.Accent, FontSize = 13, Margin = new Thickness(0, 0, 0, 12),
            TextWrapping = TextWrapping.Wrap
        };
        content.Children.Add(_statusText);

        // ═══ یادداشت‌های انتشار ═══
        _notesBox = new TextBox
        {
            Height = 140, IsReadOnly = true,
            Foreground = LuxUI.TextSecondary,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 0, 0, 12)
        };
        content.Children.Add(_notesBox);

        // ═══ نوار پیشرفت دانلود ═══
        _progressBar = new ProgressBar
        {
            Height = 6, Minimum = 0, Maximum = 100,
            Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 12)
        };
        content.Children.Add(_progressBar);

        // ═══ دکمه اصلی ═══
        _actionBtn = LuxUI.PrimaryButton("بررسی مجدد");
        _actionBtn.IsEnabled = false;
        _actionBtn.Click += ActionBtn_Click;
        content.Children.Add(_actionBtn);

        Content = outerBorder;

        KeyDown += (s, e) => { if (e.Key == Key.Escape && !_busy) Close(); };
        Loaded += async (s, e) =>
        {
            await CheckAsync();
            // ✨ v2.7: در حالت خودکار، بعد از پیدا شدن نسخه بدون پرسش نصب می‌شود
            if (AutoMode && _updateInfo is { UpdateAvailable: true } && !_busy)
                await DownloadAndInstallAsync(confirm: false);
        };
    }

    private async Task CheckAsync()
    {
        _busy = true;
        _actionBtn.IsEnabled = false;
        _statusText.Text = "در حال بررسی آخرین نسخه از گیت‌هاب...";
        _statusText.Foreground = LuxUI.Accent;

        try
        {
            _updateInfo = await _updateService.CheckForUpdateAsync();

            if (_updateInfo.UpdateAvailable)
            {
                _versionText.Text = $"نسخه فعلی: {_updateInfo.CurrentVersion}   ⟵   نسخه جدید: {_updateInfo.LatestVersion}";
                _statusText.Text = $"نسخه جدید {_updateInfo.LatestVersion} در دسترس است!" +
                    (_updateInfo.AssetSize > 0 ? $" (حجم: {_updateInfo.AssetSize / 1024.0 / 1024.0:F1} MB)" : "");
                _statusText.Foreground = LuxUI.Success;

                if (!string.IsNullOrWhiteSpace(_updateInfo.ReleaseNotes))
                {
                    _notesBox.Text = _updateInfo.ReleaseNotes;
                    _notesBox.Visibility = Visibility.Visible;
                }

                _actionBtn.Content = "دانلود و نصب بروزرسانی";
            }
            else
            {
                _statusText.Text = string.IsNullOrEmpty(_updateInfo.DownloadUrl) && _updateInfo.LatestVersion == _updateInfo.CurrentVersion
                    ? "شما آخرین نسخه را دارید."
                    : $"شما آخرین نسخه را دارید. ({_updateInfo.ReleaseNotes})".TrimEnd('(', ')', ' ', '.') + ".";
                _statusText.Foreground = LuxUI.Success;
                _actionBtn.Content = "بررسی مجدد";
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Update/Check", "", ex.ToString());
            _statusText.Text = $"خطا در بررسی بروزرسانی:\n{ex.Message}\n(اتصال اینترنت/فیلترشکن را بررسی کنید)";
            _statusText.Foreground = LuxUI.Danger;
            _actionBtn.Content = "تلاش مجدد";
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

        // ✨ v3.2.2: بعد از شکست دانلود (مثلاً فیلترینگ CDN گیت‌هاب) → باز کردن صفحه‌ی دانلود دستی
        if (_downloadFailed)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/ArtinSJ/Price-Scanner/releases/latest",
                    UseShellExecute = true
                });
            }
            catch { }
            return;
        }

        // اگر آپدیتی موجود نیست → بررسی مجدد
        if (_updateInfo == null || !_updateInfo.UpdateAvailable)
        {
            await CheckAsync();
            return;
        }

        await DownloadAndInstallAsync(confirm: true);
    }

    /// <summary>✨ v2.7: دانلود + نصب — در حالت خودکار بدون MessageBox تایید</summary>
    private async Task DownloadAndInstallAsync(bool confirm)
    {
        if (_updateInfo == null || !_updateInfo.UpdateAvailable) return;

        // ═══ دانلود و نصب ═══
        if (confirm && MessageBox.Show(this,
                $"نسخه {_updateInfo.LatestVersion} دانلود و نصب می‌شود.\nبرنامه بعد از دانلود بسته و دوباره باز خواهد شد.\n(دیتابیس و اطلاعات شما دست نمی‌خورد)\n\nادامه می‌دهید؟",
                "تایید بروزرسانی", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _busy = true;
        _actionBtn.IsEnabled = false;
        _progressBar.Visibility = Visibility.Visible;
        _progressBar.Value = 0;

        var progress = new Progress<int>(p =>
        {
            _progressBar.Value = p;
            _statusText.Text = $"در حال دانلود... {p}٪";
        });

        try
        {
            var payloadDir = await _updateService.DownloadUpdateAsync(_updateInfo, progress);
            _statusText.Text = "دانلود کامل شد. در حال نصب و راه‌اندازی مجدد...";
            await Task.Delay(800);
            _updateService.ApplyUpdateAndRestart(payloadDir); // برنامه بسته می‌شود
        }
        catch (Exception ex)
        {
            Logger.Error("Update/Download", _updateInfo.DownloadUrl, ex.ToString());
            _statusText.Text = $"خطا در دانلود/نصب:\n{ex.Message}\n(می‌توانید بسته را از صفحه‌ی انتشار دستی دانلود کنید)";
            _statusText.Foreground = LuxUI.Danger;
            _progressBar.Visibility = Visibility.Collapsed;
            _busy = false;
            _downloadFailed = true;
            _actionBtn.Content = "باز کردن صفحه‌ی دانلود دستی";
            _actionBtn.IsEnabled = true;
        }
    }
}
