using ColorVision.Engine.FlowProcessing.Nodes;
using FlowEngineLib.Node.PG;
using FlowEngineLib;
using ColorVision.Engine.PropertyEditor;
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

    [Theory]
    [InlineData(typeof(LocalFindLuminousAreaNode))]
    [InlineData(typeof(LocalFindCrossNode))]
    [InlineData(typeof(LocalGridDistortionNode))]
    [InlineData(typeof(LocalRgbCrossNode))]
    [InlineData(typeof(LocalRingGhostNode))]
    [InlineData(typeof(LocalTemplateMatchingNode))]
    [InlineData(typeof(LocalCalibrationRealPoiNode))]
    public void LocalImageConsumersIgnoreLegacyFileConfiguration(Type nodeType)
    {
        LocalFlowNodeBase node = (LocalFlowNodeBase)Activator.CreateInstance(nodeType)!;
        node.Create();
        Dictionary<string, byte[]> state = ParseState(node.GetSaveData());
        state["ImageFilePath"] = Encoding.UTF8.GetBytes(@"C:\images\legacy-input.cvraw");
        node.OnLoadNode(state);

        Assert.Null(nodeType.GetProperty("ImageFilePath"));
        Assert.DoesNotContain("ImageFilePath", ParseState(node.GetSaveData()).Keys);
        Assert.Equal(state["NodeID"], ParseState(node.GetSaveData())["NodeID"]);
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
}
