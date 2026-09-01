using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TorobScanner.Models;
using TorobScanner.Services;
using TorobScanner.Views;

namespace TorobScanner;

/// <summary>
/// ✨ v3.1 — تب مقایسه محصولات (لینک محصولات فروشگاه‌های مختلف):
/// • کلیک روی هر کارت محصول → تب جدیدی باز می‌شود؛ در همان تب محصولاتِ همان کالا از
///   فروشگاه‌های دیگر (مثلا «ارپیچیو سام‌کیش» با «ارپیچیو کافی‌کالا») لینک می‌شوند
///   و قیمت‌هایشان کنار هم نمایش داده می‌شود.
/// • نوار تب بالای فضای کاری: «همه محصولات» (ثابت) + تب‌های مقایسه‌ی قابل بستن.
/// • گروه‌های مقایسه در SQLite ذخیره می‌شوند (CompareGroups / CompareItems) — بعد از
///   بستن برنامه هم باقی می‌مانند.
/// • ارزان‌ترین عضو با نشان «✦ ارزان‌ترین» و بقیه با درصد گران‌تر بودن مشخص می‌شوند.
/// • جستجوی زنده بین محصولات ذخیره‌شده برای افزودن سریع به مقایسه + بروزرسانی قیمت‌های گروه.
/// • هر بار فعال‌شدن تب، صفحه از نو ساخته می‌شود → همیشه قیمت‌های تازه.
/// </summary>
public partial class MainWindow
{
    // ═══════════ وضعیت تب‌ها ═══════════

    private Panel _tabsPanel = null!;
    private Panel _pageHost = null!;
    private CompareTabRecord _productsTab = null!;
    private CompareTabRecord _activeTab = null!;
    private readonly List<CompareTabRecord> _tabs = new();

    private sealed class CompareTabRecord
    {
        public bool IsProductsTab;
        public SavedProduct? Anchor;      // null = هاب مقایسه (بدون محصول خاص — از سایدبار)
        public string Title = "";
        public FrameworkElement Page = null!;
        public Border Chip = null!;
        public TextBlock IconText = null!;
        public TextBlock TitleText = null!;
        public TextBlock? CloseText;
    }

    // ═══════════ راه‌اندازی نوار تب و میزبان صفحات ═══════════

    private void InitTabs(FrameworkElement productsPage)
    {
        _tabs.Clear();
        _productsTab = new CompareTabRecord { IsProductsTab = true, Title = "همه محصولات", Page = productsPage };
        _tabs.Add(_productsTab);

        _tabsPanel.Children.Clear();
        foreach (var t in _tabs)
        {
            t.Chip = BuildTabChip(t);
            _tabsPanel.Children.Add(t.Chip);
        }

        _pageHost.Children.Clear();
        _pageHost.Children.Add(productsPage);
        _activeTab = _productsTab;
        RefreshTabChipVisuals();
    }

    private CompareTabRecord AddCompareTab(SavedProduct? anchor, bool activate)
    {
        var rec = new CompareTabRecord
        {
            Anchor = anchor,
            Title = anchor == null ? "مقایسه قیمت‌ها" : anchor.ProductName
        };
        rec.Page = BuildComparePage(anchor);
        rec.Chip = BuildTabChip(rec);
        _tabs.Add(rec);
        _tabsPanel.Children.Add(rec.Chip);
        if (activate) ActivateTab(rec);
        return rec;
    }

    /// <summary>✨ کلیک روی کارت محصول → تب مقایسه‌ی آن محصول (یا تب موجودِ همان محصول)</summary>
    private void OpenCompareTabForProduct(SavedProduct anchor)
    {
        var existing = _tabs.FirstOrDefault(t => !t.IsProductsTab && t.Anchor != null && t.Anchor.Id == anchor.Id);
        if (existing != null) { ActivateTab(existing); return; }
        AddCompareTab(anchor, activate: true);
    }

    /// <summary>دکمه سایدبار «مقایسه محصولات» → هاب مقایسه (همه‌ی گروه‌ها)</summary>
    private void OpenCompareHub()
    {
        var existing = _tabs.FirstOrDefault(t => !t.IsProductsTab && t.Anchor == null);
        if (existing != null) { ActivateTab(existing); return; }
        AddCompareTab(null, activate: true);
    }

    private void CompareHub_Click(object sender, RoutedEventArgs e) => OpenCompareHub();

    private void CloseCompareTab(CompareTabRecord rec)
    {
        int idx = _tabs.IndexOf(rec);
        if (idx < 0) return;
        bool wasActive = ReferenceEquals(_activeTab, rec);
        _tabs.RemoveAt(idx);
        _tabsPanel.Children.RemoveAt(idx);
        if (wasActive && _tabs.Count > 0)
            ActivateTab(_tabs[Math.Min(idx, _tabs.Count - 1)]);
    }

