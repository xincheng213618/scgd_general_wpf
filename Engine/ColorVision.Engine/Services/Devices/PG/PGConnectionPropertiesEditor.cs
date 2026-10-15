using ColorVision.UI;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Services.Devices.PG;

/// <summary>Addr and Port have different meanings for TCP, serial and CH431.I2C.</summary>
public sealed class PGConnectionPropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj)
    {
        var config = (ConfigPG)obj;
        var content = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch };
        var binding = new MultiBinding { Mode = BindingMode.OneWay, Converter = new ConnectionEditorConverter(property, config) };
        binding.Bindings.Add(new Binding(nameof(ConfigPG.IsNet)) { Source = config });
        binding.Bindings.Add(new Binding(nameof(ConfigPG.Category)) { Source = config });
        content.SetBinding(ContentControl.ContentProperty, binding);
        var panel = new DockPanel();
        panel.Children.Add(content);
        return panel;
    }

    private sealed class ConnectionEditorConverter(PropertyInfo property, ConfigPG config) : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            bool isI2C = string.Equals(config.Category, "CH431.I2C", StringComparison.Ordinal);
            bool isNetwork = config.IsNet && !isI2C;
            bool isAddress = property.Name == nameof(ConfigPG.Addr);
            // Serial ports belong to the service computer; accept manual COM names without local enumeration.
            IPropertyEditor editor = isAddress && isNetwork ? new NetworkAddressPropertiesEditor() : new TextboxPropertiesEditor();
            DockPanel panel = editor.GenProperties(property, config);
            var label = (TextBlock)panel.Children[0];
            label.Text = isAddress
                ? isI2C ? Properties.Resources.Engine_PG_DeviceIndex : isNetwork ? Properties.Resources.IPAddress : Properties.Resources.SzComName
                : isI2C ? Properties.Resources.DeviceAddr : isNetwork ? Properties.Resources.Port : Properties.Resources.BaudRate;
            return panel;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
