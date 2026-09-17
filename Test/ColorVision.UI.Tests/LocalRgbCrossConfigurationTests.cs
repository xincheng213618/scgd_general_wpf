using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.EditorTools.Algorithms;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.PropertyEditor;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Nodes;
using ICSharpCode.AvalonEdit;
using ColorVision.Themes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class LocalRgbCrossConfigurationTests
{
    [Fact]
    public void DefaultsMatchMeasurementAndDoNotAddJudgment()
    {
        Assert.True(RgbCrossConfigurationDraft.TryCreate("{}", out var draft, out _));
        Assert.True(draft!.TryGetParameters(out var p, out _));
        Assert.Equal(0.5, p.TargetThreshold);
        Assert.Equal(3, p.Rows); Assert.Equal(3, p.Columns);
        Assert.Null(p.MaximumEdgeSeparationPixels);
        Assert.True(draft.TryGetJson(out var json, out _)); Assert.Equal("{}", json);
        var property = typeof(LocalRgbCrossNode).GetProperty(nameof(LocalRgbCrossNode.ParameterJson))!;
        Assert.Equal(typeof(LocalRgbCrossConfigurationEditor), property.GetCustomAttribute<PropertyEditorTypeAttribute>()!.EditorType);
    }
    [Theory]
    [InlineData("{broken")]
    [InlineData("[]")]
    [InlineData("{\"TemplateName\":\"old\"}")]
    [InlineData("{\"Channel\":2}")]
    [InlineData("{\"MaximumEdgeSeparationPixels\":6}")]
    [InlineData("{\"TargetThreshold\":0.5,\"targetThreshold\":0.6}")]
    [InlineData("{\"TargetThreshold\":\"0.5\"}")]
    public void BrokenOrUnrelatedJsonIsNotSilentlyAccepted(string json)
        => Assert.False(RgbCrossConfigurationDraft.TryCreate(json, out _, out _));
    [Fact]
    public void EditingPreservesOtherSettingsAndInvalidTextCanBeCorrected()
    {
        const string json = "{ \"targetThreshold\":0.65,\"DecodeExponent\":2.2 }";
        Assert.True(RgbCrossConfigurationDraft.TryCreate(json, out var draft, out _));
        Assert.True(draft!.TryGetJson(out var untouched, out _)); Assert.Equal(json, untouched);
        draft.MinimumArmCoverage = "wrong";
        Assert.False(draft.TryGetJson(out _, out _)); Assert.Equal("MinimumArmCoverage", draft.ErrorProperty);
        draft.MinimumArmCoverage = "0.8";
        Assert.True(draft.TryGetJson(out var changed, out _));
        Assert.True(RgbCrossConfigurationDraft.TryCreate(changed, out var restored, out _));
        Assert.True(restored!.TryGetParameters(out var parameters, out _));
        Assert.Equal(0.65, parameters.TargetThreshold); Assert.Equal(2.2, parameters.DecodeExponent);
        Assert.Equal(0.8, parameters.MinimumArmCoverage); Assert.Null(parameters.MaximumEdgeSeparationPixels);
    }
    [Theory]
    [InlineData("0.09")]
    [InlineData("0.91")]
    [InlineData("NaN")]
    public void OutOfRangeValueCannotExecute(string value)
    {
        Assert.True(RgbCrossConfigurationDraft.TryCreate("{}", out var draft, out _));
        draft!.TargetThreshold = value;
        Assert.False(draft.TryGetParameters(out _, out _));
    }
    [Fact]
    public void GridConfigurationRoundTripsAndRejectsFractionalDimensions()
    {
        Assert.True(RgbCrossConfigurationDraft.TryCreate("{\"Rows\":1,\"Columns\":1}", out var draft, out _));
        Assert.True(draft!.TryGetParameters(out var parameters, out _));
        Assert.Equal(1, parameters.Rows); Assert.Equal(1, parameters.Columns);
        draft.Columns = "2";
        Assert.True(draft.TryGetJson(out var json, out _));
        Assert.True(RgbCrossConfigurationDraft.TryCreate(json, out var restored, out _));
        Assert.Equal("2", restored!.Columns);
        draft.Rows = "1.5"; Assert.False(draft.TryGetParameters(out _, out _));
        draft.Rows = "0"; Assert.False(draft.TryGetParameters(out _, out _));
        draft.Rows = "17"; Assert.False(draft.TryGetParameters(out _, out _));
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
            var node = new LocalRgbCrossNode();
            string original = node.ParameterJson;
            var window = new RgbCrossConfigurationWindow(original) { Left = -16000, Top = -16000, ShowInTaskbar = false, ShowActivated = false, Width = 780, Height = 620 };
            try
            {
                window.Show(); window.UpdateLayout();
                var advanced = (Expander)window.FindName("AdvancedSection");
                Assert.False(advanced.IsExpanded);
                Assert.Equal(Visibility.Collapsed, ((Expander)window.FindName("JudgmentSection")).Visibility);
                advanced.IsExpanded = true; window.UpdateLayout();
                var threshold = Descendants(window).OfType<TextBox>().Single(box => box.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "TargetThreshold");
                threshold.Text = "bad";
                Assert.False(window.TryGetConfiguration(out _));
                Click(window, "JsonMode");
                Assert.True(((RadioButton)window.FindName("FormMode")).IsChecked);
                Assert.Equal("bad", threshold.Text);
                threshold.Text = "0.65";
                Click(window, "JsonMode");
                var json = (TextEditor)window.FindName("JsonText");
                Assert.Equal(0.65, (double)JsonNode.Parse(json.Text)!["TargetThreshold"]!);
                json.Text = "{broken";
                Click(window, "FormMode");
                Assert.True(((RadioButton)window.FindName("JsonMode")).IsChecked);
                Assert.Equal("{broken", json.Text);
                json.Text = "{\"TargetThreshold\":0.7}";
                Click(window, "FormMode");
                Assert.True(window.TryGetConfiguration(out var changed));
                Assert.Equal(0.7, (double)JsonNode.Parse(changed)!["TargetThreshold"]!);
                window.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                string directory = Path.Combine(AppContext.BaseDirectory, "editor-evidence"); Directory.CreateDirectory(directory);
                using var stream = File.Create(Path.Combine(directory, dark ? "local-rgb-cross-dark.png" : "local-rgb-cross-light.png"));
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(stream);
            }
            finally { window.Close(); }
            Assert.Equal(original, node.ParameterJson);
            Assert.Null(window.ResultJson);
            Assert.Null(window.ResultParameters);
        });
    }


    [Fact]
    public void BothHostsUseAlgorithmDefaultsAndKeepMeasurementValuesIdentical()
    {
        var defaults = new RgbCrossRegistrationParameters();
        foreach (bool includeJudgment in new[] { false, true })
        {
            string json = RgbCrossConfigurationDraft.SerializeParameters(defaults, includeJudgment);
            Assert.DoesNotContain("Channel", json);
            Assert.True(RgbCrossConfigurationDraft.TryCreate(json, out var draft, out _, includeJudgment));
            Assert.True(draft!.TryGetParameters(out var parameters, out _));
            Assert.Equal(defaults.Rows, parameters.Rows);
            Assert.Equal(defaults.Columns, parameters.Columns);
            Assert.Equal(defaults.TargetThreshold, parameters.TargetThreshold);
            Assert.Equal(defaults.MinimumArmSpanFraction, parameters.MinimumArmSpanFraction);
            Assert.Equal(defaults.AxisBandThreshold, parameters.AxisBandThreshold);
            Assert.Equal(defaults.MinimumArmCoverage, parameters.MinimumArmCoverage);
            Assert.Equal(defaults.MinimumContrast, parameters.MinimumContrast);
            Assert.Equal(defaults.DecodeExponent, parameters.DecodeExponent);
            Assert.Null(parameters.MaximumEdgeSeparationPixels);
            draft.Rows = "1"; draft.Columns = "1"; draft.DecodeExponent = "2.2";
            Assert.True(draft.TryGetJson(out string changed, out _));
            Assert.True(RgbCrossConfigurationDraft.TryCreate(changed, out var restored, out _, includeJudgment));
            Assert.True(restored!.TryGetParameters(out var edited, out _));
            Assert.Equal(1, edited.Rows); Assert.Equal(1, edited.Columns); Assert.Equal(2.2, edited.DecodeExponent);
        }
    }

    [Fact]
    public void OneOffJudgmentRoundTripsAndCanBeClearedWithoutChangingMeasurement()
    {
        const string json = "{\"TargetThreshold\":0.65,\"MaximumEdgeSeparationPixels\":null}";
        Assert.True(RgbCrossConfigurationDraft.TryCreate(json, out var draft, out _, includeJudgment: true));
        draft!.MaximumEdgeSeparationPixels = "4.25";
        Assert.True(draft.TryGetJson(out string changed, out _));
        Assert.False(RgbCrossConfigurationDraft.TryCreate(changed, out _, out _));
        Assert.True(RgbCrossConfigurationDraft.TryCreate(changed, out var restored, out _, includeJudgment: true));
        Assert.True(restored!.TryGetParameters(out var p, out _));
        Assert.Equal(4.25, p.MaximumEdgeSeparationPixels); Assert.Equal(0.65, p.TargetThreshold);
        foreach (string value in new[] { "-1", "1001", "NaN", "invalid" })
        {
            restored.MaximumEdgeSeparationPixels = value;
            Assert.False(restored.TryGetJson(out _, out _));
            Assert.Equal("MaximumEdgeSeparationPixels", restored.ErrorProperty);
        }
        restored.MaximumEdgeSeparationPixels = " ";
        Assert.True(restored.TryGetParameters(out p, out _));
        Assert.Null(p.MaximumEdgeSeparationPixels); Assert.Equal(0.65, p.TargetThreshold);
        Assert.True(restored.TryGetJson(out changed, out _));
        Assert.Null(JsonNode.Parse(changed)!["MaximumEdgeSeparationPixels"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedWindowAppliesTypedSettingsWithHostSpecificJudgment(bool includeJudgment)
    {
        WpfTestHost.Invoke(() =>
        {
            foreach (var uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) });
            var source = new RgbCrossRegistrationParameters();
            var window = new RgbCrossConfigurationWindow(RgbCrossConfigurationDraft.SerializeParameters(source, includeJudgment), includeJudgment)
                { Left = -16000, Top = -16000, ShowInTaskbar = false, ShowActivated = false };
            Exception? failure = null;
            window.Loaded += (_, _) =>
            {
                try
                {
                    var advanced = (Expander)window.FindName("AdvancedSection");
                    Assert.False(advanced.IsExpanded);
                    var judgment = (Expander)window.FindName("JudgmentSection");
                    Assert.Equal(includeJudgment ? Visibility.Visible : Visibility.Collapsed, judgment.Visibility);
                    Capture(window, includeJudgment ? "imageview-rgb-cross-default.png" : "node-rgb-cross-default.png");
                    advanced.IsExpanded = true;
                    judgment.IsExpanded = includeJudgment;
                    window.UpdateLayout();
                    var fields = Descendants(window).OfType<TextBox>().Where(b => b.GetBindingExpression(TextBox.TextProperty) != null)
                        .ToDictionary(b => b.GetBindingExpression(TextBox.TextProperty)!.ParentBinding.Path.Path);
                    Assert.DoesNotContain("Channel", fields.Keys);
                    fields["Rows"].Text = "1"; fields["Columns"].Text = "1";
                    fields["MinimumArmCoverage"].Text = "0.75";
                    if (includeJudgment) fields["MaximumEdgeSeparationPixels"].Text = "4.25";
                    Capture(window, includeJudgment ? "imageview-rgb-cross-expanded.png" : "node-rgb-cross-expanded.png");
                    ((Button)window.FindName("ApplyButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                }
                catch (Exception error) { failure = error; window.Close(); }
            };
            bool? applied = window.ShowDialog();
            Assert.Null(failure); Assert.True(applied);
            var result = Assert.IsType<RgbCrossRegistrationParameters>(window.ResultParameters);
            Assert.Equal(1, result.Rows); Assert.Equal(1, result.Columns); Assert.Equal(0.75, result.MinimumArmCoverage);
            Assert.Equal(includeJudgment ? 4.25 : (double?)null, result.MaximumEdgeSeparationPixels);
            Assert.Equal(3, source.Rows); Assert.Equal(0.5, source.MinimumArmCoverage); Assert.Null(source.MaximumEdgeSeparationPixels);
            Assert.NotNull(window.ResultJson);
        });
    }

    private static void Capture(Window window, string name)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        string directory = Path.Combine(AppContext.BaseDirectory, "editor-evidence"); Directory.CreateDirectory(directory);
        using var stream = File.Create(Path.Combine(directory, name));
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(stream);
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
