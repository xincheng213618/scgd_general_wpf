using ColorVision.Common.Utilities;
using ColorVision.Themes.Properties;
using ColorVision.UI;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ColorVision.Themes
{
    public sealed class ThemePropertiesEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var dockPanel = new DockPanel();
            var label = PropertyEditorHelper.CreateLabel(property, PropertyEditorHelper.GetResourceManager(obj));
            var optionPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var optionHost = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            optionHost.Children.Add(optionPanel);

            string groupName = $"ThemePreview_{Guid.NewGuid():N}";
            var options = new List<(Theme Theme, RadioButton Button)>();
            Theme selectedTheme = ReadTheme(property, obj);

            foreach (Theme theme in ThemeManager.SupportedThemes)
            {
                string displayName = Resources.ResourceManager.GetString(theme.ToDescription(), CultureInfo.CurrentUICulture) ?? theme.ToString();
                var option = new RadioButton
                {
                    GroupName = groupName,
                    Tag = displayName,
                    Content = CreateThemePreview(theme),
                    IsChecked = theme == selectedTheme
                };
                option.SetResourceReference(FrameworkElement.StyleProperty, "ThemePreviewRadioButtonStyle");
                AutomationProperties.SetName(option, displayName);
                option.Checked += (_, _) => SelectTheme(property, obj, theme);

                options.Add((theme, option));
                optionPanel.Children.Add(option);
            }

            if (obj is INotifyPropertyChanged notifyPropertyChanged)
            {
                PropertyChangedEventHandler? handler = null;
                handler = (_, args) =>
                {
                    if (!string.IsNullOrEmpty(args.PropertyName) && args.PropertyName != property.Name) return;

                    Theme currentTheme = ReadTheme(property, obj);
                    foreach (var option in options)
                    {
                        option.Button.IsChecked = option.Theme == currentTheme;
                    }
                };

                notifyPropertyChanged.PropertyChanged += handler;
                optionPanel.Unloaded += (_, _) => notifyPropertyChanged.PropertyChanged -= handler;
            }

            DockPanel.SetDock(optionHost, Dock.Right);
            dockPanel.Children.Add(optionHost);
            dockPanel.Children.Add(label);
            return dockPanel;
        }

        private static void SelectTheme(PropertyInfo property, object source, Theme theme)
        {
            if (ReadTheme(property, source) == theme) return;

            property.SetValue(source, theme);
            Application.Current?.ApplyTheme(theme);
        }

        private static Theme ReadTheme(PropertyInfo property, object source)
        {
            return property.GetValue(source) is Theme theme ? ThemeManager.NormalizeTheme(theme) : Theme.UseSystem;
        }

        private static Grid CreateThemePreview(Theme theme)
        {
            return theme == Theme.UseSystem ? CreateSystemPreview() : CreateApplicationPreview(theme == Theme.Dark);
        }

        private static Grid CreateSystemPreview()
        {
            var preview = new Grid { Height = 80, ClipToBounds = true };
            preview.ColumnDefinitions.Add(new ColumnDefinition());
            preview.ColumnDefinitions.Add(new ColumnDefinition());

            var lightHalf = CreateSystemHalf(isDark: false);
            var darkHalf = CreateSystemHalf(isDark: true);
            Grid.SetColumn(darkHalf, 1);
            preview.Children.Add(lightHalf);
            preview.Children.Add(darkHalf);
            return preview;
        }

        private static Border CreateSystemHalf(bool isDark)
        {
            var half = new Border
            {
                Background = CreateBrush(isDark ? "#2E2E2E" : "#E7E7E7"),
                Padding = new Thickness(isDark ? 3 : 7, 12, isDark ? 7 : 3, 6)
            };

            var surface = new Border
            {
                Background = CreateBrush(isDark ? "#3B3B3B" : "#FAFAFA"),
                BorderBrush = CreateBrush(isDark ? "#555555" : "#D0D0D0"),
                BorderThickness = new Thickness(isDark ? 0 : 1, 1, isDark ? 1 : 0, 1),
                CornerRadius = new CornerRadius(isDark ? 0 : 5, isDark ? 5 : 0, isDark ? 5 : 0, isDark ? 0 : 5),
                Padding = new Thickness(5)
            };

            var content = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            content.Children.Add(CreateLine(isDark ? "#8A8A8A" : "#B5B5B5", 32));
            content.Children.Add(CreateLine(isDark ? "#707070" : "#D0D0D0", 44, new Thickness(0, 4, 0, 0)));
            content.Children.Add(CreateLine(isDark ? "#707070" : "#D0D0D0", 36, new Thickness(0, 4, 0, 0)));
            surface.Child = content;
            half.Child = surface;
            return half;
        }

        private static Grid CreateApplicationPreview(bool isDark)
        {
            string page = isDark ? "#303030" : "#F1F1F1";
            string shell = isDark ? "#252525" : "#FFFFFF";
            string sidebar = isDark ? "#353535" : "#E6E6E6";
            string border = isDark ? "#5B5B5B" : "#D2D2D2";
            string line = isDark ? "#777777" : "#D0D0D0";

            var preview = new Grid
            {
                Height = 80,
                Background = CreateBrush(page),
                ClipToBounds = true
            };

            var window = new Border
            {
                Margin = new Thickness(8, 10, 8, 6),
                Background = CreateBrush(shell),
                BorderBrush = CreateBrush(border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6)
            };

            var layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            layout.ColumnDefinitions.Add(new ColumnDefinition());
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            layout.RowDefinitions.Add(new RowDefinition());

            var header = new Border
            {
                Background = CreateBrush(isDark ? "#2A2A2A" : "#F8F8F8"),
                BorderBrush = CreateBrush(border),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            Grid.SetColumnSpan(header, 2);
            layout.Children.Add(header);

            var sidebarPanel = new Border
            {
                Background = CreateBrush(sidebar),
                CornerRadius = new CornerRadius(0, 0, 0, 5)
            };
            Grid.SetRow(sidebarPanel, 1);
            layout.Children.Add(sidebarPanel);

            var contentCard = new Border
            {
                Margin = new Thickness(4),
                Padding = new Thickness(5),
                Background = CreateBrush(isDark ? "#303030" : "#FFFFFF"),
                BorderBrush = CreateBrush(border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4)
            };
            Grid.SetColumn(contentCard, 1);
            Grid.SetRow(contentCard, 1);

            var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            lines.Children.Add(CreateLine(line, 36));
            lines.Children.Add(CreateLine(line, 56, new Thickness(0, 4, 0, 0)));
            lines.Children.Add(CreateLine(line, 46, new Thickness(0, 4, 0, 0)));
            contentCard.Child = lines;
            layout.Children.Add(contentCard);

            window.Child = layout;
            preview.Children.Add(window);
            return preview;
        }

        private static Border CreateLine(string color, double width, Thickness? margin = null)
        {
            return new Border
            {
                Width = width,
                Height = 3,
                Margin = margin ?? new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = CreateBrush(color),
                CornerRadius = new CornerRadius(3)
            };
        }

        private static SolidColorBrush CreateBrush(string color)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            return brush;
        }
    }
}
