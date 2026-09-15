using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.POIFilters;
using ColorVision.Engine.Templates.POI.POIRevise;
using FlowEngineLib;
using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public sealed class CompatibilityCameraPropertyEditorTests
{
    [Theory]
    [InlineData(nameof(LVCameraNode.POITempName))]
    [InlineData(nameof(LVCameraNode.POIFilterTempName))]
    [InlineData(nameof(LVCameraNode.POIReviseTempName))]
    public void DirectPoiEditorsPreserveAndUpdateTheNodeProperty(string propertyName) => WpfTestHost.Invoke(() =>
    {
        using var resources = new EditorResources();
        (IList Templates, TemplateBase Added) data = propertyName switch
        {
            nameof(LVCameraNode.POITempName) => (TemplatePoi.Params, new TemplateModel<PoiParam>("editor-test-poi", new PoiParam())),
            nameof(LVCameraNode.POIFilterTempName) => (TemplatePoiFilterParam.Params, new TemplateModel<PoiFilterParam>("editor-test-filter", new PoiFilterParam())),
            _ => (TemplatePoiReviseParam.Params, new TemplateModel<PoiReviseParam>("editor-test-revise", new PoiReviseParam()))
        };
        var (templates, added) = data;
        templates.Add(added);
        try
        {
            var node = new LVCameraNode();
            PropertyInfo property = node.GetType().GetProperty(propertyName)!;
            var direct = (IPropertyEditor)Activator.CreateInstance(property.GetCustomAttribute<PropertyEditorTypeAttribute>()!.EditorType!)!;
            VerifySelection(direct, node, property, added);

        }
        finally { templates.Remove(added); }
    });

    [Fact]
    public void DirectCalibrationEditorPreservesUnavailableTemplateWithoutADevice() => WpfTestHost.Invoke(() =>
    {
        using var resources = new EditorResources();
        var node = new LVCameraNode { DeviceCode = string.Empty, CaliTempName = "unavailable-calibration" };
        DockPanel panel = new CalibrationTemplatePropertiesEditor().GenProperties(typeof(LVCameraNode).GetProperty(nameof(LVCameraNode.CaliTempName))!, node);
        try
        {
            var grid = Assert.IsType<Grid>(Assert.Single(panel.Children));
            var combo = Assert.Single(grid.Children.OfType<HandyControl.Controls.ComboBox>());
            Assert.Empty(combo.Items);
            Assert.Equal("unavailable-calibration", node.CaliTempName);
        }
        finally { panel.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
    });

    private static void VerifySelection(IPropertyEditor editor, LVCameraNode node, PropertyInfo property, TemplateBase template)
    {
        property.SetValue(node, "unavailable-template");
        DockPanel panel = editor.GenProperties(property, node);
        try
        {
            Assert.Equal("unavailable-template", property.GetValue(node));
            var grid = Assert.IsType<Grid>(Assert.Single(panel.Children));
            var combo = Assert.Single(grid.Children.OfType<HandyControl.Controls.ComboBox>());
            Assert.Contains(template, combo.Items.Cast<object>());
            combo.SelectedItem = template;
            Assert.Equal(template.Key, property.GetValue(node));
            combo.SelectedItem = null;
            Assert.Equal(string.Empty, property.GetValue(node));
        }
        finally { panel.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
    }

    private sealed class EditorResources : IDisposable
    {
        private readonly Dictionary<string, object> replacements = new()
        {
            ["GlobalTextBrush"] = Brushes.Black,
            ["GlobalBorderBrush"] = Brushes.Gray,
            ["BorderBrush"] = Brushes.Gray,
            ["ButtonCommand"] = new Style(typeof(Button)),
            ["ComboBox.Small"] = new Style(typeof(ComboBox)),
            ["TextBox.Small"] = new Style(typeof(TextBox)),
            ["bool2VisibilityConverter"] = new BooleanToVisibilityConverter(),
            ["ComboBoxPlus.Small"] = new Style(typeof(HandyControl.Controls.ComboBox)),
            ["DrawingImageEdit"] = new DrawingImage()
        };
        private readonly Dictionary<string, object?> previous = new();

        public EditorResources()
        {
            foreach (var item in replacements)
            {
                if (Application.Current.Resources.Contains(item.Key))
                    previous[item.Key] = Application.Current.Resources[item.Key];
                Application.Current.Resources[item.Key] = item.Value;
            }
        }

        public void Dispose()
        {
            foreach (string key in replacements.Keys)
            {
                if (previous.TryGetValue(key, out object? value)) Application.Current.Resources[key] = value;
                else Application.Current.Resources.Remove(key);
            }
        }
    }
}
