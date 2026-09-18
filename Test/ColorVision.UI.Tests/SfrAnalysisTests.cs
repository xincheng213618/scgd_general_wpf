using ColorVision.Core;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class SfrNativeFactAttribute : FactAttribute
{
    public SfrNativeFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("COLORVISION_RUN_SFR_NATIVE_TESTS") != "1")
            Skip = "Set COLORVISION_RUN_SFR_NATIVE_TESTS=1 with the matching native DLL to exercise SFR interop and WPF integration.";
    }
}

public sealed class SfrAnalysisTests
{
    [Fact]
    public void ThresholdQueryUsesFirstDownwardCrossingAndNeverAnEndpointFallback()
    {
        double[] x = [0, .1, .2, .3, .4, .5, .6];
        double[] y = [1, .7, .4, .6, .3, .2, .05];
        Assert.Equal(1.0 / 6, SfrCurveQueries.Crossing(x, y, .5)!.Value, 12);
        Assert.Null(SfrCurveQueries.Crossing(x, y, .1));
        Assert.Equal(.5, SfrCurveQueries.Crossing(x, y, .2));
        Assert.Equal(.5, SfrCurveQueries.AtFrequency(x, y, .25));
        Assert.Null(SfrCurveQueries.AtFrequency(x, y, .6));
        Assert.Null(SfrCurveQueries.Crossing(x, y, 0));
        Assert.Null(SfrCurveQueries.Crossing([0, .1, .1], [1, .5, .2], .5));
        Assert.Null(SfrCurveQueries.AtFrequency([0, .1], [1, double.NaN], .05));
    }

    [Fact]
    public void MissingThresholdAndInvalidInputRemainDifferentStates()
    {
        var c = new SfrChannelAnalysis { Valid = true, Reason = "ok", Frequencies = [0, .5, 1], Mtf = [1, .7, .4], EdgePositions = [-1, 1], Esf = [0, 1], LsfPositions = [-1, 1], Lsf = [0, 1] };
        var result = new SfrAnalysisResult { AlgorithmVersion = "2.0", Unit = "cycles/pixel", Nyquist = .5, Channels = [c] };
        Assert.Null(SfrAnalysisResult.Parse(JsonSerializer.Serialize(result)).Channels[0].Mtf50);
        Assert.Throws<FormatException>(() => SfrAnalysisResult.Parse(JsonSerializer.Serialize(result with { Channels = [c with { Mtf50 = .495 }] })));
        Assert.Throws<FormatException>(() => SfrAnalysisResult.Parse(JsonSerializer.Serialize(result with { Channels = [c with { Valid = false }] })));
        Assert.Throws<FormatException>(() => SfrAnalysisResult.Parse(JsonSerializer.Serialize(result with { Channels = [c with { Channel = "R" }] })));
        Assert.Throws<FormatException>(() => SfrAnalysisResult.Parse("{\"algorithmVersion\":\"2.0\",\"unit\":\"cycles/pixel\",\"nyquist\":0.5,\"channels\":null}"));
    }

    [Fact]
    public void OptionsSpecifyRadiometryWithoutRoiStretchingOrImplicitGamma()
    {
        var options = new SfrAnalysisOptions();
        Assert.Contains("\"encoding\":\"unknown\"", options.ToJson(), StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => (options with { MinimumSnr = double.NaN }).Validate());
        Assert.Throws<ArgumentException>(() => (options with { BlackLevel = 10, WhiteLevel = 9 }).Validate());
        Assert.Contains("\"encoding\":\"srgb\"", (options with { InputEncoding = SfrInputEncoding.Srgb }).ToJson(), StringComparison.Ordinal);
    }

