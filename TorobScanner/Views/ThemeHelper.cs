using System.Windows;
using System.Windows.Markup;

namespace TorobScanner.Views;

/// <summary>تم دارک سراسری برای ComboBox ها</summary>
public static class ThemeHelper
{
    public static void ApplyObsidianTheme(Window window)
    {
        string xaml = @"<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
            <Style TargetType=""ComboBox"">
                <Setter Property=""Background"" Value=""#14181E""/>
                <Setter Property=""Foreground"" Value=""White""/>
                <Setter Property=""BorderBrush"" Value=""#3C3C3C""/>
                <Setter Property=""BorderThickness"" Value=""1""/>
                <Setter Property=""Padding"" Value=""5""/>
                <Setter Property=""VerticalContentAlignment"" Value=""Center""/>
                <Setter Property=""Template"">
                    <Setter.Value>
                        <ControlTemplate TargetType=""ComboBox"">
                            <Grid>
                                <Border Background=""#14181E"" BorderBrush=""#3C3C3C"" BorderThickness=""1"" CornerRadius=""4"">
                                    <Grid>
                                        <ToggleButton x:Name=""ToggleButton"" Focusable=""false"" IsChecked=""{Binding Path=IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"" ClickMode=""Press"" Background=""Transparent"" BorderThickness=""0""/>
                                        <ContentPresenter x:Name=""ContentSite"" IsHitTestVisible=""False"" Content=""{TemplateBinding SelectionBoxItem}"" ContentTemplate=""{TemplateBinding SelectionBoxItemTemplate}"" ContentTemplateSelector=""{TemplateBinding ItemTemplateSelector}"" Margin=""10,3,23,3"" VerticalAlignment=""Center"" HorizontalAlignment=""Left"" />
                                        <TextBlock Text=""▼"" Foreground=""#888888"" Margin=""0,0,10,0"" VerticalAlignment=""Center"" HorizontalAlignment=""Right""/>
                                    </Grid>
                                </Border>
                                <Popup x:Name=""Popup"" Placement=""Bottom"" IsOpen=""{TemplateBinding IsDropDownOpen}"" AllowsTransparency=""True"" Focusable=""False"" PopupAnimation=""Slide"">
                                    <Grid x:Name=""DropDown"" SnapsToDevicePixels=""True"" MinWidth=""{TemplateBinding ActualWidth}"" MaxHeight=""{TemplateBinding MaxDropDownHeight}"">
                                        <Border x:Name=""DropDownBorder"" Background=""#14181E"" BorderThickness=""1"" BorderBrush=""#3C3C3C"" CornerRadius=""4""/>
                                        <ScrollViewer Margin=""4,6,4,6"" SnapsToDevicePixels=""True"">
                                            <StackPanel IsItemsHost=""True"" KeyboardNavigation.DirectionalNavigation=""Contained"" />
                                        </ScrollViewer>
                                    </Grid>
                                </Popup>
                            </Grid>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>
            <Style TargetType=""ComboBoxItem"">
                <Setter Property=""Background"" Value=""Transparent""/>
                <Setter Property=""Foreground"" Value=""White""/>
                <Setter Property=""Padding"" Value=""10,5,10,5""/>
                <Setter Property=""Template"">
                    <Setter.Value>
                        <ControlTemplate TargetType=""ComboBoxItem"">
                            <Border Background=""{TemplateBinding Background}"" Padding=""{TemplateBinding Padding}"" CornerRadius=""4"">
                                <ContentPresenter/>
                            </Border>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
                <Style.Triggers>
                    <Trigger Property=""IsMouseOver"" Value=""True"">
                        <Setter Property=""Background"" Value=""#1F232B""/>
                        <Setter Property=""Foreground"" Value=""#00F0FF""/>
                    </Trigger>
                    <Trigger Property=""IsSelected"" Value=""True"">
                        <Setter Property=""Background"" Value=""#00F0FF""/>
                        <Setter Property=""Foreground"" Value=""Black""/>
                    </Trigger>
                </Style.Triggers>
            </Style>
        </ResourceDictionary>";

        var dict = (ResourceDictionary)XamlReader.Parse(xaml);
        window.Resources.MergedDictionaries.Add(dict);
    }
}
