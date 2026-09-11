using ColorVision.UI;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Media;

public sealed class CvcieTemplatePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj)
    {
        var panel = new DockPanel();
        panel.Children.Add(PropertyEditorHelper.CreateLabel(property, PropertyEditorHelper.GetResourceManager(obj)));
        var button = new Button { Content = "选择字段…", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(8, 0, 0, 0) };
        DockPanel.SetDock(button, Dock.Right);
        panel.Children.Add(button);
        var summary = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        summary.SetBinding(TextBlock.TextProperty, new Binding(property.Name) { Source = obj, Converter = new SummaryConverter() });
        panel.Children.Add(summary);
        button.Click += (_, _) =>
        {
            var window = new CvcieTemplateWindow(property.GetValue(obj) as string ?? "") { Owner = Window.GetWindow(panel), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            if (window.ShowDialog() == true) property.SetValue(obj, window.Draft.Template);
        };
        return panel;
    }

    private sealed class SummaryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var draft = new CvcieTemplateDraft(value as string ?? "");
            if (draft.Summary.Length == 0) return draft.HasCustomLayout ? "自定义文字" : "未选择字段";
            return draft.Summary + (draft.HasCustomLayout ? "（自定义排版）" : "");
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
