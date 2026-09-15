using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.PhyCameras.Group;
using FlowEngineLib;
using FlowEngineLib.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.PropertyEditor
{
    public sealed class CameraCalibrationGainPropertiesEditor : IPropertyEditor
    {
        private const string GainPropertyName = "Gain";
        private const string CalibrationPropertyName = "CaliTempName";
        private const string AlternateCalibrationPropertyName = "CalibTempName";

        internal static bool IsSupported(PropertyInfo property)
        {
            Type? nodeType = property.ReflectedType ?? property.DeclaringType;
            PropertyInfo? calibrationProperty = nodeType == null ? null : GetCalibrationProperty(nodeType);
            return nodeType != null
                && property.Name == GainPropertyName
                && property.PropertyType == typeof(float)
                && property.CanWrite
                && calibrationProperty != null
                && calibrationProperty.GetCustomAttribute<PropertyEditorTypeAttribute>()?.EditorType == typeof(CalibrationTemplatePropertiesEditor)
                && nodeType.GetProperty(nameof(CVCommonNode.DeviceCode))?.PropertyType == typeof(string);
        }

        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            DockPanel panel = new TextboxPropertiesEditor().GenProperties(property, obj);
            ToolTipService.SetShowOnDisabled(panel, true);

            PropertyInfo? calibrationProperty = GetCalibrationProperty(obj.GetType());
            bool isRefreshing = false;

            void Refresh()
            {
                if (isRefreshing)
                    return;

                isRefreshing = true;
                try
                {
                    bool isControlled = TrySynchronizeCurrentSelection(property, obj, calibrationProperty, out string hint);
                    panel.IsEnabled = !isControlled;
                    panel.ToolTip = isControlled ? hint : null;
                }
                finally
                {
                    isRefreshing = false;
                }
            }

            if (obj is INotifyPropertyChanged notifyPropertyChanged)
            {
                bool isSubscribed = true;
                PropertyChangedEventHandler sourceChanged = (_, args) =>
                {
                    if (string.IsNullOrEmpty(args.PropertyName)
                        || args.PropertyName == GainPropertyName
                        || args.PropertyName == calibrationProperty?.Name
                        || args.PropertyName == nameof(CVCommonNode.DeviceCode))
                    {
                        Refresh();
                    }
                };

                notifyPropertyChanged.PropertyChanged += sourceChanged;
                panel.Unloaded += (_, _) =>
                {
                    if (!isSubscribed)
                        return;

                    notifyPropertyChanged.PropertyChanged -= sourceChanged;
                    isSubscribed = false;
                };
                panel.Loaded += (_, _) =>
                {
                    if (!isSubscribed)
                    {
                        notifyPropertyChanged.PropertyChanged += sourceChanged;
                        isSubscribed = true;
                    }
                    Refresh();
                };
            }

            Refresh();
            return panel;
        }

        internal static bool Synchronize(PropertyInfo gainProperty, object obj, CalibrationParam? calibration, IEnumerable<GroupResource> groups, out string hint)
        {
            if (!CalibrationGroupGainResolver.TryResolve(calibration, groups, out float gain, out string groupName))
            {
                hint = string.Empty;
                return false;
            }

            hint = CalibrationGroupGainResolver.CreateHint(groupName, gain);
            ApplyGain(gainProperty, obj, gain);
            return true;
        }

        private static bool TrySynchronizeCurrentSelection(PropertyInfo gainProperty, object obj, PropertyInfo? calibrationProperty, out string hint)
        {
            hint = string.Empty;
            string calibrationTemplateName = calibrationProperty?.GetValue(obj)?.ToString() ?? string.Empty;
            string deviceCode = obj.GetType().GetProperty(nameof(CVCommonNode.DeviceCode))?.GetValue(obj)?.ToString() ?? string.Empty;
            DeviceCamera? device = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>()
                .FirstOrDefault(camera => string.Equals(camera.Code, deviceCode, StringComparison.Ordinal));
            if (!CalibrationGroupGainResolver.TryResolve(device, calibrationTemplateName, out float gain, out string groupName))
                return false;

            hint = CalibrationGroupGainResolver.CreateHint(groupName, gain);
            ApplyGain(gainProperty, obj, gain);
            return true;
        }

        private static void ApplyGain(PropertyInfo gainProperty, object obj, float gain)
        {
            if (gainProperty.GetValue(obj) is not float currentGain || currentGain != gain)
            {
                gainProperty.SetValue(obj, gain);
                if (obj is CVCommonNode node)
                    node.nodeEvent?.Invoke(node, new FlowEngineNodeEventArgs());
            }
        }

        private static PropertyInfo? GetCalibrationProperty(Type nodeType)
        {
            return nodeType.GetProperty(CalibrationPropertyName)
                ?? nodeType.GetProperty(AlternateCalibrationPropertyName);
        }
    }
}
