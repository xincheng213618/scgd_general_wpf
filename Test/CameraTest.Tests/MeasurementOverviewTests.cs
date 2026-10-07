using CameraTest.Application;
using CameraTest.Models;
using ColorVision.Core;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR;
using ColorVision.UI.Tests;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace CameraTest.Tests;

public sealed class MeasurementOverviewTests
{
    private static string TestSettingsPath() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CameraTest-tests", Guid.NewGuid().ToString("N"), "settings.json");

    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void OverviewKeepsEdgesSeparateAndNeverTreatsMissingMeasurementsAsZero()
    {
        var analysis = Sample();
        var rows = analysis.Rows().ToArray();
        var overview = MeasurementOverview.Create(rows, 0, .25);
        Assert.Equal(8, overview.Length);
        Assert.All(overview, row => Assert.Equal(4, row.Rows.Count));
        Assert.Equal("左", overview.Single(r => r.Target == "Point_1" && r.Edge == "Left").EdgeLabel);
        var missing = rows[0] with { Mtf50 = null, Response = null };
        Assert.Equal("—", MeasurementOverview.Format(missing, 0, .25));
        Assert.Null(MeasurementOverview.Lowest([missing, missing with { Mtf50 = double.NaN }], 0, .25));
        Assert.Equal(rows[1], MeasurementOverview.Lowest([missing, rows[1]], 0, .25));
        Assert.Contains("%", MeasurementOverview.Format(rows[0], 2, .25));
        Assert.Equal(32, analysis.Rows().Count);
    }

    [Fact]
    public void ValidCurveWithoutCrossingIsDifferentFromARejectedChannel()
    {
        var channel = new SfrChannelAnalysis { Channel = "L", Valid = true, Frequencies = [0, .5], Mtf = [1, .7] };
        var row = new MetricRow("P", "Left", "Y (L)", "unknown_input_encoding", null, null, .85, new() { Channels = [channel] });
        var overview = Assert.Single(MeasurementOverview.Create([row], 0, .25));
        Assert.Equal("—", overview.Y);
        Assert.Contains("曲线有效", overview.YHint);
        Assert.Contains("50%", overview.YHint);
        Assert.DoesNotContain("未通过", overview.YHint);
        Assert.Contains("10%", MeasurementOverview.Describe(row, 1, .25));
        Assert.False(MeasurementOverview.Missing(row, 2, .25));

        var rejected = row with { Status = "edge_angle_out_of_range", Analysis = new() { Channels = [channel with { Valid = false, Reason = "edge_angle_out_of_range", Frequencies = [], Mtf = [] }] } };
        Assert.Equal("未通过", Assert.Single(MeasurementOverview.Create([rejected], 0, .25)).Y);
        // An unavailable fit must not be presented as a measured zero-degree angle.
        Assert.DoesNotContain("实测", MeasurementOverview.Diagnostic(rejected));
        Assert.Contains("1°–15°", MeasurementOverview.Diagnostic(rejected));
    }