    [SfrNativeFact]
    public async Task RealInteropAndWindowShowAllChannelsAndRetainSnapshotUntilClosed()
    {
        var fixture = WpfTestHost.Invoke(() =>
        {
            const int size = 128;
            byte[] bytes = new byte[size * size * 3];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                for (int c = 0; c < 3; c++) bytes[(y * size + x) * 3 + c] = (byte)Math.Round(25 + 205 / (1 + Math.Exp(-(x - 63.5 - .1 * (y - 63.5)) / (1.0 + c * .2))));
            IntPtr pointer = Marshal.AllocCoTaskMem(bytes.Length);
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            var image = new HImage { rows = size, cols = size, channels = 3, depth = 8, stride = size * 3, pData = pointer };
            int released = 0;
            var frame = new SourceImageFrame(image, 42, h => { Marshal.FreeCoTaskMem(h.pData); released++; });
            var lease = frame.Acquire();
            var window = new SfrSimplePlotWindow(lease, new RoiRect(0, 0, size, size), Guid.NewGuid())
            {
                ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20000, Top = -20000
            };
            frame.Dispose();
            Task analysis = (Task)typeof(SfrSimplePlotWindow).GetMethod("AnalyzeAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null)!;
            return (window, lease, analysis, released: (Func<int>)(() => released));
        });
        try
        {
            await fixture.analysis;
            WpfTestHost.Invoke(() => fixture.window.Show());
            await fixture.window.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            WpfTestHost.Invoke(() =>
            {
                var metrics = (DataGrid)fixture.window.FindName("MetricsGrid");
                var status = (TextBlock)fixture.window.FindName("StatusText");
                Assert.True(metrics.Items.Count == 4, status.Text);
                Assert.Contains("4/4", status.Text, StringComparison.Ordinal);
                Assert.True(((Button)fixture.window.FindName("ExportJsonButton")).IsEnabled);
                Assert.Equal(0, fixture.released());
                var content = (FrameworkElement)fixture.window.Content;
                fixture.window.UpdateLayout();
                Assert.True(metrics.ActualWidth > 1000);
                string? capture = Environment.GetEnvironmentVariable("COLORVISION_SFR_CAPTURE");
                if (!string.IsNullOrEmpty(capture))
                {
                    var bitmap = new RenderTargetBitmap((int)fixture.window.ActualWidth, (int)fixture.window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(fixture.window);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = System.IO.File.Create(capture); encoder.Save(stream);
                }
            });
            // A failed parameter rerun must not leave successful numbers or an exportable old result.
            Task rejected = WpfTestHost.Invoke(() =>
            {
                typeof(SfrSimplePlotWindow).GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(fixture.window, new SfrAnalysisOptions { WhiteLevel = 1 });
                return (Task)typeof(SfrSimplePlotWindow).GetMethod("AnalyzeAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(fixture.window, null)!;
            });
            await rejected;
            WpfTestHost.Invoke(() =>
            {
                Assert.Empty(((DataGrid)fixture.window.FindName("MetricsGrid")).Items);
                Assert.False(((Button)fixture.window.FindName("ExportJsonButton")).IsEnabled);
                var plot = (SfrSimplePlotControl)fixture.window.FindName("Plot");
                Assert.Empty(((ScottPlot.WPF.WpfPlot)plot.FindName("WpfPlot")).Plot.GetPlottables());
            });
        }
        finally { WpfTestHost.Invoke(() => fixture.window.Close()); }
        Assert.Equal(1, fixture.released());
        Assert.Throws<ObjectDisposedException>(() => _ = fixture.lease.Image);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    public void PreviewSupportsHighPrecisionPixelsWithoutChangingSource(int depth)
    {
        WpfTestHost.Invoke(() =>
        {
            byte[] bytes = depth switch { 16 => BitConverter.GetBytes((ushort)32768), 32 => BitConverter.GetBytes(.5f), _ => BitConverter.GetBytes(.5) };
            IntPtr pointer = Marshal.AllocCoTaskMem(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, pointer, bytes.Length);
                var image = new HImage { rows = 1, cols = 1, depth = depth, channels = 1, pData = pointer, isDispose = true };
                var preview = SfrSimplePlotWindow.CreatePreview(image, new RoiRect(0, 0, 1, 1), new());
                byte[] pixel = new byte[3]; preview.CopyPixels(pixel, 3, 0);
                Assert.All(pixel, p => Assert.Equal((byte)128, p));
                byte[] after = new byte[bytes.Length]; Marshal.Copy(pointer, after, 0, after.Length); Assert.Equal(bytes, after);
            }
            finally { Marshal.FreeCoTaskMem(pointer); }
        });
    }
}
