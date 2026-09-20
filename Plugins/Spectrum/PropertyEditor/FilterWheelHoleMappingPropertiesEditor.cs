using ColorVision.UI;
using Spectrum.Configs;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Spectrum.PropertyEditor;

public sealed class FilterWheelHoleMappingPropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj)
    {
        var panel = new DockPanel();
        var edit = new Button { Content = "编辑", MinWidth = 60, Margin = new Thickness(5, 0, 0, 0) };
        DockPanel.SetDock(edit, Dock.Right);
        panel.Children.Add(edit);
        panel.Children.Add(PropertyEditorHelper.CreateLabel(property, PropertyEditorHelper.GetResourceManager(obj)));

        var summary = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 0, 0) };
        summary.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
        summary.SetBinding(TextBlock.TextProperty, new Binding($"{property.Name}.Count")
        {
            Source = obj,
            StringFormat = "{0} 个孔位",
            TargetNullValue = "0 个孔位",
            FallbackValue = "0 个孔位"
        });
        panel.Children.Add(summary);

        edit.Click += (_, _) =>
        {
            var window = new FilterWheelHoleMappingWindow(property.GetValue(obj) as IEnumerable<FilterWheelHoleMap>)
            {
                Owner = Window.GetWindow(edit)
            };
            if (window.ShowDialog() == true)
                property.SetValue(obj, window.Result);
        };
        return panel;
    }
}