    [Fact]
    public void MissingChannelNavigationShowsMeasuredReasonsAndRespectsFiltersAndNewFrames()
    {
        WpfTestHost.Invoke(() =>
        {
            var window = CreateWindow();
            try
            {
                var sample = Sample();
                var result = sample with
                {
                    Options = new() { MaximumFitRms = .2 },
                    Targets = sample.Targets.Take(1).Select(target => target with
                    {
                        Edges = target.Edges.Select(edge => edge with
                        {
                            Analysis = edge.Analysis! with
                            {
                                Channels = edge.Analysis!.Channels.Select(channel =>
                                    (edge.Id == BmwEdgeId.Left && channel.Channel == "B") || (edge.Id == BmwEdgeId.Top && channel.Channel == "R")
                                    ? channel with { Valid = false, Reason = edge.Id == BmwEdgeId.Left ? "edge_angle_out_of_range" : "edge_fit_residual_too_large",
                                        FitAvailable = true, AngleDegrees = .4, FitRms = .6, Mtf50 = null, Mtf10 = null, Frequencies = [], Mtf = [], EdgePositions = [], Esf = [], LsfPositions = [], Lsf = [] }
                                    : channel with { Warnings = ["unknown_input_encoding"] }).ToArray()
                            }
                        }).ToArray()
                    }).ToArray()
                };
                Publish(window, result);
                var metrics = (DataGrid)window.FindName("Metrics");
                var button = (Button)window.FindName("MissingResultsButton");
                var hint = (TextBlock)window.FindName("ResultHint");
                var overview = (DataGrid)window.FindName("Overview");
                Assert.Equal("查看缺值 (2)", button.Content);
                Assert.Contains("2 项缺值", hint.Text); // Encoding warning must not hide missing channels.
                Assert.Equal("未通过", overview.Items.OfType<MeasurementOverview>().Single(row => row.Edge == "Left").B);
                Assert.Contains("实测倾角", overview.Items.OfType<MeasurementOverview>().Single(row => row.Edge == "Left").BHint);
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal("B", Assert.IsType<MetricRow>(metrics.SelectedItem).Channel);
                Assert.Contains("左边 · B", hint.Text);
                Assert.Contains($"{.4:F2}°", hint.Text);
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal("R", Assert.IsType<MetricRow>(metrics.SelectedItem).Channel);
                Assert.Contains("上边 · R", hint.Text);
                Assert.Contains($"{.6:F3} px", hint.Text);
                Assert.Contains($"上限 {.2:G} px", hint.Text);
                if (Environment.GetEnvironmentVariable("CAMERATEST_DIAGNOSTIC_CAPTURE") is { Length: > 0 } capture)
                {
                    var content = (FrameworkElement)window.Content;
                    content.Measure(new Size(1100, 700));
                    content.Arrange(new Rect(0, 0, 1100, 700));
                    content.UpdateLayout();
                    var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(1100, 700, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    bitmap.Render(content);
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    using var stream = System.IO.File.Create(capture);
                    encoder.Save(stream);
                }
                Publish(window, result with { FrameId = Guid.NewGuid() });
                Assert.Equal("R", Assert.IsType<MetricRow>(metrics.SelectedItem).Channel);
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal("B", Assert.IsType<MetricRow>(metrics.SelectedItem).Channel);
                SetChannels(window, "Y");
                Assert.Equal(Visibility.Collapsed, button.Visibility);
                Assert.DoesNotContain("缺值", hint.Text);
                Assert.Equal(4, metrics.Items.Count);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void DisplayFrequencyQueriesItsOwnChannelWithoutChangingRecordedMetrics()
    {
        var result = Sample();
        var original = result.Rows().ToArray();
        var green = original.First(row => row.Channel == "G");
        Assert.Equal(.36, MeasurementOverview.Value(green, 2, .25)!.Value, 8);
        Assert.Equal(.58, MeasurementOverview.Value(green, 2, .175)!.Value, 8);
        Assert.Equal(.05, MeasurementOverview.Value(green, 3, .175)!.Value, 8);
        var missing = green with { Channel = "Missing" };
        Assert.Null(MeasurementOverview.Value(missing, 2, .25));
        Assert.Equal(original, result.Rows());
        Assert.Equal(.25, result.Frequency);
    }

    [Fact]
    public void DirectionNavigationAndDisplaySettingsShareTheSameMetricAndFrequency()
    {
        WpfTestHost.Invoke(() =>
        {
            var window = CreateWindow();
            try
            {
                var result = Sample();
                Publish(window, result);
                var profile = (TestProfile)typeof(CameraTestWindow).GetField("_profile", Private)!.GetValue(window)!;
                var metrics = (DataGrid)window.FindName("Metrics");
                var metric = (ComboBox)window.FindName("OverviewMetric");
                metrics.SelectedItem = metrics.Items.OfType<MetricRow>().Single(row => row.Target == "Point_1" && row.Edge == "Left" && row.Channel == "G");
                ((Button)window.FindName("TopEdgeButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal("Top", Assert.IsType<MetricRow>(metrics.SelectedItem).Edge);
                Assert.Equal("G", Assert.IsType<MetricRow>(metrics.SelectedItem).Channel);
                Assert.Equal(((TextBlock)window.FindName("SelectedMetricValue")).Text, ((TextBlock)window.FindName("TopEdgeValue")).Text);

                metric.SelectedIndex = 1;
                Assert.Equal(BmwSfrDisplayMetric.Mtf10, profile.Display.Metric);
                Assert.Contains("MTF10", ((TextBlock)window.FindName("SelectedMetricCaption")).Text);
                Assert.Equal(.4.ToString("F4"), ((TextBlock)window.FindName("TopEdgeValue")).Text);
                profile.Display.Metric = BmwSfrDisplayMetric.AtFrequency;
                profile.Display.Frequency = .175;
                Invoke(window, "SynchronizeDisplayMetric");
                Assert.Equal(2, metric.SelectedIndex);
                Assert.Contains("0.175", ((TextBlock)window.FindName("SelectedMetricCaption")).Text);
                Assert.Equal(.58.ToString("P1"), ((TextBlock)window.FindName("TopEdgeValue")).Text);
                Assert.Equal(.58.ToString("P1"), ((TextBlock)window.FindName("SelectedMetricValue")).Text);
                metric.SelectedIndex = 3;
                Assert.Equal(BmwSfrDisplayMetric.AtNyquist, profile.Display.Metric);
                Assert.Equal(.05.ToString("P1"), ((TextBlock)window.FindName("TopEdgeValue")).Text);
                Assert.Same(result, typeof(CameraTestWindow).GetField("_result", Private)!.GetValue(window));
                Assert.Equal(.25, profile.Analysis.TargetFrequency);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void OverviewAndDetailSelectionStayLinkedAcrossFiltersMetricsAndNewFrames()
    {
        WpfTestHost.Invoke(() =>
        {
            var window = CreateWindow();
            try
            {
                var detail = (DataGrid)window.FindName("Metrics");
                var overview = (DataGrid)window.FindName("Overview");
                var filter = (ComboBox)window.FindName("TargetFilter");
                Invoke(window, "ShowFrame", new TestFrame(new StandaloneCameraFrame(new byte[400 * 300], 400, 300, 8, 1, 400, DateTimeOffset.Now), "selection test", FrameSourceKind.Capture));
                var draw = ((ImageView)window.FindName("ImageView")).EditorContext.DrawEditorContext;
                draw.DrawCanvas.AddVisualCommand(new DVRectangleText(new() { Text = "Point_1", Rect = new(5, 5, 80, 80) }));
                draw.DrawCanvas.AddVisualCommand(new DVRectangleText(new() { Text = "Point_2", Rect = new(150, 5, 80, 80) }));
                ((ListBox)window.FindName("RegionList")).SelectedIndex = 0;
                Publish(window, Sample());
                Assert.Equal(8, overview.Items.Count);
                Assert.Equal(Visibility.Collapsed, detail.Visibility);
                var edge = overview.Items.OfType<MeasurementOverview>().Single(r => r.Target == "Point_2" && r.Edge == "Right");
                overview.SelectedItem = edge;
                Assert.Equal("Right", Assert.IsType<MetricRow>(detail.SelectedItem).Edge);
                Invoke(window, "SelectOverviewRow", edge, "G");
                Assert.Equal("G", Assert.IsType<MetricRow>(detail.SelectedItem).Channel);
                Assert.Contains("Point_2", ((TextBlock)window.FindName("CurveSelectionText")).Text);

                SetChannels(window, "Y");
                Assert.Equal(8, detail.Items.Count);
                Assert.All(detail.Items.OfType<MetricRow>(), row => Assert.Equal("Y (L)", row.Channel));
                Assert.Equal(Visibility.Collapsed, ((DataGridColumn)window.FindName("OverviewG")).Visibility);
                Assert.Equal("Right", Assert.IsType<MetricRow>(detail.SelectedItem).Edge);
                filter.SelectedItem = "Point_2";
                Assert.Equal(4, overview.Items.Count);
                Invoke(window, "Refresh");
                Assert.Equal("Point_2", filter.SelectedItem);
                ((ComboBox)window.FindName("OverviewMetric")).SelectedIndex = 2;
                Assert.Contains("MTF@0.25", ((TextBlock)window.FindName("SelectedMetricCaption")).Text);
                Assert.Contains("%", ((TextBlock)window.FindName("SelectedMetricValue")).Text);
                Publish(window, Sample() with { FrameId = Guid.NewGuid() });
                Assert.Equal(4, overview.Items.Count);
                Assert.Equal("Right", Assert.IsType<MetricRow>(detail.SelectedItem).Edge);

                // Image selection deliberately escapes a different target filter.
                Invoke(window, "SelectMeasurementEdge", "Point_1", BmwEdgeId.Top);
                Assert.Equal("全部点位", filter.SelectedItem);
                Assert.Equal("Point_1", Assert.IsType<MeasurementOverview>(overview.SelectedItem).Target);
                SetChannels(window);
                Assert.Empty(overview.Items);
                Assert.Null(detail.SelectedItem);
                Assert.False(((Button)window.FindName("LowestButton")).IsEnabled);
                Assert.Contains("至少一个通道", ((TextBlock)window.FindName("ResultHint")).Text);

                SetChannels(window, "Y");
                Publish(window, Sample() with { Targets = [new("Failed", default, false, "target_not_found", default, 0, 0, [new(BmwEdgeId.Top, default, false, "target_not_found", null)])] });
                Assert.Single(overview.Items);
                Assert.Equal("—", Assert.IsType<MeasurementOverview>(overview.SelectedItem).Y);
                Assert.Equal(Visibility.Visible, ((Border)window.FindName("EmptyPlot")).Visibility);
                Assert.Contains("完整靶标", ((TextBlock)window.FindName("ResultHint")).Text);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task ExpandingAFailedRegionClampsToTheImageAndCanBeUndone()
    {
        CameraTestWindow? window = null;
        Task? retry = null;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                window = CreateWindow();
                var frame = new TestFrame(new StandaloneCameraFrame(new byte[400 * 300], 400, 300, 8, 1, 400, DateTimeOffset.Now), "retry test", FrameSourceKind.Capture);
                Invoke(window, "ShowFrame", frame);
                var draw = ((ImageView)window.FindName("ImageView")).EditorContext.DrawEditorContext;
                draw.DrawCanvas.AddVisualCommand(new DVRectangleText(new() { Text = "Failed", Rect = new(5, 5, 80, 80) }));
                Publish(window, Sample() with { FrameId = frame.Id, Targets = [new("Failed", new(5, 5, 80, 80), false, "target_not_found", default, 0, 0, [new(BmwEdgeId.Left, default, false, "target_not_found", null)])] });
                Assert.True(((Button)window.FindName("ExpandRegionButton")).IsEnabled);
                retry = (Task)Invoke(window, "ExpandSelectedRegionAsync")!;
            });
            await retry!;
            WpfTestHost.Invoke(() =>
            {
                var draw = ((ImageView)window!.FindName("ImageView")).EditorContext.DrawEditorContext;
                var rect = Assert.IsAssignableFrom<ISelectVisual>(draw.DrawingVisualLists.Single());
                Assert.Equal(new Rect(0, 0, 105, 105), rect.GetRect());
                draw.DrawCanvas.Undo();
                Assert.Equal(new Rect(5, 5, 80, 80), rect.GetRect());
                Assert.Empty(((DataGrid)window.FindName("Overview")).Items);
                draw.DrawCanvas.Redo();
                Assert.Equal(new Rect(0, 0, 105, 105), rect.GetRect());
            });
        }
        finally { if (window != null) WpfTestHost.Invoke(window.Close); }
    }

    private static CameraTestWindow CreateWindow()
    {
        System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml", UriKind.Relative) });
        return new CameraTestWindow(TestSettingsPath());
    }

    private static object? Invoke(CameraTestWindow window, string name, params object?[] args) => typeof(CameraTestWindow).GetMethod(name, Private)!.Invoke(window, args);
    private static void Publish(CameraTestWindow window, FrameAnalysis analysis)
    {
        var selected = ((DataGrid)window.FindName("Metrics")).SelectedItem;
        typeof(CameraTestWindow).GetField("_result", Private)!.SetValue(window, analysis);
        Invoke(window, "RefreshMetricRows", selected);
    }

    private static void SetChannels(CameraTestWindow window, params string[] names)
    {
        foreach (string channel in new[] { "Y", "R", "G", "B" }) ((CheckBox)window.FindName("Show" + channel)).IsChecked = names.Contains(channel);
        ((CheckBox)window.FindName("ShowY")).RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
    }

    private static FrameAnalysis Sample() => new(Guid.NewGuid(), "test", DateTimeOffset.Now, 400, 300, 8, 3, .25, new(), 1,
        Enumerable.Range(1, 2).Select(target => new BmwTargetAnalysis($"Point_{target}", default, true, "", default, 0, 0,
            Enum.GetValues<BmwEdgeId>().Select(edge => new BmwEdgeAnalysis(edge, default, true, "", new SfrAnalysisResult
            {
                Channels = new[] { "L", "R", "G", "B" }.Select((channel, i) => new SfrChannelAnalysis
                {
                    Channel = channel, Valid = true, Frequencies = [0, .1, .25, .5], Mtf = [1, .8, .4 - i * .02, .05],
                    Mtf50 = .2 + i * .01, Mtf10 = .4, EdgePositions = [-1, 0, 1], Esf = [0, .5, 1], LsfPositions = [-1, 0, 1], Lsf = [0, 1, 0]
                }).ToArray()
            })).ToArray())).ToArray());
}
