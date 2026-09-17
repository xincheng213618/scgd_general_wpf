using ColorVision.ImageEditor.EditorTools.Algorithms;
using ColorVision.UI;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.PropertyEditor;

public sealed class LocalRgbCrossConfigurationEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj)
    {
        var panel = new DockPanel();
        panel.Children.Add(PropertyEditorHelper.CreateLabel(property, PropertyEditorHelper.GetResourceManager(obj)));
        var button = new Button { Content = "编辑…", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(8, 0, 0, 0) };
        DockPanel.SetDock(button, Dock.Right);
        panel.Children.Add(button);
        var summary = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        summary.SetBinding(TextBlock.TextProperty, new Binding(property.Name) { Source = obj, Converter = new SummaryConverter() });
        summary.SetBinding(FrameworkElement.ToolTipProperty, new Binding(nameof(TextBlock.Text)) { Source = summary });
        panel.Children.Add(summary);
        button.Click += (_, _) =>
        {
            var window = new RgbCrossConfigurationWindow(property.GetValue(obj) as string ?? "")
            {
                Owner = Window.GetWindow(panel), WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            if (window.ShowDialog() == true) property.SetValue(obj, window.ResultJson);
        };
        return panel;
    }

    private sealed class SummaryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!RgbCrossConfigurationDraft.TryCreate(value as string ?? "", out var draft, out _) || !draft!.TryGetJson(out _, out _)) return "配置有误 · 点击编辑";
            return $"{draft.Rows}×{draft.Columns} · 阈值 {draft.TargetThreshold}";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
