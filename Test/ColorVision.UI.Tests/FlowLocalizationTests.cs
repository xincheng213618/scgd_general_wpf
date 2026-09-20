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
using System.Windows.Data;
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
