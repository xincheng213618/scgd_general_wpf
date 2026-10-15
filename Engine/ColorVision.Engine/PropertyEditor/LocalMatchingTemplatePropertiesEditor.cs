using ColorVision.Core;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.P2;
using ColorVision.UI;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.PropertyEditor;

public sealed class LocalMatchingTemplatePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj)
    {
        DockPanel panel = new();
        panel.Children.Add(PropertyEditorHelper.CreateLabel(property, PropertyEditorHelper.GetResourceManager(obj)));
        Button edit = new() { Content = "编辑…", Margin = new Thickness(5, 0, 0, 0), ToolTip = "编辑节点内的参考图和参数；支持导入、导出模板" };
        DockPanel.SetDock(edit, Dock.Right);
        panel.Children.Add(edit);
        TextBlock summary = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        summary.SetBinding(TextBlock.TextProperty, new Binding(property.Name) { Source = obj, Converter = new SummaryConverter() });
        panel.Children.Add(summary);
        edit.Click += (_, _) =>
        {
            try
            {
                TemplateMatchingWindow window = TemplateMatchingWindow.ForNode(property.GetValue(obj) as string ?? string.Empty);
                window.Owner = Window.GetWindow(panel);
                if (window.ShowDialog() == true) property.SetValue(obj, window.ResultJson);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "匹配配置", MessageBoxButton.OK, MessageBoxImage.Warning); }
        };
        return panel;
    }

    private sealed class SummaryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string json || string.IsNullOrWhiteSpace(json)) return "未配置 · 点击编辑";
            try
            {
                TemplateMatchingDocument document = TemplateMatchingDocument.FromJson(json);
                return $"{document.Name} · {document.Image.Width}×{document.Image.Height} · 分数 ≥ {document.Options.ScoreThreshold}";
            }
            catch (Exception) { return "配置无效 · 点击编辑"; }
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
