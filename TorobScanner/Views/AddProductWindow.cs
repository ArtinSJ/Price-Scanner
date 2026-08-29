using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TorobScanner.Views;

/// <summary>
/// پنجره افزودن لینک محصول — ترب یا هر سایت دیگر
/// ✅ تغییر: دیگر محدود به ترب نیست؛ لینک محصولات سایت‌های دیگر هم پذیرفته می‌شود
/// ✅ تغییر: دکمه بستن (✕) اضافه شد — قبلاً بدون ذخیره راهی برای بستن نبود (WindowStyle=None)
/// </summary>
public class AddProductWindow : Window
{
    public string ProductName { get; private set; } = "";
    public string ProductUrl { get; private set; } = "";
    public string SelectedCategory { get; private set; } = "عمومی";

    public AddProductWindow(List<string> categories)
    {
        Title = "افزودن محصول جدید";
        Width = 420;
        // ✅ رفع ریسک بریده شدن دکمه ذخیره: ارتفاع = محتوا (قبلاً ثابت 400px و محتوا 396px — مرزی)
        SizeToContent = SizeToContent.Height;
        MaxHeight = 500;
        FlowDirection = FlowDirection.RightToLeft;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = Application.Current?.MainWindow;
        WindowStyle = WindowStyle.None; AllowsTransparency = true;
        FontFamily = new FontFamily("Segoe UI"); Background = Brushes.Transparent;
        ThemeHelper.ApplyObsidianTheme(this);

        var outerBorder = new Border {
            CornerRadius = new CornerRadius(10), Background = new SolidColorBrush(Color.FromRgb(12, 14, 18)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 0, 240, 255)), BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 30, Color = Colors.Black, Opacity = 0.6, ShadowDepth = 0 }
        };

        var stack = new StackPanel { Margin = new Thickness(30) };

        // ═══ سربرگ: عنوان + دکمه بستن (✕) ═══
        var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 25) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleTxt = new TextBlock { Text = "✨ افزودن لینک محصول", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
        headerGrid.Children.Add(titleTxt);

        var closeBtn = new Button {
            Content = "✕", Width = 30, Height = 30,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(Color.FromRgb(150, 160, 180)),
            FontSize = 14, Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Top
        };
        closeBtn.MouseEnter += (s, e) => closeBtn.Foreground = new SolidColorBrush(Color.FromRgb(255, 69, 58));
        closeBtn.MouseLeave += (s, e) => closeBtn.Foreground = new SolidColorBrush(Color.FromRgb(150, 160, 180));
        closeBtn.Click += (s, e) => { DialogResult = false; Close(); };
        headerGrid.Children.Add(closeBtn);
        Grid.SetColumn(closeBtn, 1);

        stack.Children.Add(headerGrid);

        stack.Children.Add(new TextBlock { Text = "نام محصول (اختیاری):", Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 150)), Margin = new Thickness(0, 0, 0, 8), FontSize = 12 });
        var nameBox = CreateObsidianTextBox(); stack.Children.Add(nameBox);

        // ✅ تغییر: لینک محصول از ترب یا هر سایت دیگر پذیرفته می‌شود
        stack.Children.Add(new TextBlock { Text = "لینک محصول (ترب یا سایت‌های دیگر):", Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 150)), Margin = new Thickness(0, 15, 0, 8), FontSize = 12 });
        var urlBox = CreateObsidianTextBox(); stack.Children.Add(urlBox);
        stack.Children.Add(new TextBlock {
            Text = "💡 لینک صفحه محصول از هر فروشگاه اینترنتی قابل قبول است.",
            Foreground = new SolidColorBrush(Color.FromRgb(90, 100, 120)),
            FontSize = 10, Margin = new Thickness(0, 5, 0, 0)
        });

        stack.Children.Add(new TextBlock { Text = "دسته‌بندی:", Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 150)), Margin = new Thickness(0, 15, 0, 8), FontSize = 12 });
        var catCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 25), Height = 38 };
        foreach (var c in categories) catCombo.Items.Add(c);
        if (catCombo.Items.Count > 0) catCombo.SelectedIndex = 0;
        stack.Children.Add(catCombo);

        var saveBtn = new Button { Content = "ذخیره و ردیابی", Height = 44, Background = new SolidColorBrush(Color.FromRgb(0, 240, 255)), Foreground = Brushes.Black, BorderThickness = new Thickness(0), FontSize = 13, FontWeight = FontWeights.Bold, Cursor = Cursors.Hand };
        saveBtn.Click += (s, e) => {
            var url = urlBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(url)) { MessageBox.Show(this, "لطفا لینک محصول را وارد کنید."); return; }
            if (!url.StartsWith("http", System.StringComparison.OrdinalIgnoreCase))
            { MessageBox.Show(this, "لینک معتبر نیست — باید با http یا https شروع شود."); return; }
            // ✅ رفع ناسازگاری: نام خالی → «محصول جدید» (مثل Import)
            ProductName = string.IsNullOrWhiteSpace(nameBox.Text) ? "محصول جدید" : nameBox.Text.Trim();
            ProductUrl = url;
            SelectedCategory = catCombo.SelectedItem?.ToString() ?? "عمومی";
            DialogResult = true; Close();
        };
        stack.Children.Add(saveBtn);
        outerBorder.Child = stack; Content = outerBorder;

        // ✅ میانبرها: Escape = بستن پنجره | Enter در فیلد لینک = ذخیره
        KeyDown += (s, e) => { if (e.Key == Key.Escape) { DialogResult = false; Close(); } };
        urlBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) saveBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };
    }

    private TextBox CreateObsidianTextBox() => new TextBox { Background = new SolidColorBrush(Color.FromRgb(18, 22, 28)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), BorderThickness = new Thickness(1), Padding = new Thickness(10), Height = 38, FontSize = 13, CaretBrush = new SolidColorBrush(Color.FromRgb(0, 240, 255)), VerticalContentAlignment = VerticalAlignment.Center };
}
