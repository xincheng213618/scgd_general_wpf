using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Editor.NodeConfiguration;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Jsons.AAFindPoints;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.POIFilters;
using ColorVision.Engine.Templates.POI.POIRevise;
using ColorVision.UI;
using FlowEngineLib;
using FlowEngineLib.Node.Camera;
using FlowEngineLib.Node.POI;
using Newtonsoft.Json.Linq;
using ST.Library.UI.NodeEditor;
using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Text;
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
    public void SettingsFormUsesRegisteredEditorsForUnspecifiedEditorTypes() => WpfTestHost.Invoke(() =>
    {
        using var resources = new EditorResources();
        var settings = new PrimitiveSettings();
        var panel = Assert.IsType<StackPanel>(SettingsPropertyPresenter.Create(settings));

        var toggle = GetSettingsControl<ColorVision.Themes.Controls.ToggleSwitch>(panel, nameof(settings.Enabled));
        toggle.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true);
        Assert.True(settings.Enabled);

        var choices = GetSettingsControl<ComboBox>(panel, nameof(settings.Day));
        choices.SetCurrentValue(ComboBox.SelectedValueProperty, DayOfWeek.Friday);
        Assert.Equal(DayOfWeek.Friday, settings.Day);

        var attempts = GetSettingsControl<TextBox>(panel, nameof(settings.Attempts));
        attempts.SetCurrentValue(TextBox.TextProperty, "1.5");
        Assert.True(Validation.GetHasError(attempts));
        Assert.Equal(3, settings.Attempts);
        attempts.SetCurrentValue(TextBox.TextProperty, "5");
        Assert.False(Validation.GetHasError(attempts));
        Assert.Equal(5, settings.Attempts);
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
    [InlineData(typeof(POINode), nameof(POINode.TempName))]
    [InlineData(typeof(POINode), nameof(POINode.FilterTemplateName))]
    [InlineData(typeof(POINode), nameof(POINode.ReviseTemplateName))]
    [InlineData(typeof(POIReviseNode), nameof(POIReviseNode.Output))]
    public void DirectPoiEditorsPreserveAndUpdateTheNodeProperty(Type nodeType, string propertyName) => WpfTestHost.Invoke(() =>
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
            var node = (STNode)Activator.CreateInstance(nodeType)!;
            node.Create();
            PropertyInfo property = node.GetType().GetProperty(propertyName)!;
            var direct = (IPropertyEditor)Activator.CreateInstance(property.GetCustomAttribute<PropertyEditorTypeAttribute>()!.EditorType!)!;
            VerifySelection(direct, node, property, added);

        }
        finally { templates.Remove(added); }
    });

    [Fact]
    public void CommonCameraPropertyRowEditsTheOrdinaryTemplate() => WpfTestHost.Invoke(() =>
    {
        using var resources = new EditorResources();
        var originalTemplates = TemplateCameraRunParam.Params;
        var template = new TemplateModel<CameraRunParam>("editor-test-camera", new CameraRunParam());
        TemplateCameraRunParam.Params = [template];
        try
        {
            var node = new CommCameraNode();
            node.Create();
            PropertyInfo property = typeof(CommCameraNode).GetProperty(nameof(CommCameraNode.CamTempName))!;
            Assert.Equal(typeof(CameraRunTemplatePropertiesEditor), property.GetCustomAttribute<PropertyEditorTypeAttribute>()!.EditorType);
            Assert.Null(NodeConfiguratorRegistry.GetConfigurator(typeof(CommCameraNode)));
            VerifySelection(new CameraRunTemplatePropertiesEditor(), node, property, template);
        }
        finally { TemplateCameraRunParam.Params = originalTemplates; }
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
    public void LocalCameraStoresInlineAutoExposureJsonAndIgnoresLegacyTemplateNames() => WpfTestHost.Invoke(() =>
    {
        using var resources = new EditorResources();
        var node = new LocalCameraNode();
        node.Create();
        node.OnLoadNode(new Dictionary<string, byte[]>
        {
            [nameof(LocalCameraNode.IsAutoExp)] = Encoding.UTF8.GetBytes(bool.TrueString),
            ["AutoExpTempName"] = Encoding.UTF8.GetBytes("old-exposure-template")
        });
        Assert.Empty(node.AutoExposureJson);
        Assert.True(node.IsAutoExp);

        PropertyInfo property = typeof(LocalCameraNode).GetProperty(nameof(LocalCameraNode.AutoExposureJson))!;
        Assert.Equal(typeof(LocalAutoExposurePropertiesEditor), property.GetCustomAttribute<PropertyEditorTypeAttribute>()!.EditorType);
        DockPanel panel = PropertyEditorHelper.GenProperties(property, node);
        Assert.Equal(Visibility.Visible, panel.Visibility);
        Assert.Single(panel.Children.OfType<Button>());
        Assert.Empty(panel.Children.OfType<TextBox>());
        Assert.Equal(EngineLocalization.Get("跟随设备"), panel.Children.OfType<TextBlock>().Last().Text);
        node.AutoExposureJson = "{\"expTimeCfg\":{\"autoExpSaturation\":70}}";
        Assert.Equal(EngineLocalization.Get("配置有误"), panel.Children.OfType<TextBlock>().Last().Text);
        node.IsAutoExp = false;
        Assert.Equal(Visibility.Collapsed, panel.Visibility);

        var saved = STNodeSavedState.Parse(node.GetSaveData()).Properties;
        Assert.DoesNotContain("AutoExpTempName", saved.Keys);
        var reloaded = new LocalCameraNode();
        reloaded.Create();
        reloaded.OnLoadNode(saved);
        Assert.Equal(node.AutoExposureJson, reloaded.AutoExposureJson);
    });

    [Fact]
    public void LocalCameraExposureFormKeepsDeviceInheritanceAndRoundTripsSdkOptions() => WpfTestHost.Invoke(() =>
    {
        using var resources = new EditorResources();
        var defaults = new PhyExpTimeCfg { AutoExpTimeBegin = 15 };
        var inherited = new LocalAutoExposureConfigurationWindow("", defaults);
        Assert.True(((RadioButton)inherited.FindName("FormMode")).IsChecked);
        Assert.True(inherited.TryGetConfiguration(out string json));
        Assert.Empty(json);
        ((RadioButton)inherited.FindName("JsonMode")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var jsonEditor = Assert.IsType<ICSharpCode.AvalonEdit.TextEditor>(inherited.FindName("JsonText"));
        Assert.Equal(15, (int?)JObject.Parse(jsonEditor.Text)["expTimeCfg"]!["autoExpTimeBegin"]);
        Assert.True(inherited.TryGetConfiguration(out json));
        Assert.Empty(json);

        var settings = JObject.Parse(LocalCameraAutoExposure.BuildConfiguration(null, defaults));
        settings["expTimeCfg"]!["type"] = 1;
        settings["expTimeCfg"]!["autoExpFlag"] = false;
        settings["expTimeCfg"]!["autoExpMaxPecentage"] = 1e-20;
        var custom = new LocalAutoExposureConfigurationWindow(settings.ToString(), defaults);
        var form = Assert.IsType<StackPanel>(((ContentControl)custom.FindName("BasicForm")).Content);
        var advanced = Assert.IsType<StackPanel>(((ContentControl)custom.FindName("AdvancedForm")).Content);
        Assert.Equal("1E-20", GetSettingsControl<TextBox>(advanced, nameof(LocalAutoExposureConfigurationDraft.MaxPercentage)).Text);
        var saturation = GetSettingsControl<TextBox>(form, nameof(LocalAutoExposureConfigurationDraft.Saturation));
        saturation.SetCurrentValue(TextBox.TextProperty, "bad");
        Assert.False(custom.TryGetConfiguration(out _));
        saturation.SetCurrentValue(TextBox.TextProperty, "82.5");
        Assert.True(custom.TryGetConfiguration(out json));
        Assert.Equal(82.5, (double?)JObject.Parse(json)["expTimeCfg"]!["autoExpSaturation"]);
        Assert.Equal(1, (int?)JObject.Parse(json)["expTimeCfg"]!["type"]);
        Assert.False((bool?)JObject.Parse(json)["expTimeCfg"]!["autoExpFlag"]);
        Assert.Equal(1e-20, (double?)JObject.Parse(json)["expTimeCfg"]!["autoExpMaxPecentage"]);
        var minimum = GetSettingsControl<TextBox>(form, nameof(LocalAutoExposureConfigurationDraft.MinTime));
        minimum.SetCurrentValue(TextBox.TextProperty, "0");
        Assert.False(custom.TryGetConfiguration(out _));

        var invalid = new LocalAutoExposureConfigurationWindow("{}", defaults);
        Assert.True(((RadioButton)invalid.FindName("JsonMode")).IsChecked);
        Assert.False(invalid.TryGetConfiguration(out _));
        ((Button)invalid.FindName("UseDeviceButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.True(((RadioButton)invalid.FindName("FormMode")).IsChecked);
        Assert.True(invalid.TryGetConfiguration(out json));
        Assert.Empty(json);
    });

    [Fact]
    public void CommonCameraAutoExposureSwitchShowsTemplateEditorAndKeepsItsSelection() => WpfTestHost.Invoke(() =>
    {
        using var resources = new EditorResources();
        var template = new TemplateModel<AutoExpTimeParam>("editor-test-auto-exposure", new AutoExpTimeParam { Name = "editor-test-auto-exposure" });
        TemplateAutoExpTime.Params.Add(template);
        try
        {
            var node = new CommCameraNode();
            var property = typeof(CommCameraNode).GetProperty(nameof(CommCameraNode.TempName))!;
            DockPanel panel = PropertyEditorHelper.GenProperties(property, node);
            Assert.Equal(Visibility.Collapsed, panel.Visibility);
            node.IsAutoExp = true;
            Assert.Equal(Visibility.Visible, panel.Visibility);

            var grid = Assert.IsType<Grid>(Assert.Single(panel.Children));
            var combo = Assert.Single(grid.Children.OfType<HandyControl.Controls.ComboBox>());
            Assert.Single(grid.Children.OfType<Button>());
            combo.SelectedItem = template;
            Assert.Equal(template.Key, property.GetValue(node));

            node.IsAutoExp = false;
            Assert.Equal(Visibility.Collapsed, panel.Visibility);
            Assert.Equal(template.Key, property.GetValue(node));
        }
        finally { TemplateAutoExpTime.Params.Remove(template); }
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

    private static void VerifySelection(IPropertyEditor editor, STNode node, PropertyInfo property, TemplateBase template)
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

    private static T GetSettingsControl<T>(StackPanel form, string propertyName) where T : UIElement
    {
        var row = form.Children.OfType<Border>().Select(b => b.Child).OfType<Grid>()
            .SelectMany(g => g.Children.OfType<DockPanel>())
            .Single(p => p.Tag is PropertyInfo property && property.Name == propertyName);
        return Assert.Single(row.Children.OfType<T>());
    }

    private sealed class PrimitiveSettings
    {
        [PropertyEditorType] public bool Enabled { get; set; }
        [PropertyEditorType] public DayOfWeek Day { get; set; }
        [PropertyEditorType] public int Attempts { get; set; } = 3;
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
