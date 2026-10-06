using ColorVision.Engine.Services.PhyCameras.Calibration.Editing;
using ColorVision.Engine.Services.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class CalibrationDocumentEditorTests
{
    public static IEnumerable<object[]> JsonTypes => new[]
    {
        ServiceTypes.DarkNoise, ServiceTypes.ColorShift, ServiceTypes.Distortion,
        ServiceTypes.ColorDiff, ServiceTypes.AngleShift, ServiceTypes.Luminance,
        ServiceTypes.LumOneColor, ServiceTypes.LumFourColor, ServiceTypes.LumMultiColor
    }.Select(type => new object[] { type });

    [Theory]
    [MemberData(nameof(JsonTypes))]
    public void EditingKnownValuesPreservesUnknownNestedDataAndNegativeNumbers(ServiceTypes type)
    {
        var original = CalibrationJsonDocument.CreateDefault(type).Json;
        original["vendor"] = JObject.Parse("""{"revision":4,"date":"2026-01-02","nested":[null,false,-2.5,{"extra":"keep"}]}""");
        var document = CalibrationJsonDocument.Parse(type, original.ToString());
        var value = document.Tables.SelectMany(table => table.Rows)
            .Concat(document.Parameters.Where(parameter => parameter.Kind == CalibrationParameterKind.Number))
            .First(parameter => parameter.Path is not "MeasDis" and not "interpolate_ratio");
        value.Value = "-0.125";
        var saved = JObject.Parse(document.BuildJson());
        Assert.True(JToken.DeepEquals(original["vendor"], saved["vendor"]));
        Assert.Equal(-0.125, saved.SelectToken(value.Path)!.Value<double>());
    }

    [Theory]
    [InlineData(ServiceTypes.Distortion, "cameraMatrix")]
    [InlineData(ServiceTypes.LumMultiColor, "pa")]
    [InlineData(ServiceTypes.LumMultiColor, "Gain")]
    public void LegacyArrayExtensionsSurviveEditing(ServiceTypes type, string key)
    {
        var root = CalibrationJsonDocument.CreateDefault(type).Json;
        ((JArray)root[key]!).Add(17.75);
        var document = CalibrationJsonDocument.Parse(type, root.ToString());
        document.Tables.First(table => table.Key == key).Rows[0].Value = "-0.5";
        var result = (JArray)JObject.Parse(document.BuildJson())[key]!;
        Assert.Equal(((JArray)root[key]!).Count, result.Count);
        Assert.Equal(17.75, result.Last!.Value<double>());
    }

    [Theory]
    [InlineData("{\"a\":1,\"a\":2}")]
    [InlineData("{\"vendor\":{\"a\":1,\"a\":2}}")]
    [InlineData("[]")]
    [InlineData("{} {}")]
    public void AmbiguousJsonCannotEnterTheEditor(string json)
        => Assert.ThrowsAny<JsonException>(() => CalibrationJsonDocument.Parse(ServiceTypes.Luminance, json));

    [Theory]
    [InlineData(ServiceTypes.Distortion, "w", "0")]
    [InlineData(ServiceTypes.ColorDiff, "MeasDis", "0")]
    [InlineData(ServiceTypes.AngleShift, "interpolate_ratio", "0")]
    [InlineData(ServiceTypes.AngleShift, "target_row", "-1")]
    [InlineData(ServiceTypes.DarkNoise, "DarkNoiseRatio", "NaN")]
    public void UnsafeEditedValuesBlockSerialization(ServiceTypes type, string path, string value)
    {
        var document = CalibrationJsonDocument.CreateDefault(type);
        document.Parameters.Single(parameter => parameter.Path == path).Value = value;
        Assert.NotEmpty(document.Validate());
        Assert.Throws<FormatException>(() => document.BuildJson());
    }

    [Theory]
    [InlineData(ServiceTypes.Distortion, "cameraMatrix")]
    [InlineData(ServiceTypes.LumMultiColor, "pa")]
    [InlineData(ServiceTypes.AngleShift, "coeff_r")]
    [InlineData(ServiceTypes.ColorDiff, "ColRowCoeffs_GR")]
    public void WrongArrayShapesAndNonFiniteCoefficientsBlockSerialization(ServiceTypes type, string key)
    {
        var root = CalibrationJsonDocument.CreateDefault(type).Json;
        root[key] = new JArray();
        var malformed = CalibrationJsonDocument.Parse(type, root.ToString());
        Assert.NotEmpty(malformed.Validate());
        Assert.Throws<FormatException>(() => malformed.BuildJson());
        var document = CalibrationJsonDocument.CreateDefault(type);
        document.Tables.Single(table => table.Key == key).Rows[0].Value = "Infinity";
        Assert.Throws<FormatException>(() => document.BuildJson());
    }

    [Theory]
    [InlineData(ServiceTypes.LumOneColor, "Gain_x")]
    [InlineData(ServiceTypes.LumMultiColor, "Gain[0]")]
    public void ZeroEffectiveNormalizationGainCannotBeSaved(ServiceTypes type, string path)
    {
        var document = CalibrationJsonDocument.CreateDefault(type);
        document.Parameters.Concat(document.Tables.SelectMany(table => table.Rows)).Single(parameter => parameter.Path == path).Value = "0";
        Assert.Throws<FormatException>(() => document.BuildJson());
    }

    [Theory]
    [InlineData(ServiceTypes.LumMultiColor, "pa[0]")]
    [InlineData(ServiceTypes.LumMultiColor, "Gain[0]")]
    [InlineData(ServiceTypes.Distortion, "cameraMatrix[0]")]
    [InlineData(ServiceTypes.Distortion, "distCoeffs[0]")]
    [InlineData(ServiceTypes.DarkNoise, "DarkNoiseRatio")]
    [InlineData(ServiceTypes.ColorShift, "offset[0].X")]
    public void NativeNarrowingCannotOverflowFromFiniteDoubleValues(ServiceTypes type, string path)
    {
        var document = CalibrationJsonDocument.CreateDefault(type);
        document.Parameters.Concat(document.Tables.SelectMany(table => table.Rows)).Single(parameter => parameter.Path == path).Value = "1e300";
        Assert.Throws<FormatException>(() => document.BuildJson());
    }

    [Fact]
    public void NativeDoubleFieldsAndUnusedArrayExtensionsKeepTheirWiderRange()
    {
        var four = CalibrationJsonDocument.CreateDefault(ServiceTypes.LumFourColor);
        four.Tables.SelectMany(table => table.Rows).Single(parameter => parameter.Path == "a").Value = "1e300";
        Assert.Equal(1e300, JObject.Parse(four.BuildJson())["a"]!.Value<double>());
        var multi = CalibrationJsonDocument.CreateDefault(ServiceTypes.LumMultiColor).Json;
        ((JArray)multi["pa"]!).Add("vendor extension");
        Assert.Equal("vendor extension", JObject.Parse(CalibrationJsonDocument.Parse(ServiceTypes.LumMultiColor, multi.ToString()).BuildJson())["pa"]![9]!.Value<string>());
        var tinyGain = CalibrationJsonDocument.CreateDefault(ServiceTypes.LumMultiColor);
        tinyGain.Tables.SelectMany(table => table.Rows).Single(parameter => parameter.Path == "Gain[0]").Value = "1e-300";
        Assert.Throws<FormatException>(() => tinyGain.BuildJson());
    }

    [Fact]
    public void DraftJsonPreservesOtherFormEditsWhileStructuralValidationStillFails()
    {
        var document = CalibrationJsonDocument.CreateDefault(ServiceTypes.AngleShift);
        document.Parameters.Single(parameter => parameter.Path == "coefficient_order").Value = "3";
        document.Parameters.Single(parameter => parameter.Path == "optical_center_x").Value = "123.5";
        var draft = JObject.Parse(document.BuildDraftJson());
        Assert.Equal(3, draft["coefficient_order"]!.Value<int>());
        Assert.Equal(123.5, draft["optical_center_x"]!.Value<double>());
        Assert.Equal(2, ((JArray)draft["coeff_r"]!).Count);
        Assert.NotEmpty(document.Validate());
        Assert.Throws<FormatException>(() => document.BuildJson());
    }

    [Fact]
    public void SpatialPreviewUsesNativeBgrOrderAndTruncatedSourceSamplingOffsets()
    {
        var document = CalibrationJsonDocument.CreateDefault(ServiceTypes.ColorShift);
        var identity = CalibrationSpatialPreview.Create(document, 101, 81);
        Assert.Equal(new[] { "B", "G", "R" }, identity.Series.Select(series => series.Name));
        foreach (var series in identity.Series)
            Assert.Equal(identity.ReferenceGrid.SelectMany(line => line), series.Lines.SelectMany(line => line));
        document.Parameters.Single(parameter => parameter.Path == "offset[0].X").Value = "1.75";
        var shifted = CalibrationSpatialPreview.Create(document, 101, 81);
        // Grid line x=10 is the first retained vertical line after a positive one-pixel shift.
        Assert.Equal(9, shifted.Series[0].Lines[0][0].X);
        Assert.Equal(0, shifted.Series[0].Lines[0][0].Y);
    }

    [Fact]
    public void NeutralColorDiffAndAnglePreviewRetainIdentityAndAngleValidityMask()
    {
        var color = CalibrationSpatialPreview.Create(CalibrationJsonDocument.CreateDefault(ServiceTypes.ColorDiff));
        foreach (var series in color.Series)
        {
            var expected = color.ReferenceGrid.SelectMany(line => line).ToArray();
            var actual = series.Lines.SelectMany(line => line).ToArray();
            Assert.Equal(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i].X, actual[i].X, 9);
                Assert.Equal(expected[i].Y, actual[i].Y, 9);
            }
        }
        var angle = CalibrationSpatialPreview.Create(CalibrationJsonDocument.CreateDefault(ServiceTypes.AngleShift));
        Assert.Equal(3, angle.Series.Count);
        Assert.All(angle.Series, series => Assert.NotEmpty(series.Lines));
        Assert.All(angle.Series.SelectMany(series => series.Lines).SelectMany(line => line), point => Assert.True(
            Math.Pow(point.X - 960, 2) + Math.Pow(point.Y - 540, 2) <= 540 * 540 + 1e-6));
    }

    [Fact]
    public void AnglePreviewUsesDocumentTargetLayoutInsteadOfDefaultPreviewDimensions()
    {
        var json = CalibrationJsonDocument.CreateDefault(ServiceTypes.AngleShift).Json;
        json["target_col"] = 320; json["target_row"] = 240;
        json["optical_center_x"] = 160; json["optical_center_y"] = 120;
        var preview = CalibrationSpatialPreview.Create(CalibrationJsonDocument.Parse(ServiceTypes.AngleShift, json.ToString()), 1920, 1080);
        Assert.Equal(320, preview.Width); Assert.Equal(240, preview.Height);
        Assert.All(preview.Series.SelectMany(series => series.Lines).SelectMany(line => line), point =>
            Assert.True(point.X >= 0 && point.X < 320 && point.Y >= 0 && point.Y < 240));
        Assert.All(preview.Series, series => Assert.NotEmpty(series.Lines));
    }

    [Fact]
    public void ColorDiffConstantTermKeepsNativeCenterDirectionAndDistanceScale()
    {
        var json = CalibrationJsonDocument.CreateDefault(ServiceTypes.ColorDiff).Json;
        json["w"] = 101; json["h"] = 81; json["CenterCol"] = 50; json["CenterRow"] = 40;
        json["MeasDis"] = 2; json["CalibDis"] = 4;
        json["ColorDiffCoeffs_GR"] = new JArray(1, 0);
        var preview = CalibrationSpatialPreview.Create(CalibrationJsonDocument.Parse(ServiceTypes.ColorDiff, json.ToString()));
        Assert.Contains(new Point(50, 38), preview.Series.Single(series => series.Name == "R").Lines.SelectMany(line => line));
    }

    [Fact]
    public void FishEyePreviewSupportsOutputDimensionsAndProducesFiniteSourceCoordinates()
    {
        var json = CalibrationJsonDocument.CreateDefault(ServiceTypes.Distortion).Json;
        json["w"] = 640; json["h"] = 480; json["s_w"] = 320; json["s_h"] = 240;
        json["cameraMatrix"] = new JArray(500, 0, 300, 0, 500, 220, 0, 0, 1);
        json["useFisheye"] = true;
        var preview = CalibrationSpatialPreview.Create(CalibrationJsonDocument.Parse(ServiceTypes.Distortion, json.ToString()));
        Assert.Single(preview.Series);
        Assert.NotEmpty(preview.Series[0].Lines);
        Assert.Equal(319, preview.ReferenceGrid.SelectMany(line => line).Max(point => point.X));
        Assert.Equal(239, preview.ReferenceGrid.SelectMany(line => line).Max(point => point.Y));
        Assert.All(preview.Series.SelectMany(series => series.Lines).SelectMany(line => line), point =>
            Assert.True(double.IsFinite(point.X) && double.IsFinite(point.Y)));
    }

    [Theory]
    [InlineData("utf8")]
    [InlineData("utf8bom")]
    [InlineData("utf16le")]
    [InlineData("utf16be")]
    [InlineData("utf32le")]
    public void ReplacementBacksUpOriginalBytesAndPreservesEncoding(string encodingName)
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "source.json");
        Encoding encoding = encodingName switch
        {
            "utf8" => new UTF8Encoding(false), "utf8bom" => new UTF8Encoding(true),
            "utf16le" => new UnicodeEncoding(false, true), "utf16be" => new UnicodeEncoding(true, true),
            _ => new UTF32Encoding(false, true)
        };
        File.WriteAllText(path, "{\"a\":1,\"vendor\":\"中文\"}", encoding);
        byte[] original = File.ReadAllBytes(path);
        var snapshot = CalibrationJsonFileStore.Load(path);
        var saved = CalibrationJsonFileStore.Save(snapshot, "{\"a\":-2,\"vendor\":\"中文\"}");
        Assert.NotNull(saved.LastBackupPath);
        Assert.Equal(original, File.ReadAllBytes(saved.LastBackupPath!));
        Assert.Equal(encoding.CodePage, saved.Encoding.CodePage);
        Assert.Equal(encoding.GetPreamble(), saved.Encoding.GetPreamble());
        Assert.Equal(-2, JObject.Parse(saved.Json)["a"]!.Value<int>());
        Assert.Throws<IOException>(() => CalibrationJsonFileStore.Save(snapshot, "{\"a\":3}"));
        Assert.Equal(-2, JObject.Parse(File.ReadAllText(path))["a"]!.Value<int>());
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public void ExternalSourceChangesAndSaveAsCollisionsNeverOverwriteFiles()
    {
        using var directory = new TemporaryDirectory();
        string source = Path.Combine(directory.Path, "source.json"), target = Path.Combine(directory.Path, "copy.json");
        File.WriteAllText(source, "{\"a\":1}");
        var snapshot = CalibrationJsonFileStore.Load(source);
        File.WriteAllText(target, "{\"vendor\":42}");
        Assert.Throws<IOException>(() => CalibrationJsonFileStore.SaveAs(target, "{\"a\":2}", snapshot));
        Assert.Equal("{\"vendor\":42}", File.ReadAllText(target));
        File.WriteAllText(source, "{\"a\":9}");
        Assert.Throws<IOException>(() => CalibrationJsonFileStore.Save(snapshot, "{\"a\":2}"));
        string newTarget = Path.Combine(directory.Path, "new.json");
        Assert.Throws<IOException>(() => CalibrationJsonFileStore.SaveAs(newTarget, "{\"a\":2}", snapshot));
        Assert.False(File.Exists(newTarget));
        Assert.Equal("{\"a\":9}", File.ReadAllText(source));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.bak"));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public void InvalidJsonOrLockedSourceCannotReplaceOriginal()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "source.json");
        File.WriteAllText(path, "{\"a\":1}");
        var snapshot = CalibrationJsonFileStore.Load(path);
        Assert.ThrowsAny<Exception>(() => CalibrationJsonFileStore.Save(snapshot, "{\"a\":1,\"a\":2}"));
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.Throws<IOException>(() => CalibrationJsonFileStore.Save(snapshot, "{\"a\":2}"));
        Assert.Equal("{\"a\":1}", File.ReadAllText(path));
    }

    [Fact]
    public void ManualDraftRequiresConfirmationAndExistingFileTracksUnsavedEdits()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "source.json");
        File.WriteAllText(path, CalibrationJsonDocument.CreateDefault(ServiceTypes.Luminance).BuildJson());
        WithTheme(() =>
        {
                var draft = new CalibrationJsonEditorWindow(ServiceTypes.Luminance);
                Assert.True(draft.HasUnsavedChanges);
                Assert.False(Assert.IsType<Button>(draft.FindName("SaveButton")).IsEnabled);
                Assert.IsType<CheckBox>(draft.FindName("ManualConfirmation")).IsChecked = true;
                Assert.True(Assert.IsType<Button>(draft.FindName("SaveButton")).IsEnabled);
                var existing = new CalibrationJsonEditorWindow(ServiceTypes.Luminance, path);
                Assert.False(existing.HasUnsavedChanges);
                existing.Document.Tables.SelectMany(table => table.Rows).First().Value = "2";
                Assert.True(existing.HasUnsavedChanges);
                Assert.Equal(1, JObject.Parse(File.ReadAllText(path))["a"]!.Value<double>());
        });
    }

    [Fact]
    public void CoefficientTableRendersBoundNamesAndValuesAfterDispatcherLayout()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "angle.json");
        File.WriteAllText(path, CalibrationJsonDocument.CreateDefault(ServiceTypes.AngleShift).BuildJson());
        WithTheme(() =>
        {
            var window = new CalibrationJsonEditorWindow(ServiceTypes.AngleShift, path);
            window.Show();
            try
            {
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
                window.UpdateLayout();
                var grid = FindVisuals<DataGrid>(window).First();
                Assert.Equal("常数项", Assert.IsType<TextBlock>(grid.Columns[0].GetCellContent(grid.Items[0])).Text);
                Assert.Equal("0", Assert.IsType<TextBlock>(grid.Columns[1].GetCellContent(grid.Items[0])).Text);
                Assert.True(grid.Columns[0].ActualWidth > 0);
                string? renderFolder = Environment.GetEnvironmentVariable("CALIBRATION_RENDER_DIR");
                if (renderFolder != null)
                {
                    Directory.CreateDirectory(renderFolder);
                    var root = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
                    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                    var visual = new DrawingVisual();
                    using (var context = visual.RenderOpen())
                    {
                        context.DrawRectangle(window.Background, null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
                        context.DrawRectangle(new VisualBrush(root), null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
                    }
                    bitmap.Render(visual);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(renderFolder, "AngleShift-pumped.png")); encoder.Save(stream);
                }
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void AnglePolynomialButtonsSynchronizeRgbArraysAndOrderWithoutDiscardingFormEdits()
    {
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "angle.json");
        File.WriteAllText(path, CalibrationJsonDocument.CreateDefault(ServiceTypes.AngleShift).BuildJson());
        WithTheme(() =>
        {
            var window = new CalibrationJsonEditorWindow(ServiceTypes.AngleShift, path);
            window.Document.Parameters.Single(parameter => parameter.Path == "optical_center_x").Value = "123.5";
            FindVisuals<Button>(Assert.IsType<StackPanel>(window.FindName("ParameterPanel"))).First(button => Equals(button.Content, "增加高阶项"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var enlarged = JObject.Parse(window.Document.BuildJson());
            Assert.Equal(2, enlarged["coefficient_order"]!.Value<int>());
            foreach (string key in new[] { "coeff_r", "coeff_g", "coeff_b" }) Assert.Equal(3, ((JArray)enlarged[key]!).Count);
            Assert.Equal(123.5, enlarged["optical_center_x"]!.Value<double>());
            FindVisuals<Button>(Assert.IsType<StackPanel>(window.FindName("ParameterPanel"))).First(button => Equals(button.Content, "删除末项"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var reduced = JObject.Parse(window.Document.BuildJson());
            Assert.Equal(1, reduced["coefficient_order"]!.Value<int>());
            foreach (string key in new[] { "coeff_r", "coeff_g", "coeff_b" }) Assert.Equal(2, ((JArray)reduced[key]!).Count);
            Assert.Equal(123.5, reduced["optical_center_x"]!.Value<double>());
            Assert.Equal(960, JObject.Parse(File.ReadAllText(path))["optical_center_x"]!.Value<double>());
        });
    }

    private static IEnumerable<T> FindVisuals<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T found) yield return found;
            foreach (var nested in FindVisuals<T>(child)) yield return nested;
        }
    }

    private static void WithTheme(Action action) => WpfTestHost.Invoke(() =>
    {
        var themes = new List<ResourceDictionary>();
        try
        {
            foreach (string source in new[] { "/HandyControl;component/Themes/basic/colors/colors.xaml", "/HandyControl;component/Themes/Theme.xaml", "/ColorVision.Themes;component/Themes/White.xaml", "/ColorVision.Themes;component/Themes/Base.xaml", "/ColorVision.Themes;component/Themes/Icons/Images.xaml" })
            {
                var theme = new ResourceDictionary { Source = new Uri(source, UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Add(theme); themes.Add(theme);
            }
            action();
        }
        finally { foreach (var theme in themes) Application.Current.Resources.MergedDictionaries.Remove(theme); }
    });

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ColorVisionCalibrationTests", Guid.NewGuid().ToString("N"));
        public TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, true);
    }
}
