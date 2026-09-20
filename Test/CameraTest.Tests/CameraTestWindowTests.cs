using ColorVision.UI.Tests;
using ColorVision.Themes;
using CameraTest.Application;
using CameraTest.Models;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
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
    [Fact]
    public async Task DrawnRectanglesCanAnalyzeAndStaySynchronizedThroughEditingAndUndo()
    {
        string output = Path.Combine(Path.GetTempPath(), "CameraTest-drawing-validation");
        Directory.CreateDirectory(output);
        string? fieldSample = Environment.GetEnvironmentVariable("CAMERATEST_SMOKE_IMAGE");
        string sample = fieldSample ?? Path.Combine(output, "gray.png");
        CameraTestWindow? window = null;
        Task? pending = null;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml", UriKind.Relative) });
                if (fieldSample == null)
                {
                    var bitmap = BitmapSource.Create(3840, 2160, 96, 96, PixelFormats.Gray8, null, Enumerable.Repeat((byte)180, 3840 * 2160).ToArray(), 3840);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(sample);
                    encoder.Save(stream);
                }
                window = new CameraTestWindow { Width = 1500, Height = 920, Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual, ShowInTaskbar = false, ShowActivated = false };
                window.Show();
                pending = window.OpenImageAsync(sample);
            });
            await pending!;
            DVRectangleText[] drawings = [];
            WpfTestHost.Invoke(() =>
            {
                var editor = ((ImageView)window!.FindName("ImageView")).EditorContext.DrawEditorContext;
                var analyze = (Button)window.FindName("AnalyzeButton");
                Assert.False(analyze.IsEnabled);
                Rect[] bounds = [new(650, 250, 500, 510), new(650, 1420, 500, 500), new(2700, 1410, 510, 500), new(2700, 250, 510, 510)];
                drawings = bounds.Select((rect, i) => new DVRectangleText(new() { Id = i + 1, Text = $"Point_{i + 1}", Rect = rect })).ToArray();
                // RectangleManager uses this same editor command to commit a drawn rectangle.
                foreach (var drawing in drawings) editor.DrawCanvas.AddVisualCommand(drawing);
                Assert.Equal(4, ((ListBox)window.FindName("RegionList")).Items.Count);
                Assert.True(analyze.IsEnabled);
                Assert.Equal("开始分析", analyze.Content);
                pending = window.AnalyzeCurrentFrameAsync();
            });
            await pending!;
            WpfTestHost.Invoke(() =>
            {
                var rows = ((DataGrid)window!.FindName("Metrics")).Items.OfType<MetricRow>().ToArray();
                Assert.Equal(4, rows.Select(r => r.Target).Distinct().Count());
                Assert.All(rows.GroupBy(r => r.Target), group => Assert.Equal(4, group.Select(r => r.Edge).Distinct().Count()));
                if (fieldSample != null)
                {
                    Assert.Equal(64, rows.Length);
                    Assert.Equal(16, rows.Count(r => r.Channel == "G" && r.Mtf50.HasValue));
                }
                Assert.True(((Button)window.FindName("ExportButton")).IsEnabled);
                var editor = ((ImageView)window.FindName("ImageView")).EditorContext.DrawEditorContext;
                var list = (ListBox)window.FindName("RegionList");
                drawings[0].SetRect(new(660, 250, 500, 510));
                Assert.Equal(660, list.Items.OfType<SearchRegion>().Single(r => r.Id == "Point_1").X);
                Assert.Empty(((DataGrid)window.FindName("Metrics")).Items);
                Assert.False(((Button)window.FindName("ExportButton")).IsEnabled);
                editor.DrawCanvas.RemoveVisualCommand(drawings[0]);
                Assert.Equal(3, list.Items.Count);
                editor.DrawCanvas.Undo();
                Assert.Equal(4, list.Items.Count);
                Assert.Single(list.Items.OfType<SearchRegion>(), r => r.Id == "Point_1");
                editor.DrawCanvas.Redo();
                Assert.Equal(3, list.Items.Count);
                var duplicateName = new DVRectangleText(new() { Text = "Point_1", Rect = new(650, 250, 500, 510) });
                editor.DrawCanvas.AddVisualCommand(duplicateName);
                Assert.NotEqual("Point_1", duplicateName.Attribute.Text);
                editor.DrawCanvas.AddVisual(drawings[0]);
                Assert.Equal(5, list.Items.OfType<SearchRegion>().Select(r => r.Id).Distinct().Count());
                editor.DrawCanvas.RemoveVisual(drawings[0]);
                duplicateName.SetRect(new(0, 0, 30, 30));
                Assert.False(((Button)window.FindName("AnalyzeButton")).IsEnabled);
                Assert.Contains("40×40", ((TextBlock)window.FindName("StatusText")).Text);
                duplicateName.SetRect(new(650, 250, 500, 510));
                Assert.True(((Button)window.FindName("AnalyzeButton")).IsEnabled);

                string profilePath = Path.Combine(output, "drawn.profile.json");
                ProfileStore.Save(profilePath, new TestProfile { ImageWidth = 3840, ImageHeight = 2160, Regions = list.Items.OfType<SearchRegion>().ToList() });
                window.LoadProfile(profilePath);
                Assert.Equal(4, editor.DrawingVisualLists.OfType<IRectangle>().Count());
                Assert.Empty(editor.DrawCanvas.UndoStack);
                var restored = editor.DrawingVisualLists.OfType<DVRectangleText>().First();
                restored.SetRect(new(100, 100, 150, 150));
                Assert.Equal(100, list.Items.OfType<SearchRegion>().Single(r => r.Id == restored.Attribute.Text).X);
                list.SelectedItem = list.Items[0];
                ((Button)window.FindName("RemoveRegionButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(3, list.Items.Count);
                Assert.Equal(3, editor.DrawingVisualLists.OfType<IRectangle>().Count());
            });
            await WpfTestHost.Invoke(async () =>
            {
                var add = (Button)window!.FindName("AddRegionButton");
                var canvas = ((ImageView)window.FindName("ImageView")).EditorContext.DrawEditorContext.DrawCanvas;
                add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.True(canvas.IsEnabled);
                canvas.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                await Dispatcher.Yield(DispatcherPriority.Background);
                Assert.True(add.IsEnabled);
                Assert.Equal(3, ((ListBox)window.FindName("RegionList")).Items.Count);
            });
        }
        finally { WpfTestHost.Invoke(() => window?.Close()); }
    }

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
            window = new CameraTestWindow();
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
            Assert.False(((Button)window.FindName("ExportButton")).IsEnabled);
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
                Assert.True(((Button)window.FindName("ExportButton")).IsEnabled);
            });
        }
        WpfTestHost.Invoke(() =>
        {
            Assert.InRange(((ImageView)window!.FindName("ImageView")).EditorContext.DrawEditorContext.Zoombox.ContentMatrix.M11, 0.01, 1);
            var tabs = (TabControl)window.FindName("AnalysisTabs");
            string[] images = ["window.png", "window-color.png", "window-focus.png", "window-judgment.png"];
            for (int i = 0; i < images.Length; i++)
            {
                tabs.SelectedIndex = i;
                window.Measure(new Size(1500, 920));
                window.Arrange(new Rect(0, 0, 1500, 920));
                window.UpdateLayout();
                var render = new RenderTargetBitmap(1500, 920, 96, 96, PixelFormats.Pbgra32);
                render.Render((Visual)window.Content);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(render));
                using var stream = File.Create(Path.Combine(output, images[i]));
                encoder.Save(stream);
            }
            window.Close();
        });
    }
}
