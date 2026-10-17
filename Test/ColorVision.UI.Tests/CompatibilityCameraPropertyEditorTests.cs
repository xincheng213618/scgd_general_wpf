using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Editor.NodeConfiguration;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Jsons.AAFindPoints;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.POIFilters;
using ColorVision.Engine.Templates.POI.POIRevise;
using ColorVision.UI;
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
    [Fact]
    public void PoiCctWaveDisplaysFixedReadOnlyText() => WpfTestHost.Invoke(() =>
    {
        using var resources = new EditorResources();
        var node = new POINode();
        node.Create();
        node.IsCCTWave = false;
        PropertyInfo property = typeof(POINode).GetProperty(nameof(node.IsCCTWave))!;
        DockPanel panel = PropertyEditorHelper.GenProperties(property, node);
        Assert.False(panel.IsEnabled);
        var textBox = Assert.Single(panel.Children.OfType<TextBox>());
        textBox.GetBindingExpression(TextBox.TextProperty)!.UpdateTarget();
        Assert.Equal(bool.TrueString, textBox.Text);
        Assert.Equal(typeof(TextboxPropertiesEditor), property.GetCustomAttribute<PropertyEditorTypeAttribute>()!.EditorType);
    });

    [Fact]
    public void LightAreaAaEditorUpdatesThePersistedTemplateField() => WpfTestHost.Invoke(() =>
    {
        using var resources = new EditorResources();
        var template = new TemplateModel<TAAFindPointsParam>("editor-test-aa", new TAAFindPointsParam());
        TemplateAAFindPoints.Params.Add(template);
        try
        {
            var node = new FlowEngineLib.Node.Algorithm.AlgorithmFindLightAreaNode { TempName = "unavailable-template" };
            PropertyInfo property = node.GetType().GetProperty(nameof(node.TempName))!;
            var editor = (IPropertyEditor)Activator.CreateInstance(property.GetCustomAttribute<PropertyEditorTypeAttribute>()!.EditorType!)!;
            DockPanel panel = editor.GenProperties(property, node);
            try
            {
                Assert.Equal("unavailable-template", node.TempName);
                var grid = Assert.IsType<Grid>(Assert.Single(panel.Children));
                var combo = Assert.Single(grid.Children.OfType<HandyControl.Controls.ComboBox>());
                combo.SelectedItem = template;
                Assert.Equal(template.Key, node.TempName);
                combo.SelectedItem = null;
                Assert.Equal(string.Empty, node.TempName);
            }
            finally { panel.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
        }
        finally { TemplateAAFindPoints.Params.Remove(template); }
    });

    [Theory]
    [InlineData(nameof(POINode.TempName))]
    [InlineData(nameof(POINode.FilterTemplateName))]
    [InlineData(nameof(POINode.ReviseTemplateName))]
    public void DirectPoiEditorsPreserveAndUpdateTheNodeProperty(string propertyName) => WpfTestHost.Invoke(() =>
    {
        using var resources = new EditorResources();
        (IList Templates, TemplateBase Added) data = propertyName switch
        {
            nameof(POINode.TempName) => (TemplatePoi.Params, new TemplateModel<PoiParam>("editor-test-poi", new PoiParam())),
            nameof(POINode.FilterTemplateName) => (TemplatePoiFilterParam.Params, new TemplateModel<PoiFilterParam>("editor-test-filter", new PoiFilterParam())),
            _ => (TemplatePoiReviseParam.Params, new TemplateModel<PoiReviseParam>("editor-test-revise", new PoiReviseParam()))
        };
        var (templates, added) = data;
        templates.Add(added);
        try
        {
            var node = new POINode();
            node.Create();
            PropertyInfo property = node.GetType().GetProperty(propertyName)!;
            var direct = (IPropertyEditor)Activator.CreateInstance(property.GetCustomAttribute<PropertyEditorTypeAttribute>()!.EditorType!)!;
            VerifySelection(direct, node, property, added);

        }
        finally { templates.Remove(added); }
    });

    [Fact]
    public void DirectCalibrationEditorDisplaysAndCanClearUnavailableTemplateWithoutADevice() => WpfTestHost.Invoke(() =>
    {
        using var resources = new EditorResources();
        var node = new LVCameraNode { DeviceCode = string.Empty, CaliTempName = "unavailable-calibration" };
        DockPanel panel = new CalibrationTemplatePropertiesEditor().GenProperties(typeof(LVCameraNode).GetProperty(nameof(LVCameraNode.CaliTempName))!, node);
        try
        {
            var grid = Assert.IsType<Grid>(Assert.Single(panel.Children));
            var combo = Assert.Single(grid.Children.OfType<HandyControl.Controls.ComboBox>());
            var missing = Assert.IsType<TemplateSelectionDisplay.MissingTemplate>(combo.SelectedItem);
            Assert.Equal(EngineLocalization.Format($"找不到模板：{node.CaliTempName}"), combo.Text);
            Assert.Equal(missing.Key, combo.ToolTip);
            Assert.Equal("unavailable-calibration", node.CaliTempName);
            Assert.All(grid.Children.OfType<Button>(), button => Assert.False(button.IsEnabled));
            combo.SelectedItem = null;
            Assert.Equal(string.Empty, node.CaliTempName);
        }
        finally { panel.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
    });

    [Fact]
    public void MultiTemplateEditorDisplaysMissingReferenceAndPersistsOnlyTheName() => WpfTestHost.Invoke(() =>
    {
        using var resources = new EditorResources();
        var node = new POINode();
        node.Create();
        node.TempName = "unavailable-template";
        PropertyInfo property = typeof(POINode).GetProperty(nameof(node.TempName))!;
        DockPanel panel = FlowNodePropertyEditorRegistration.CreateMultiTemplateEditor(property, node, new TemplatePoi(), new TemplatePoiFilterParam());
        try
        {
            var grid = Assert.IsType<Grid>(Assert.Single(panel.Children));
            var combo = Assert.Single(grid.Children.OfType<HandyControl.Controls.ComboBox>());
            var missing = Assert.IsType<TemplateSelectionDisplay.MissingTemplate>(combo.SelectedItem);
            Assert.Equal(missing.Key, combo.Text);
            Assert.Equal("unavailable-template", node.TempName);
            Assert.DoesNotContain(TemplatePoi.Params, item => item.Key.Contains("找不到模板", StringComparison.Ordinal));
            combo.SelectedItem = null;
            Assert.Equal(string.Empty, node.TempName);
            combo.SelectedItem = missing;
            Assert.Equal("unavailable-template", node.TempName);
        }
        finally { panel.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
    });

    [Fact]
    public void SupplementalTemplatePanelDisplaysMissingReferenceAndTracksPropertyChanges() => WpfTestHost.Invoke(() =>
    {
        using var resources = new EditorResources();
        var node = new POINode();
        node.Create();
        node.TempName = "unavailable-template";
        var template = new TemplateModel<PoiParam>("available-template", new PoiParam());
        var templates = new System.Collections.ObjectModel.ObservableCollection<TemplateModel<PoiParam>> { template };
        var context = new NodeConfiguratorContext { Node = node, SignStackPanel = new StackPanel() };
        context.AddTemplateCollectionPanel(nameof(node.TempName), "POI", templates);
        var panel = Assert.IsType<DockPanel>(Assert.Single(context.SignStackPanel.Children));
        var combo = Assert.Single(panel.Children.OfType<HandyControl.Controls.ComboBox>());
        try
        {
            Assert.Equal("unavailable-template", node.TempName);
            Assert.Equal(EngineLocalization.Format($"找不到模板：{node.TempName}"), combo.Text);
            Assert.Single(templates);
            combo.SelectedItem = template;
            Assert.Equal("available-template", node.TempName);
            Assert.Null(combo.ToolTip);
            node.TempName = "another-missing-template";
            Assert.Equal(EngineLocalization.Format($"找不到模板：{node.TempName}"), combo.Text);
            combo.SelectedItem = null;
            Assert.Equal(string.Empty, node.TempName);
            Assert.Equal(string.Empty, combo.Text);
        }
        finally { combo.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
    });

    private static void VerifySelection(IPropertyEditor editor, POINode node, PropertyInfo property, TemplateBase template)
    {
        property.SetValue(node, "unavailable-template");
        DockPanel panel = editor.GenProperties(property, node);
        try
        {
            Assert.Equal("unavailable-template", property.GetValue(node));
            var grid = Assert.IsType<Grid>(Assert.Single(panel.Children));
            var combo = Assert.Single(grid.Children.OfType<HandyControl.Controls.ComboBox>());
            var missing = Assert.IsType<TemplateSelectionDisplay.MissingTemplate>(combo.SelectedItem);
            Assert.Equal(missing.Key, combo.Text);
            Assert.Contains(template, combo.Items.Cast<object>());
            combo.SelectedItem = template;
            Assert.Equal(template.Key, property.GetValue(node));
            Assert.Null(combo.ToolTip);
            combo.SelectedItem = missing;
            Assert.Equal("unavailable-template", property.GetValue(node));
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
