using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TorobScanner.Data;

namespace TorobScanner.Views;

/// <summary>پنجره تنظیمات وارد کردن (Import Settings)</summary>
public class ImportSettingsWindow : Window
{
    public string SelectedCategory { get; private set; } = "عمومی";
    private readonly DatabaseManager _db;
    private ComboBox _catCombo = null!;

    public ImportSettingsWindow(DatabaseManager db)
    {
        _db = db;
        Title = "تنظیمات دسته‌بندی";
        Width = 400;
        // ✅ رفع ریسک بریده شدن دکمه تایید: ارتفاع = محتوا (قبلاً ثابت 300px بود و محتوا 305px)
        SizeToContent = SizeToContent.Height;
        MaxHeight = 450;
        FlowDirection = FlowDirection.RightToLeft;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = Application.Current?.MainWindow; // جلوگیری از گم‌شدن در Alt-Tab
        WindowStyle = WindowStyle.None; AllowsTransparency = true;
        FontFamily = new FontFamily("Segoe UI"); Background = Brushes.Transparent;
        ThemeHelper.ApplyObsidianTheme(this);

        var outerBorder = new Border {
            CornerRadius = new CornerRadius(10), Background = new SolidColorBrush(Color.FromRgb(12, 14, 18)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 0, 240, 255)), BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 30, Color = Colors.Black, Opacity = 0.6, ShadowDepth = 0 }
        };

        var stack = new StackPanel { Margin = new Thickness(25) };
        stack.Children.Add(new TextBlock { Text = "📁 لینک‌ها آماده وارد شدن هستند", FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, Margin = new Thickness(0,0,0,20), HorizontalAlignment = HorizontalAlignment.Center });

        stack.Children.Add(new TextBlock { Text = "لطفا دسته‌بندی مقصد را انتخاب کنید:", Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 150)), Margin = new Thickness(0, 0, 0, 8), FontSize = 12 });

        _catCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 20), Height = 38 };
        foreach (var c in _db.GetAllCategories()) _catCombo.Items.Add(c);
        if (_catCombo.Items.Count > 0) _catCombo.SelectedIndex = 0;
        stack.Children.Add(_catCombo);

        stack.Children.Add(new TextBlock { Text = "یا دسته جدید بسازید:", Foreground = new SolidColorBrush(Color.FromRgb(120, 130, 150)), Margin = new Thickness(0, 0, 0, 8), FontSize = 12 });

        var newCatStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 25) };
        var newCatTxt = new TextBox { Background = new SolidColorBrush(Color.FromRgb(18, 22, 28)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), BorderThickness = new Thickness(1), Padding = new Thickness(10), Height = 38, Width = 250, FontSize = 13, CaretBrush = new SolidColorBrush(Color.FromRgb(0, 240, 255)), VerticalContentAlignment = VerticalAlignment.Center };

        var addCatBtn = new Button { Content = "➕ افزودن", Height = 38, Padding = new Thickness(10,0,10,0), Background = new SolidColorBrush(Color.FromArgb(30, 0, 240, 255)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromArgb(60, 0, 240, 255)), BorderThickness = new Thickness(1), Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(10, 0, 0, 0) };
        addCatBtn.Click += (s, e) => {
            if(!string.IsNullOrWhiteSpace(newCatTxt.Text)) {
                _db.AddCategory(newCatTxt.Text);
                _catCombo.Items.Clear();
                foreach (var c in _db.GetAllCategories()) _catCombo.Items.Add(c);
                _catCombo.SelectedItem = newCatTxt.Text;
                newCatTxt.Text = "";
            }
        };

        newCatStack.Children.Add(newCatTxt);
        newCatStack.Children.Add(addCatBtn);
        stack.Children.Add(newCatStack);

        var confirmBtn = new Button { Content = "✅ تایید و وارد کردن", Height = 44, Background = new SolidColorBrush(Color.FromRgb(0, 240, 255)), Foreground = Brushes.Black, BorderThickness = new Thickness(0), FontSize = 13, FontWeight = FontWeights.Bold, Cursor = System.Windows.Input.Cursors.Hand };
        confirmBtn.Click += (s, e) => {
            SelectedCategory = _catCombo.SelectedItem?.ToString() ?? "عمومی";
            DialogResult = true; Close();
        };
        stack.Children.Add(confirmBtn);

        outerBorder.Child = stack; Content = outerBorder;
    }
}
