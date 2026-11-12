using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Views;
using ColorVision.Testing;
using ScottPlot.Plottables;
using ScottPlot.WPF;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class CameraViewLifecycleTests
{
    [Fact]
    public void FocusCurveRespectsManualCloseAndResetsWhenVideoEnds() => StaTest.Run(() =>
    {
        var chart = new CameraFocusChart();
        var panel = (Border)chart.FindName("ChartPanel");
        var show = (Button)chart.FindName("ShowChartButton");
        var hide = (Button)chart.FindName("HideChartButton");
        var readout = (TextBlock)chart.FindName("ChartReadout");
        var detail = (TextBlock)chart.FindName("ChartDetailReadout");
        var host = (ContentControl)chart.FindName("PlotHost");
        Assert.Null(host.Content);

        chart.BeginAutoFocus();
        Assert.Equal(Visibility.Visible, panel.Visibility);
        Assert.Equal(TextWrapping.Wrap, readout.TextWrapping);
        hide.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        chart.AddAutoFocusSample(200, 5);
        chart.AddAutoFocusSample(100, 3);
        chart.CompleteAutoFocus(new LocalAutoFocusResult(200, 5, 2, 300) { VidPosition = 42 });
        Assert.Equal(Visibility.Collapsed, panel.Visibility);
        Assert.Equal(Visibility.Visible, show.Visibility);

        show.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(Visibility.Visible, panel.Visibility);
        Assert.Contains("VID 42", readout.Text);
        chart.MaxWidth = 260;
        chart.Measure(new Size(260, 240));
        chart.Arrange(new Rect(0, 0, 260, 240));
        Assert.InRange(hide.TranslatePoint(new Point(hide.ActualWidth, 0), chart).X, 1, chart.ActualWidth);
        var plot = Assert.IsType<WpfPlot>(host.Content).Plot;
        var focus = Assert.Single(plot.PlottableList.OfType<Scatter>());
        Assert.Equal(new double[] { 100, 200 }, focus.Data.GetScatterPoints().Select(point => point.X));
        var best = Assert.Single(plot.PlottableList.OfType<Marker>());
        Assert.Equal(200, best.Location.X);

        chart.BeginVideo(calculateFocus: true);
        string initialMetrics = readout.Text;
        hide.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        chart.AddVideoSample(7);
        chart.UpdateVideoDisplayMetrics(3.5, 123);
        chart.UpdateVideoCrossGuideStatus("PASS · ΔX 1.00 · ΔY 2.00 · d 2.24 px\nRz +1.00° · Rx 0.00° · Ry 0.00°");
        show.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Empty(plot.PlottableList.OfType<Marker>());
        Assert.Equal(7, Assert.Single(Assert.Single(plot.PlottableList.OfType<Scatter>()).Data.GetScatterPoints()).Y);
        Assert.Equal(TextWrapping.NoWrap, readout.TextWrapping);
        string activeMetrics = readout.Text;
        Assert.Contains(3.5.ToString("F1", CultureInfo.CurrentCulture), activeMetrics);
        Assert.Contains(123.ToString("N0", CultureInfo.CurrentCulture), activeMetrics);
        Assert.Contains(7.ToString("G6", CultureInfo.CurrentCulture), activeMetrics);
        Assert.DoesNotContain('\n', activeMetrics);
        Assert.Equal(Visibility.Visible, detail.Visibility);
        Assert.Contains("PASS", detail.Text);
        Assert.Contains("Rz +1.00°", detail.Text);

        chart.ResetVideoSamples(calculateFocus: false);
        Assert.Equal(Visibility.Collapsed, host.Visibility);
        Assert.Contains(3.5.ToString("F1", CultureInfo.CurrentCulture), readout.Text);
        Assert.Contains(123.ToString("N0", CultureInfo.CurrentCulture), readout.Text);
        Assert.Contains("—", readout.Text);
        chart.UpdateVideoCrossGuideStatus(null);
        Assert.Equal(Visibility.Collapsed, detail.Visibility);
        chart.EndVideo();
        chart.AddVideoSample(9);
        Assert.Equal(Visibility.Collapsed, panel.Visibility);
        Assert.Equal(Visibility.Collapsed, show.Visibility);
        chart.BeginVideo(calculateFocus: true);
        Assert.Empty(plot.PlottableList);
        Assert.Equal(Visibility.Visible, host.Visibility);
        Assert.Equal(initialMetrics, readout.Text);
        chart.EndVideo();
    });

    [Fact]
    public void CompletedFocusCurveExportsWhileHiddenAndKeepsExistingFile() => StaTest.Run(() =>
    {
        var chart = new CameraFocusChart();
        var panel = (Border)chart.FindName("ChartPanel");
        var hide = (Button)chart.FindName("HideChartButton");
        var samples = new SortedDictionary<int, double> { [100] = 3, [200] = 5, [300] = 4 };
        var best = new LocalAutoFocusResult(200, 5, 3, 300) { VidPosition = 42 };
        chart.BeginAutoFocus("Variance");
        hide.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        foreach (var sample in samples) chart.AddAutoFocusSample(sample.Key, sample.Value);
        chart.CompleteAutoFocus(best);

        DirectoryInfo output = Directory.CreateTempSubdirectory("ColorVision-AutoFocus-");
        try
        {
            var time = new DateTime(2026, 11, 11, 16, 30, 15, 123);
            string path = CameraFocusCurve.SavePng(output.FullName, "VID100", "Variance", samples, best, time);
            string duplicate = CameraFocusCurve.SavePng(output.FullName, "VID100", "Variance", samples, best, time);
            string day = Path.Combine(output.FullName, "VID100", "2026-11-11");
            Assert.Equal(Path.Combine(day, "20261111_163015_123.png"), path);
            Assert.Equal(Path.Combine(day, "20261111_163015_123_2.png"), duplicate);
            Assert.Equal(2, Directory.GetFiles(day).Length);
            using var stream = File.OpenRead(path);
            var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            BitmapFrame frame = Assert.Single(decoder.Frames);
            Assert.Equal(1120, frame.PixelWidth);
            Assert.Equal(660, frame.PixelHeight);
            Assert.Equal(Visibility.Collapsed, panel.Visibility);
        }
        finally
        {
            output.Delete(recursive: true);
        }
    });

    [Fact]
    public void CurrentVidReturnsAfterVideoAndDoesNotDuplicateBestFocusVid() => StaTest.Run(() =>
    {
        var chart = new CameraFocusChart { MotorVid = 42 };
        var show = (Button)chart.FindName("ShowChartButton");
        var readout = (TextBlock)chart.FindName("ChartReadout");
        var detail = (TextBlock)chart.FindName("ChartDetailReadout");
        Assert.Contains("VID 42", readout.Text);

        chart.BeginAutoFocus();
        chart.CompleteAutoFocus(new LocalAutoFocusResult(200, 5, 2, 300) { VidPosition = 42 });
        Assert.Contains("VID 42", readout.Text);
        Assert.Equal(Visibility.Collapsed, detail.Visibility);
        chart.MotorVid = 43;
        Assert.Contains("VID 43", detail.Text);

        chart.BeginVideo(calculateFocus: false);
        Assert.Equal(Visibility.Collapsed, detail.Visibility);
        chart.MotorVid = 44;
        chart.EndVideo();
        Assert.Equal(Visibility.Visible, show.Visibility);
        show.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Contains("VID 44", readout.Text);
    });

    [Fact]
    public void DetachResultListViewClearsSharedDataAndUiHandlersIdempotently()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                ObservableCollection<string> items = ["first", "second"];
                ListView listView = new()
                {
                    ItemsSource = items,
                    ContextMenu = new ContextMenu(),
                };
                BindingOperations.SetBinding(listView, ListView.HeightProperty, new Binding { Source = 100d });
                listView.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy));

                int selectionNotifications = 0;
                SelectionChangedEventHandler selectionHandler = (_, _) => selectionNotifications++;
                KeyEventHandler previewKeyDownHandler = (_, _) => { };
                listView.SelectionChanged += selectionHandler;
                listView.PreviewKeyDown += previewKeyDownHandler;

                ViewCamera.DetachResultListView(listView, selectionHandler, previewKeyDownHandler);
                ViewCamera.DetachResultListView(listView, selectionHandler, previewKeyDownHandler);

                Assert.Null(listView.ItemsSource);
                selectionNotifications = 0;
                listView.ItemsSource = items;
                listView.SelectedIndex = 0;

                Assert.Equal(0, selectionNotifications);
                Assert.Null(BindingOperations.GetBindingExpression(listView, ListView.HeightProperty));
                Assert.Null(listView.ContextMenu);
                Assert.Empty(listView.CommandBindings);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        Assert.True(thread.TrySetApartmentState(ApartmentState.STA));
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "STA lifecycle test did not finish.");

        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
