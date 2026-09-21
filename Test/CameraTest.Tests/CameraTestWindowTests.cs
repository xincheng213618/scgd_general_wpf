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
    [Fact]
    public async Task CheckerboardRegionsUseSharedFourEdgeResultsAndDisplayDetectedType()
    {
        CameraTestWindow? window = null;
        Task? pending = null;
        int regionCount = 0;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml", UriKind.Relative) });
                window = new CameraTestWindow(TestSettingsPath()) { Width=1500,Height=920,Left=-20000,Top=-20000,ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual };
                window.Show();
                TestFrame frame;
                Rect[] regions;
                if (Environment.GetEnvironmentVariable("CAMERATEST_CHECKERBOARD_IMAGE") is { Length: > 0 } path)
                {
                    frame = TestFrame.Open(path);
                    regions = [new(5136,3113,500,500),new(3729,3140,500,500),new(4425,2773,500,500),new(5117,2234,500,500),new(3714,2261,500,500)];
                }
                else
                {
                    const int size=720;
                    byte[] pixels=new byte[size*size]; double angle=5*Math.PI/180;
                    for(int y=0;y<size;y++) for(int x=0;x<size;x++)
                    {
                        double dx=x-360,dy=y-360;
                        int u=(int)Math.Floor((dx*Math.Cos(angle)+dy*Math.Sin(angle))/180),v=(int)Math.Floor((-dx*Math.Sin(angle)+dy*Math.Cos(angle))/180);
                        pixels[y*size+x]=(byte)((u+v)%2==0?40:200);
                    }
                    frame=new(new(pixels,size,size,8,1,size,DateTimeOffset.Now),"Synthetic checkerboard");
                    regions=[new(60,60,600,600),new(150,150,420,420),new(293,250,160,230)];
                }
                var profile=Field<TestProfile>(window,"_profile");
                regionCount=regions.Length;
                ((ComboBox)window.FindName("ChartTypeSelector")).SelectedIndex = 2;
                Assert.Equal(SfrChartType.Auto, profile.MeasurementRoi.ChartType);
                Invoke(window,"ShowFrame",frame);
                var draw=((ImageView)window.FindName("ImageView")).EditorContext.DrawEditorContext;
                foreach(var r in regions) draw.DrawCanvas.AddVisualCommand(new DVRectangleText(new() { Rect=r }));
                pending=window.AnalyzeCurrentFrameAsync();
            });
            await pending!;
            await window!.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            WpfTestHost.Invoke(() =>
            {
                var result=Field<FrameAnalysis>(window!,"_result");
                Assert.Equal(regionCount,result.Targets.Count);
                Assert.All(result.Targets,t => { Assert.True(t.Located); Assert.Equal(SfrChartType.Checkerboard,t.DetectedChartType); Assert.Equal(4,t.Edges.Count); });
                var rows=((DataGrid)window!.FindName("Metrics")).Items.OfType<MetricRow>().ToArray();
                Assert.Equal(regionCount*4,rows.Length);
                Assert.All(rows,r => Assert.Equal(r.Analysis == null ? "—" : "Y (L)",r.Channel));
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CAMERATEST_CHECKERBOARD_IMAGE")))
                {
                    var partial = result.Targets.Last();
                    Assert.Contains(partial.Edges, edge => edge.Analysis != null);
                    Assert.Contains(partial.Edges, edge => edge.Analysis == null && edge.Reason == "checkerboard_insufficient_edge_support");
                    var unavailable = Assert.Single(rows.Where(row => row.Target == partial.Id && row.Status == "checkerboard_insufficient_edge_support"));
                    Assert.Null(unavailable.Mtf50);
                    Assert.Contains("增大外部选框", MeasurementOverview.Explain(unavailable.Status));
                }
                Assert.Contains("棋盘格",((TextBlock)window.FindName("SelectedMetricCaption")).Text);
                if(Environment.GetEnvironmentVariable("CAMERATEST_CHECKERBOARD_CAPTURE") is { Length: >0 } capture)
                {
                    window.UpdateLayout();
                    var bitmap=new RenderTargetBitmap(1500,920,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
                    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(capture);encoder.Save(stream);
                }
            });
        }
        finally { if(window!=null) WpfTestHost.Invoke(window.Close); }
    }

    private static string TestSettingsPath() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CameraTest-tests", Guid.NewGuid().ToString("N"), "settings.json");

    [Fact]
    public void UpdatingRegionGeometryPreservesTheCanvasMultiSelection()
    {
        WpfTestHost.Invoke(() =>
        {
            System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml", UriKind.Relative) });
            var window = new CameraTestWindow(TestSettingsPath());
            try
            {
                var frame = new ColorVision.Engine.Services.Devices.Camera.Local.StandaloneCameraFrame(new byte[400 * 300], 400, 300, 8, 1, 400, DateTimeOffset.Now);
                Invoke(window, "ShowFrame", new TestFrame(frame, "Selection regression", FrameSourceKind.Capture));
                var draw = ((ImageView)window.FindName("ImageView")).EditorContext.DrawEditorContext;
                var list = (ListBox)window.FindName("RegionList");
                DVRectangleText first = new(new() { Text = "Point_1", Rect = new(20, 30, 60, 70) });
                DVRectangleText second = new(new() { Text = "Point_2", Rect = new(120, 30, 60, 70) });
                draw.DrawCanvas.AddVisualCommand(first);
                draw.DrawCanvas.AddVisualCommand(second);
                list.SelectedIndex = 0;
                Assert.Same(first, draw.SelectionVisual.PrimarySelectedVisual);
                draw.SelectionVisual.SetRenders(new[] { first, second });

                // Each drag update raises synchronous geometry notifications and rebuilds RegionList.
                for (int step = 1; step <= 2; step++)
                {
                    first.SetRect(new(20 + step * 6, 30, 60, 70));
                    second.SetRect(new(120 + step * 6, 30, 60, 70));
                    Assert.Equal(new ISelectVisual[] { first, second }, draw.SelectionVisual.SelectVisuals);
                    Assert.Equal(20 + step * 6, list.Items.OfType<SearchRegion>().Single(r => r.Id == "Point_1").X);
                    Assert.Equal(120 + step * 6, list.Items.OfType<SearchRegion>().Single(r => r.Id == "Point_2").X);
                }

                // Deliberate list selection must still select the corresponding drawing.
                list.SelectedIndex = 1;
                Assert.Same(second, draw.SelectionVisual.PrimarySelectedVisual);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void UnifiedBmwSettingsCancelOrApplyDisplayAndExistingRoiParametersTogether()
    {
        WpfTestHost.Invoke(() =>
        {
            System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml", UriKind.Relative) });
            var window = new CameraTestWindow(TestSettingsPath()) { WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000, ShowInTaskbar = false, ShowActivated = false };
            try
            {
                window.Show();
                var profile = Field<TestProfile>(window, "_profile");
                profile.MeasurementRoi = new() { AlongEdgePixels = 80, AcrossEdgePixels = 60, CenterDistancePixels = 95 };
                var originalRoi = profile.MeasurementRoi with { };
                foreach (bool confirm in new[] { false, true })
                {
                    Exception? failure = null;
                    window.Dispatcher.BeginInvoke(() =>
                    {
                        var dialog = System.Windows.Application.Current.Windows.OfType<PropertyEditorWindow>().Single();
                        try
                        {
                            var edited = Assert.IsType<BmwSfrViewSettings>(dialog.EditConfig);
                            Assert.Equal(originalRoi, edited.MeasurementRoi);
                            edited.MeasurementRoi.AlongEdgePixels = 100;
                            edited.MeasurementRoi.ChartType = SfrChartType.Checkerboard;
                            edited.Display.ShowTargetCenter = false;
                            if (confirm && Environment.GetEnvironmentVariable("CAMERATEST_SETTINGS_CAPTURE") is { Length: > 0 } path)
                            {
                                dialog.TreeNodes.Single(n => n.Header == "图卡与测量框").IsSelected = true;
                                dialog.UpdateLayout();
                                var bitmap = new RenderTargetBitmap((int)dialog.ActualWidth, (int)dialog.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                                bitmap.Render(dialog);
                                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                                using var stream = File.Create(path); encoder.Save(stream);
                            }
                            if (confirm) ((Button)dialog.FindName("ConfirmButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            else dialog.Close();
                        }
                        catch (Exception error) { failure = error; dialog.Close(); }
                    }, DispatcherPriority.ApplicationIdle);
                    ((MenuItem)window.FindName("DisplaySettingsMenuItem")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    Assert.Null(failure);
                    Assert.Equal(confirm ? 100 : 80, profile.MeasurementRoi.AlongEdgePixels);
                    Assert.Equal(confirm ? SfrChartType.Checkerboard : SfrChartType.Bmw, profile.MeasurementRoi.ChartType);
                    Assert.Equal(confirm ? 1 : 0, ((ComboBox)window.FindName("ChartTypeSelector")).SelectedIndex);
                    Assert.Equal(60, profile.MeasurementRoi.AcrossEdgePixels);
                    Assert.Equal(95, profile.MeasurementRoi.CenterDistancePixels);
                    Assert.Equal(!confirm, profile.Display.ShowTargetCenter);
                }
            }
            finally { window.Close(); }
        });
    }

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
                window = new CameraTestWindow(TestSettingsPath()) { Width = 1500, Height = 920, Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual, ShowInTaskbar = false, ShowActivated = false };
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
                VerifyDisplaySettingsAndZoom(window);
                if (fieldSample != null)
                {
                    Assert.Equal(64, rows.Length);
                    Assert.Equal(16, rows.Count(r => r.Channel == "G" && r.Mtf50.HasValue));
                    VerifyInnerEdgeSelectionAndSfr(window);
                }
                Assert.True(((MenuItem)window.FindName("ExportMenuItem")).IsEnabled);
                var editor = ((ImageView)window.FindName("ImageView")).EditorContext.DrawEditorContext;
                var list = (ListBox)window.FindName("RegionList");
                drawings[0].SetRect(new(660, 250, 500, 510));
                Assert.Equal(660, list.Items.OfType<SearchRegion>().Single(r => r.Id == "Point_1").X);
                Assert.Empty(((DataGrid)window.FindName("Metrics")).Items);
                Assert.False(((MenuItem)window.FindName("ExportMenuItem")).IsEnabled);
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
                ((MenuItem)window.FindName("RemoveRegionMenuItem")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
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

    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    private static object? Invoke(CameraTestWindow window, string name, params object[] args) => typeof(CameraTestWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, args);

    private static void VerifyDisplaySettingsAndZoom(CameraTestWindow window)
    {
        var view = (ImageView)window.FindName("ImageView");
        var draw = view.EditorContext.DrawEditorContext;
        var originalResult = Field<FrameAnalysis>(window, "_result");
        static IEnumerable<Rect> Glyphs(Drawing drawing)
        {
            if (drawing is GlyphRunDrawing glyph) yield return glyph.Bounds;
            if (drawing is DrawingGroup group)
                foreach (var child in group.Children)
                    foreach (var bounds in Glyphs(child)) yield return group.Transform?.TransformBounds(bounds) ?? bounds;
        }
        double TextHeight() => draw.DrawCanvas.Visuals.OfType<DrawingVisual>().SelectMany(v => Glyphs(v.Drawing)).First().Height * draw.ZoomRatio;
        double height = TextHeight();
        draw.Zoombox.Zoom(2);
        Assert.Equal(height, TextHeight(), 4);
        Exception? editorFailure = null;
        window.Dispatcher.BeginInvoke(() =>
        {
            var dialog = System.Windows.Application.Current.Windows.OfType<PropertyEditorWindow>().Single();
            try
            {
                var settings = (BmwSfrViewSettings)dialog.EditConfig;
                Assert.Equal(Field<TestProfile>(window, "_profile").MeasurementRoi, settings.MeasurementRoi);
                settings.Display.FontSize = 24;
                settings.Display.Metric = BmwSfrDisplayMetric.AtFrequency;
                settings.Display.Frequency = .25;
                settings.Display.ShowTargetCenter = false;
                settings.Display.ShowRoiDimensions = true;
                ((Button)dialog.FindName("ConfirmButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            catch (Exception error) { editorFailure = error; dialog.Close(); }
        }, DispatcherPriority.ApplicationIdle);
        ((MenuItem)window.FindName("DisplaySettingsMenuItem")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Assert.Null(editorFailure);
        Assert.Equal(24, Field<TestProfile>(window, "_profile").Display.FontSize);
        Assert.Equal(BmwSfrDisplayMetric.AtFrequency, Field<TestProfile>(window, "_profile").Display.Metric);
        Assert.False(Field<TestProfile>(window, "_profile").Display.ShowTargetCenter);
        Assert.True(Field<TestProfile>(window, "_profile").Display.ShowRoiDimensions);
        Assert.True(TextHeight() > height); // Glyph hinting changes ink bounds nonlinearly across font sizes.
        double enlargedHeight = TextHeight();
        draw.Zoombox.Zoom(0.5);
        Assert.Equal(enlargedHeight, TextHeight(), 4);
        Assert.Same(originalResult, Field<FrameAnalysis>(window, "_result"));
        Assert.True(((MenuItem)window.FindName("ExportMenuItem")).IsEnabled);
        Field<TestProfile>(window, "_profile").Display.FontSize = 12;
        Invoke(window, "RenderOverlays");
    }

    private static void VerifyInnerEdgeSelectionAndSfr(CameraTestWindow window)
    {
        var result = Field<FrameAnalysis>(window, "_result");
        var target = result.Targets.Last();
        var edge = target.Edges.First(e => e.Roi.Width > 0);
        object? hit = Invoke(window, "HitMeasurementEdge", new Point(edge.Roi.X + edge.Roi.Width / 2.0, edge.Roi.Y + edge.Roi.Height / 2.0));
        Assert.NotNull(hit);
        Assert.Null(Invoke(window, "HitMeasurementEdge", new Point(-1, -1)));
        Invoke(window, "SelectMeasurementEdge", target.Id, edge.Id);
        var selected = Assert.IsType<MetricRow>(((DataGrid)window.FindName("Metrics")).SelectedItem);
        Assert.Equal(target.Id, selected.Target);
        Assert.Equal(edge.Id.ToString(), selected.Edge);
        var color = Assert.IsType<ColorShiftRow>(((DataGrid)window.FindName("ColorMetrics")).SelectedItem);
        Assert.Equal(target.Id, color.Target);
        Assert.Equal(edge.Id.ToString(), color.Edge);
        Assert.Equal(target.Id, Assert.IsType<SearchRegion>(((ListBox)window.FindName("RegionList")).SelectedItem).Id);
        var menu = Assert.IsType<ContextMenu>(Invoke(window, "CreateEdgeMenu", target.Id, edge));
        var sfr = menu.Items.OfType<MenuItem>().Single(item => item.IsEnabled);
        Assert.Contains("SFR/MTF", sfr.Header.ToString());
        try
        {
            sfr.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            var child = Assert.Single(System.Windows.Application.Current.Windows.OfType<SfrSimplePlotWindow>());
            child.Left = child.Top = -20000;
            Assert.Equal(edge.Roi, Field<RoiRect>(child, "_roi"));
            Assert.Equal(edge.Roi.Width, ((Canvas)child.FindName("RoiCanvas")).Width);
            Assert.Same(result, Field<FrameAnalysis>(window, "_result"));
        }
        finally
        {
            foreach (var child in System.Windows.Application.Current.Windows.OfType<SfrSimplePlotWindow>().ToArray()) child.Close();
        }
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
