using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.UI;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace ColorVision.Engine.PropertyEditor;

public sealed class SpectrumFilterWheelPositionEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj)
    {
        var panel = new DockPanel();
        panel.Children.Add(PropertyEditorHelper.CreateLabel(property, PropertyEditorHelper.GetResourceManager(obj)));
        var combo = new ComboBox
        {
            Margin = new Thickness(5, 0, 0, 0),
            MinWidth = PropertyEditorHelper.ControlMinWidth,
            Style = PropertyEditorHelper.ComboBoxSmallStyle,
            DisplayMemberPath = "HoleName",
            SelectedValuePath = "HoleIndex"
        };
        combo.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(ShutterConfig.FilterWheelDeviceCode))
        {
            Source = obj, Mode = BindingMode.OneWay, Converter = new HoleMappingConverter(), NotifyOnTargetUpdated = true
        });
        var selected = PropertyEditorHelper.CreateTwoWayBinding(obj, property);
        selected.Converter = new PositionConverter();
        combo.SetBinding(Selector.SelectedValueProperty, selected);
        // Replacing a wheel's choices clears the selector before restoring its saved value.
        combo.TargetUpdated += (_, args) =>
        {
            if (args.Property == ItemsControl.ItemsSourceProperty)
                combo.GetBindingExpression(Selector.SelectedValueProperty)?.UpdateTarget();
        };
        combo.DropDownOpened += (_, _) => combo.GetBindingExpression(ItemsControl.ItemsSourceProperty)?.UpdateTarget();
        panel.Children.Add(combo);
        return panel;
    }

    private sealed class HoleMappingConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => ServiceManager.Current?.DeviceServices.OfType<DeviceCfwPort>().FirstOrDefault(device => device.Code == value as string)?.FilterWheelConfig.HoleMapping;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    private sealed class PositionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is int ? value : Binding.DoNothing;
    }
}
