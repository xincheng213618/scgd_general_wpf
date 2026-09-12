using ColorVision.Core;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.GridDistortion;
using ColorVision.Themes;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Xunit.Abstractions;

namespace ColorVision.UI.Tests;

public sealed class GridDistortionNativeSampleFactAttribute : FactAttribute
{
    public GridDistortionNativeSampleFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(GridDistortionRealSampleTests.SampleVariable)) &&
            !string.Equals(Environment.GetEnvironmentVariable(GridDistortionRealSampleTests.NativeOptInVariable), "1", StringComparison.Ordinal))
            Skip = $"Set {GridDistortionRealSampleTests.SampleVariable} to a 3x3 CVRAW sample, or {GridDistortionRealSampleTests.NativeOptInVariable}=1 for the synthetic 7x7 native and WPF check.";
    }
}

[Collection(LuminousAreaNativeInteropCollection.CollectionName)]
[Trait("Category", "NativeIntegration")]
public sealed class GridDistortionRealSampleTests(ITestOutputHelper output)
{
    internal const string SampleVariable = "COLORVISION_GRID_DISTORTION_SAMPLE";
    internal const string EvidenceVariable = "COLORVISION_GRID_DISTORTION_EVIDENCE_DIR";
    internal const string NativeOptInVariable = "COLORVISION_RUN_GRID_DISTORTION_NATIVE_TESTS";

