using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TorobScanner.Data;

namespace TorobScanner.Views;

/// <summary>پنجره تنظیمات دسته‌بندی هنگام Import — تم لوکس Platinum-Glass (v2.5)</summary>
public class ImportSettingsWindow : Window
{
    public string SelectedCategory { get; private set; } = "عمومی";
    private readonly DatabaseManager _db;
    private readonly ComboBox _catCombo = null!;

    public ImportSettingsWindow(DatabaseManager db)
    {
        _db = db;
        Title = "تنظیمات دسته‌بندی";
        Width = 420;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 480;
        FlowDirection = FlowDirection.RightToLeft;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = Application.Current?.MainWindow; // جلوگیری از گم‌شدن در Alt-Tab
        WindowStyle = WindowStyle.None; AllowsTransparency = true;
        ResizeMode = ResizeMode.NoResize;
        Background = Brushes.Transparent;
        ThemeHelper.ApplyObsidianTheme(this);

        var content = LuxUI.BuildDialogShell(this, "▤",
            "وارد کردن لینک‌ها",
            (Brush)Application.Current!.Resources["LuxSuccessFill"],
            out _, out var outerBorder);

        content.Children.Add(new TextBlock
        {
            Text = "دسته‌بندی مقصد لینک‌ها را انتخاب کنید:",
            Foreground = LuxUI.TextSecondary, FontSize = 12, Margin = new Thickness(0, 0, 0, 10)
        });

        _catCombo = new ComboBox { Height = 40 };
        foreach (var c in _db.GetAllCategories()) _catCombo.Items.Add(c);
        if (_catCombo.Items.Count > 0) _catCombo.SelectedIndex = 0;
        content.Children.Add(_catCombo);

        content.Children.Add(new TextBlock
        {
            Text = "یا دسته جدید بسازید:", Foreground = LuxUI.TextSecondary,
            FontSize = 11.5, Margin = new Thickness(0, 16, 0, 0)
        });

        var newCatStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 24) };
        var newCatTxt = new TextBox { MinHeight = 40, VerticalContentAlignment = VerticalAlignment.Center, Width = 262 };
        var addCatBtn = LuxUI.GhostButton("افزودن");
        addCatBtn.Height = 40;
        addCatBtn.Padding = new Thickness(14, 0, 14, 0);
        addCatBtn.Margin = new Thickness(8, 0, 0, 0);
        addCatBtn.Click += (s, e) =>
        {
            // ✅ رفع باگ ۱۶ (v2.5): Trim نام دسته — قبلاً «قوه  » با فاصله ثبت می‌شد
            var catName = newCatTxt.Text?.Trim() ?? "";
            if (catName.Length == 0) return;
            _db.AddCategory(catName);
            _catCombo.Items.Clear();
            foreach (var c in _db.GetAllCategories()) _catCombo.Items.Add(c);
            _catCombo.SelectedItem = catName;
            newCatTxt.Text = "";
        };

        newCatStack.Children.Add(newCatTxt);
        newCatStack.Children.Add(addCatBtn);
        content.Children.Add(newCatStack);

        var confirmBtn = LuxUI.PrimaryButton("تایید و وارد کردن");
        confirmBtn.Click += (s, e) =>
        {
            SelectedCategory = _catCombo.SelectedItem?.ToString() ?? "عمومی";
            DialogResult = true;
            Close();
        };
        content.Children.Add(confirmBtn);

        Content = outerBorder;
    }
}
