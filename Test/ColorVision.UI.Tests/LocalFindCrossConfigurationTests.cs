using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Themes;
using ICSharpCode.AvalonEdit;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class LocalFindCrossConfigurationTests
{
    [Fact]
    public void ExistingJsonIsPreservedExactlyWithoutEditing()
    {
        const string json = "{ \"expectedangledegrees\": 1.25, \"opticsParams\": { \"stdCenter\": {\"x\":4784.5,\"y\":3190.25}, \"focusLength\": 25.4 } }";
        Assert.True(LocalFindCrossConfigurationDraft.TryCreate(json, out var draft, out _));
        Assert.False(draft!.UseImageCenter);
        Assert.Equal("4784.5", draft.CenterX);
        Assert.True(draft.TryGetJson(out var result, out _));
        Assert.Equal(json, result);
    }

    [Fact]
    public void EditingOneValuePreservesCalibrationAndDisabledDistortion()
    {
        const string json = """{"Name":"sample","CalibrationOffset":{"x":0.3,"y":-1.2},"opticsParams":{"stdCenter":{"x":100.25,"y":90.5},"distortion":{"Enabled":false,"K1":0.15,"Fx":1000,"Fy":1001,"Cx":99.5,"Cy":91.25}}}""";
        Assert.True(LocalFindCrossConfigurationDraft.TryCreate(json, out var draft, out _));
        draft!.ExpectedAngle = "2.5";
        Assert.True(draft.TryGetJson(out var result, out var error), error);
        var parsed = JsonNode.Parse(result)!;
        Assert.Equal(0.3, (double)parsed["CalibrationOffset"]!["x"]!);
        Assert.Equal(100.25, (double)parsed["opticsParams"]!["stdCenter"]!["x"]!);
        Assert.Equal(0.15, (double)parsed["opticsParams"]!["distortion"]!["K1"]!);
        Assert.False((bool)parsed["opticsParams"]!["distortion"]!["Enabled"]!);
        Assert.Equal(2.5, (double)parsed["ExpectedAngleDegrees"]!);
    }

    [Theory]
    [InlineData("ExpectedAngle", "181")]
    [InlineData("AngleTolerance", "0")]
    [InlineData("FocusLength", "-1")]
    [InlineData("PixelSize", "NaN")]
    [InlineData("ExpectedAngle", "oops")]
    public void InvalidFieldIsIdentifiedAndDraftIsRetained(string name, string value)
    {
        Assert.True(LocalFindCrossConfigurationDraft.TryCreate("{}", out var draft, out _));
        typeof(LocalFindCrossConfigurationDraft).GetProperty(name)!.SetValue(draft, value);
        Assert.False(draft!.TryGetJson(out _, out _));
        Assert.Equal(name, draft.ErrorProperty);
        Assert.Equal(value, typeof(LocalFindCrossConfigurationDraft).GetProperty(name)!.GetValue(draft));
    }

    [Fact]
    public void OptionalCalibrationSwitchesAndIntrinsicsAreValidated()
    {
        Assert.True(LocalFindCrossConfigurationDraft.TryCreate("{}", out var draft, out _));
        draft!.UseImageCenter = false; draft.CenterX = "100.25"; draft.CenterY = "90.5";
        draft.EnableDistortion = true;
        Assert.False(draft.TryGetJson(out _, out _));
        Assert.Equal("Fx", draft.ErrorProperty);
        draft.Fx = "1000"; draft.Fy = "1000"; draft.Cx = "100"; draft.Cy = "90";
        Assert.True(draft.TryGetJson(out _, out _));
        draft.UseImageCenter = true;
        Assert.True(draft.TryGetJson(out var json, out _));
        Assert.Null(JsonNode.Parse(json)!["opticsParams"]!["stdCenter"]);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("{\"debugCfg\":{}}")]
    [InlineData("{\"opticsParams\":null}")]
    public void VendorTemplateOrBrokenJsonIsNotSilentlyConverted(string json)
    {
        Assert.False(LocalFindCrossConfigurationDraft.TryCreate(json, out _, out _));
    }

    [Fact]
    public void OnlyLocalNodeHasIndependentEditor()
    {
        var property = typeof(LocalFindCrossNode).GetProperty(nameof(LocalFindCrossNode.ParameterJson))!;
        Assert.Equal(typeof(LocalFindCrossConfigurationEditor), property.GetCustomAttribute<PropertyEditorTypeAttribute>()!.EditorType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WindowEditsDraftSwitchesViewsAndCancelsWithoutChangingNode(bool dark)
    {
        WpfTestHost.Invoke(() =>
        {
            foreach (var uri in (dark ? ThemeManager.ResourceDictionaryDark : ThemeManager.ResourceDictionaryWhite).Concat(ThemeManager.ResourceDictionaryBase))
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) });
            var node = new LocalFindCrossNode();
            string original = node.ParameterJson;
            var window = new LocalFindCrossConfigurationWindow(original) { Left = -16000, Top = -16000, ShowInTaskbar = false, ShowActivated = false, Width = 780, Height = 620 };
            try
            {
                window.Show(); window.UpdateLayout();
                var angle = Descendants(window).OfType<TextBox>().Single(box => box.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "ExpectedAngle");
                angle.Text = "bad";
                Assert.False(window.TryGetConfiguration(out _));
                Click(window, "JsonMode");
                Assert.True(((RadioButton)window.FindName("FormMode")).IsChecked);
                Assert.Equal("bad", angle.Text);
                angle.Text = "3.25";
                Click(window, "JsonMode");
                var json = (TextEditor)window.FindName("JsonText");
                Assert.Equal(3.25, (double)JsonNode.Parse(json.Text)!["ExpectedAngleDegrees"]!);
                json.Text = "{broken";
                Click(window, "FormMode");
                Assert.True(((RadioButton)window.FindName("JsonMode")).IsChecked);
                Assert.Equal("{broken", json.Text);
                json.Text = "{\"ExpectedAngleDegrees\":4.5}";
                Click(window, "FormMode");
                Assert.True(window.TryGetConfiguration(out var changed));
                Assert.Equal(4.5, (double)JsonNode.Parse(changed)!["ExpectedAngleDegrees"]!);
                window.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                string directory = Path.Combine(AppContext.BaseDirectory, "editor-evidence"); Directory.CreateDirectory(directory);
                using var stream = File.Create(Path.Combine(directory, dark ? "local-cross-dark.png" : "local-cross-light.png"));
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(stream);
            }
            finally { window.Close(); }
            Assert.Equal(original, node.ParameterJson);
            Assert.Null(window.ResultJson);
        });
    }

    private static void Click(Window window, string name) => ((RadioButton)window.FindName(name)).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
