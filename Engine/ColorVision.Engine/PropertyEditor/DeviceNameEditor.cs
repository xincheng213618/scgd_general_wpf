using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.Devices.Motor;
using ColorVision.Engine.Services.Devices.PG;
using ColorVision.Engine.Services.Devices.Sensor;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.UI;
using FlowEngineLib;
using FlowEngineLib.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.Engine.PropertyEditor
{
    [AttributeUsage(AttributeTargets.Property)]
    public class DeviceSourceTypeAttribute : Attribute
    {
        public Type DeviceType { get; }
        public string? LegacyReferenceProperty { get; set; }
        public DeviceSourceTypeAttribute(Type deviceType)
        {
            DeviceType = deviceType;
        }
    }

    public class DeviceNameEditor : IPropertyEditor
    {
        private readonly Func<Type, IEnumerable<DeviceService>> findDevices;

        public DeviceNameEditor() : this(type => ServiceManager.GetInstance().DeviceServices.Where(type.IsInstanceOfType))
        {
        }

        internal DeviceNameEditor(Func<Type, IEnumerable<DeviceService>> findDevices) => this.findDevices = findDevices;

        private sealed record DeviceChoice(DeviceService Device)
        {
            public string Code => Device.Code;
            public string Label => $"{Device.Name} ({Code})";
        }

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
            var source = property.GetCustomAttribute<DeviceSourceTypeAttribute>();
            Type? targetType = source?.DeviceType ?? ResolveDeviceType(obj);
            List<DeviceService> devices = targetType == null ? [] : findDevices(targetType).ToList();
            string? code = property.GetValue(obj)?.ToString();
            DeviceService? selectedDevice;
            string? displayedText = null;
            if (source?.LegacyReferenceProperty is { } legacyProperty)
            {
                combo.SetResourceReference(FrameworkElement.StyleProperty, "ComboBoxPlus.Small");
                string? legacy = obj.GetType().GetProperty(legacyProperty)?.GetValue(obj) as string;
                selectedDevice = ScheduledDeviceJobHelper.ResolveDevice(devices, code, legacy, out bool ambiguous);
                DeviceChoice[] choices = devices.Select(device => new DeviceChoice(device)).ToArray();
                combo.IsTextSearchEnabled = false;
                combo.DisplayMemberPath = nameof(DeviceChoice.Label);
                combo.ItemsSource = choices;
                var selected = choices.FirstOrDefault(choice => ReferenceEquals(choice.Device, selectedDevice));
                combo.SelectedItem = selected;
                displayedText = selected?.Label ?? code ?? legacy ?? string.Empty;
                if (selected == null) combo.Text = displayedText;
                if (ambiguous) combo.ToolTip = EngineLocalization.Get("Scheduler_DeviceReferenceAmbiguous");
            }
            else
            {
                combo.SetBinding(Selector.SelectedValueProperty, PropertyEditorHelper.CreateTwoWayBinding(obj, property));
                combo.ItemsSource = devices;
                selectedDevice = devices.FirstOrDefault(device => device.Code == code);
                if (selectedDevice != null) combo.SelectedItem = selectedDevice;
            }

            var button = new Button
            {
                DataContext = selectedDevice,
                ToolTip = Properties.Resources.Property
            };
            button.SetResourceReference(FrameworkElement.StyleProperty, "ButtonProperty");
            button.SetBinding(Button.CommandProperty, new Binding("PropertyCommand") { Mode = BindingMode.OneWay });

            combo.SelectionChanged += (s, e) =>
            {
                DeviceChoice? selected = combo.SelectedItem as DeviceChoice;
                button.DataContext = selected?.Device ?? combo.SelectedItem;
                if (combo.SelectedValue is string selectedCode)
                {
                    SetValueAndNotify(property, obj, selectedCode);
                    if (selected != null)
                    {
                        displayedText = selected.Label;
                        combo.ToolTip = null;
                    }
                }
            };

            combo.LostFocus += (s, e) => CommitText();
            if (displayedText != null)
            {
                combo.PreviewKeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter && !combo.IsDropDownOpen) CommitText();
                };
                CommandManager.AddPreviewExecutedHandler(combo, (s, e) =>
                {
                    if (e.Command != HandyControl.Interactivity.ControlCommands.Clear) return;
                    SaveCodeFromText(string.Empty);
                    e.Handled = true;
                });
            }

            void CommitText()
            {
                if (displayedText != null)
                {
                    string text = combo.Text ?? string.Empty;
                    if (string.Equals(text, displayedText, StringComparison.Ordinal)) return;
                    SaveCodeFromText(text);
                }
                else if (combo.SelectedValue is not string && !string.IsNullOrWhiteSpace(combo.Text))
                {
                    SetValueAndNotify(property, obj, combo.Text);
                }
            }

            void SaveCodeFromText(string text)
            {
                combo.SelectedIndex = -1;
                combo.Text = text;
                SetValueAndNotify(property, obj, string.IsNullOrWhiteSpace(text) ? string.Empty : text);
                displayedText = text;
                combo.ToolTip = null;
            }


            DockPanel.SetDock(button, Dock.Right);
            dockPanel.Children.Add(button);

            dockPanel.Children.Add(textBlock);
            dockPanel.Children.Add(combo);
            return dockPanel;
        }

        private static Type? ResolveDeviceType(object obj)
        {
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
                // Legacy nodes retain editable device codes without offering unrelated devices.
                "TPALGORITHMS" => null,
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
