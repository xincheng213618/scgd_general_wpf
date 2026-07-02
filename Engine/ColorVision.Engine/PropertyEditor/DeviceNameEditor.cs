using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.Devices.Motor;
using ColorVision.Engine.Services.Devices.PG;
using ColorVision.Engine.Services.Devices.Sensor;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms;
using ColorVision.UI;
using FlowEngineLib;
using FlowEngineLib.Base;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Engine.PropertyEditor
{
    [AttributeUsage(AttributeTargets.Property)]
    public class DeviceSourceTypeAttribute : Attribute
    {
        public Type DeviceType { get; }
        public DeviceSourceTypeAttribute(Type deviceType)
        {
            DeviceType = deviceType;
        }
    }

    public class DeviceNameEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();
            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);

            var combo = new HandyControl.Controls.ComboBox
            {
                Margin = new Thickness(5, 0, 0, 0),
                Style = PropertyEditorHelper.ComboBoxSmallStyle,
                IsEditable = true,
                DisplayMemberPath = "Name",
                SelectedValuePath = "Code"
            };
            HandyControl.Controls.InfoElement.SetShowClearButton(combo, true);
            combo.SetBinding(Selector.SelectedValueProperty, PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name));


            Type targetType = ResolveDeviceType(property, obj);

            var ItemsSource = ServiceManager.GetInstance().DeviceServices.Where(d => targetType.IsInstanceOfType(d)).ToList();

            combo.ItemsSource = ItemsSource;


            string? code = property.GetValue(obj)?.ToString();
            var selectedItem = ItemsSource.FirstOrDefault(x => x.Code == code);
            if (selectedItem != null)
                combo.SelectedItem = selectedItem;

            Grid myGrid = new Grid();
            myGrid.DataContext = selectedItem;

            combo.SelectionChanged += (s, e) =>
            {
                myGrid.DataContext = combo.SelectedItem;
                if (combo.SelectedValue is string selectedCode)
                    SetValueAndNotify(property, obj, selectedCode);
            };

            combo.LostFocus += (s, e) =>
            {
                if (combo.SelectedValue is not string && !string.IsNullOrWhiteSpace(combo.Text))
                    SetValueAndNotify(property, obj, combo.Text);
            };


            var button = new Button
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };

            var toggleButton = new ToggleButton
            {
                Style = (Style)Application.Current.FindResource("ButtonMQTTConnect"),
                Height = 10,
                Width = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsChecked = true,
                IsEnabled = false
            };
            // Create an Image
            var image = new Image
            {
                Source = (ImageSource)Application.Current.FindResource("DrawingImageProperty"),
                Height = 18,
                Margin = new Thickness(0)
            };
            // Create the binding for IsChecked
            var binding1 = new Binding("PropertyCommand")
            {
                Mode = BindingMode.OneWay
            };
            // Set the binding to the ToggleButton
            button.SetBinding(Button.CommandProperty, binding1);

            // Add elements to the Grid
            myGrid.Children.Add(toggleButton);
            myGrid.Children.Add(image);
            myGrid.Children.Add(button);

            DockPanel.SetDock(myGrid,Dock.Right);
            dockPanel.Children.Add(myGrid);

            dockPanel.Children.Add(textBlock);
            dockPanel.Children.Add(combo);
            return dockPanel;
        }

        private static Type ResolveDeviceType(PropertyInfo property, object obj)
        {
            var sourceTypeAttr = property.GetCustomAttribute<DeviceSourceTypeAttribute>();
            if (sourceTypeAttr?.DeviceType != null)
                return sourceTypeAttr.DeviceType;

            var nodeType = obj.GetType().GetProperty("NodeType")?.GetValue(obj)?.ToString();
            return nodeType?.ToUpperInvariant() switch
            {
                "ALGORITHM" => typeof(DeviceAlgorithm),
                "CALIBRATION" => typeof(DeviceCalibration),
                "CAMERA" => typeof(DeviceCamera),
                "FILTERWHEEL" => typeof(DeviceCfwPort),
                "MOTOR" => typeof(DeviceMotor),
                "PG" => typeof(DevicePG),
                "SENSOR" => typeof(DeviceSensor),
                "SMU" => typeof(DeviceSMU),
                "SPECTRUM" => typeof(DeviceSpectrum),
                "TPALGORITHMS" => typeof(DeviceThirdPartyAlgorithms),
                _ => typeof(DeviceService)
            };
        }

        private static void SetValueAndNotify(PropertyInfo property, object obj, string value)
        {
            var oldValue = property.GetValue(obj)?.ToString();
            if (oldValue == value)
                return;

            property.SetValue(obj, value);
            if (obj is CVCommonNode node)
                node.nodeEvent?.Invoke(node, new FlowEngineNodeEventArgs());
        }
    }
}
