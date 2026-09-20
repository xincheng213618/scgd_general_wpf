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
using ColorVision.Engine.FlowProcessing.Nodes;
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
            combo.SetBinding(Selector.SelectedValueProperty, PropertyEditorHelper.CreateTwoWayBinding(obj, property));


            Type targetType = ResolveDeviceType(property, obj);

            var ItemsSource = ServiceManager.GetInstance().DeviceServices.Where(d => targetType.IsInstanceOfType(d)).ToList();

            combo.ItemsSource = ItemsSource;


            string? code = property.GetValue(obj)?.ToString();
            var selectedItem = ItemsSource.FirstOrDefault(x => x.Code == code);
            if (selectedItem != null)
                combo.SelectedItem = selectedItem;

            var button = new Button
            {
                DataContext = selectedItem,
                ToolTip = Properties.Resources.Property
            };
            button.SetResourceReference(FrameworkElement.StyleProperty, "ButtonProperty");
            button.SetBinding(Button.CommandProperty, new Binding("PropertyCommand") { Mode = BindingMode.OneWay });

            combo.SelectionChanged += (s, e) =>
            {
                button.DataContext = combo.SelectedItem;
                if (combo.SelectedValue is string selectedCode)
                    SetValueAndNotify(property, obj, selectedCode);
            };

            combo.LostFocus += (s, e) =>
            {
                if (combo.SelectedValue is not string && !string.IsNullOrWhiteSpace(combo.Text))
                    SetValueAndNotify(property, obj, combo.Text);
            };


            DockPanel.SetDock(button, Dock.Right);
            dockPanel.Children.Add(button);

            dockPanel.Children.Add(textBlock);
            dockPanel.Children.Add(combo);
            return dockPanel;
        }

        private static Type ResolveDeviceType(PropertyInfo property, object obj)
        {
            var sourceTypeAttr = property.GetCustomAttribute<DeviceSourceTypeAttribute>();
            if (sourceTypeAttr?.DeviceType != null)
                return sourceTypeAttr.DeviceType;

            if (IsLocalCameraNode(obj))
                return typeof(DeviceCamera);

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

        private static bool IsLocalCameraNode(object obj)
            => obj is LocalCameraNode or LocalCalibrationNodeBase;

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
