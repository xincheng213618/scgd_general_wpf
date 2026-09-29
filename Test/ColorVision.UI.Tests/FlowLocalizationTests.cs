using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.FlowProcessing.PostProcess;
using FlowEngineLib.Node.PG;
using FlowEngineLib;
using FlowEngineLib.Algorithm;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services.Devices.Camera;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public class FlowLocalizationTests
{
    [Theory]
    [InlineData("zh-Hans", "屏幕缺陷检测", "自动曝光模板", "电压量程（V）", "增益由校正组“sample”固定为 12.5")]
    [InlineData("en-US", "Screen Defect Detection", "Auto Exposure Template", "Voltage Range (V)", "Gain is fixed at 12.5 by calibration group “sample”")]
    [InlineData("zh-Hant", "螢幕缺陷檢測", "自動曝光範本", "電壓量程（V）", "增益由校正組「sample」固定為 12.5")]
    public void MigratedNodesKeepEnumTranslationsMetadataAndPersistedValues(string culture, string enumLabel, string description, string rangeHint, string gainHint)
    {
        WpfTestHost.Invoke(() =>
        {
            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                EnsurePropertyEditorResources();
                var node = new AlgorithmARVRNode();
                node.Create();
                var property = typeof(AlgorithmARVRNode).GetProperty(nameof(AlgorithmARVRNode.Algorithm))!;
                var panel = new EnumPropertiesEditor().GenProperties(property, node);
                var combo = Assert.Single(panel.Children.OfType<ComboBox>());
                Assert.Contains(combo.Items.Cast<KeyValuePair<object?, string>>(), item => Equals(item.Key, AlgorithmARVRType.屏幕缺陷检测) && item.Value == enumLabel);
                combo.SelectedValue = AlgorithmARVRType.屏幕缺陷检测;
                combo.GetBindingExpression(Selector.SelectedValueProperty)!.UpdateSource();
                Assert.Equal(AlgorithmARVRType.屏幕缺陷检测, node.Algorithm);
                Assert.Equal("屏幕缺陷检测", Encoding.UTF8.GetString(ParseState(node.GetSaveData())[nameof(node.Algorithm)]));

                var exposure = typeof(AOIRegisterPixelsCameraNode).GetProperty(nameof(AOIRegisterPixelsCameraNode.AutoExpTempName))!;
                Assert.Equal(description, FlowNodePropertyMetadataProvider.Instance.GetDescription(exposure));
                var smu = new SMUFromCSVNode();
                smu.Create();
                var range = typeof(SMUFromCSVNode).GetProperty(nameof(SMUFromCSVNode.SrcRng))!;
                DockPanel rangePanel = new SmuRangePropertiesEditor().GenProperties(range, smu);
                try { Assert.Equal(rangeHint, Assert.Single(rangePanel.Children.OfType<HandyControl.Controls.ComboBox>()).ToolTip); }
                finally { rangePanel.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
                Assert.Equal(gainHint, CalibrationGroupGainResolver.CreateHint("sample", 12.5f));
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUiCulture;
            }
        });
    }

    [Fact]
    public void EnglishEnumLabelsKeepTheOriginalEnumValuesAndSerializedPayload()
    {
        WpfTestHost.Invoke(() =>
        {
            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
                EnsurePropertyEditorResources();

                var node = new PGGECSNode();
                node.Create();
                PropertyInfo property = typeof(PGGECSNode).GetProperty(nameof(PGGECSNode.PGCmd))!;
                DockPanel panel = new EnumPropertiesEditor().GenProperties(property, node);
                ComboBox comboBox = Assert.Single(panel.Children.OfType<ComboBox>());
                var items = comboBox.Items.Cast<KeyValuePair<object?, string>>().ToArray();

                Assert.Contains(items, item => Equals(item.Key, PGGECSCommCmdType.上电) && item.Value == "Power On");
                Assert.Contains(items, item => Equals(item.Key, PGGECSCommCmdType.下电) && item.Value == "Power Off");
                Assert.Contains(items, item => Equals(item.Key, PGGECSCommCmdType.切图) && item.Value == "Switch Image");

                comboBox.SelectedValue = PGGECSCommCmdType.切图;
                comboBox.GetBindingExpression(Selector.SelectedValueProperty)?.UpdateSource();

                Assert.Equal(PGGECSCommCmdType.切图, node.PGCmd);
                Dictionary<string, byte[]> state = ParseState(node.GetSaveData());
                Assert.Equal("指定", Encoding.UTF8.GetString(state[nameof(PGGECSNode.PGCmd)]));

                var restored = new PGGECSNode();
                restored.Create();
                restored.OnLoadNode(state);
                Assert.Equal(PGGECSCommCmdType.切图, restored.PGCmd);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUiCulture;
            }
        });
    }

    [Fact]
    public void EnglishFlowInspectorAndCustomNodesDoNotFallBackToChinese()
    {
        WpfTestHost.Invoke(() =>
        {
            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
                EnsurePropertyEditorResources();

                Assert.Equal("Local Calibration", ST.Library.UI.Lang.GetOrDefault("本地校正"));
                Assert.Equal("Real-time POI", new LocalRealPoiNode().Title);

                using var canvas = new FlowEditorCanvas();
                canvas.Measure(new Size(1000, 600));
                canvas.Arrange(new Rect(0, 0, 1000, 600));
                canvas.UpdateLayout();
                string[] buttonLabels = FindVisualChildren<Button>(canvas)
                    .Select(button => button.Content?.ToString())
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Cast<string>()
                    .ToArray();

                Assert.Contains("Configuration", buttonLabels);
                Assert.Contains("Documentation", buttonLabels);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUiCulture;
            }
        });
    }

    [Theory]
    [InlineData("zh-Hans", "加载图片")]
    [InlineData("en-US", "Load Image")]
    [InlineData("zh-Hant", "載入圖片")]
    public void LocalNodeTitleSharesItsPrimaryLocalizedCategory(string culture, string expectedCategory)
    {
        WpfTestHost.Invoke(() =>
        {
            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

                var metadata = FlowNodePropertyMetadataProvider.Instance;
                PropertyInfo imageTitle = typeof(LocalImageNode).GetProperty(nameof(ST.Library.UI.NodeEditor.STNode.Title))!;
                PropertyInfo imageFile = typeof(LocalImageNode).GetProperty(nameof(TestMessageBoxNode.ImageFileUrl))!;
                Assert.Equal(expectedCategory, metadata.GetCategory(imageFile));
                Assert.Equal(expectedCategory, metadata.GetCategory(imageTitle));

                PropertyInfo distortionTitle = typeof(LocalGridDistortionNode).GetProperty(nameof(ST.Library.UI.NodeEditor.STNode.Title))!;
                PropertyInfo distortionImage = typeof(LocalGridDistortionNode).GetProperty(nameof(LocalGridDistortionNode.ImageFilePath))!;
                Assert.Equal(metadata.GetCategory(distortionImage), metadata.GetCategory(distortionTitle));
                Assert.NotEqual(nameof(LocalGridDistortionNode), metadata.GetCategory(distortionTitle));
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUiCulture;
            }
        });
    }

    [Theory]
    [InlineData("zh-Hans", "全局", "运算", "自定义节点/其他")]
    [InlineData("en-US", "Global", "Operation", "Custom Nodes/Other")]
    [InlineData("zh-Hant", "全局", "運算", "自訂節點/其他")]
    public void FlowNodeCreationMenuUsesCategoryOrderAndNestedOtherCategory(
        string culture,
        string globalHeader,
        string operationHeader,
        string otherPath)
    {
        WpfTestHost.Invoke(() =>
        {
            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

                IReadOnlyDictionary<Type, string> paths = FlowNodeContextMenuService.GetNodeCreationMenuPaths();
                Assert.Equal(otherPath, paths[typeof(CommandLineScriptNode)]);
                Assert.Equal(otherPath, paths[typeof(LocalImageNode)]);
                Assert.Equal(otherPath, paths[typeof(LocalFileFusionNode)]);
                ST.Library.UI.NodeEditor.STNodeAttribute globalAttribute = typeof(FlowEngineLib.Start.MQTTStartNode)
                    .GetCustomAttribute<ST.Library.UI.NodeEditor.STNodeAttribute>()!;
                ST.Library.UI.NodeEditor.STNodeAttribute operationAttribute = typeof(LoopNode)
                    .GetCustomAttribute<ST.Library.UI.NodeEditor.STNodeAttribute>()!;
                ST.Library.UI.NodeEditor.STNodeAttribute customAttribute = typeof(CommandLineScriptNode)
                    .GetCustomAttribute<ST.Library.UI.NodeEditor.STNodeAttribute>()!;
                Assert.Equal(("全局", 0), (globalAttribute.Path, globalAttribute.CategoryOrder));
                Assert.Equal(("运算", 100), (operationAttribute.Path, operationAttribute.CategoryOrder));
                Assert.Equal(("Flow_CustomNodes/Flow_OtherNodes", 9900), (customAttribute.Path, customAttribute.CategoryOrder));
                Assert.Equal(globalHeader, FlowNodeContextMenuService.LocalizeNodeMenuPath("FlowEngineLib/00 全局"));
                Assert.Equal(operationHeader, FlowNodeContextMenuService.LocalizeNodeMenuPath("FlowEngineLib/01 运算"));
                Assert.DoesNotContain(paths.Keys, type => type.FullName == "ColorVision.Engine.FlowProcessing.Nodes.LocalBuildPoiByTemplateNode");
                Assert.DoesNotContain(paths.Keys, type => type.FullName == "FlowEngineLib.DisplayHub");
                Assert.All(paths.Values, path => Assert.DoesNotMatch(@"^\d+(?:_\d+)?\s", path));

                Type[] registeredTypes = ST.Library.UI.NodeEditor.STNodeTypeRegistry.GetTypes();
                Assert.Contains(registeredTypes, type => type.FullName == "ColorVision.Engine.FlowProcessing.Nodes.LocalBuildPoiByTemplateNode");
                Assert.Contains(registeredTypes, type => type.FullName == "FlowEngineLib.DisplayHub");
                var builtInCategories = registeredTypes
                    .Where(type => type.Assembly == typeof(LoopNode).Assembly || type.Assembly == typeof(LocalImageNode).Assembly)
                    .Where(type => !type.IsDefined(typeof(ObsoleteAttribute), inherit: false))
                    .Select(type => type.GetCustomAttribute<ST.Library.UI.NodeEditor.STNodeAttribute>())
                    .Where(attribute => attribute != null)
                    .Cast<ST.Library.UI.NodeEditor.STNodeAttribute>()
                    .Select(attribute => new
                    {
                        Category = attribute.Path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)[0],
                        attribute.CategoryOrder,
                    })
                    .ToArray();
                Assert.All(builtInCategories, category =>
                {
                    Assert.DoesNotMatch(@"^\d+(?:_\d+)?\s", category.Category);
                    Assert.NotEqual(int.MaxValue, category.CategoryOrder);
                });
                Assert.All(
                    builtInCategories.GroupBy(category => category.Category, StringComparer.Ordinal),
                    group => Assert.Single(group.Select(category => category.CategoryOrder).Distinct()));

                IReadOnlyList<string> rootHeaders = FlowNodeContextMenuService.GetNodeCreationMenuRootHeaders();
                int globalIndex = rootHeaders.ToList().IndexOf(globalHeader);
                int operationIndex = rootHeaders.ToList().IndexOf(operationHeader);
                Assert.True(globalIndex >= 0, $"Missing root category: {globalHeader}");
                Assert.True(operationIndex > globalIndex, $"Expected {operationHeader} after {globalHeader}.");
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUiCulture;
            }
        });
    }

    [Fact]
    public void EnglishEngineUiAndPostProcessMetadataUseLocalizedDisplayText()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

            Assert.Equal("Refresh", new EngineLangExtension("刷新").ProvideValue(null!));

            PostProcessMetadata metadata = PostProcessMetadata.FromType(typeof(FileCleanupPostProcessor));
            Assert.Equal("File Cleanup", metadata.DisplayName);
            Assert.Equal("Delete temporary or specified files generated by the flow.", metadata.Description);

            PostProcessTypeOption option = Assert.Single(
                PostProcessTypeCatalog.CreateOptions([new FileCleanupPostProcessor()]));
            Assert.Equal("System Maintenance", option.Category);
            Assert.Equal("Configurable", option.ConfigurationSummary);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void EnglishFlowDiagnosticsLocalizeStaticAndFormattedText()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

            Assert.Equal("Flow Overview / Node Analysis", EngineLocalization.Get("流程概览 / 节点分析"));
            Assert.Equal("Succeeded ", new EngineLangExtension("成功 ").ProvideValue(null!));
            Assert.Equal(
                "Page 1 of 4; 120 total",
                EngineLocalization.Format($"第 {1} / {4} 页，共 {120} 条"));
            Assert.Equal(
                "Batch 8 · 3 executed node(s)",
                EngineLocalization.Format($"Batch {8} · {3} 个执行节点{string.Empty}"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private static void EnsurePropertyEditorResources()
    {
        Application application = Application.Current!;
        application.Resources["GlobalTextBrush"] = Brushes.Black;
        application.Resources["GlobalBorderBrush"] = Brushes.Transparent;
        application.Resources["BorderBrush"] = Brushes.Gray;
        application.Resources["PrimaryBrush"] = Brushes.DodgerBlue;
        application.Resources["GlobalBackground"] = Brushes.White;
        application.Resources["PrimaryTextBrush"] = Brushes.Black;
        application.Resources["SecondaryTextBrush"] = Brushes.Gray;
        application.Resources["ButtonCommand"] = new Style(typeof(Button));
        application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }

    private static Dictionary<string, byte[]> ParseState(byte[] data)
    {
        int position = 0;
        position += data[position] + 1;
        position += data[position] + 1;
        Dictionary<string, byte[]> state = new();
        while (position < data.Length)
        {
            int keyLength = BitConverter.ToInt32(data, position);
            position += sizeof(int);
            string key = Encoding.UTF8.GetString(data, position, keyLength);
            position += keyLength;
            int valueLength = BitConverter.ToInt32(data, position);
            position += sizeof(int);
            byte[] value = new byte[valueLength];
            Array.Copy(data, position, value, 0, valueLength);
            position += valueLength;
            state[key] = value;
        }
        return state;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
                yield return match;

            foreach (T descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }
}
