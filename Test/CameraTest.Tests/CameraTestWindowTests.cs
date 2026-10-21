using ColorVision.UI.Tests;
using ColorVision.Themes;
using CameraTest.Application;
using CameraTest.Models;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.Core;
using ColorVision.UI;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR;
using System.Reflection;
using System.Text.Json;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CameraTest.Tests;

public sealed class CameraTestWindowTests
{

    private static string TestSettingsPath() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CameraTest-tests", Guid.NewGuid().ToString("N"), "settings.json");

    [Fact]
    public void VideoPreviewDoesNotRebaseMeasurementRegionsOntoAnotherResolution()
    {
        WpfTestHost.Invoke(() =>
        {
            System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml", UriKind.Relative) });
            var window = new CameraTestWindow(TestSettingsPath());
            try
            {
                var profile = Field<TestProfile>(window, "_profile");
                profile.ImageWidth = 400; profile.ImageHeight = 300;
                profile.Regions = [new("Existing", 20, 30, 60, 60)];
                profile.Video.Mode = VideoAnalysisMode.Preview;
                var camera = Field<ColorVision.Engine.Services.Devices.Camera.Local.StandaloneCameraSession>(window, "_camera");
                typeof(ColorVision.Engine.Services.Devices.Camera.Local.StandaloneCameraSession).GetField("_latest", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(camera,
                    new ColorVision.Engine.Services.Devices.Camera.Local.StandaloneCameraFrame(new byte[160 * 120], 160, 120, 8, 1, 160, DateTimeOffset.Now));
                typeof(CameraTestWindow).GetField("_live", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, true);
                Invoke(window, "ProcessLatestFrameAsync");
                Invoke(window, "StopLive");
                Invoke(window, "Refresh");
                Assert.Equal(160, Field<TestFrame>(window, "_frame").Data.Width);
                Assert.Equal(400, profile.ImageWidth);
                Assert.Equal(300, profile.ImageHeight);
                Assert.False(((Button)window.FindName("AnalyzeButton")).IsEnabled);
                Assert.False(camera.IsConnected);
            }
            finally { window.Close(); }
        });
    }

    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    private static object? Invoke(CameraTestWindow window, string name, params object[] args) => typeof(CameraTestWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, args);

    [Fact]
    public async Task StandaloneWindowOpensFileWithoutConnectingDeviceOrLoadingDatabase()
    {
        string output = Path.Combine(Path.GetTempPath(), "CameraTest-validation");
        Directory.CreateDirectory(output);
        string sample = Environment.GetEnvironmentVariable("CAMERATEST_SMOKE_IMAGE") ?? Path.Combine(output, "gray-sample.png");
        CameraTestWindow? window = null;
        Task? loading = null;
        WpfTestHost.Invoke(() =>
        {
            System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml", UriKind.Relative) });
            System.Windows.Application.Current.ApplyTheme(Theme.Dark);
            if (!File.Exists(sample))
            {
                byte[] pixels = Enumerable.Repeat((byte)180, 640 * 480).ToArray();
                var bitmap = BitmapSource.Create(640, 480, 96, 96, PixelFormats.Gray8, null, pixels, 640);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(sample);
                encoder.Save(stream);
            }
            window = new CameraTestWindow(TestSettingsPath());
            window.Width = 1500;
            window.Height = 920;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -20000;
            window.Top = -20000;
            window.ShowInTaskbar = false;
            window.ShowActivated = false;
            window.Show();
            loading = window.OpenImageAsync(sample);
        });
        await loading!;
        WpfTestHost.Invoke(() =>
        {
            Assert.Contains("未连接", ((TextBlock)window!.FindName("CameraSummary")).Text);
            Assert.DoesNotContain("尚未加载", ((TextBlock)window.FindName("FrameText")).Text);
            Assert.True(((Button)window.FindName("AddRegionButton")).IsEnabled);
            Assert.Equal("相机生产调试", window.Title);
            Assert.True(((Button)window.FindName("ArchiveButton")).IsEnabled);
            Assert.False(((MenuItem)window.FindName("ExportMenuItem")).IsEnabled);
        });
        string? profilePath = Environment.GetEnvironmentVariable("CAMERATEST_SMOKE_PROFILE");
        if (!string.IsNullOrWhiteSpace(profilePath))
        {
            var analysis = FrameAnalysis.Run(TestFrame.Open(sample), ProfileStore.Load(profilePath));
            string exportPath = Path.Combine(output, "sample.results.json");
            analysis.Export(exportPath);
            analysis.Export(Path.Combine(output, "sample.results.csv"));
            using var exported = JsonDocument.Parse(File.ReadAllText(exportPath));
            Assert.Equal(2, exported.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.NotEqual(0, exported.RootElement.GetProperty("measurement").GetProperty("ColorShifts").GetArrayLength());
            Assert.Equal(analysis.FrameId, exported.RootElement.GetProperty("measurement").GetProperty("FrameId").GetGuid());
            WpfTestHost.Invoke(() => { window!.LoadProfile(profilePath); loading = window.OpenImageAsync(sample); });
            await loading!;
            WpfTestHost.Invoke(() =>
            {
                var rows = ((DataGrid)window!.FindName("Metrics")).Items.OfType<MetricRow>().ToArray();
                Assert.NotEmpty(rows);
                Assert.Contains(rows, row => row.Channel == "G" && row.Mtf50.HasValue);
                Assert.Contains(rows, row => row.Status == "target_not_found" && row.Mtf50 == null);
                var colors = ((DataGrid)window.FindName("ColorMetrics")).Items.OfType<ColorShiftRow>().ToArray();
                Assert.Equal(analysis.Targets.Count * 4 * 3, colors.Length);
                Assert.Contains(colors, row => !row.Shift.HasValue);
                Assert.Equal("未设置标准", ((TextBlock)window.FindName("VerdictText")).Text);
                Assert.True(((MenuItem)window.FindName("ExportMenuItem")).IsEnabled);
            });
        }
        WpfTestHost.Invoke(() => window!.Close());
    }
}
