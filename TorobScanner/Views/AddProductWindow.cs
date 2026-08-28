using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TorobScanner.Views;

/// <summary>پنجره افزودن محصول (فقط برای پنجره مدیریت)</summary>
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
        stack.Children.Add(new TextBlock { Text = "✨ افزودن لینک جدید", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, Margin = new Thickness(0,0,0,25) });

        stack.Children.Add(new TextBlock { Text = "نام محصول (اختیاری):", Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 150)), Margin = new Thickness(0, 0, 0, 8), FontSize = 12 });
        var nameBox = CreateObsidianTextBox(); stack.Children.Add(nameBox);

        stack.Children.Add(new TextBlock { Text = "لینک محصول در ترب:", Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 150)), Margin = new Thickness(0, 15, 0, 8), FontSize = 12 });
        var urlBox = CreateObsidianTextBox(); stack.Children.Add(urlBox);

        stack.Children.Add(new TextBlock { Text = "دسته‌بندی:", Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 150)), Margin = new Thickness(0, 15, 0, 8), FontSize = 12 });
        var catCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 25), Height = 38 };
        foreach (var c in categories) catCombo.Items.Add(c);
        if (catCombo.Items.Count > 0) catCombo.SelectedIndex = 0;
        stack.Children.Add(catCombo);

        var saveBtn = new Button { Content = "ذخیره و ردیابی", Height = 44, Background = new SolidColorBrush(Color.FromRgb(0, 240, 255)), Foreground = Brushes.Black, BorderThickness = new Thickness(0), FontSize = 13, FontWeight = FontWeights.Bold, Cursor = System.Windows.Input.Cursors.Hand };
        saveBtn.Click += (s, e) => {
            if(string.IsNullOrWhiteSpace(urlBox.Text)) { MessageBox.Show(this, "لطفا لینک محصول را وارد کنید."); return; }
            // ✅ رفع ناسازگاری: نام خالی → «محصول جدید» (مثل Import)
            ProductName = string.IsNullOrWhiteSpace(nameBox.Text) ? "محصول جدید" : nameBox.Text.Trim();
            ProductUrl = urlBox.Text.Trim();
            SelectedCategory = catCombo.SelectedItem?.ToString() ?? "عمومی";
            DialogResult = true; Close();
        };
        stack.Children.Add(saveBtn);
        outerBorder.Child = stack; Content = outerBorder;
    }

    private TextBox CreateObsidianTextBox() => new TextBox { Background = new SolidColorBrush(Color.FromRgb(18, 22, 28)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), BorderThickness = new Thickness(1), Padding = new Thickness(10), Height = 38, FontSize = 13, CaretBrush = new SolidColorBrush(Color.FromRgb(0, 240, 255)), VerticalContentAlignment = VerticalAlignment.Center };
}
