using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.UI;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.PropertyEditor;

public sealed class LocalAutoExposurePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj)
    {
        var panel = new DockPanel();
        panel.Children.Add(PropertyEditorHelper.CreateLabel(property, PropertyEditorHelper.GetResourceManager(obj)));
        var button = new Button
        {
            Content = EngineLocalization.Get("编辑…"),
            Padding = new Thickness(10, 2, 10, 2),
            Margin = new Thickness(8, 0, 0, 0)
        };
        DockPanel.SetDock(button, Dock.Right);
        panel.Children.Add(button);
        var summary = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        summary.SetBinding(TextBlock.TextProperty, new Binding(property.Name) { Source = obj, Converter = new SummaryConverter() });
        summary.SetBinding(FrameworkElement.ToolTipProperty, new Binding(nameof(TextBlock.Text)) { Source = summary });
        panel.Children.Add(summary);
        button.Click += (_, _) =>
        {
            var node = (LocalCameraNode)obj;
            var device = ServiceManager.Current?.DeviceServices.OfType<DeviceCamera>()
                .FirstOrDefault(camera => string.Equals(camera.Code, node.DeviceCode, StringComparison.Ordinal));
            var owner = Window.GetWindow(panel);
            var window = new LocalAutoExposureConfigurationWindow(node.AutoExposureJson, device?.DisplayConfig.LocalAutoExposureConfig)
            {
                Owner = owner,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner
            };
            if (window.ShowDialog() == true) node.AutoExposureJson = window.ResultJson;
        };
        return panel;
    }

    private sealed class SummaryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? json = value as string;
            if (string.IsNullOrWhiteSpace(json)) return EngineLocalization.Get("跟随设备");
            if (!LocalAutoExposureConfigurationDraft.TryCreate(json, out var draft, out _) || !draft.TryGetJson(out _, out _))
                return EngineLocalization.Get("配置有误");
            return EngineLocalization.Format($"{draft.Saturation}% · {draft.StartTime} ms 起");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
