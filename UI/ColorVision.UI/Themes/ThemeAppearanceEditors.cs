using ColorVision.UI;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Themes;

internal static class AppearanceText
{
    internal static string Get(string key) => UI.Properties.Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    internal static DockPanel Editor(PropertyInfo property, object source, FrameworkElement editor)
    {
        var panel = new DockPanel();
        DockPanel.SetDock(editor, Dock.Right);
        panel.Children.Add(PropertyEditorHelper.CreateLabel(property, PropertyEditorHelper.GetResourceManager(source)));
        panel.Children.Add(editor);
        return panel;
    }
}

public sealed class ThemeColorPropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj)
    {
        var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        text.SetBinding(TextBlock.TextProperty, new Binding(property.Name) { Source = obj });
        var swatch = new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(7), Margin = new Thickness(0, 0, 8, 0), BorderThickness = new Thickness(1) };
        swatch.SetResourceReference(Border.BorderBrushProperty, "CV.Border.Control");
        swatch.SetBinding(Border.BackgroundProperty, new Binding(property.Name) { Source = obj, Converter = new HexBrushConverter() });
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(swatch);
        content.Children.Add(text);
        var button = new Button { Content = content, HorizontalAlignment = HorizontalAlignment.Right };
        button.SetResourceReference(FrameworkElement.StyleProperty, "AppearanceChipButtonStyle");
        AutomationProperties.SetName(button, AppearanceText.Get(property.GetCustomAttribute<ConfigSettingAttribute>()?.Name ?? property.Name));
        button.Click += (_, _) =>
        {
            var picker = new HandyControl.Controls.ColorPicker();
            string current = (string?)property.GetValue(obj) ?? "#FFFFFF";
            picker.SelectedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(current));
            var hex = new TextBox { Text = current, Margin = new Thickness(0, 0, 0, 8), MaxLength = 7 };
            AutomationProperties.SetName(hex, AppearanceText.Get("AppearanceHexColor"));
            var stack = new StackPanel { Margin = new Thickness(12) };
            stack.Children.Add(hex);
            stack.Children.Add(picker);
            var window = new Window
            {
                Title = AppearanceText.Get(property.GetCustomAttribute<ConfigSettingAttribute>()?.Name ?? property.Name),
                Owner = Window.GetWindow(button), WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize, Content = stack,
                ShowInTaskbar = false
            };
            window.SetResourceReference(Window.BackgroundProperty, "GlobalBackground");
            window.SetResourceReference(Window.ForegroundProperty, "GlobalTextBrush");
            window.SetResourceReference(Window.FontFamilyProperty, "CV.Font.UI");
            hex.LostKeyboardFocus += (_, _) =>
            {
                if (ThemeAppearance.NormalizeColor(hex.Text) is string normalized)
                    picker.SelectedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(normalized));
            };
            picker.Confirmed += (_, _) =>
            {
                Color color = picker.SelectedBrush.Color;
                property.SetValue(obj, $"#{color.R:X2}{color.G:X2}{color.B:X2}");
                window.Close();
            };
            picker.Canceled += (_, _) => window.Close();
            window.Closed += (_, _) => picker.Dispose();
            window.ApplyCaption();
            window.ShowDialog();
        };
        return AppearanceText.Editor(property, obj, button);
    }

    private sealed class HexBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(ThemeAppearance.NormalizeColor(value as string) ?? "#FFFFFF"));
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}

public sealed class ThemeFontPropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj)
    {
        var choices = new List<KeyValuePair<string, string>> { new(string.Empty, AppearanceText.Get("AppearanceSystemFont")) };
        choices.AddRange(Fonts.SystemFontFamilies.Select(font => font.Source)
            .Where(name => new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI", "Arial", "SimSun" }.Contains(name, StringComparer.OrdinalIgnoreCase))
            .OrderBy(name => name).Select(name => new KeyValuePair<string, string>(name, name)));
        string configured = (string?)property.GetValue(obj) ?? string.Empty;
        if (configured.Length > 0 && !choices.Any(choice => choice.Key == configured)) choices.Add(new(configured, configured));
        var combo = new ComboBox { ItemsSource = choices, DisplayMemberPath = "Value", SelectedValuePath = "Key" };
        combo.SetResourceReference(FrameworkElement.StyleProperty, "SettingsComboBoxStyle");
        combo.SetBinding(Selector.SelectedValueProperty, PropertyEditorHelper.CreateTwoWayBinding(obj, property));
        combo.SelectionChanged += (_, _) => { if (combo.IsLoaded && obj is ThemeConfig config) config.Apply(); };
        return AppearanceText.Editor(property, obj, combo);
    }
}

public sealed class ThemeResetPropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj)
    {
        var button = new Button
        {
            Content = AppearanceText.Get("AppearanceRestoreDefaults"),
            Command = property.GetValue(obj) as System.Windows.Input.ICommand,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        button.SetResourceReference(FrameworkElement.StyleProperty, "AppearanceChipButtonStyle");
        return AppearanceText.Editor(property, obj, button);
    }
}