    [GridDistortionNativeSampleFact]
    public void RealNativeDetectionFeedsAllAnalysisSchemesAndRendersTheActualWpfResultWindow()
    {
        string? sample = Environment.GetEnvironmentVariable(SampleVariable);
        string? evidence = Environment.GetEnvironmentVariable(EvidenceVariable);
        if (!string.IsNullOrWhiteSpace(evidence))
        {
            evidence = Path.GetFullPath(evidence);
            Directory.CreateDirectory(evidence);
        }
        GridDistortionResult result;
        object imageDescription;
        double wallTime;
        if (!string.IsNullOrWhiteSpace(sample))
        {
            sample = Path.GetFullPath(sample);
            Assert.True(File.Exists(sample), $"Sample does not exist: {sample}");
            string sourceHash = HashFile(sample);
            using LocalFlowFrame frame = LocalFrameFileService.Load(sample);
            using LocalFlowFrameLease lease = frame.Acquire();
            HImage image = LocalFindLuminousAreaNode.CreateBorrowedImage(lease);
            Assert.Equal(lease.Metadata.Width, image.cols);
            Assert.Equal(lease.Metadata.Height, image.rows);
            Assert.True(image.cols > 0 && image.rows > 0);
            Assert.NotEqual(IntPtr.Zero, image.pData);
            imageDescription = new { Source = sample, Width = image.cols, Height = image.rows, Depth = image.depth, Channels = image.channels, image.stride, FileSha256 = sourceHash };
            string pixelsHash = HashImage(image);
            Stopwatch stopwatch = Stopwatch.StartNew();
            result = GridDistortionNative.Run(image, new RoiRect(), new GridDistortionOptions());
            stopwatch.Stop();
            wallTime = stopwatch.Elapsed.TotalMilliseconds;
            Assert.Equal(pixelsHash, HashImage(image));
            Assert.Equal(sourceHash, HashFile(sample));
        }
        else
        {
            using SyntheticGrid image = new();
            imageDescription = new { Source = "Synthetic7x7", Width = image.Image.cols, Height = image.Image.rows, Depth = image.Image.depth, Channels = image.Image.channels };
            string pixelsHash = HashImage(image.Image);
            Stopwatch stopwatch = Stopwatch.StartNew();
            result = GridDistortionNative.Run(image.Image, new RoiRect(), new GridDistortionOptions { ExpectedRows = 7, ExpectedCols = 7 });
            stopwatch.Stop();
            wallTime = stopwatch.Elapsed.TotalMilliseconds;
            Assert.Equal(pixelsHash, HashImage(image.Image));
        }

        // Keep the native rejection diagnostic even when the assertion below fails.
        if (evidence != null) File.WriteAllText(Path.Combine(evidence, "native-result.json"), result.RawJson, new UTF8Encoding(false));
        Assert.True(result.Success, $"{result.StatusCode}: {result.Message}; native={result.NativeReturnCode}; {result.InteropDiagnostic}; JSON={result.RawJson}");
        Assert.True(result.NativeReturnCode > 0);
        Assert.Equal(result.ExpectedRows * result.ExpectedCols, result.SelectedCount);
        Assert.Equal(9, result.ReferencePointIds.Count);
        Assert.NotNull(result.Metrics);
        GridDistortionAnalysis analysis = GridDistortionAnalysis.Calculate(result);
        Assert.True(analysis.Optical.IsAvailable, string.Join("; ", analysis.Optical.Warnings));
        Assert.False(analysis.Optical.IsCalibrated);
        Assert.Equal(result.SelectedCount - 1, analysis.Optical.Samples.Count);
        Assert.Equal(analysis.StandardTv.HorizontalPercent / 2, analysis.HalfTv.HorizontalPercent);
        Assert.Equal(analysis.StandardTv.VerticalPercent / 2, analysis.HalfTv.VerticalPercent);
        string analysisJson = GridDistortionResultWindow.CreateAnalysisJson(result, analysis);
        if (evidence != null)
        {
            File.WriteAllText(Path.Combine(evidence, "all-analysis.json"), analysisJson, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(evidence, "sample-validation.json"), JsonSerializer.Serialize(new
            {
                Image = imageDescription, ManagedNativeCallMilliseconds = wallTime, NativeTimings = result.Timings,
                PixelBufferUnchanged = true, Schemes = new[] { "StandardTv", "HalfTv", "ReferencePoint9", "LegacyPoint9", "CentralPitchRadial/v1" }
            }, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        }

        WpfTestHost.Invoke(() =>
        {
            GridDistortionResultWindow window = new(result, analysis)
            {
                Left = -16000, Top = -16000, WindowStartupLocation = WindowStartupLocation.Manual,
                ShowActivated = false, ShowInTaskbar = false
            };
            foreach (string source in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.RelativeOrAbsolute) });
            try
            {
                window.Show();
                DrainLayout(window);
                DataGrid metrics = Assert.IsType<DataGrid>(window.FindName("MetricsGrid"));
                GridDistortionMetricRow[] rows = metrics.Items.Cast<GridDistortionMetricRow>().ToArray();
                Assert.Equal(2, rows.Count(row => row.Method == "标准 TV"));
                Assert.Equal(2, rows.Count(row => row.Method == "半值 TV"));
                Assert.Equal(6, rows.Count(row => row.Method == "对边均值 9 点"));
                Assert.Equal(6, rows.Count(row => row.Method == "旧 P9 三跨度"));
                Assert.Contains(rows, row => row.Method == "中央节距估计" && row.Description.Contains("非已标定镜头畸变"));
                Assert.True(metrics.IsVisible && metrics.ActualWidth > 0 && metrics.ActualHeight > 0);
                Assert.NotNull(metrics.ItemContainerGenerator.ContainerFromIndex(0));
                Assert.Equal(analysisJson, Assert.IsType<TextBox>(window.FindName("AnalysisJsonText")).Text);
                SaveWindowContent(window, evidence, "result-metrics-top.png");

                metrics.ScrollIntoView(metrics.Items[metrics.Items.Count - 1]);
                DrainLayout(window);
                SaveWindowContent(window, evidence, "result-metrics-bottom.png");

                TabControl tabs = Descendants(window).OfType<TabControl>().Single();
                TabItem opticalTab = tabs.Items.OfType<TabItem>().Single(tab => Equals(tab.Header, "光学相对估计"));
                tabs.SelectedItem = opticalTab;
                DrainLayout(window);
                DataGrid optical = Assert.IsType<DataGrid>(window.FindName("OpticalGrid"));
                Assert.True(optical.IsVisible && optical.ActualWidth > 0 && optical.ActualHeight > 0);
                Assert.Equal(result.SelectedCount - 1, optical.Items.Count);
                Assert.NotNull(optical.ItemContainerGenerator.ContainerFromIndex(0));
                SaveWindowContent(window, evidence, "result-optical.png");
            }
            finally { window.Close(); }
        });
        output.WriteLine($"{result.ExpectedRows}x{result.ExpectedCols}, {result.SelectedCount} points; native={result.Timings.TotalMs:F3} ms; managed native call={wallTime:F3} ms; buffer unchanged; real WPF metrics and optical tabs laid out. Evidence: {evidence ?? "not requested"}");
    }

