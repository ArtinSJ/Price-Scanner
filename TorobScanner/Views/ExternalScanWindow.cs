using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TorobScanner.Views;

/// <summary>
/// پنجره اسکن سایت‌های خارجی — تم لوکس Platinum-Glass (v2.5)
/// ✅ ارتفاع پنجره = محتوا؛ دکمه اسکن همیشه دیده می‌شود
/// ✅ لیست سایت‌های بهینه‌شده به‌صورت pill های شیشه‌ای
/// ✅ میانبر Enter
/// </summary>
public class ExternalScanWindow : Window
{
    public string CategoryUrl { get; private set; } = "";
    public string TargetCategory { get; private set; } = "عمومی";
    public string StoreName { get; private set; } = "سایت خارجی";

    private readonly TextBox _storeBox = null!;
    private readonly TextBox _urlBox = null!;
    private readonly ComboBox _catCombo = null!;

    public ExternalScanWindow(List<string> categories, List<string> registeredSites)
    {
        Title = "اسکن سایت خارجی";
        Width = 540;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 700;
        FlowDirection = FlowDirection.RightToLeft;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = Application.Current?.MainWindow;
        WindowStyle = WindowStyle.None; AllowsTransparency = true;
        ResizeMode = ResizeMode.NoResize;
        Background = Brushes.Transparent;
        ThemeHelper.ApplyObsidianTheme(this);

        var content = LuxUI.BuildDialogShell(this, "◈",
            "اسکن سایت‌های دیگر",
            new SolidColorBrush(Tint(LuxUI.Accent, 0x2E)),
            out _, out var outerBorder);

        // ═══ موتور تطبیقی + سایت‌های بهینه‌شده ═══
        var engineCard = new Border
        {
            Background = new SolidColorBrush(Tint(LuxUI.Accent, 0x14)),
            BorderBrush = LuxUI.GlassStroke, BorderThickness = LuxUI.CardBorderThick,
            CornerRadius = new CornerRadius(LuxUI.CardRadius),
            Padding = new Thickness(14), Margin = new Thickness(0, 0, 0, 18)
        };
        var engineStack = new StackPanel();
        engineStack.Children.Add(new TextBlock
        {
            Text = "موتور اسکن تطبیقی فعال — سایت‌های بهینه‌شده:",
            Foreground = LuxUI.Accent, FontSize = 11.5,
            FontWeight = FontWeights.Medium, Margin = new Thickness(0, 0, 0, 10)
        });
        var sitesWrap = new WrapPanel();
        foreach (var site in registeredSites)
        {
            sitesWrap.Children.Add(LuxUI.Pill(
                site.Replace("✅ ", "").Replace("🌐 ", ""),
                LuxUI.ChipFill,
                LuxUI.TextSecondary));
        }
        engineStack.Children.Add(sitesWrap);
        engineCard.Child = engineStack;
        content.Children.Add(engineCard);

        // ═══ فیلدها ═══
        content.Children.Add(LuxUI.Caption("نام فروشگاه (اختیاری)"));
        _storeBox = new TextBox { MinHeight = 40, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 7, 0, 0) };
        content.Children.Add(_storeBox);

        content.Children.Add(new TextBlock
        {
            Text = "لینک دسته‌بندی سایت:", Foreground = LuxUI.TextSecondary,
            FontSize = 11.5, Margin = new Thickness(0, 15, 0, 0)
        });
        _urlBox = new TextBox { MinHeight = 40, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 7, 0, 0) };
        content.Children.Add(_urlBox);

        content.Children.Add(new TextBlock
        {
            Text = "ذخیره در دسته‌بندی:", Foreground = LuxUI.TextSecondary,
            FontSize = 11.5, Margin = new Thickness(0, 15, 0, 0)
        });
        _catCombo = new ComboBox { Height = 40, Margin = new Thickness(0, 7, 0, 10) };
        foreach (var c in categories) _catCombo.Items.Add(c);
        if (_catCombo.Items.Count > 0) _catCombo.SelectedIndex = 0;
        content.Children.Add(_catCombo);

        // ═══ دکمه اسکن — همیشه دیده می‌شود ═══
        var scanBtn = LuxUI.PrimaryButton("شروع اسکن سایت");
        scanBtn.Margin = new Thickness(0, 12, 0, 0);
        scanBtn.Click += (s, e) => StartScan();
        content.Children.Add(scanBtn);

        Content = outerBorder;

        // میانبر: Enter در فیلد لینک = شروع اسکن
        _urlBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) StartScan(); };
        _storeBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) _urlBox.Focus(); };
        Loaded += (s, e) => _storeBox.Focus();
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

    /// <summary>رنگ لهجه با آلفای دلخواه — هم‌گام با تم فعال (✨ v2.8)</summary>
    private static Color Tint(Brush accent, byte alpha)
    {
        var c = accent is SolidColorBrush sc ? sc.Color : Color.FromRgb(0x8F, 0xB8, 0xFF);
        return Color.FromArgb(alpha, c.R, c.G, c.B);
    }
}
