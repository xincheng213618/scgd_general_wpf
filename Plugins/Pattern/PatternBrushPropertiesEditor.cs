using ColorVision.Themes;
using ColorVision.UI;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace Pattern
{
    public sealed class PatternBrushPropertiesEditor : IPropertyEditor
    {
        private static readonly (string Tag, string Name, SolidColorBrush Brush)[] Presets =
        [
            ("R", "红色", Brushes.Red),
            ("G", "绿色", Brushes.Lime),
            ("B", "蓝色", Brushes.Blue),
            ("W", "白色", Brushes.White),
            ("K", "黑色", Brushes.Black)
        ];

        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var panel = new DockPanel();
            var label = PropertyEditorHelper.CreateLabel(property, PropertyEditorHelper.GetResourceManager(obj));
            label.VerticalAlignment = VerticalAlignment.Center;
            panel.Children.Add(label);

            var colors = new UniformGrid { Rows = 1, Columns = 6, Margin = new Thickness(5, 0, 0, 0) };
            var swatch = new Button { Height = 24, Padding = new Thickness(0), Margin = new Thickness(0, 0, 2, 0), ToolTip = "选择颜色" };
            swatch.SetBinding(Control.BackgroundProperty, new Binding(property.Name) { Source = obj, Mode = BindingMode.OneWay });
            AutomationProperties.SetName(swatch, $"{label.Text}：选择颜色");
            swatch.Click += (_, _) =>
            {
                var picker = new HandyControl.Controls.ColorPicker
                {
                    SelectedBrush = ((SolidColorBrush?)property.GetValue(obj) ?? Brushes.Black).CloneCurrentValue()
                };
                var owner = Window.GetWindow(swatch) ?? Application.Current?.GetActiveWindow();
                var window = new Window
                {
                    Title = label.Text,
                    Owner = owner,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Content = picker,
                    Width = 250,
                    Height = 400
                };
                window.ApplyCaption();
                picker.Confirmed += (_, _) =>
                {
                    property.SetValue(obj, picker.SelectedBrush.CloneCurrentValue());
                    window.Close();
                };
                picker.Canceled += (_, _) => window.Close();
                window.Closed += (_, _) => picker.Dispose();
                window.ShowDialog();
            };
            colors.Children.Add(swatch);

            foreach (var preset in Presets)
            {
                var button = new Button { Content = preset.Tag, Height = 24, Padding = new Thickness(0), Margin = new Thickness(2, 0, 0, 0), ToolTip = preset.Name };
                AutomationProperties.SetName(button, $"{label.Text}：{preset.Name}");
                button.Click += (_, _) => property.SetValue(obj, preset.Brush);
                colors.Children.Add(button);
            }

            panel.Children.Add(colors);
            return panel;
        }
    }
}