    private void ActivateTab(CompareTabRecord rec)
    {
        _activeTab = rec;
        if (!rec.IsProductsTab)
            rec.Page = BuildComparePage(rec.Anchor);   // هر بار با دیتای تازه
        _pageHost.Children.Clear();
        _pageHost.Children.Add(rec.Page);
        RefreshTabChipVisuals();
    }

    /// <summary>بعد از اسکن/بروزرسانی — اگر تب فعال مقایسه است، با قیمت‌های جدید بازسازی شود</summary>
    private void RefreshActiveCompareTab()
    {
        if (_activeTab != null && !_activeTab.IsProductsTab) ActivateTab(_activeTab);
    }

    private void RefreshTabChipVisuals()
    {
        foreach (var t in _tabs)
        {
            bool active = ReferenceEquals(t, _activeTab);
            t.Chip.Background = active ? LuxUI.PlatinumMetal : LuxUI.ChipFill;
            t.Chip.BorderBrush = active ? new SolidColorBrush(LuxUI.LogoTextColor) : LuxUI.GlassStroke;
            t.TitleText.Foreground = active ? new SolidColorBrush(LuxUI.LogoTextColor) : LuxUI.TextSecondary;
            t.TitleText.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
            t.IconText.Foreground = active ? new SolidColorBrush(LuxUI.LogoTextColor) : LuxUI.Accent;
            if (t.CloseText != null)
                t.CloseText.Foreground = active ? new SolidColorBrush(LuxUI.LogoTextColor) : LuxUI.TextDim;
        }
    }