    private static string HashFile(string file)
    {
        using FileStream stream = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string HashImage(HImage image)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[1024 * 1024];
        int length = checked(image.stride * image.rows);
        for (int offset = 0; offset < length;)
        {
            int count = Math.Min(buffer.Length, length - offset);
            Marshal.Copy(IntPtr.Add(image.pData, offset), buffer, 0, count);
            hash.AppendData(buffer, 0, count);
            offset += count;
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void DrainLayout(Window window)
    {
        window.UpdateLayout();
        DispatcherFrame frame = new();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
        window.UpdateLayout();
    }

    private static void SaveWindowContent(Window window, string? directory, string name)
    {
        if (directory == null) return;
        FrameworkElement content = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
        int width = (int)Math.Ceiling(window.ActualWidth), height = (int)Math.Ceiling(window.ActualHeight);
        Assert.True(width > 0 && height > 0);
        Assert.True(width >= Math.Ceiling(content.ActualWidth + content.Margin.Left + content.Margin.Right));
        Assert.True(height >= Math.Ceiling(content.ActualHeight + content.Margin.Top + content.Margin.Bottom));
        Rect bounds = new(0, 0, width, height);
        VisualBrush windowBrush = new(window)
        {
            AutoLayoutContent = false,
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = bounds,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = bounds,
            Stretch = Stretch.None,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top
        };
        DrawingVisual snapshot = new();
        using (DrawingContext drawing = snapshot.RenderOpen())
        {
            drawing.DrawRectangle(window.Background ?? SystemColors.WindowBrush, null, bounds);
            drawing.DrawRectangle(windowBrush, null, bounds);
        }
        RenderTargetBitmap bitmap = new(width, height, 96, 96, PixelFormats.Pbgra32);
        // A fresh full-size visual/brush renders the whole current WPF tree after
        // tab changes or scrolling, instead of reusing the HWND's dirty regions.
        // Absolute equal-sized viewbox/viewport preserves one DIP per output pixel.
        bitmap.Render(snapshot);
        Assert.Equal(width, bitmap.PixelWidth);
        Assert.Equal(height, bitmap.PixelHeight);
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(Path.Combine(directory, name));
        encoder.Save(stream);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child)) yield return descendant;
        }
    }

    private sealed class SyntheticGrid : IDisposable
    {
        private readonly GCHandle _handle;
        public HImage Image { get; }
        public SyntheticGrid()
        {
            const int width = 1000, height = 800;
            ushort[] pixels = new ushort[width * height];
            Array.Fill(pixels, (ushort)800);
            for (int row = 0; row < 7; row++)
                for (int col = 0; col < 7; col++)
                {
                    int centerX = 140 + col * 120, centerY = 100 + row * 100;
                    for (int dy = -12; dy <= 12; dy++)
                        for (int dx = -12; dx <= 12; dx++)
                            if (dx * dx + dy * dy <= 144) pixels[(centerY + dy) * width + centerX + dx] = 48000;
                }
            _handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            Image = new HImage { cols = width, rows = height, channels = 1, depth = 16, stride = width * 2, pData = _handle.AddrOfPinnedObject(), isDispose = true };
        }
        public void Dispose() => _handle.Free();
    }
}
