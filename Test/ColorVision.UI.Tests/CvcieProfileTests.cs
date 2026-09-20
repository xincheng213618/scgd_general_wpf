using ColorVision.Algorithms;
using ColorVision.Engine.Media;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.ImageProfile;
using ColorVision.Themes;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class CvcieProfileTests
{
    [Theory]
    [InlineData(32, 1)]
    [InlineData(32, 3)]
    [InlineData(64, 1)]
    [InlineData(64, 3)]
    public async Task OriginalYStatisticsAndExportsRetainMeasurementPrecision(int bits, int channels)
    {
        double[] y = bits == 32 ? [-17.25, 1024.125, 9000.5] : [-17.1234567890123, 1024.1234567890123, 9000.123456789013];
        using Fixture fixture = new(bits, channels, y);
        IReadOnlyList<ImageProfileSourceOption> options = CvcieProfileSource.CreateOptions(fixture.Path, channels);
        Assert.Equal("CIE 全部通道", options[0].Name);
        Assert.Equal(channels == 1 ? 2 : 5, options.Count);
        using IImageProfileMeasurementSource source = options.Single(option => option.Name == "CIE Y").Open(default);
        using AlgorithmResult result = Run(source);
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        AlgorithmTableArtifact table = result.GetArtifact<AlgorithmTableArtifact>("image-profile-samples")!;
        Assert.Equal(y, table.Rows.Select(row => row["CIE Y"].GetDouble()).ToArray());
        Assert.DoesNotContain(table.Columns, column => column.Name is "Gray" or "Luminance" or "R");
        Assert.Null(table.Columns.Single(column => column.Name == "CIE Y").Unit);
        AlgorithmMeasurement[] values = result.GetArtifact<AlgorithmMeasurementArtifact>()!.Measurements.ToArray();
        Assert.Equal(y.Average(), values.Single(value => value.Name == "channel.mean").Value, 10);
        double stddev = Math.Sqrt(y.Sum(value => Math.Pow(value - y.Average(), 2)) / y.Length);
        Assert.Equal(stddev, values.Single(value => value.Name == "channel.stddev.population").Value, 10);
        Assert.All(values.Where(value => value.Name.StartsWith("channel.") && !value.Name.EndsWith("count")), value => Assert.Null(value.Unit));

        string jsonPath = System.IO.Path.Combine(fixture.Directory, "profile.json");
        await AlgorithmResultExporter.ExportJsonAsync(result, jsonPath);
        string json = File.ReadAllText(jsonPath);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.DoesNotContain("cd/m", json);
        Assert.DoesNotContain("\"DN\"", json);
        foreach (double value in y) Assert.Contains(value.ToString("R", CultureInfo.InvariantCulture), json);
        IReadOnlyList<string> csv = await AlgorithmResultExporter.ExportCsvBundleAsync(result, System.IO.Path.Combine(fixture.Directory, "profile.csv"));
        string samples = File.ReadAllText(csv.Single(path => path.Contains("image-profile-samples")));
        Assert.Contains("CIE Y", samples);
        foreach (double value in y) Assert.Contains(value.ToString("R", CultureInfo.InvariantCulture), samples);
        string statistics = File.ReadAllText(csv.Single(path => System.IO.Path.GetFileName(path) == "profile.csv"));
        Assert.Contains("channel.mean", statistics);
        Assert.Contains("channel.stddev.population", statistics);
        Assert.Contains("CIE Y", statistics);
    }

    [Fact]
    public void DefaultIncludesAllCieChannelsAndAssociatedRawWhenAvailable()
    {
        using Fixture fixture = new(64, 3, [500, 600, 700]);
        using (var cie = CvcieProfileSource.CreateOptions(fixture.Path, 3)[0].Open(default))
            Assert.Equal(new[] { "CIE X", "CIE Y", "CIE Z", "CIE x", "CIE y" }, cie.ChannelNames);
        string rawPath = System.IO.Path.ChangeExtension(fixture.Path, ".cvraw");
        using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(3, 1, 16, 3);
        Assert.True(CVFileUtil.WriteCIEFile(rawPath, raw));
        var options = CvcieProfileSource.CreateOptions(fixture.Path, 3);
        using (var combined = options[0].Open(default))
        using (var result = Run(combined, parameters: new ImageProfileParameters { IncludeLuminance = false }))
        {
            Assert.Equal(8, combined.ChannelNames.Count);
            var table = result.GetArtifact<AlgorithmTableArtifact>()!;
            Assert.Equal(BitConverter.ToUInt16(raw.Data, 0), table.Rows[0]["B"].GetDouble());
            Assert.Equal(500, table.Rows[0]["CIE Y"].GetDouble());
            Assert.Equal(11d / 542, table.Rows[0]["CIE x"].GetDouble());
            Assert.DoesNotContain(table.Columns, column => column.Name == "Luminance");
            Assert.Equal("DN", table.Columns.Single(column => column.Name == "R").Unit);
            Assert.Null(table.Columns.Single(column => column.Name == "CIE Y").Unit);
            Assert.Equal("1", table.Columns.Single(column => column.Name == "CIE x").Unit);
        }
        File.SetLastWriteTimeUtc(rawPath, File.GetLastWriteTimeUtc(rawPath).AddMinutes(1));
        Assert.Throws<IOException>(() => options[0].Open(default));
    }

    [Fact]
    public void XyzAndIndividualChoicesReadPlanarChannelsWithoutFloatDowncast()
    {
        using Fixture fixture = new(64, 3, [1000.1234567890123, 2000.5, 3000.75]);
        var options = CvcieProfileSource.CreateOptions(fixture.Path, 3);
        using IImageProfileMeasurementSource source = options.Single(option => option.Name == "CIE XYZ").Open(default);
        using AlgorithmResult result = Run(source);
        var rows = result.GetArtifact<AlgorithmTableArtifact>()!.Rows;
        Assert.Equal(11, rows[0]["CIE X"].GetDouble());
        Assert.Equal(1000.1234567890123, rows[0]["CIE Y"].GetDouble());
        Assert.Equal(31, rows[0]["CIE Z"].GetDouble());
        Assert.Equal(12, source.Read(1, 0, 0));
        Assert.Equal(32, source.Read(1, 0, 2));
    }

    [Fact]
    public async Task ChromaticityUsesInterpolatedXyzAndExportsYxyWithIndependentUnits()
    {
        using Fixture fixture = new(64, 3, [1, 40, 3]);
        var options = CvcieProfileSource.CreateOptions(fixture.Path, 3);
        using var source = options.Single(option => option.Name == "CIE Yxy").Open(default);
        using var result = Run(source, parameters: new ImageProfileParameters { SampleSpacingPixels = 0.5 });
        var table = result.GetArtifact<AlgorithmTableArtifact>()!;
        var row = table.Rows[1];
        Assert.Equal(20.5, row["CIE Y"].GetDouble());
        Assert.Equal(11.5 / 63.5, row["CIE x"].GetDouble(), 14);
        Assert.Equal(20.5 / 63.5, row["CIE y"].GetDouble(), 14);
        Assert.NotEqual((11d / 43 + 12d / 84) / 2, row["CIE x"].GetDouble());
        Assert.Equal("1", table.Columns.Single(column => column.Name == "CIE x").Unit);
        Assert.Null(table.Columns.Single(column => column.Name == "CIE Y").Unit);
        var measurements = result.GetArtifact<AlgorithmMeasurementArtifact>()!.Measurements;
        Assert.Equal(table.Rows.Average(value => value["CIE x"].GetDouble()),
            measurements.Single(value => value.Name == "channel.mean" && value.Channel == 1).Value, 14);
        string jsonPath = System.IO.Path.Combine(fixture.Directory, "yxy.json");
        await AlgorithmResultExporter.ExportJsonAsync(result, jsonPath);
        string json = File.ReadAllText(jsonPath);
        Assert.Contains("CIE Y", json);
        Assert.Contains("CIE x", json);
        Assert.Contains("CIE y", json);
        var csv = await AlgorithmResultExporter.ExportCsvBundleAsync(result, System.IO.Path.Combine(fixture.Directory, "yxy.csv"));
        string samples = File.ReadAllText(csv.Single(path => path.Contains("image-profile-samples")));
        Assert.Contains(row["CIE x"].GetDouble().ToString("R", CultureInfo.InvariantCulture), samples);
        using var xy = options.Single(option => option.Name == "CIE x/y").Open(default);
        using var xyResult = Run(xy);
        Assert.DoesNotContain(xyResult.GetArtifact<AlgorithmTableArtifact>()!.Columns, column => column.Name == "CIE Y");
    }

    [Fact]
    public void ZeroXyzSumMarksChromaticityInvalidWithoutDiscardingY()
    {
        using Fixture fixture = new(64, 3, [-42, -44, -46]);
        using var source = CvcieProfileSource.CreateOptions(fixture.Path, 3).Single(option => option.Name == "CIE Yxy").Open(default);
        using var result = Run(source);
        foreach (var row in result.GetArtifact<AlgorithmTableArtifact>()!.Rows)
        {
            Assert.Equal("Finite", row["CIE YStatus"].GetString());
            Assert.Equal("NaN", row["CIE xStatus"].GetString());
            Assert.Equal("NaN", row["CIE yStatus"].GetString());
            Assert.Equal(JsonValueKind.Null, row["CIE x"].ValueKind);
        }
        Assert.DoesNotContain(result.GetArtifact<AlgorithmMeasurementArtifact>()!.Measurements,
            value => value.Name == "channel.mean" && value.Channel == 1);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(3u)]
    public void FileVersionsUseCorrectPayloadLengthPrefix(uint version)
    {
        using Fixture fixture = new(64, 3, [500, 600, 700], version);
        using var source = new CvcieProfileSource(fixture.Path, [1]);
        Assert.Equal(600, source.Read(1, 0, 0));
    }

    [Fact]
    public void BilinearSamplingRetainsFractionsAndNonFiniteStatus()
    {
        using Fixture fixture = new(64, 1, [1000.125, 1002.375, double.NaN]);
        using var source = new CvcieProfileSource(fixture.Path, [0]);
        using var result = Run(source, parameters: new ImageProfileParameters { SampleSpacingPixels = 0.5 });
        var rows = result.GetArtifact<AlgorithmTableArtifact>()!.Rows;
        Assert.Equal(1001.25, rows[1]["CIE Y"].GetDouble());
        Assert.Equal(JsonValueKind.Null, rows[^1]["CIE Y"].ValueKind);
        Assert.Equal("NaN", rows[^1]["CIE YStatus"].GetString());
    }

    [Fact]
    public void InvalidDimensionsChannelTruncationAndChangedFileAreRejected()
    {
        using Fixture fixture = new(64, 3, [500, 600, 700]);
        using (var source = new CvcieProfileSource(fixture.Path, [1]))
        {
            Assert.Throws<ArgumentException>(() => Run(source, width: 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => source.Read(3, 0, 0));
        }
        Assert.Throws<InvalidDataException>(() => new CvcieProfileSource(fixture.Path, [3]));
        var options = CvcieProfileSource.CreateOptions(fixture.Path, 3);
        File.SetLastWriteTimeUtc(fixture.Path, File.GetLastWriteTimeUtc(fixture.Path).AddMinutes(1));
        Assert.Throws<IOException>(() => options[0].Open(default));
        using (var stream = new FileStream(fixture.Path, FileMode.Open, FileAccess.Write)) stream.SetLength(stream.Length - 1);
        Assert.Throws<InvalidDataException>(() => new CvcieProfileSource(fixture.Path, [1]));
    }

    [Fact]
    public void CancelledAndDisposedReadersNeverReturnData()
    {
        using Fixture fixture = new(32, 1, [500, 600, 700]);
        using CancellationTokenSource cancellation = new();
        using var source = new CvcieProfileSource(fixture.Path, [0], cancellation.Token);
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => source.Read(0, 0, 0));
        Assert.Throws<OperationCanceledException>(() => Run(source, token: cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => new CvcieProfileSource(fixture.Path, [0], cancellation.Token));
        source.Dispose();
        Assert.Throws<ObjectDisposedException>(() => source.Read(0, 0, 0));
        using var unlocked = new FileStream(fixture.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public void ChannelPresetsOnlyChangeCurvesAndChartFollowsTheme()
    {
        using Fixture fixture = new(64, 3, [500, 600, 700]);
        using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(3, 1, 16, 3);
        Assert.True(CVFileUtil.WriteCIEFile(System.IO.Path.ChangeExtension(fixture.Path, ".cvraw"), raw));
        using var source = CvcieProfileSource.CreateOptions(fixture.Path, 3)[0].Open(default);
        using var result = Run(source);
        WpfTestHost.Invoke(() =>
        {
            Theme previous = ThemeManager.Current.CurrentUITheme;
            Application.Current.ForceApplyTheme(Theme.Dark);
            try
            {
                using ImageView view = new();
                view.SetImageSource(new WriteableBitmap(3, 1, 96, 96, PixelFormats.Gray8, null));
                using var window = new ImageProfileResultWindow(result, view.EditorContext.ProcessingContext, view.EditorContext.DrawEditorContext);
                var panel = (WrapPanel)window.FindName("ChannelPanel");
                var plot = ((ScottPlot.WPF.WpfPlot)window.FindName("ProfilePlot")).Plot;
                var curves = plot.PlottableList.OfType<ScottPlot.Plottables.Scatter>().ToArray();
                Assert.Equal(8, curves.Length);
                Assert.Equal(8, panel.Children.OfType<CheckBox>().Count());
                Assert.Equal(new[] { "B", "G", "R", "CIE Y" }, curves.Where(curve => curve.IsVisible).Select(curve => curve.LegendText));
                Assert.DoesNotContain(curves, curve => curve.LegendText == "Luminance");
                Assert.NotEqual(ScottPlot.Colors.White, plot.DataBackground.Color);
                Assert.Same(plot.Axes.Right, curves.Single(curve => curve.LegendText == "CIE Y").Axes.YAxis);
                void Select(string label) => panel.Children.OfType<Button>().Single(button => (string)button.Content == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Select("RGB");
                Assert.Equal(new[] { "B", "G", "R" }, curves.Where(curve => curve.IsVisible).Select(curve => curve.LegendText));
                Assert.False(plot.Axes.Right.IsVisible);
                Select("x/y");
                Assert.Equal(2, curves.Count(curve => curve.IsVisible));
                Assert.False(plot.Axes.Right.IsVisible);
                Assert.True(plot.Axes.Left.IsVisible);
                Assert.All(curves.Where(curve => curve.IsVisible), curve => Assert.Same(plot.Axes.Left, curve.Axes.YAxis));
                Select("全部");
                Assert.All(curves, curve => Assert.True(curve.IsVisible));
                Assert.NotSame(plot.Axes.Right, curves.Single(curve => curve.LegendText == "CIE x").Axes.YAxis);
                Assert.Equal(8, ((DataGrid)window.FindName("StatisticsGrid")).Items.Count);
                Assert.Equal(23, result.GetArtifact<AlgorithmTableArtifact>()!.Columns.Count);
                Application.Current.ForceApplyTheme(Theme.Light);
                Assert.Equal(ScottPlot.Colors.White, plot.FigureBackground.Color);
                window.Dispose();
                Application.Current.ForceApplyTheme(Theme.Dark);
                Assert.Equal(ScottPlot.Colors.White, plot.FigureBackground.Color);
            }
            finally { Application.Current.ForceApplyTheme(previous); }
        });
    }

    [Fact]
    public void SourceClearAndStaleSelectionAreRejectedAndResultNamesAreExplicit()
    {
        using Fixture fixture = new(64, 3, [500, 600, 700]);
        using var source = new CvcieProfileSource(fixture.Path, [1]);
        AlgorithmResult result = Run(source);
        WpfTestHost.Invoke(() =>
        {
            Application application = Application.Current!;
            application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
            application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
            application.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
            application.Resources["ToolBarImage"] = new Style(typeof(Image));
            application.Resources["BaseStyle"] = new Style(typeof(Control));
            application.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
            application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
            using ImageView view = new();
            view.SetImageSource(new WriteableBitmap(3, 1, 96, 96, PixelFormats.Gray8, null));
            var context = view.EditorContext.ProcessingContext;
            context.ProfileMeasurementSources = CvcieProfileSource.CreateOptions(fixture.Path, 3);
            ImageSelectionScope scope = TransientRoiSelectionSession.CaptureSourceScope(context)!;
            var window = new ImageProfileResultWindow(result, context, view.EditorContext.DrawEditorContext);
            Assert.Contains("CIE Y", window.Title);
            Assert.Contains("XYZ 单位未声明", ((TextBlock)window.FindName("SummaryText")).Text);
            int requested = -1;
            window.ConfigureSources(["CIE Y", "CIE XYZ", "当前显示图像"], 0, index => requested = index);
            var selector = (ComboBox)window.FindName("SourceSelector");
            selector.SelectedIndex = 1;
            Assert.Equal(1, requested);
            Assert.Equal(0, selector.SelectedIndex);
            view.Config.ClearProperties();
            Assert.Null(context.ProfileMeasurementSources);
            view.SetImageSource(new WriteableBitmap(3, 1, 96, 96, PixelFormats.Gray8, null));
            Assert.False(TransientRoiSelectionSession.IsSourceScopeCurrent(context, scope));
            context.ProfileMeasurementSources = CvcieProfileSource.CreateOptions(fixture.Path, 3);
            context.NotifySourcePixelsChanged();
            Assert.Null(context.ProfileMeasurementSources);
            window.Dispose();
        });
    }

    private static AlgorithmResult Run(IImageProfileMeasurementSource source, int width = 3,
        ImageProfileParameters? parameters = null, CancellationToken token = default)
    {
        parameters ??= new();
        var invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImageProfile, parameters, new PolylineAlgorithmRoi([new(0, 0), new(2, 0)]));
        return new ImageProfileAlgorithmProvider().ExecuteMeasurement(invocation, parameters, source,
            new ImageSelectionScope(Guid.NewGuid(), 1, width, 1, 96, 96), token);
    }

    private sealed class Fixture : IDisposable
    {
        public string Directory { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ColorVision-CieProfile-" + Guid.NewGuid().ToString("N"));
        public string Path => System.IO.Path.Combine(Directory, "measurement.cvcie");
        public Fixture(int bits, int channels, double[] y, uint version = 2)
        {
            System.IO.Directory.CreateDirectory(Directory);
            double[] values = channels == 1 ? y : new double[] { 11, 12, 13 }.Concat(y).Concat([31d, 32d, 33d]).ToArray();
            byte[] data = values.SelectMany(value => bits == 32 ? BitConverter.GetBytes((float)value) : BitConverter.GetBytes(value)).ToArray();
            if (version == 3)
            {
                // The production writer omits v3 NDPort (documented FileIO limitation).
                // Build an independent fixture matching the reader's published v3 layout.
                using var writer = new BinaryWriter(File.Create(Path));
                writer.Write("CVCIE".ToCharArray());
                writer.Write(3u);
                writer.Write(0); // source filename length
                writer.Write(0); // NDPort
                writer.Write(1f);
                writer.Write(channels);
                for (int i = 0; i < channels; i++) writer.Write(1f);
                writer.Write(3); // width
                writer.Write(1); // height
                writer.Write(bits);
                writer.Write(data.Length);
                writer.Write(data);
                return;
            }
            using CVCIEFile file = new()
            {
                Version = version, FileExtType = CVType.CIE, Rows = 1, Cols = 3, Bpp = bits, Channels = channels,
                Gain = 1, Exp = Enumerable.Repeat(1f, channels).ToArray(), Data = data,
            };
            Assert.True(CVFileUtil.WriteCIEFile(Path, file));
        }
        public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
    }
}
