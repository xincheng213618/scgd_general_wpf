using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Controls;
using ColorVision.Engine;
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
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class CameraViewLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FocusStopUsesTheStartButtonAndLocksOtherCameraControls(bool fineFocus) => WpfTestHost.Invoke(() =>
    {
        var resources = Application.Current.Resources;
        bool addConverter = !resources.Contains("bool2VisibilityConverter");
        bool addStyle = !resources.Contains("ComboBox.Small");
        if (addConverter) resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
        if (addStyle) resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        try
        {
            var capture = new CameraCaptureControl();
            Button start = fineFocus ? capture.FineFocusButton : capture.AutoFocusButton;
            Button other = fineFocus ? capture.AutoFocusButton : capture.FineFocusButton;
            object original = start.Content;

            capture.SetFocusOperationState(operating: true, busy: true, start, canStop: true, stopping: false);
            Assert.Equal(EngineLocalization.Get("CameraPanel_StopAutoFocus"), start.Content);
            Assert.True(start.IsEnabled);
            Assert.False(other.IsEnabled);
            Assert.False(capture.MoveButton.IsEnabled);
            Assert.False(capture.TakePhotoButton.IsEnabled);
            Assert.False(capture.ComboxAutoFocus.IsEnabled);

            capture.SetFocusOperationState(operating: true, busy: true, start, canStop: false, stopping: true);
            Assert.Equal(EngineLocalization.Get("CameraPanel_AutoFocusStopping"), start.Content);
            Assert.False(start.IsEnabled);
            capture.ResetFocusButton(start);
            capture.SetFocusOperationState(operating: false, busy: false, null, canStop: false, stopping: false);
            Assert.Equal(original, start.Content);
            Assert.True(start.IsEnabled);
            Assert.True(other.IsEnabled);
        }
        finally
        {
            if (addConverter) resources.Remove("bool2VisibilityConverter");
            if (addStyle) resources.Remove("ComboBox.Small");
        }
    });

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
    public void CompletedFocusResultExportsAllFilesWhileHiddenAndKeepsExistingResults() => StaTest.Run(() =>
    {
        var chart = new CameraFocusChart();
        var panel = (Border)chart.FindName("ChartPanel");
        var hide = (Button)chart.FindName("HideChartButton");
        var samples = new OrderedDictionary<int, double> { [300] = 4.25, [200] = 5.12345678912345, [100] = 3.5 };
        var best = new LocalAutoFocusResult(200, samples[200], 3, 300) { VidPosition = 42 };
        chart.BeginAutoFocus("Variance");
        hide.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        foreach (var sample in samples) chart.AddAutoFocusSample(sample.Key, sample.Value);
        chart.CompleteAutoFocus(best);
        var bitmap = BitmapSource.Create(360, 240, 96, 96, PixelFormats.Gray8, null,
            Enumerable.Repeat((byte)180, 360 * 240).ToArray(), 360);
        bitmap.Freeze();
        var evidence = new CameraAutoFocusEvidence(bitmap, best, samples, "VID100", "Variance");

        DirectoryInfo output = Directory.CreateTempSubdirectory("ColorVision-AutoFocus-");
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var time = new DateTime(2026, 11, 11, 16, 30, 15, 123);
            string day = Path.Combine(output.FullName, "VID100", "2026-11-11");
            Directory.CreateDirectory(day);
            string legacy = Path.Combine(day, "20261111_163015_123.png");
            File.WriteAllText(legacy, "previous release");
            string path = Task.Run(() => evidence.SaveLocalResult(output.FullName, time, default)).GetAwaiter().GetResult();
            string duplicate = evidence.SaveLocalResult(output.FullName, time, default);
            Assert.Equal(Path.Combine(day, "20261111_163015_123"), path);
            Assert.Equal(Path.Combine(day, "20261111_163015_123_2"), duplicate);
            Assert.Equal("previous release", File.ReadAllText(legacy));
            Assert.Equal(2, Directory.GetDirectories(day).Length);
            Assert.Equal(3, Directory.GetFiles(path).Length);
            Assert.Equal(new[] { "MotorPosition,Sharpness", "300,4.25", "200,5.12345678912345", "100,3.5" },
                File.ReadAllLines(Path.Combine(path, "FocusSamples.csv")));

            BitmapFrame curve = LoadPng("FocusCurve.png");
            Assert.Equal((1120, 660), (curve.PixelWidth, curve.PixelHeight));
            BitmapFrame image = LoadPng("FocusImage.png");
            Assert.Equal((360, 240), (image.PixelWidth, image.PixelHeight));
            var composed = new FormatConvertedBitmap(image, PixelFormats.Gray8, null, 0);
            var pixel = new byte[1];
            composed.CopyPixels(new Int32Rect(8, 8, 1, 1), pixel, 1, 0);
            Assert.Equal(0, pixel[0]);
            composed.CopyPixels(new Int32Rect(350, 230, 1, 1), pixel, 1, 0);
            Assert.Equal(180, pixel[0]);
            Assert.Equal(Visibility.Collapsed, panel.Visibility);

            BitmapFrame LoadPng(string name)
            {
                using var stream = File.OpenRead(Path.Combine(path, name));
                return Assert.Single(new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            output.Delete(recursive: true);
        }
    });

    [Fact]
    public void CurrentVidReturnsAfterVideoAndDoesNotDuplicateBestFocusVid() => StaTest.Run(() =>
    {
        var chart = new CameraFocusChart { MotorVid = 42, MotorPosition = 200 };
        var show = (Button)chart.FindName("ShowChartButton");
        var readout = (TextBlock)chart.FindName("ChartReadout");
        var detail = (TextBlock)chart.FindName("ChartDetailReadout");
        Assert.Contains("VID 42", readout.Text);
        Assert.Contains("200", readout.Text);

        chart.BeginAutoFocus();
        chart.CompleteAutoFocus(new LocalAutoFocusResult(200, 5, 2, 300) { VidPosition = 42 });
        Assert.Contains("VID 42", readout.Text);
        Assert.Equal(Visibility.Collapsed, detail.Visibility);
        chart.MotorPosition = 201;
        Assert.Contains("201", detail.Text);
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
