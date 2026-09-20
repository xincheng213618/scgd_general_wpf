using ColorVision.Themes;
using ColorVision.UI;
using Conoscope.ApplicationServices.Preprocess;
using Conoscope.Core;
using Conoscope.Presentation;
using Conoscope.Processing.Preprocess;
using log4net;
using OpenCvSharp;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Xunit.Abstractions;
using Point = System.Windows.Point;
using Window = System.Windows.Window;

namespace Conoscope.Tests;

[Collection("Conoscope view export")]
public sealed class ConoscopeAngularWorkflowTests(ITestOutputHelper output)
{
    [Fact]
    public void OptionsDialogDefaultsOffValidatesInputsAndKeepsAnUncommittedDraft()
    {
        OnSta(() =>
        {
            ConoscopeExportContext source = new()
            {
                ModelName = "VA60", ImageWidth = 121, ImageHeight = 121, Center = new Point(60, 60),
                MaxAngle = 60, PixelsPerDegree = 1, ReadXyz = (_, _) => new(0, 100, 0)
            };
            var dialog = new ConoscopeAngularAnalysisWindow(source);
            try
            {
                Assert.True(dialog.TryGetOptions(out var initial, out _));
                Assert.Equal(ConoscopeAngularQuantity.Luminance, initial.Quantity);
                Assert.Equal(ConoscopeMirrorDirection.None, initial.MirrorDirection);
                ((TextBox)dialog.FindName("EmitterSize")).Text = "0";
                Assert.True(dialog.TryGetOptions(out _, out _));
                ((RadioButton)dialog.FindName("IntensityOption")).IsChecked = true;
                Assert.False(dialog.TryGetOptions(out _, out _));
                Assert.False(((Button)dialog.FindName("GenerateButton")).IsEnabled);
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                ((TextBox)dialog.FindName("EmitterSize")).Text = "0,5";
                ((ComboBox)dialog.FindName("SizeMode")).SelectedIndex = 1;
                Assert.True(dialog.TryGetOptions(out var intensity, out _));
                Assert.Equal(0.5e-6, intensity.AreaSquareMeters);
                ((CheckBox)dialog.FindName("MirrorEnabled")).IsChecked = true;
                for (int direction = 0; direction < 4; direction++)
                {
                    ((ComboBox)dialog.FindName("MirrorDirection")).SelectedIndex = direction;
                    Assert.True(dialog.TryGetOptions(out var mirrored, out _));
                    Assert.Equal((ConoscopeMirrorDirection)(direction + 1), mirrored.MirrorDirection);
                    Assert.Equal(ConoscopeAngularAnalysisOptions.DefaultRegion(mirrored.MirrorDirection, 60), mirrored.MirrorRegion);
                    Assert.Equal(Visibility.Visible, ((FrameworkElement)dialog.FindName("TargetRectangle")).Visibility);
                }
                ((TextBox)dialog.FindName("MaxX")).Text = "10"; // Right -> left must not cross into the reference half.
                Assert.False(dialog.TryGetOptions(out _, out _));
                dialog.SelectRegion(new Point(-20, 10), new Point(-5, -30));
                Assert.True(dialog.TryGetOptions(out var rectangle, out _));
                Assert.Equal(new ConoscopeAngularRegion(-20, -5, -30, 10), rectangle.MirrorRegion);
                var targetRectangle = (FrameworkElement)dialog.FindName("TargetRectangle");
                Assert.Equal(120, Canvas.GetLeft(targetRectangle));
                Assert.Equal(150, Canvas.GetTop(targetRectangle));
                Assert.Equal(45, targetRectangle.Width);
                Assert.Equal(120, targetRectangle.Height);
                dialog.SelectRegion(new Point(-20, 10), new Point(100, -100));
                Assert.True(dialog.TryGetOptions(out var clamped, out _));
                Assert.Equal(new ConoscopeAngularRegion(-20, 0, -60, 10), clamped.MirrorRegion);
                ((CheckBox)dialog.FindName("MirrorEnabled")).IsChecked = false;
                Assert.True(dialog.TryGetOptions(out _, out _));
                Assert.Equal(Visibility.Collapsed, ((FrameworkElement)dialog.FindName("TargetRectangle")).Visibility);
                Assert.Equal(new ConoscopeAngularAnalysisOptions(), dialog.Options);
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void AnalysisAndNormalizationPreserveLegacyCurvesExportsAndSessionValues()
    {
        OnSta(() =>
        {
            using ConoscopeView view = new();
            Mat y = new(241, 241, MatType.CV_32FC1);
            for (int row = 0; row < 241; row++)
                for (int col = 0; col < 241; col++)
                    y.Set(row, col, (float)(1000 * Math.Exp(-((col - 120d) * (col - 120) + (row - 120d) * (row - 120)) / 5000)));
            SetDocumentProperty(view, nameof(ConoscopeDocument.Y), y);
            SetGeometry(view, new Point(120, 120), 2);
            VerifyWorkflow(view, null);
        });
    }

    [Fact]
    public void SourceCanCloseWhileAnalysisRetainsItsReadOnlyBuffer()
    {
        OnSta(() =>
        {
            var view = new ConoscopeView();
            SetDocumentProperty(view, nameof(ConoscopeDocument.Y), new Mat(241, 241, MatType.CV_32FC1, Scalar.All(12)));
            SetGeometry(view, new Point(120, 120), 2);
            Task<IReadOnlyList<ConoscopeCurveSnapshot>> task = view.CreateAngularSnapshotsAsync(CancellationToken.None);
            view.Dispose();
            var snapshots = task.GetAwaiter().GetResult();
            Assert.Equal(5, snapshots.Count);
            Assert.All(snapshots.SelectMany(s => s.Values), value => Assert.Equal(12, value, 9));
        });
    }

    // Opt in with a local read-only CVCIE/calibrated CVRAW fixture; never commit customer data.
    [LocalSampleFact]
    public async Task RealMeasurementPreservesFileXyzBuffersLegacyExportsAndReferenceCurves()
    {
        string sample = Environment.GetEnvironmentVariable("CONOSCOPE_RAW_SAMPLE") ?? Environment.GetEnvironmentVariable("CONOSCOPE_REAL_SAMPLE")!;
        string originalFileHash = HashFile(sample);
        using ConoscopeDocument loaded = new(LogManager.GetLogger(typeof(ConoscopeAngularWorkflowTests)));
        ConoscopePreprocessOptions options = new(false, 0.000001f, false,
            new DustRemovalOptions(DustRemovalMode.DarkSpot, 12, 1, 500, 3),
            new ImageFilterOptions(ImageFilterType.None, 1, 1, 1, 1, 1));
        await loaded.OpenAsync(sample, null, options, applyPreprocess: false);
        Assert.Null(loaded.LoadError);
        Assert.True(loaded.HasXyzData);
        string[] hashes = [HashMat(loaded.X!), HashMat(loaded.Y!), HashMat(loaded.Z!)];
        int dataVersion = loaded.DataVersion;
        output.WriteLine($"Source: {loaded.Y!.Width} x {loaded.Y.Height}; SHA256={originalFileHash}");
        OnSta(() =>
        {
            using ConoscopeView view = new();
            SetDocumentProperty(view, nameof(ConoscopeDocument.X), loaded.X!.SubMat(0, loaded.X.Rows, 0, loaded.X.Cols));
            SetDocumentProperty(view, nameof(ConoscopeDocument.Y), loaded.Y.SubMat(0, loaded.Y.Rows, 0, loaded.Y.Cols));
            SetDocumentProperty(view, nameof(ConoscopeDocument.Z), loaded.Z!.SubMat(0, loaded.Z.Rows, 0, loaded.Z.Cols));
            SetDocumentProperty(view, nameof(ConoscopeDocument.FileName), sample);
            SetDocumentProperty(view, nameof(ConoscopeDocument.ProcessingDescription), loaded.ProcessingDescription);
            double scale = view.CurrentModelProfile.GetConoscopeCoefficient(loaded.Y.Width, loaded.Y.Height);
            SetGeometry(view, new Point(loaded.Y.Width / 2d, loaded.Y.Height / 2d), scale);
            output.WriteLine($"Geometry: {view.CurrentModelProfile.ModelType}, max={view.MaxAngle}, pixels/degree={scale:R}");
            VerifyWorkflow(view, Environment.GetEnvironmentVariable("CONOSCOPE_ANALYSIS_OUTPUT"));
        });
        Assert.Equal(hashes, new[] { HashMat(loaded.X!), HashMat(loaded.Y!), HashMat(loaded.Z!) });
        Assert.Equal(dataVersion, loaded.DataVersion);
        Assert.Equal(originalFileHash, HashFile(sample));
        output.WriteLine("PASS: source file, full XYZ buffers, legacy exports, fixed-H/V curves, display state and raw session values unchanged.");
    }

    private void VerifyWorkflow(ConoscopeView view, string? artifactDirectory)
    {
        view.State.CoordinateSystem = ConoscopeCoordinateSystem.EastPolar;
        view.SetReferenceMode(ConoscopeCoordinateReferenceMode.FixedVertical);
        view.SetReferenceValue(-13.4);
        view.State.DisplayChannel = view.XMat == null ? ExportChannel.Y : ExportChannel.CieX;
        Invoke(view, "UpdateHorizontalVerticalReference");
        ConoscopeCurveSnapshot original = view.CreateCurrentCurveSnapshot()!;
        Assert.NotNull(original);
        var displayChannel = view.State.DisplayChannel;
        var referenceMode = view.State.CoordinateAxis.ReferenceMode;
        var referenceAngle = view.State.CoordinateAxis.ReferenceAngle;
        int version = Document(view).DataVersion;
        string buffer = HashMat(view.YMat!);
        byte[][] exports = LegacyExports((ConoscopeExportContext)Invoke(view, "CreateExportContext")!);
        var curves = view.CreateAngularSnapshotsAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert.Equal(5, curves.Count);
        Assert.All(curves, c => { Assert.Equal("Y", c.ChannelLabel); Assert.Equal("polar-diameter-angle", c.AxisKey); Assert.Equal(1201, c.Values.Count); });
        Assert.Contains("Mean=Full360Required", curves[^1].Metadata);
        var derived = VerifyDerivedOptions(view, curves);
        string? baselinePath = Environment.GetEnvironmentVariable("CONOSCOPE_BASELINE_SESSION");
        if (artifactDirectory != null && !string.IsNullOrWhiteSpace(baselinePath))
        {
            var baseline = ConoscopeCurveSessionFile.Load(baselinePath);
            Assert.Equal(curves.Count, baseline.Entries.Count);
            for (int i = 0; i < curves.Count; i++)
            {
                Assert.Equal(baseline.Entries[i].Snapshot.Positions.Select(BitConverter.DoubleToInt64Bits), curves[i].Positions.Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(baseline.Entries[i].Snapshot.Values.Select(BitConverter.DoubleToInt64Bits), curves[i].Values.Select(BitConverter.DoubleToInt64Bits));
            }
            output.WriteLine("PASS: all five default curves are bit-for-bit equal to the pre-extension sample session.");
        }
        Assert.Equal(ConoscopeCoordinateSystem.EastPolar, view.State.CoordinateSystem);
        Assert.Equal(displayChannel, view.State.DisplayChannel);
        Assert.Equal(referenceMode, view.State.CoordinateAxis.ReferenceMode);
        Assert.Equal(referenceAngle, view.State.CoordinateAxis.ReferenceAngle);
        Assert.Equal(version, Document(view).DataVersion);
        Assert.Equal(buffer, HashMat(view.YMat!));
        Invoke(view, "UpdateHorizontalVerticalReference");
        ConoscopeCurveSnapshot after = view.CreateCurrentCurveSnapshot()!;
        Assert.Equal(original.Positions, after.Positions);
        Assert.Equal(original.Values, after.Values);
        byte[][] afterExports = LegacyExports((ConoscopeExportContext)Invoke(view, "CreateExportContext")!);
        for (int index = 0; index < exports.Length; index++) Assert.Equal(exports[index], afterExports[index]);

        var window = new ConoscopeCurveSnapshotWindow();
        try
        {
            window.AddSnapshots(curves);
            string[] beforeCsv = curves.Select(Csv).ToArray();
            var normalize = (CheckBox)window.FindName("NormalizeCurves");
            normalize.IsChecked = true;
            ConoscopeCurveSession session = window.CaptureSession();
            for (int i = 0; i < curves.Count; i++) Assert.Equal(beforeCsv[i], Csv(session.Entries[i].Snapshot));
            string sessionPath = Path.GetTempFileName();
            try
            {
                ConoscopeCurveSessionFile.Save(sessionPath, session);
                var restored = ConoscopeCurveSessionFile.Load(sessionPath);
                Assert.Equal(4, restored.SelectedIndex);
                for (int i = 0; i < curves.Count; i++) Assert.Equal(beforeCsv[i], Csv(restored.Entries[i].Snapshot));
            }
            finally { File.Delete(sessionPath); }
            foreach (var curve in curves)
            {
                var metric = ConoscopeCurveMetrics.Measure(curve.Positions, curve.Values);
                output.WriteLine($"{curve.Name}: count={curve.Values.Count}, missing={curve.Values.Count(v => !double.IsFinite(v))}, FWHM={metric.Width:F6}, peak={metric.PeakAngle:F2}, status={metric.Status}");
            }
            if (!string.IsNullOrWhiteSpace(artifactDirectory))
            {
                Directory.CreateDirectory(artifactDirectory);
                ConoscopeCurveSessionFile.Save(Path.Combine(artifactDirectory, "angular-analysis.conocurves"), session);
                for (int i = 0; i < curves.Count; i++) File.WriteAllText(Path.Combine(artifactDirectory, $"curve-{i + 1}.csv"), beforeCsv[i]);
                window.Resources.MergedDictionaries.Add(new ThemeResourceDictionary());
                window.ShowActivated = false;
                window.ShowInTaskbar = false;
                window.Left = -10000;
                window.Top = -10000;
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Show();
                Render(window, Path.Combine(artifactDirectory, "normalized.png"));
                normalize.IsChecked = false;
                Render(window, Path.Combine(artifactDirectory, "absolute.png"));
                window.Width = window.MinWidth;
                window.Height = window.MinHeight;
                Render(window, Path.Combine(artifactDirectory, "minimum-size.png"));
                var derivedWindow = new ConoscopeCurveSnapshotWindow();
                var optionsWindow = new ConoscopeAngularAnalysisWindow(view.CreateExportContext());
                try
                {
                    derivedWindow.AddSnapshots(derived);
                    ConoscopeCurveSessionFile.Save(Path.Combine(artifactDirectory, "mirrored-intensity.conocurves"), derivedWindow.CaptureSession());
                    PreparePreviewWindow(derivedWindow);
                    Render(derivedWindow, Path.Combine(artifactDirectory, "mirrored-intensity.png"));
                    derivedWindow.Width = derivedWindow.MinWidth;
                    derivedWindow.Height = derivedWindow.MinHeight;
                    Render(derivedWindow, Path.Combine(artifactDirectory, "mirrored-intensity-minimum.png"));
                    ((RadioButton)optionsWindow.FindName("IntensityOption")).IsChecked = true;
                    ((CheckBox)optionsWindow.FindName("MirrorEnabled")).IsChecked = true;
                    ((ComboBox)optionsWindow.FindName("MirrorDirection")).SelectedIndex = 3;
                    PreparePreviewWindow(optionsWindow);
                    Render(optionsWindow, Path.Combine(artifactDirectory, "analysis-options.png"));
                    optionsWindow.SelectRegion(new Point(-25, -20), new Point(-5, 20));
                    Render(optionsWindow, Path.Combine(artifactDirectory, "analysis-options-region.png"));
                    optionsWindow.Width = optionsWindow.MinWidth;
                    optionsWindow.Height = optionsWindow.MinHeight;
                    Render(optionsWindow, Path.Combine(artifactDirectory, "analysis-options-minimum.png"));
                }
                finally { derivedWindow.Close(); optionsWindow.Close(); }
            }
        }
        finally { window.Close(); }
    }

    private IReadOnlyList<ConoscopeCurveSnapshot> VerifyDerivedOptions(ConoscopeView view, IReadOnlyList<ConoscopeCurveSnapshot> raw)
    {
        IReadOnlyList<ConoscopeCurveSnapshot> last = [];
        foreach (var direction in Enum.GetValues<ConoscopeMirrorDirection>().Where(d => d != ConoscopeMirrorDirection.None))
            foreach (var quantity in Enum.GetValues<ConoscopeAngularQuantity>())
            {
                var options = new ConoscopeAngularAnalysisOptions { MirrorDirection = direction, Quantity = quantity };
                var curves = view.CreateAngularSnapshotsAsync(CancellationToken.None, options).GetAwaiter().GetResult();
                Assert.Equal(10, curves.Count);
                for (int i = 0; i < 5; i++)
                {
                    Assert.True(curves[i].IsCompatibleWith(curves[i + 5]));
                    Assert.Contains("SampleMirror=None", curves[i].Metadata);
                    Assert.Contains($"SampleMirror={direction}", curves[i + 5].Metadata);
                    Assert.Contains("TargetRectangleDegrees=", curves[i + 5].Metadata);
                    Assert.Equal("polar-diameter-angle", curves[i + 5].AxisKey);
                    Assert.True(ConoscopeCurveMetrics.SupportsFwhm(curves[i + 5]));
                    if (quantity == ConoscopeAngularQuantity.Luminance) Assert.Equal(raw[i].Values, curves[i].Values);
                    else
                    {
                        Assert.Equal("cd", curves[i].UnitLabel);
                        Assert.Equal("Iv", curves[i].ChannelLabel);
                        Assert.False(raw[i].IsCompatibleWith(curves[i]));
                        Assert.Contains("UniformPlanarEmitter", curves[i].Metadata);
                        for (int j = 0; j < raw[i].Values.Count; j++)
                        {
                            double expected = raw[i].Values[j] * options.AreaSquareMeters * Math.Cos(raw[i].Positions[j] * Math.PI / 180);
                            if (!double.IsFinite(expected)) Assert.True(double.IsNaN(curves[i].Values[j]));
                            else Assert.Equal(expected, curves[i].Values[j], 10);
                        }
                    }
                }
                last = curves;
            }
        var session = new ConoscopeCurveSession(last.Select((c, i) => new ConoscopeCurveSessionEntry(c, true, i)).ToArray(), 9);
        string file = Path.GetTempFileName();
        try
        {
            ConoscopeCurveSessionFile.Save(file, session);
            var restored = ConoscopeCurveSessionFile.Load(file);
            for (int i = 0; i < last.Count; i++) Assert.Equal(Csv(last[i]), Csv(restored.Entries[i].Snapshot));
        }
        finally { File.Delete(file); }
        output.WriteLine("PASS: all four mirror directions in luminance/intensity modes; raw comparison curves, units, metadata and session round-trip.");
        return last;
    }

    private static void PreparePreviewWindow(Window window)
    {
        window.Resources.MergedDictionaries.Add(new ThemeResourceDictionary());
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.Left = -10000;
        window.Top = -10000;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Show();
    }

    private static byte[][] LegacyExports(ConoscopeExportContext context)
    {
        string file = Path.GetTempFileName();
        try
        {
            var result = new List<byte[]>();
            foreach (ExportChannel channel in new[] { ExportChannel.Y, ExportChannel.CieX, ExportChannel.CieY, ExportChannel.ColorDifference, ExportChannel.Contrast })
            {
                var options = new ConoscopeCrossSectionExportOptions { StepDegrees = 0.1, DecimalPlaces = 8, IncludeMetadata = false };
                ConoscopeExportService.ExportAzimuthCrossSection(file, channel, context, 37, options);
                result.Add(File.ReadAllBytes(file));
                ConoscopeExportService.ExportPolarCrossSection(file, channel, context, 20, options);
                result.Add(File.ReadAllBytes(file));
            }
            ConoscopeExportService.ExportAngleModeToCsv(file, ExportChannel.Y, context);
            result.Add(System.Text.Encoding.UTF8.GetBytes(string.Join('\n', File.ReadLines(file).Where(l => !l.StartsWith('#')))));
            ConoscopeExportService.ExportCircleModeToCsv(file, ExportChannel.Y, context);
            result.Add(System.Text.Encoding.UTF8.GetBytes(string.Join('\n', File.ReadLines(file).Where(l => !l.StartsWith('#')))));
            return result.ToArray();
        }
        finally { File.Delete(file); }
    }

    private static void Render(Window window, string path)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        var image = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var drawing = background.RenderOpen())
            drawing.DrawRectangle(window.Background, null, new System.Windows.Rect(0, 0, window.ActualWidth, window.ActualHeight));
        image.Render(background);
        image.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static string Csv(ConoscopeCurveSnapshot snapshot) { using StringWriter writer = new(CultureInfo.InvariantCulture); snapshot.WriteCsv(writer); return writer.ToString(); }
    private static string HashFile(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    private static string HashMat(Mat mat)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] row = new byte[mat.Cols * sizeof(float)];
        int rows = mat.Rows;
        for (int y = 0; y < rows; y++) { Marshal.Copy(mat.Ptr(y), row, 0, row.Length); hash.AppendData(row); }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    private static ConoscopeDocument Document(ConoscopeView view) => (ConoscopeDocument)typeof(ConoscopeView).GetField("document", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view)!;
    private static void SetDocumentProperty(ConoscopeView view, string name, object value) => typeof(ConoscopeDocument).GetProperty(name)!.SetValue(Document(view), value);
    private static void SetGeometry(ConoscopeView view, Point center, double scale)
    {
        typeof(ConoscopeView).GetField("sourceImageCenter", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(view, center);
        typeof(ConoscopeView).GetField("sourcePixelsPerDegree", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(view, scale);
    }
    private static object? Invoke(ConoscopeView view, string name) => typeof(ConoscopeView).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(view, null);
    private static void OnSta(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            IConfigService previous = ConfigService.Instance;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
                ConfigService.SetInstance(new MemoryConfig());
                action();
            }
            catch (Exception ex) { failure = ex; }
            finally { ConfigService.SetInstance(previous); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(90)), "Angular workflow did not finish.");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private sealed class MemoryConfig : IConfigService
    {
        private readonly Dictionary<Type, IConfig> values = new();
        public IConfig GetRequiredService(Type type) => values.TryGetValue(type, out IConfig? value) ? value : values[type] = (IConfig)Activator.CreateInstance(type)!;
        public T GetRequiredService<T>() where T : IConfig => (T)GetRequiredService(typeof(T));
        public void SaveConfigs() { }
        public void LoadConfigs() { }
        public void Save<T>() where T : IConfig { }
    }
    public sealed class LocalSampleFactAttribute : FactAttribute
    {
        public LocalSampleFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CONOSCOPE_REAL_SAMPLE"))
                && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CONOSCOPE_RAW_SAMPLE")))
                Skip = "Set CONOSCOPE_REAL_SAMPLE (CVCIE) or CONOSCOPE_RAW_SAMPLE (calibrated CVRAW) to run read-only local validation.";
        }
    }
}
