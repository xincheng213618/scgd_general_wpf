using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.FlowProcessing.Editor.NodeConfiguration;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.DetectScreenDefects;
using ColorVision.Engine.Templates.Jsons.FindCross;
using ColorVision.Engine.Templates.Jsons.Ghost2;
using ColorVision.Engine.Templates.Jsons.LedCheck2;
using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.Engine.Templates.Jsons.SFR2;
using ColorVision.Engine.Templates.LedCheck;
using ColorVision.Themes;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Node.Algorithm;
using FlowEngineLib.Node.OLED;
using ST.Library.UI.NodeEditor;
using System.Collections;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public sealed class AlgorithmNodePropertyEditorTests
{
    [Theory]
    [InlineData(typeof(AlgorithmNode), AlgorithmType.MTF)]
    [InlineData(typeof(Algorithm2InNode), Algorithm2Type.十字计算)]
    [InlineData(typeof(AlgorithmARVRNode), AlgorithmARVRType.屏幕缺陷检测)]
    [InlineData(typeof(AlgorithmGhostV2Node), null)]
    [InlineData(typeof(AlgorithmFindLEDNode), null)]
    public void ParameterTemplateIsEditedInItsPropertyRow(Type nodeType, object? algorithm) => WithEditorTheme(() =>
    {
        var node = (STNode)Activator.CreateInstance(nodeType)!;
        node.Create();
        if (algorithm != null)
            nodeType.GetProperty(nameof(AlgorithmNode.Algorithm))!.SetValue(node, algorithm);

        const string templateName = "editor-test-algorithm";
        (IList Collection, TemplateBase Sample) templateEntry = node switch
        {
            AlgorithmNode => (TemplateMTF2.Params, new TemplateModel<TJMTFParam>(templateName, new())),
            Algorithm2InNode => (TemplateFindCross.Params, new TemplateModel<TJFindCrossParam>(templateName, new())),
            AlgorithmARVRNode => (TemplateDetectScreenDefects.Params, new TemplateModel<TemplateJsonParam>(templateName, new())),
            AlgorithmGhostV2Node => (TemplateGhostQK.Params, new TemplateModel<TemplateJsonGhost2>(templateName, new())),
            AlgorithmFindLEDNode => (TemplateLedCheck2.Params, new TemplateModel<TemplateJsonParam>(templateName, new())),
            _ => throw new InvalidOperationException(nodeType.FullName)
        };
        var (collection, sample) = templateEntry;

        collection.Add(sample);
        var legacyPixel = node is AlgorithmFindLEDNode
            ? new TemplateModel<LedCheckParam>("editor-test-pixel-led", new()) : null;
        if (legacyPixel != null)
            TemplateLedCheck.Params.Add(legacyPixel);
        DockPanel? row = null;
        try
        {
            const string savedName = "unavailable-algorithm-template";
            PropertyInfo template = nodeType.GetProperty(nameof(AlgorithmNode.TempName))!;
            template.SetValue(node, savedName);
            Assert.Null(NodeConfiguratorRegistry.GetConfigurator(nodeType));
            StackPanel propertyEditor = GenerateInspectorProperties(node);
            row = GetPropertyRow(propertyEditor, template.Name);
            ComboBox combo = GetSelector(row);
            var missing = Assert.IsType<TemplateSelectionDisplay.MissingTemplate>(combo.SelectedItem);
            Assert.Equal(savedName, missing.TemplateName);
            Assert.Equal(savedName, template.GetValue(node));
            Assert.Same(sample, Assert.Single(combo.Items.OfType<TemplateBase>(), item => item.Key == templateName));
            if (legacyPixel != null)
                Assert.DoesNotContain(legacyPixel, combo.Items.Cast<object>());
            combo.SelectedItem = sample;
            Assert.Equal(sample.Key, template.GetValue(node));
            Assert.Equal(sample.Key, Encoding.UTF8.GetString(STNodeSavedState.Parse(node.GetSaveData()).Properties[template.Name]));
            combo.SelectedItem = null;
            Assert.Equal(string.Empty, template.GetValue(node));

            if (node is AlgorithmNode or AlgorithmARVRNode)
                Assert.NotNull(GetSelector(GetPropertyRow(propertyEditor, nameof(AlgorithmNode.POITempName))));
        }
        finally
        {
            row?.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            collection.Remove(sample);
            if (legacyPixel != null)
                TemplateLedCheck.Params.Remove(legacyPixel);
        }
    });

    [Fact]
    public void ChangingOperatorRefreshesTheExistingTemplateRow() => WithEditorTheme(() =>
    {
        var node = new Algorithm2InNode();
        node.Create();
        Assert.Equal(Algorithm2Type.MTF, node.Algorithm);
        var mtf = new TemplateModel<TJMTFParam>("editor-test-mtf", new());
        var sfr = new TemplateModel<TJSFRParam>("editor-test-sfr", new());
        TemplateMTF2.Params.Add(mtf);
        TemplateSFR2.Params.Add(sfr);
        DockPanel? row = null;
        try
        {
            row = GetPropertyRow(GenerateInspectorProperties(node), nameof(node.TempName));
            ComboBox combo = GetSelector(row);
            Assert.Contains(mtf, combo.Items.Cast<object>());
            combo.SelectedItem = mtf;
            Assert.Equal(mtf.Key, node.TempName);

            node.Algorithm = Algorithm2Type.SFR;
            combo = GetSelector(row);
            Assert.DoesNotContain(mtf, combo.Items.Cast<object>());
            Assert.Contains(sfr, combo.Items.Cast<object>());
            combo.SelectedItem = sfr;
            Assert.Equal(sfr.Key, node.TempName);
        }
        finally
        {
            row?.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            TemplateMTF2.Params.Remove(mtf);
            TemplateSFR2.Params.Remove(sfr);
        }
    });

    [Theory]
    [InlineData(typeof(AlgorithmNode), 5, 6, 9)]
    [InlineData(typeof(Algorithm2InNode), 1, 2, 6)]
    public void RetiredOperatorIdsAreAbsentFromTheNodePicker(Type nodeType, int first, int second, int third) => WithEditorTheme(() =>
    {
        var node = (STNode)Activator.CreateInstance(nodeType)!;
        node.Create();
        PropertyInfo algorithm = nodeType.GetProperty(nameof(AlgorithmNode.Algorithm))!;
        algorithm.SetValue(node, Enum.ToObject(algorithm.PropertyType, first));
        StackPanel propertyEditor = GenerateInspectorProperties(node);
        DockPanel templateRow = GetPropertyRow(propertyEditor, nameof(AlgorithmNode.TempName));
        try
        {
            var combo = GetSelector(GetPropertyRow(propertyEditor, nameof(AlgorithmNode.Algorithm)));
            var operatorIds = combo.Items.Cast<object>().Select(item => item switch
            {
                KeyValuePair<object?, string> option => Convert.ToInt32(option.Key),
                Enum option => Convert.ToInt32(option),
                _ => throw new InvalidOperationException(item.GetType().FullName)
            }).ToArray();
            Assert.Contains(0, operatorIds);
            Assert.DoesNotContain(first, operatorIds);
            Assert.DoesNotContain(second, operatorIds);
            Assert.DoesNotContain(third, operatorIds);
            Assert.Equal(first, Convert.ToInt32(algorithm.GetValue(node)));
        }
        finally
        {
            templateRow.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        }
    });

    private static StackPanel GenerateInspectorProperties(STNode node) => PropertyEditorHelper.GenPropertyEditorControl(
        node, PropertyEditorHelper.GetResourceManager(node), metadataProvider: FlowNodePropertyMetadataProvider.Instance,
        advancedOptions: FlowNodePropertyMetadataProvider.AdvancedOptions);

    private static DockPanel GetPropertyRow(DependencyObject parent, string propertyName) => Assert.Single(
        Descendants(parent).OfType<DockPanel>(), row => row.Tag is PropertyInfo property && property.Name == propertyName);

    private static ComboBox GetSelector(DockPanel row) => Assert.Single(Descendants(row).OfType<ComboBox>());

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static void WithEditorTheme(Action verify) => WpfTestHost.Invoke(() =>
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var theme = ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase)
            .Select(uri => new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) }).ToArray();
        foreach (var dictionary in theme)
            dictionaries.Add(dictionary);
        try { verify(); }
        finally
        {
            foreach (var dictionary in theme)
                dictionaries.Remove(dictionary);
        }
    });
}
