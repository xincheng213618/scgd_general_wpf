using CameraTest.Application;
using CameraTest.Models;
using ColorVision.Core;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.UI.Tests;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace CameraTest.Tests;

public sealed class CameraOperationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string Root() => Path.Combine(Path.GetTempPath(), "CameraTest-operations-tests", Guid.NewGuid().ToString("N"));
    private static T Field<T>(CameraTestWindow window, string name) => (T)typeof(CameraTestWindow).GetField(name, Private)!.GetValue(window)!;
    private static object? Invoke(CameraTestWindow window, string name, params object[] args) => typeof(CameraTestWindow).GetMethod(name, Private)!.Invoke(window, args);
    private static CameraTestWindow Window(string directory)
    {
        System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml", UriKind.Relative) });
        return new CameraTestWindow(Path.Combine(directory, "settings.json"));
    }

    [Fact]
    public void ReopeningRemembersAcquisitionAndVideoWithoutConnectingHardware()
    {
        WpfTestHost.Invoke(() =>
        {
            string directory = Root();
            var first = Window(directory);
            Invoke(first, "QueueAcquisition", true, 138.65173f);
            Invoke(first, "QueueAcquisition", false, 14.851484f);
            var ids = (ComboBox)first.FindName("CameraIds");
            ids.ItemsSource = new[] { "test-device" }; ids.SelectedIndex = 0;
            var save = (CheckBox)first.FindName("SaveCaptureCheck");
            save.IsChecked = true;
            save.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
            ((ComboBox)first.FindName("VideoModeSelector")).SelectedIndex = 1;
            first.Close(); // Flush the debounce; no timer or sleep is needed.
            var second = Window(directory);
            try
            {
                var profile = Field<TestProfile>(second, "_profile");
                Assert.Equal(138.65173f, profile.Camera.ExposureMilliseconds);
                Assert.Equal(14.851484f, profile.Camera.Gain);
                Assert.Equal("test-device", ((ComboBox)second.FindName("CameraIds")).SelectedItem);
                Assert.True(((CheckBox)second.FindName("SaveCaptureCheck")).IsChecked);
                Assert.Equal(VideoAnalysisMode.Sharpness, profile.Video.Mode);
                Assert.False(Field<StandaloneCameraSession>(second, "_camera").IsConnected);
                Assert.Empty(profile.Regions); // Operating preferences never import image-specific POIs.
            }
            finally { second.Close(); }
        });
    }

    [Fact]
    public void InvalidSettingsAreReportedAndNeverSilentlyOverwriteTheOriginal()
    {
        string directory = Root();
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "settings.json");
        File.WriteAllText(path, "broken settings");
        WpfTestHost.Invoke(() =>
        {
            var window = Window(directory);
            Assert.Contains("读取失败", ((TextBlock)window.FindName("StatusText")).Text);
            Assert.Equal(100, Field<TestProfile>(window, "_profile").Camera.ExposureMilliseconds);
            window.Close();
        });
        Assert.Equal("broken settings", File.ReadAllText(path));
        var store = new CameraOperationSettingsStore(path);
        Assert.Throws<InvalidDataException>(() => store.Save(new() { Camera = new() { ExposureMilliseconds = float.NaN } }));
        Assert.Equal("broken settings", File.ReadAllText(path));
    }

    [Fact]
    public async Task AutomaticCaptureSaveHonorsOptInPreserves16BitPixelsAndReportsFailures()
    {
        string directory = Root();
        CameraTestWindow? window = null;
        Task? pending = null;
        byte[] pixels = Enumerable.Range(0, 8 * 6 * 6).Select(i => (byte)(i * 13 % 256)).ToArray();
        var options = new StandaloneCameraOptions { CameraId = "sample", ExposureMilliseconds = 138.6f, Gain = 14.8f };
        TestFrame Frame() => new(new(pixels, 8, 6, 16, 3, 48, DateTimeOffset.Now), "Capture:test", FrameSourceKind.Capture, options);
        try
        {
            WpfTestHost.Invoke(() =>
            {
                window = Window(directory);
                Field<CameraOperationSettings>(window, "_operationSettings").CaptureDirectory = Path.Combine(directory, "captures");
                pending = (Task)Invoke(window, "AcceptCaptureAsync", Frame(), 42d, Stopwatch.StartNew())!;
            });
            await pending!;
            Assert.False(Directory.Exists(Path.Combine(directory, "captures")));
            WpfTestHost.Invoke(() =>
            {
                Assert.Contains("未保存", ((TextBlock)window!.FindName("FrameSaveText")).Text);
                Assert.Contains("取图 42 ms", ((TextBlock)window.FindName("CaptureTimingText")).Text);
                Field<CameraOperationSettings>(window, "_operationSettings").SaveCapturedImages = true;
                pending = (Task)Invoke(window, "AcceptCaptureAsync", Frame(), 53d, Stopwatch.StartNew())!;
            });
            await pending!;
            string image = Assert.Single(Directory.GetFiles(Path.Combine(directory, "captures"), "*.png"));
            var restored = TestFrame.Open(image);
            Assert.Equal(16, restored.Data.BitDepth);
            Assert.Equal(pixels, restored.Data.Pixels);
            using var metadata = JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(image, ".json")));
            Assert.Equal(53, metadata.RootElement.GetProperty("CaptureMilliseconds").GetDouble());
            Assert.Equal(options.ExposureMilliseconds, metadata.RootElement.GetProperty("AcquisitionSettings").GetProperty("ExposureMilliseconds").GetSingle());
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal("已保存", ((TextBlock)window!.FindName("FrameSaveText")).Text);
                Assert.Contains("保存", ((TextBlock)window.FindName("CaptureTimingText")).ToolTip.ToString());
                string blocker = Path.Combine(directory, "file-instead-of-folder");
                File.WriteAllText(blocker, "preserve");
                Field<CameraOperationSettings>(window, "_operationSettings").CaptureDirectory = blocker;
                pending = (Task)Invoke(window, "AcceptCaptureAsync", Frame(), 64d, Stopwatch.StartNew())!;
            });
            await pending!;
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal("保存失败", ((TextBlock)window!.FindName("FrameSaveText")).Text);
                Assert.Contains("取图成功", ((TextBlock)window.FindName("StatusText")).Text);
                Assert.Equal(pixels, Field<TestFrame>(window, "_frame").Data.Pixels);
            });
            Assert.Equal("preserve", File.ReadAllText(Path.Combine(directory, "file-instead-of-folder")));
        }
        finally { if (window != null) WpfTestHost.Invoke(window.Close); }
    }

    [Fact]
    public void DisplayFpsUsesActualPresentedFramesAndDetectsInterruptedInput()
    {
        var metrics = new VideoRunMetrics();
        for (int i = 1; i <= 5; i++) metrics.Presented(TimeSpan.FromMilliseconds(200 * i), 123.4, 17);
        Assert.Equal(5, metrics.DisplayFramesPerSecond);
        Assert.Equal(123.4, metrics.Sharpness);
        Assert.True(metrics.IsStalled(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(3)));
        metrics.Reset();
        Assert.Null(metrics.DisplayFramesPerSecond);
        Assert.Null(metrics.Sharpness);
        metrics.Presented(TimeSpan.FromSeconds(2), double.NaN, 2000);
        Assert.Equal(.5, metrics.DisplayFramesPerSecond);
        Assert.Null(metrics.Sharpness);
    }

    [Fact]
    public async Task StopWaitsForInFlightWorkAndStillCompletesAfterAnAnalysisFailure()
    {
        CameraTestWindow? window = null;
        var work = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? stopping = null;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                window = Window(Root());
                typeof(CameraTestWindow).GetField("_live", Private)!.SetValue(window, true);
                typeof(CameraTestWindow).GetField("_operationTask", Private)!.SetValue(window, work.Task);
                Invoke(window, "Stop_Click", window, new RoutedEventArgs());
                stopping = Field<Task>(window, "_stopTask");
                Assert.False(stopping.IsCompleted);
                Assert.False(((Button)window.FindName("LiveButton")).IsEnabled);
                Assert.False(((Button)window.FindName("CaptureButton")).IsEnabled);
                work.SetException(new InvalidOperationException("Synthetic analysis failure"));
            });
            await stopping!;
            WpfTestHost.Invoke(() =>
            {
                Assert.False(Field<bool>(window!, "_live"));
                Assert.Contains("已冻结", ((TextBlock)window!.FindName("StatusText")).Text);
                Assert.False(Field<StandaloneCameraSession>(window, "_camera").IsConnected);
            });
        }
        finally { work.TrySetResult(); if (window != null) WpfTestHost.Invoke(window.Close); }
    }

    [Fact]
    public void RepeatedSavingNeverOverwritesOrDeletesAnExistingCapture()
    {
        string directory = Root();
        var frame = new TestFrame(new(new byte[] { 20, 30 }, 2, 1, 8, 1, 2, DateTimeOffset.Now), "repeated");
        var saved = CaptureFileStore.Save(frame, directory, 12);
        byte[] original = File.ReadAllBytes(saved.ImagePath);
        Assert.Throws<IOException>(() => CaptureFileStore.Save(frame, directory, 12));
        Assert.Equal(original, File.ReadAllBytes(saved.ImagePath));
        Assert.True(File.Exists(Path.ChangeExtension(saved.ImagePath, ".json")));
    }

    [Fact]
    public async Task VideoComputesSfrAndSharpnessWithoutTakingOverCameraControls()
    {
        CameraTestWindow? window = null;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? busy = null, analysis = null;
        int disconnectStateChanges = 0;
        var data = BmwFrame();
        try
        {
            WpfTestHost.Invoke(() =>
            {
                window = Window(Root());
                var profile = Field<TestProfile>(window, "_profile");
                profile.ImageWidth = data.Width; profile.ImageHeight = data.Height;
                profile.Regions = [new("Center", 100, 30, 440, 420)];
                profile.Sfr.InputEncoding = SfrInputEncoding.Linear;
                profile.Video.Mode = VideoAnalysisMode.BmwSfr;
                typeof(CameraTestWindow).GetField("_live", Private)!.SetValue(window, true);
                busy = (Task)Invoke(window, "PerformAsync", (Func<Task>)(() => gate.Task))!;
                var camera = Field<StandaloneCameraSession>(window, "_camera");
                var latest = typeof(StandaloneCameraSession).GetField("_latest", Private)!;
                latest.SetValue(camera, data with { CapturedAt = DateTimeOffset.Now.AddSeconds(-2) });
                Invoke(window, "ProcessLatestFrameAsync");
                latest.SetValue(camera, data);
                Invoke(window, "ProcessLatestFrameAsync");
                Assert.Null(typeof(CameraTestWindow).GetField("_frame", Private)!.GetValue(window));
                Assert.Same(data, latest.GetValue(camera));
                gate.SetResult();
            });
            await busy!;
            WpfTestHost.Invoke(() =>
            {
                var camera = Field<StandaloneCameraSession>(window!, "_camera");
                // A sentinel connection is used only for UI state; never pass it to the SDK.
                typeof(StandaloneCameraSession).GetField("_handle", Private)!.SetValue(camera, new IntPtr(1));
                Invoke(window!, "Refresh");
                var disconnect = (Button)window!.FindName("DisconnectButton");
                Assert.True(disconnect.IsEnabled);
                disconnect.IsEnabledChanged += (_, _) => disconnectStateChanges++;
                analysis = (Task)Invoke(window!, "ProcessLatestFrameAsync")!;
                Assert.False(Field<bool>(window, "_busy"));
                Assert.True(Field<bool>(window, "_processingFrame"));
                Assert.True(disconnect.IsEnabled);
                var next = data with { CapturedAt = data.CapturedAt.AddSeconds(1) };
                typeof(StandaloneCameraSession).GetField("_latest", Private)!.SetValue(camera, next);
                var overlapping = (Task<bool>)Invoke(window, "ProcessLatestFrameAsync")!;
                Assert.True(overlapping.IsCompletedSuccessfully);
                Assert.False(overlapping.Result);
                Assert.Same(next, typeof(StandaloneCameraSession).GetField("_latest", Private)!.GetValue(camera));
            });
            await analysis!;
            WpfTestHost.Invoke(() =>
            {
                var frame = Field<TestFrame>(window!, "_frame");
                var result = Field<FrameAnalysis>(window!, "_result");
                Assert.Equal(0, disconnectStateChanges);
                Assert.Same(data, frame.Data);
                Assert.Equal(frame.Id, result.FrameId);
                Assert.True(Assert.Single(result.Targets).Located);
                Assert.Equal(4, result.Rows().Count);
                Assert.NotNull(Field<VideoRunMetrics>(window!, "_videoMetrics").Sharpness);
                Assert.Contains("清晰度", ((TextBlock)window!.FindName("VideoSharpnessText")).Text);
                Assert.Contains("计算", ((TextBlock)window.FindName("VideoAnalysisText")).Text);
            });
        }
        finally
        {
            gate.TrySetResult();
            if (window != null) WpfTestHost.Invoke(() =>
            {
                typeof(StandaloneCameraSession).GetField("_handle", Private)!.SetValue(Field<StandaloneCameraSession>(window, "_camera"), IntPtr.Zero);
                window.Close();
            });
        }
    }

    [Fact]
    public async Task ContinuousPreviewProcessesAvailableFramesAndStopsWithoutAPendingTimer()
    {
        CameraTestWindow? window = null;
        Task? run = null;
        int shown = 0;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                window = Window(Root());
                Field<TestProfile>(window, "_profile").Video.Mode = VideoAnalysisMode.Preview;
                var camera = Field<StandaloneCameraSession>(window, "_camera");
                var latest = typeof(StandaloneCameraSession).GetField("_latest", Private)!;
                var data = new StandaloneCameraFrame(new byte[80 * 60], 80, 60, 8, 1, 80, DateTimeOffset.Now);
                latest.SetValue(camera, data);
                ((ColorVision.ImageEditor.ImageView)window.FindName("ImageView")).ImageSourceLoaded += (_, _) =>
                {
                    shown++;
                    if (shown < 4) latest.SetValue(camera, data with { CapturedAt = data.CapturedAt.AddSeconds(shown) });
                    else window.Dispatcher.BeginInvoke(new Action(() => Invoke(window, "StopLive")));
                };
                typeof(CameraTestWindow).GetField("_live", Private)!.SetValue(window, true);
                run = (Task)Invoke(window, "RunVideoAsync", Field<long>(window, "_generation"))!;
            });
            await run!;
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(4, shown);
                Assert.False(Field<bool>(window!, "_live"));
                Assert.False(Field<bool>(window!, "_processingFrame"));
            });
        }
        finally { if (window != null) WpfTestHost.Invoke(window.Close); }
    }

    [Fact]
    public async Task DisconnectWaitsForVideoAnalysisWithoutBlockingTheButton()
    {
        CameraTestWindow? window = null;
        var frame = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? stopping = null;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                window = Window(Root());
                typeof(CameraTestWindow).GetField("_live", Private)!.SetValue(window, true);
                typeof(CameraTestWindow).GetField("_frameTask", Private)!.SetValue(window, frame.Task);
                Invoke(window, "Disconnect_Click", window, new RoutedEventArgs());
                stopping = Field<Task>(window, "_stopTask");
                Assert.False(stopping.IsCompleted);
                Assert.True(Field<bool>(window, "_stopping"));
                Assert.False(((Button)window.FindName("CaptureButton")).IsEnabled);
            });
            frame.SetResult(false);
            await stopping!;
            WpfTestHost.Invoke(() => Assert.Contains("已冻结", ((TextBlock)window!.FindName("StatusText")).Text));
        }
        finally { frame.TrySetResult(false); if (window != null) WpfTestHost.Invoke(window.Close); }
    }

    private static StandaloneCameraFrame BmwFrame()
    {
        const int width = 640, height = 480;
        byte[] pixels = new byte[width * height];
        double angle = 6 * Math.PI / 180;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                double dx = x - 320, dy = y - 240;
                double u = dx * Math.Cos(angle) + dy * Math.Sin(angle), v = -dx * Math.Sin(angle) + dy * Math.Cos(angle);
                pixels[y * width + x] = (byte)(dx * dx + dy * dy < 160 * 160 && u * v < 0 ? 20 : 220);
            }
        return new(pixels, width, height, 8, 1, width, DateTimeOffset.Now);
    }
}
