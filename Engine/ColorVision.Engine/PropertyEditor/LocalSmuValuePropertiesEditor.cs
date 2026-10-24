using ColorVision.Engine.FlowProcessing.Nodes;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.PropertyEditor;

public sealed class LocalSmuValuePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj)
    {
        var panel = new TextboxPropertiesEditor().GenProperties(property, obj);
        if (obj is LocalSmuNode node)
        {
            var label = panel.Children.OfType<TextBlock>().Single();
            label.SetBinding(TextBlock.TextProperty, new Binding(nameof(node.Source))
            {
                Source = node, Mode = BindingMode.OneWay, Converter = new LabelConverter(node, property.Name)
            });
        }
        return panel;
    }

    // Use the same parameter metadata as the repeat editor, including after a source-mode change.
    private sealed class LabelConverter(LocalSmuNode node, string key) : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var definition = node.GetRepeatParameters().Single(item => item.Key == key);
            return $"{definition.DisplayName} ({definition.Unit})";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
