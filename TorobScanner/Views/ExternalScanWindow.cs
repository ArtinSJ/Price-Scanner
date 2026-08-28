using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TorobScanner.Views;

/// <summary>
/// پنجره اسکن سایت‌های خارجی (External Scan)
///
/// ✅ رفع باگ «دکمه اسکن دیده نمی‌شود»:
///    با زیاد شدن سایت‌های رجیستری (۷ سایت)، جعبه اطلاعات ۸ خط شد و دکمه
///    ۶۸ پیکسل از پنجره ثابت ۴۸۰px بیرون می‌زد (و چون WindowStyle=None است، بریده می‌شد).
///    راه‌حل: SizeToContent.Height + MaxHeight + دکمه dock شده در ردیف پایین +
///    لیست سایت‌ها به‌صورت pill های فشرده در WrapPanel
/// </summary>
public class ExternalScanWindow : Window
{
    public string CategoryUrl { get; private set; } = "";
    public string TargetCategory { get; private set; } = "عمومی";
    public string StoreName { get; private set; } = "سایت خارجی";

    private readonly SolidColorBrush _auroraCyan = new(Color.FromRgb(0, 240, 255));

    private TextBox _storeBox = null!;
    private TextBox _urlBox = null!;
    private ComboBox _catCombo = null!;

    public ExternalScanWindow(List<string> categories, List<string> registeredSites)
    {
        Title = "اسکن سایت خارجی";
        Width = 520;
        // ✅ ارتفاع پنجره = ارتفاع محتوا — دیگر هیچ دکمه‌ای بریده نمی‌شود
        SizeToContent = SizeToContent.Height;
        MaxHeight = 680;
        FlowDirection = FlowDirection.RightToLeft;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = Application.Current?.MainWindow;
        WindowStyle = WindowStyle.None; AllowsTransparency = true;
        ResizeMode = ResizeMode.NoResize;
        FontFamily = new FontFamily("Segoe UI"); Background = Brushes.Transparent;
        ThemeHelper.ApplyObsidianTheme(this);

        var outerBorder = new Border {
            CornerRadius = new CornerRadius(10), Background = new SolidColorBrush(Color.FromRgb(12, 14, 18)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 123, 97, 255)), BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 30, Color = Colors.Black, Opacity = 0.6, ShadowDepth = 0 }
        };

        // ═══ ریشه: ردیف محتوا (اسکرول‌پذیر در حالت حدی) + ردیف دکمه (همیشه دیده می‌شود) ═══
        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var stack = new StackPanel { Margin = new Thickness(25, 25, 25, 12) };

        stack.Children.Add(new TextBlock { Text = "🌐 اسکن سایت‌های دیگر", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 15) });

        // ═══ لیست سایت‌ها: pill های فشرده در WrapPanel (به‌جای هر سایت یک خط) ═══
        var supportedInfo = new Border {
            Background = new SolidColorBrush(Color.FromArgb(25, 0, 240, 255)),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 18)
        };
        var infoStack = new StackPanel();
        infoStack.Children.Add(new TextBlock {
            Text = "✨ موتور اسکن تطبیقی فعال — سایت‌های بهینه‌شده:",
            Foreground = _auroraCyan, FontSize = 11, Margin = new Thickness(0, 0, 0, 8)
        });
        var sitesWrap = new WrapPanel();
        foreach (var site in registeredSites)
        {
            var pill = new Border {
                Background = new SolidColorBrush(Color.FromArgb(30, 0, 240, 255)),
                CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 6, 6)
            };
            pill.Child = new TextBlock {
                Text = site.Replace("✅ ", "").Replace("🌐 ", ""),
                Foreground = new SolidColorBrush(Color.FromRgb(120, 220, 240)), FontSize = 10
            };
            sitesWrap.Children.Add(pill);
        }
        infoStack.Children.Add(sitesWrap);
        supportedInfo.Child = infoStack;
        stack.Children.Add(supportedInfo);

        // ═══ فیلدها ═══
        stack.Children.Add(new TextBlock { Text = "نام فروشگاه (اختیاری):", Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 150)), Margin = new Thickness(0, 0, 0, 8), FontSize = 12 });
        _storeBox = new TextBox { Background = new SolidColorBrush(Color.FromRgb(18, 22, 28)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), BorderThickness = new Thickness(1), Padding = new Thickness(10), Height = 38, FontSize = 13, CaretBrush = new SolidColorBrush(Color.FromRgb(0, 240, 255)), VerticalContentAlignment = VerticalAlignment.Center };
        stack.Children.Add(_storeBox);

        stack.Children.Add(new TextBlock { Text = "لینک دسته‌بندی سایت:", Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 150)), Margin = new Thickness(0, 15, 0, 8), FontSize = 12 });
        _urlBox = new TextBox { Background = new SolidColorBrush(Color.FromRgb(18, 22, 28)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), BorderThickness = new Thickness(1), Padding = new Thickness(10), Height = 38, FontSize = 13, CaretBrush = new SolidColorBrush(Color.FromRgb(0, 240, 255)), VerticalContentAlignment = VerticalAlignment.Center };
        stack.Children.Add(_urlBox);

        stack.Children.Add(new TextBlock { Text = "ذخیره در دسته‌بندی:", Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 150)), Margin = new Thickness(0, 15, 0, 8), FontSize = 12 });
        _catCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 5), Height = 38 };
        foreach (var c in categories) _catCombo.Items.Add(c);
        if (_catCombo.Items.Count > 0) _catCombo.SelectedIndex = 0;
        stack.Children.Add(_catCombo);

        scroll.Content = stack;
        rootGrid.Children.Add(scroll);

        // ═══ دکمه اسکن: dock شده در ردیف پایین — هرگز از دید خارج نمی‌شود ═══
        var scanBtn = new Button {
            Content = "🚀 شروع اسکن سایت",
            Height = 46,
            Margin = new Thickness(25, 8, 25, 25),
            Background = new SolidColorBrush(Color.FromRgb(123, 97, 255)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Cursor = Cursors.Hand
        };
        scanBtn.Click += (s, e) => StartScan();
        Grid.SetRow(scanBtn, 1);
        rootGrid.Children.Add(scanBtn);

        outerBorder.Child = rootGrid;
        Content = outerBorder;

        // ✅ میانبر: Enter در فیلد لینک = شروع اسکن
        _urlBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) StartScan(); };
        _storeBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) _urlBox.Focus(); };
    }

    private void StartScan()
    {
        if (string.IsNullOrWhiteSpace(_urlBox.Text))
        {
            MessageBox.Show(this, "لطفا لینک دسته‌بندی را وارد کنید.");
            _urlBox.Focus();
            return;
        }
        CategoryUrl = _urlBox.Text.Trim();
        TargetCategory = _catCombo.SelectedItem?.ToString() ?? "عمومی";
        StoreName = string.IsNullOrWhiteSpace(_storeBox.Text) ? "سایت خارجی" : _storeBox.Text.Trim();
        DialogResult = true;
        Close();
    }
}
