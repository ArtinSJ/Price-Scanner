using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TorobScanner.Services;

namespace TorobScanner.Views;

/// <summary>
/// پنجره افزودن لینک محصول — تم لوکس Platinum-Glass (v2.5)
/// ✅ لینک محصولات سایت‌های دیگر هم پذیرفته می‌شود (نه فقط ترب)
/// ✅ دکمه بستن (✕) + Escape / Enter میانبر
/// ✅ اعتبارسنجی URL پیش از ذخیره
/// </summary>
public class AddProductWindow : Window
{
    public string ProductName { get; private set; } = "";
    public string ProductUrl { get; private set; } = "";
    public string SelectedCategory { get; private set; } = "عمومی";

    private readonly TextBox _urlBox;
    private readonly Button _saveBtn;

    public AddProductWindow(List<string> categories)
    {
        Title = "افزودن محصول جدید";
        Width = 440;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 540;
        FlowDirection = FlowDirection.RightToLeft;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = Application.Current?.MainWindow;
        WindowStyle = WindowStyle.None; AllowsTransparency = true;
        ResizeMode = ResizeMode.NoResize;
        Background = Brushes.Transparent;
        ThemeHelper.ApplyObsidianTheme(this);

        var content = LuxUI.BuildDialogShell(this, "＋",
            "افزودن لینک محصول",
            (Brush)Application.Current!.Resources["LuxFocusStroke"],
            out _, out var outerBorder);

        content.Children.Add(LuxUI.Caption("نام محصول (اختیاری)"));
        var nameBox = new TextBox { MinHeight = 40, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 7, 0, 0) };
        content.Children.Add(nameBox);

        content.Children.Add(LuxUI.Caption("لینک محصول (ترب یا سایت‌های دیگر)", 11.5).WithTopMargin(15));
        _urlBox = new TextBox { MinHeight = 40, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 7, 0, 0) };
        content.Children.Add(_urlBox);

        content.Children.Add(new TextBlock
        {
            Text = "لینک صفحه محصول از هر فروشگاه اینترنتی قابل قبول است.",
            Foreground = LuxUI.TextDim, FontSize = 10.5, Margin = new Thickness(2, 7, 0, 0)
        });

        content.Children.Add(LuxUI.Caption("دسته‌بندی").WithTopMargin(15));
        var catCombo = new ComboBox { Height = 40, Margin = new Thickness(0, 7, 0, 24) };
        foreach (var c in categories) catCombo.Items.Add(c);
        if (catCombo.Items.Count > 0) catCombo.SelectedIndex = 0;
        content.Children.Add(catCombo);

        _saveBtn = LuxUI.PrimaryButton("ذخیره و ردیابی");
        _saveBtn.Click += (s, e) => Save(nameBox, catCombo);
        content.Children.Add(_saveBtn);

        Content = outerBorder;

        // میانبرها: Escape = بستن | Enter در فیلد لینک = ذخیره
        KeyDown += (s, e) => { if (e.Key == Key.Escape) { DialogResult = false; Close(); } };
        _urlBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) Save(nameBox, catCombo); };
        Loaded += (s, e) => _urlBox.Focus();
    }

    private void Save(TextBox nameBox, ComboBox catCombo)
    {
        var url = _urlBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(url))
        { MessageBox.Show(this, "لطفا لینک محصول را وارد کنید."); _urlBox.Focus(); return; }
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        { MessageBox.Show(this, "لینک معتبر نیست — باید با http یا https شروع شود."); return; }

        ProductName = string.IsNullOrWhiteSpace(nameBox.Text) ? "محصول جدید" : nameBox.Text.Trim();
        ProductUrl = url;
        SelectedCategory = catCombo.SelectedItem?.ToString() ?? "عمومی";
        DialogResult = true;
        Close();
    }
}

/// <summary>افزودن حاشیه بالا به TextBlock (خوانایی بهتر کد دیالوگ‌ها)</summary>
internal static class LuxTextExtensions
{
    public static TextBlock WithTopMargin(this TextBlock tb, double margin)
    {
        tb.Margin = new Thickness(0, margin, 0, 0);
        return tb;
    }
}
