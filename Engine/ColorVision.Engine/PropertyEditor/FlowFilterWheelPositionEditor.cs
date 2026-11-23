using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.UI;
using FlowEngineLib;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.PropertyEditor;

public sealed class FlowFilterWheelPositionEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj)
    {
        var panel = new TextboxPropertiesEditor().GenProperties(property, obj);
        if (obj is not FWNode node) return panel;

        int last = panel.Children.Count - 1;
        UIElement textEditor = panel.Children[last];
        panel.Children.RemoveAt(last);
        var combo = new ComboBox
        {
            Style = PropertyEditorHelper.ComboBoxSmallStyle,
            Margin = new Thickness(5, 0, 0, 0),
            MinWidth = PropertyEditorHelper.ControlMinWidth,
            DisplayMemberPath = nameof(PositionChoice.Label),
            SelectedValuePath = nameof(PositionChoice.Index)
        };
        panel.Children.Add(new Grid { Children = { textEditor, combo } });

        bool refreshing = false;
        void Refresh()
        {
            if (!panel.IsVisible) return;
            var (holes, current) = ResolveHoles(node);
            var choices = holes.DistinctBy(hole => hole.HoleIndex)
                .Select(hole => new PositionChoice(hole.HoleIndex, hole.HoleIndex == current
                    ? EngineLocalization.Format($"{hole.HoleName}（当前）") : hole.HoleName))
                .ToList();
            refreshing = true;
            try
            {
                bool hasMapping = choices.Count != 0;
                textEditor.Visibility = hasMapping ? Visibility.Collapsed : Visibility.Visible;
                combo.Visibility = hasMapping ? Visibility.Visible : Visibility.Collapsed;
                if (hasMapping && choices.All(choice => choice.Index != node.Port))
                    choices.Add(new PositionChoice(node.Port, node.Port.ToString(CultureInfo.CurrentCulture)));
                combo.ItemsSource = choices;
                combo.SelectedValue = node.Port;
            }
            finally { refreshing = false; }
        }

        combo.SelectionChanged += (_, _) =>
        {
            if (refreshing || combo.SelectedValue is not int position || node.Port == position) return;
            node.Port = position;
            node.nodeEvent?.Invoke(node, new FlowEngineNodeEventArgs());
        };
        combo.DropDownOpened += (_, _) => Refresh();
        panel.IsVisibleChanged += (_, _) => Refresh();
        PropertyChangedEventManager.AddHandler(node, (_, args) =>
        {
            if (args.PropertyName is nameof(FWNode.DeviceCode) or nameof(FWNode.ModelType)
                || (args.PropertyName == nameof(FWNode.Port) && combo.SelectedValue as int? != node.Port))
                Refresh();
        }, string.Empty);
        return panel;
    }

    private static (IEnumerable<HoleMap> Holes, int? Current) ResolveHoles(FWNode node)
    {
        var device = ServiceManager.Current?.DeviceServices.FirstOrDefault(item => item.Code == node.DeviceCode);
        return (node.ModelType, device) switch
        {
            (FWModelType.FilterWheel, DeviceCfwPort wheel) => (wheel.FilterWheelConfig.HoleMapping, wheel.ConfirmedPort),
            (FWModelType.Spectrum_FilterWheel, DeviceSpectrum spectrum) => (spectrum.GetNDHoleMappings(),
                spectrum.Config.NDConfig.IsBingNDDevice ? spectrum.GetBoundCfwPort()?.ConfirmedPort : null),
            (FWModelType.Camera_FilterWheel, DeviceCamera { PhyCamera: { } camera }) => (camera.Config.FilterWheelConfig.HoleMapping, null),
            _ => ([], null)
        };
    }

    private sealed record PositionChoice(int Index, string Label);
}