    private Border BuildTabChip(CompareTabRecord rec)
    {
        var chip = new Border
        {
            CornerRadius = new CornerRadius(LuxUI.PillRadius),
            Margin = new Thickness(0, 0, 6, 0),
            Padding = new Thickness(13, 6, 13, 7),
            Cursor = Cursors.Hand,
            Background = LuxUI.ChipFill,
            BorderBrush = LuxUI.GlassStroke,
            BorderThickness = new Thickness(1)
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var icon = new TextBlock
        {
            Text = rec.IsProductsTab ? "▦" : "⇄",
            FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 7, 0)
        };
        var title = new TextBlock
        {
            Text = rec.Title, FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 190, TextTrimming = TextTrimming.CharacterEllipsis
        };
        row.Children.Add(icon);
        row.Children.Add(title);
        rec.IconText = icon;
        rec.TitleText = title;

        if (!rec.IsProductsTab)
        {
            var closeText = new TextBlock
            { Text = "✕", FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(9, 1, 0, 0) };
            var closeZone = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 0, 4, 2),
                Cursor = Cursors.Hand,
                Child = closeText,
                ToolTip = "بستن تب"
            };
            closeZone.MouseLeftButtonUp += (s, e) => { e.Handled = true; CloseCompareTab(rec); };
            row.Children.Add(closeZone);
            rec.CloseText = closeText;
        }

        chip.Child = row;
        chip.MouseEnter += (s, e) => { if (!ReferenceEquals(rec, _activeTab)) chip.Background = LuxUI.HoverFill; };
        chip.MouseLeave += (s, e) => { if (!ReferenceEquals(rec, _activeTab)) chip.Background = LuxUI.ChipFill; };
        chip.MouseLeftButtonUp += (s, e) => { e.Handled = true; ActivateTab(rec); };
        return chip;
    }

    // ═══════════ ساخت صفحه‌ی مقایسه ═══════════

    private FrameworkElement BuildComparePage(SavedProduct? anchor)
    {
        var root = new Grid { Margin = new Thickness(0, 2, 6, 0) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });              // سربرگ محصول/هاب
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });              // انتخاب/ساخت گروه
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });              // آمار گروه
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // اعضا + افزودن

        // ─── وضعیت محلی صفحه (هر تب مقایسه مستقل است) ───
        var groups = new List<CompareGroup>();
        var members = new List<SavedProduct>();
        var pool = new List<SavedProduct>();
        CompareGroup? currentGroup = null;
        bool suppressCombo = false;

        // ═══ سربرگ ═══
        var header = LuxUI.GlassPanel();
        header.Margin = new Thickness(0, 0, 0, 10);
        header.Padding = new Thickness(16, 12, 14, 13);
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        if (anchor != null)
        {
            var hInfo = new Grid();
            hInfo.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hInfo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            hInfo.Children.Add(new Border
            {
                Width = 3.5, Background = LuxUI.Iridescent,
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 3, 12, 3),
                HorizontalAlignment = HorizontalAlignment.Left
            });

            var hStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            hStack.Children.Add(new TextBlock
            {
                Text = anchor.ProductName, Foreground = LuxUI.TextPrimary,
                FontSize = 16, FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            var hMeta = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            hMeta.Children.Add(new TextBlock { Text = anchor.StoreName, Foreground = LuxUI.Accent, FontSize = 11 });
            hMeta.Children.Add(new TextBlock
            {
                Text = $"  •  {anchor.CategoryName}  •  بروزرسانی: {LuxUI.RelativeTimeFa(anchor.LastUpdate)}",
                Foreground = LuxUI.TextDim, FontSize = 11
            });
            hStack.Children.Add(hMeta);
            hInfo.Children.Add(hStack);
            Grid.SetColumn(hStack, 1);
            headerGrid.Children.Add(hInfo);

            var hPriceStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0)
            };
            hPriceStack.Children.Add(new TextBlock
            {
                Text = anchor.LastPrice > 0 ? LuxUI.FaPrice(anchor.LastPrice) : "—",
                Foreground = LuxUI.Platinum, FontSize = 17, FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            hPriceStack.Children.Add(new TextBlock
            {
                Text = anchor.LastPrice > 0 ? "تومان" : "قیمت ثبت نشده",
                Foreground = LuxUI.TextDim, FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0)
            });
            headerGrid.Children.Add(hPriceStack);
            Grid.SetColumn(hPriceStack, 1);

            var hLink = LuxUI.OpenLinkButton(this, anchor.TorobUrl);
            headerGrid.Children.Add(hLink);
            Grid.SetColumn(hLink, 2);
        }
        else
        {
            var hRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            hRow.Children.Add(LuxUI.IconChip("⇄", new SolidColorBrush(Tint(LuxUI.Accent, 0x22)), 38, 17));
            var hStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            hStack.Children.Add(new TextBlock
            { Text = "مقایسه محصولات", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = LuxUI.TextPrimary });
            hStack.Children.Add(new TextBlock
            {
                Text = "محصولات فروشگاه‌های مختلف را لینک کنید و قیمت‌هایشان را کنار هم ببینید.",
                FontSize = 11, Foreground = LuxUI.TextDim, Margin = new Thickness(0, 5, 0, 0)
            });
            hRow.Children.Add(hStack);
            headerGrid.Children.Add(hRow);
        }

        header.Child = headerGrid;
        root.Children.Add(header);
        Grid.SetRow(header, 0);

        // ═══ انتخاب/ساخت گروه مقایسه ═══
        var selector = LuxUI.GlassPanel();
        selector.Margin = new Thickness(0, 0, 0, 10);
        selector.Padding = new Thickness(14, 11, 14, 12);
        var selStack = new StackPanel();

        var selRow = new Grid();
        selRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        selRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        selRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        selRow.Children.Add(new TextBlock
        {
            Text = "گروه مقایسه:", FontSize = 12, Foreground = LuxUI.TextSecondary,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0)
        });

        var combo = new ComboBox
        {
            Height = 36, MinWidth = 200,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        selRow.Children.Add(combo);
        Grid.SetColumn(combo, 1);

        var btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var refreshBtn = new Button
        {
            Content = "⟳", Style = LuxUI.StyleOf("LuxBtnGhost"),
            Width = 36, Height = 34, Padding = new Thickness(0), FontSize = 13,
            ToolTip = "بروزرسانی قیمت‌های این گروه", Margin = new Thickness(0, 0, 8, 0)
        };
        var delBtn = new Button
        {
            Content = "حذف گروه", Style = LuxUI.StyleOf("LuxBtnDanger"),
            Height = 34, Padding = new Thickness(14, 0, 14, 0), FontSize = 11.5,
            ToolTip = "حذف گروه — محصولات از لیست اصلی پاک نمی‌شوند", Margin = new Thickness(0, 0, 8, 0)
        };
        var newBtn = new Button
        {
            Content = "+ گروه جدید", Style = LuxUI.StyleOf("LuxBtnPrimary"),
            Height = 34, Padding = new Thickness(16, 0, 16, 0), FontSize = 11.5
        };
        btns.Children.Add(refreshBtn);
        btns.Children.Add(delBtn);
        btns.Children.Add(newBtn);
        selRow.Children.Add(btns);
        Grid.SetColumn(btns, 2);

        selStack.Children.Add(selRow);

        // پنل ساخت گروه جدید (پیش‌فرض بسته)
        var creationPanel = new Grid { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 10, 0, 0) };
        creationPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        creationPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        creationPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        creationPanel.Children.Add(new TextBlock
        {
            Text = "نام گروه جدید:", FontSize = 12, Foreground = LuxUI.TextSecondary,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0)
        });
        var newGroupName = new TextBox { MinHeight = 40, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        creationPanel.Children.Add(newGroupName);
        Grid.SetColumn(newGroupName, 1);
        var createBtn = new Button
        {
            Content = "ساخت گروه", Style = LuxUI.StyleOf("LuxBtnPrimary"),
            Height = 40, Padding = new Thickness(18, 0, 18, 0), FontSize = 12
        };
        creationPanel.Children.Add(createBtn);
        Grid.SetColumn(createBtn, 2);
        selStack.Children.Add(creationPanel);

        selector.Child = selStack;
        root.Children.Add(selector);
        Grid.SetRow(selector, 1);

        // ═══ آمار گروه ═══
        var statsPanel = new WrapPanel { Margin = new Thickness(2, 0, 0, 8) };
        root.Children.Add(statsPanel);
        Grid.SetRow(statsPanel, 2);

        // ═══ اعضای گروه + کادر افزودن ═══
        var membersScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = Brushes.Transparent
        };
        var membersColumn = new StackPanel();
        var membersContainer = new StackPanel();
        membersColumn.Children.Add(membersContainer);

        var addCard = LuxUI.GlassPanel();
        addCard.Margin = new Thickness(0, 4, 0, 6);
        addCard.Padding = new Thickness(14, 12, 14, 14);
        var addStack = new StackPanel();

        var addHead = new StackPanel { Orientation = Orientation.Horizontal };
        addHead.Children.Add(LuxUI.IconChip("⊕", new SolidColorBrush(Tint(LuxUI.Accent, 0x22)), 34, 15));
        var addHeadText = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        addHeadText.Children.Add(new TextBlock
        { Text = "افزودن محصول به مقایسه", FontSize = 13, FontWeight = FontWeights.Medium, Foreground = LuxUI.TextPrimary });
        addHeadText.Children.Add(new TextBlock
        {
            Text = "نام همان محصول در فروشگاه دیگر را جستجو و لینک کنید", FontSize = 10.5,
            Foreground = LuxUI.TextDim, Margin = new Thickness(0, 4, 0, 0)
        });
        addHead.Children.Add(addHeadText);
        addStack.Children.Add(addHead);

        var searchHost = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        var search = new TextBox { MinHeight = 40, VerticalContentAlignment = VerticalAlignment.Center };
        var searchPh = new TextBlock
        {
            Text = "نام محصول را بنویسید…", FontSize = 12, Foreground = LuxUI.TextDim,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 14, 0), IsHitTestVisible = false
        };
        searchHost.Children.Add(search);
        searchHost.Children.Add(searchPh);
        addStack.Children.Add(searchHost);

        var resultsPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        addStack.Children.Add(resultsPanel);
        addCard.Child = addStack;
        membersColumn.Children.Add(addCard);

        membersScroll.Content = membersColumn;
        root.Children.Add(membersScroll);
        Grid.SetRow(membersScroll, 3);

        // ═══════════ منطق داده‌ی صفحه ═══════════

        void ReloadGroups()
        {
            groups = anchor == null ? _db.GetCompareGroups() : _db.GetGroupsForProduct(anchor.Id);

            suppressCombo = true;
            try
            {
                combo.Items.Clear();
                if (groups.Count == 0)
                {
                    combo.Items.Add(new ComboBoxItem { Content = "— هنوز گروهی ساخته نشده —", IsEnabled = false });
                    combo.SelectedIndex = 0;
                    combo.IsEnabled = false;
                    currentGroup = null;
                }
                else
                {
                    combo.IsEnabled = true;
                    foreach (var g in groups)
                        combo.Items.Add(new ComboBoxItem { Content = $"{g.Name}  ({LuxUI.Fa(g.ItemCount)})", Tag = g });
                    var keep = groups.FirstOrDefault(g => g.Id == currentGroup?.Id) ?? groups[0];
                    combo.SelectedIndex = groups.IndexOf(keep);
                }
            }
            finally { suppressCombo = false; }

            if (groups.Count > 0)
            {
                var sel = (combo.SelectedItem as ComboBoxItem)?.Tag as CompareGroup ?? groups[0];
                LoadGroup(sel);
            }
            else
            {
                members.Clear();
                pool.Clear();
                RenderMembers();
                RenderResults();
            }

            // محصول لنگر + بدون گروه → پنل ساخت گروه خودکار باز و پیش‌پر شود
            if (groups.Count == 0 && anchor != null)
            {
                creationPanel.Visibility = Visibility.Visible;
                if (string.IsNullOrWhiteSpace(newGroupName.Text))
                    newGroupName.Text = anchor.ProductName;
            }
        }

        void LoadGroup(CompareGroup g)
        {
            currentGroup = g;
            members = _db.GetGroupProducts(g.Id);
            pool = _db.GetAllProducts();
            RenderMembers();
            RenderResults();
        }

        void RenderMembers()
        {
            statsPanel.Children.Clear();
            membersContainer.Children.Clear();
            refreshBtn.IsEnabled = currentGroup != null && members.Count > 0;
            delBtn.IsEnabled = currentGroup != null;
            addCard.Visibility = currentGroup == null ? Visibility.Collapsed : Visibility.Visible;
            statsPanel.Visibility = currentGroup == null ? Visibility.Collapsed : Visibility.Visible;

            if (currentGroup == null)
            {
                membersContainer.Children.Add(BuildCompareEmptyState(
                    groups.Count == 0 ? "هنوز گروه مقایسه‌ای ساخته نشده" : "گروهی انتخاب نشده",
                    groups.Count == 0
                        ? "با «+ گروه جدید» اولین گروه را بسازید" + (anchor != null
                            ? " — این محصول خودکار عضو گروه می‌شود."
                            : " و بعد محصولات فروشگاه‌های مختلف را به آن اضافه کنید.")
                        : "از فهرست «گروه مقایسه» بالای صفحه یک گروه را انتخاب کنید.",
                    anchor != null && groups.Count == 0));
                return;
            }

            var priced = members.Where(m => m.LastPrice > 0).ToList();
            decimal min = priced.Count > 0 ? priced.Min(m => m.LastPrice) : 0;
            decimal max = priced.Count > 0 ? priced.Max(m => m.LastPrice) : 0;
            decimal diffPct = min > 0 && max > min ? Math.Round((max - min) / min * 100m, 1) : 0;

            statsPanel.Children.Add(GroupStatChip("◈", "فروشگاه‌ها",
                LuxUI.Fa(members.Select(m => m.StoreName).Distinct().Count()) + " فروشگاه", LuxUI.Accent));
            statsPanel.Children.Add(GroupStatChip("↓", "ارزان‌ترین",
                min > 0 ? LuxUI.FaPrice(min) + " تومان" : "—", LuxUI.Success));
            statsPanel.Children.Add(GroupStatChip("⇅", "اختلاف قیمت",
                min > 0 && max > min ? LuxUI.Fa(diffPct.ToString("0.#", CultureInfo.InvariantCulture)) + "٪" : "—", LuxUI.Warning));

            if (members.Count == 0)
            {
                membersContainer.Children.Add(BuildCompareEmptyState(
                    "این گروه هنوز خالی است",
                    "از کادر «افزودن محصول به مقایسه» پایین صفحه، همان محصول را از فروشگاه‌های دیگر اضافه کنید.",
                    false));
            }

            foreach (var m in members)
                membersContainer.Children.Add(BuildMemberCard(m, min, priced.Count > 0));
        }

        void ToggleCreationPanel()
        {
            bool opening = creationPanel.Visibility != Visibility.Visible;
            creationPanel.Visibility = opening ? Visibility.Visible : Visibility.Collapsed;
            if (opening)
            {
                if (string.IsNullOrWhiteSpace(newGroupName.Text) && anchor != null)
                    newGroupName.Text = anchor.ProductName;
                newGroupName.Focus();
            }
        }

        void CreateGroupClicked()
        {
            var name = (newGroupName.Text ?? "").Trim();
            if (name.Length == 0)
            {
                newGroupName.Focus();
                ShowToast("برای گروه مقایسه یک نام بنویسید", "✎");
                return;
            }
            try
            {
                int id = _db.CreateCompareGroup(name);
                if (anchor != null) _db.AddProductToGroup(id, anchor.Id);
                creationPanel.Visibility = Visibility.Collapsed;
                newGroupName.Clear();
                currentGroup = null;   // ReloadGroups جدیدترین گروه (همین) را انتخاب می‌کند
                ReloadGroups();
                ShowToast($"گروه «{name}» ساخته شد" + (anchor != null ? " و محصول فعلی عضو شد" : ""), "⇄");
            }
            catch (Exception ex)
            {
                Logger.Error("CompareCreate", name, ex.ToString());
                MessageBox.Show(this, "ساخت گروه ناموفق بود:\n" + ex.Message, "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        void DeleteGroupClicked()
        {
            if (currentGroup == null) { ShowToast("اول یک گروه مقایسه انتخاب کنید", "⇄"); return; }
            var g = currentGroup;
            var r = MessageBox.Show(this,
                $"گروه «{g.Name}» حذف شود؟\n\nلینک‌های این گروه پاک می‌شوند؛ خود محصولات از لیست اصلی حذف نمی‌شوند.",
                "حذف گروه مقایسه", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;
            try
            {
                _db.DeleteCompareGroup(g.Id);
                currentGroup = null;
                ReloadGroups();
                ShowToast($"گروه «{g.Name}» حذف شد", "✕");
            }
            catch (Exception ex)
            {
                Logger.Error("CompareDelete", g.Name, ex.ToString());
                MessageBox.Show(this, "حذف گروه ناموفق بود:\n" + ex.Message, "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        async void RefreshGroupPrices()
        {
            if (_isBusy) { MessageBox.Show(this, "یک عملیات در حال اجراست. لطفاً منتظر بمانید یا آن را متوقف کنید."); return; }
            if (currentGroup == null || members.Count == 0) { ShowToast("این گروه محصولی ندارد", "⇄"); return; }

            var g = currentGroup;
            var items = members.ToList();
            _isBusy = true;
            _cts = new CancellationTokenSource();
            _progressBar.Visibility = Visibility.Visible;
            _progressBar.IsIndeterminate = true;
            _stopBtn.Visibility = Visibility.Visible;
            _statusText.Text = $"بروزرسانی قیمت‌های گروه «{g.Name}»...";

            var progress = new Progress<(int current, int total, string status)>(p =>
                _statusText.Text = LuxUI.Fa($"{p.status} ({p.current}/{p.total})"));
            try
            {
                await Task.Run(() => _scraper.RefreshProductsAsync(items, progress, _cts.Token));
                _statusText.Text = "بروزرسانی گروه کامل شد.";
                ShowToast("قیمت‌های این گروه بروزرسانی شد", "⟳");
            }
            catch (OperationCanceledException)
            {
                _statusText.Text = "بروزرسانی متوقف شد.";
            }
            catch (Exception ex)
            {
                _statusText.Text = $"خطا در بروزرسانی گروه: {ex.Message}";
                MessageBox.Show(this, $"خطا در بروزرسانی گروه:\n{ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                EndBusyState();
                RefreshActiveCompareTab();
            }
        }

        void RenderResults()
        {
            resultsPanel.Children.Clear();
            if (currentGroup == null) return;

            var q = (search.Text ?? "").Trim();
            if (q.Length == 0)
            {
                resultsPanel.Children.Add(new TextBlock
                {
                    Text = "برای افزودن، بخشی از نام محصول را بنویسید…",
                    FontSize = 11, Foreground = LuxUI.TextDim, Margin = new Thickness(2, 4, 2, 0)
                });
                return;
            }

            var memberIds = members.Select(x => x.Id).ToHashSet();
            var matches = pool
                .Where(p => !memberIds.Contains(p.Id) && p.ProductName.Contains(q, StringComparison.OrdinalIgnoreCase))
                .Take(8)
                .ToList();

            if (matches.Count == 0)
            {
                resultsPanel.Children.Add(new TextBlock
                {
                    Text = "محصولی با این نام در ذخیره‌شده‌ها پیدا نشد.",
                    FontSize = 11, Foreground = LuxUI.TextDim, Margin = new Thickness(2, 4, 2, 0)
                });
                return;
            }

            foreach (var p in matches)
                resultsPanel.Children.Add(BuildResultRow(p));
        }

        FrameworkElement BuildResultRow(SavedProduct p)
        {
            var row = new Border
            {
                Background = LuxUI.GhostFill,
                BorderBrush = LuxUI.GlassStroke, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(LuxUI.PillRadius + 2),
                Padding = new Thickness(12, 8, 12, 9),
                Margin = new Thickness(0, 0, 0, 6)
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            infoStack.Children.Add(new TextBlock
            {
                Text = p.ProductName, FontSize = 12.5, Foreground = LuxUI.TextPrimary,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            infoStack.Children.Add(new TextBlock
            { Text = $"{p.StoreName}  •  {p.CategoryName}", FontSize = 10.5, Foreground = LuxUI.TextDim, Margin = new Thickness(0, 4, 0, 0) });
            grid.Children.Add(infoStack);

            var priceTxt = new TextBlock
            {
                Text = p.LastPrice > 0 ? LuxUI.FaPrice(p.LastPrice) : "—",
                Foreground = LuxUI.Platinum, FontSize = 13, FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0)
            };
            grid.Children.Add(priceTxt);
            Grid.SetColumn(priceTxt, 1);

            var addBtn = new Button
            {
                Content = "+ افزودن", Style = LuxUI.StyleOf("LuxBtnGhost"),
                Height = 30, Padding = new Thickness(12, 0, 12, 0), FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            addBtn.Click += (s, e) =>
            {
                if (currentGroup == null) return;
                try
                {
                    _db.AddProductToGroup(currentGroup.Id, p.Id);
                    if (!members.Any(x => x.Id == p.Id)) members.Add(p);
                    ShowToast($"«{p.ProductName}» به مقایسه اضافه شد", "⊕");
                    RenderMembers();
                    RenderResults();
                }
                catch (Exception ex)
                {
                    Logger.Error("CompareAdd", p.TorobUrl, ex.ToString());
                    MessageBox.Show(this, "افزودن محصول به گروه ناموفق بود:\n" + ex.Message);
                }
            };
            grid.Children.Add(addBtn);
            Grid.SetColumn(addBtn, 2);

            row.Child = grid;
            return row;
        }

        FrameworkElement BuildMemberCard(SavedProduct m, decimal min, bool anyPriced)
        {
            bool cheapest = anyPriced && m.LastPrice > 0 && m.LastPrice == min;

            var card = new Border
            {
                Background = cheapest ? new SolidColorBrush(Tint(LuxUI.Success, 0x12)) : LuxUI.GlassFill,
                BorderBrush = LuxUI.GlassStroke,
                BorderThickness = LuxUI.CardBorderThick,
                CornerRadius = new CornerRadius(LuxUI.CardRadius),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(16, 10, 14, 11)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new Grid();
            info.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            info.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            info.Children.Add(new Border
            {
                Width = 3,
                Background = cheapest ? LuxUI.Success : LuxUI.Iridescent,
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 3, 12, 3),
                HorizontalAlignment = HorizontalAlignment.Left
            });

            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            infoStack.Children.Add(new TextBlock
            {
                Text = m.ProductName, Foreground = LuxUI.TextPrimary,
                FontSize = 13.5, FontWeight = FontWeights.Medium,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            var metaRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            metaRow.Children.Add(new TextBlock { Text = m.StoreName, Foreground = LuxUI.Accent, FontSize = 10.5 });
            metaRow.Children.Add(new TextBlock
            {
                Text = $"  •  {m.CategoryName}  •  بروزرسانی: {LuxUI.RelativeTimeFa(m.LastUpdate)}",
                Foreground = LuxUI.TextDim, FontSize = 10.5
            });
            infoStack.Children.Add(metaRow);
            info.Children.Add(infoStack);
            Grid.SetColumn(infoStack, 1);
            grid.Children.Add(info);

            var priceStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            priceStack.Children.Add(new TextBlock
            {
                Text = m.LastPrice > 0 ? LuxUI.FaPrice(m.LastPrice) : "—",
                Foreground = cheapest ? LuxUI.Success : LuxUI.Platinum,
                FontSize = 16.5, FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            priceStack.Children.Add(new TextBlock
            {
                Text = m.LastPrice > 0 ? "تومان" : "قیمت ثبت نشده",
                Foreground = LuxUI.TextDim, FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0)
            });
            grid.Children.Add(priceStack);
            Grid.SetColumn(priceStack, 1);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };

            actions.Children.Add(MemberBadge(m, min, anyPriced));

            var link = LuxUI.OpenLinkButton(this, m.TorobUrl);
            link.Margin = new Thickness(8, 0, 0, 0);
            actions.Children.Add(link);

            var rm = new Button
            {
                Content = "✕", Style = LuxUI.StyleOf("LuxBtnGhost"),
                Width = 34, Height = 34, Padding = new Thickness(0), FontSize = 11,
                Margin = new Thickness(6, 0, 0, 0),
                ToolTip = "حذف از گروه مقایسه"
            };
            rm.Click += (s, e) =>
            {
                if (currentGroup == null) return;
                try
                {
                    _db.RemoveProductFromGroup(currentGroup.Id, m.Id);
                    members.Remove(m);
                    ShowToast($"«{m.ProductName}» از مقایسه حذف شد", "✕");
                    RenderMembers();
                    RenderResults();
                }
                catch (Exception ex)
                {
                    Logger.Error("CompareRemove", m.TorobUrl, ex.ToString());
                    MessageBox.Show(this, "حذف محصول از گروه ناموفق بود:\n" + ex.Message);
                }
            };
            actions.Children.Add(rm);
            grid.Children.Add(actions);
            Grid.SetColumn(actions, 2);

            card.Child = grid;
            return card;
        }

        FrameworkElement MemberBadge(SavedProduct m, decimal min, bool anyPriced)
        {
            string text; Brush fg, bg;
            if (m.LastPrice == 0)
            {
                text = "در انتظار قیمت"; fg = LuxUI.Warning; bg = new SolidColorBrush(Tint(LuxUI.Warning, 0x20));
            }
            else if (anyPriced && m.LastPrice == min)
            {
                text = "✦ ارزان‌ترین"; fg = LuxUI.Success; bg = new SolidColorBrush(Tint(LuxUI.Success, 0x20));
            }
            else
            {
                decimal pct = min > 0 ? Math.Round((m.LastPrice - min) / min * 100m, 1) : 0;
                text = $"▲ {LuxUI.Fa(pct.ToString("0.#", CultureInfo.InvariantCulture))}٪ گران‌تر";
                fg = LuxUI.Danger; bg = new SolidColorBrush(Tint(LuxUI.Danger, 0x20));
            }
            return new Border
            {
                Background = bg,
                CornerRadius = new CornerRadius(LuxUI.PillRadius),
                Padding = new Thickness(9, 4, 9, 5),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = text, Foreground = fg, FontSize = 10, FontWeight = FontWeights.Medium }
            };
        }

        FrameworkElement GroupStatChip(string icon, string label, string value, Brush tint)
        {
            var chip = new Border
            {
                Background = LuxUI.GlassFill,
                BorderBrush = LuxUI.GlassStroke, BorderThickness = LuxUI.ChipBorderThick,
                CornerRadius = new CornerRadius(LuxUI.PillRadius + 2),
                Padding = new Thickness(13, 8, 13, 9),
                Margin = new Thickness(0, 0, 8, 2),
                VerticalAlignment = VerticalAlignment.Top
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock { Text = icon, FontSize = 12.5, Foreground = tint, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock { Text = "  " + label + ":  ", FontSize = 11, Foreground = LuxUI.TextDim, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock
            {
                Text = value, FontSize = 12.5, FontWeight = FontWeights.SemiBold,
                Foreground = LuxUI.TextPrimary, VerticalAlignment = VerticalAlignment.Center
            });
            chip.Child = row;
            return chip;
        }

        FrameworkElement BuildCompareEmptyState(string title, string sub, bool hintCreate)
        {
            var panel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 30)
            };
            var chip = new Border
            {
                Width = 56, Height = 56,
                Background = LuxUI.ChipFill,
                BorderBrush = LuxUI.GlassStroke, BorderThickness = LuxUI.ChipBorderThick,
                CornerRadius = new CornerRadius(LuxUI.LogoRadius),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "⇄", FontSize = 22, Foreground = LuxUI.Accent,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            panel.Children.Add(chip);
            panel.Children.Add(new TextBlock
            {
                Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold,
                Foreground = LuxUI.TextPrimary,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 16, 0, 0)
            });
            panel.Children.Add(new TextBlock
            {
                Text = sub, FontSize = 11.5, Foreground = LuxUI.TextSecondary,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 7, 0, 0),
                TextWrapping = TextWrapping.Wrap, MaxWidth = 430, TextAlignment = TextAlignment.Center
            });
            if (hintCreate)
                panel.Children.Add(new TextBlock
                {
                    Text = "پنل «ساخت گروه» بالای صفحه برای شما باز شده است — کافی است دکمه‌ی «ساخت گروه» را بزنید.",
                    FontSize = 10.5, Foreground = LuxUI.TextDim,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 0),
                    TextWrapping = TextWrapping.Wrap, MaxWidth = 430, TextAlignment = TextAlignment.Center
                });
            return new Border { Child = panel };
        }

        // ═══════════ اتصال رویدادهای صفحه ═══════════

        combo.SelectionChanged += (s, e) =>
        {
            if (suppressCombo) return;
            if (combo.SelectedItem is ComboBoxItem it && it.Tag is CompareGroup g) LoadGroup(g);
        };
        refreshBtn.Click += (s, e) => RefreshGroupPrices();
        newBtn.Click += (s, e) => ToggleCreationPanel();
        createBtn.Click += (s, e) => CreateGroupClicked();
        newGroupName.KeyDown += (s, e) => { if (e.Key == Key.Enter) CreateGroupClicked(); };
        delBtn.Click += (s, e) => DeleteGroupClicked();

        // پیش‌پر کردن جستجو قبل از وصل کردن TextChanged (بدون رندر زودهنگام)
        if (anchor != null) search.Text = anchor.ProductName;
        search.TextChanged += (s, e) =>
        {
            searchPh.Visibility = string.IsNullOrEmpty(search.Text) ? Visibility.Visible : Visibility.Collapsed;
            RenderResults();
        };

        ReloadGroups();

        return root;
    }
}
