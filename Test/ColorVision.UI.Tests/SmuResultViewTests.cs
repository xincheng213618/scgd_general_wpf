using ColorVision.Core;
using ColorVision.Engine;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.SMU.Local;
using ColorVision.Engine.Services.Devices.SMU.Views;
using ColorVision.Themes;
using Newtonsoft.Json;
using ScottPlot.Plottables;
using ScottPlot.WPF;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class SmuResultViewTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResultSelectionSwitchesPointAndScanWithoutChangingUnitsOrJoiningPointHistory(bool insertAtBeginning)
    {
        WpfTestHost.Invoke(() =>
        {
            IConfigService previous = ConfigService.Instance;
            List<ResourceDictionary> dictionaries = [];
            Window? window = null;
            try
            {
                ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
                ViewSMUConfig.Instance.InsertAtBeginning = insertAtBeginning;
                foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                {
                    ResourceDictionary dictionary = new() { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                    dictionaries.Add(dictionary);
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                }
                ViewSMU view = new();
                window = new Window { Content = view, Width = 1440, Height = 900, Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false };
                window.Show();
                ListView history = (ListView)view.FindName("listView1");
                ListView points = (ListView)view.FindName("listView2");
                WpfPlot plot = (WpfPlot)view.FindName("wpfplot1");
                Grid metrics = (Grid)view.FindName("MetricBand");
                Grid scanPanel = (Grid)view.FindName("ScanPanel");
                CheckBox follow = (CheckBox)view.FindName("FollowLatest");
                DateTime time = new(2026, 10, 20, 3, 50, 43);
                for (int index = 0; index < 9; index++)
                    view.AddViewResultSMU(new(new SMUResultModel
                    {
                        Id = 1800 + index, CreateDate = time.AddSeconds(index * 2), TotalTime = 31 + index % 3,
                        IsSourceV = true, SrcValue = -1.3f, LimitValue = 100, VResult = -1.30032f, IResult = -0.00003834f * (1 + index * 0.01f)
                    }));
                var point = Assert.IsType<ViewResultSMU>(history.SelectedItem);
                Assert.Equal(1808, point.Id);
                Assert.Same(point, insertAtBeginning ? view.ViewResults[0] : view.ViewResults[^1]);
                Assert.True(follow.IsChecked);
                Pump(window);
                Assert.Equal(Visibility.Collapsed, scanPanel.Visibility);
                Assert.Same(point.SMUDatas[0], metrics.DataContext);
                Assert.Empty(plot.Plot.GetPlottables());
                Assert.Contains("nA", ((TextBlock)view.FindName("CurrentReading")).Text);
                Capture(view, "single-point.png");

                double[] voltages = Enumerable.Range(0, 21).Select(index => 0.1 * index).ToArray();
                double[] amps = voltages.Select(voltage => 0.022 * Math.Pow(voltage / 2, 5)).ToArray();
                ViewResultSMU scan = new(new SmuScanModel
                {
                    Id = 1900, CreateDate = time.AddMinutes(1), TotalTime = 862, IsSourceV = false,
                    ChannelType = SMUChannelType.B, SrcBegin = 0, SrcEnd = 22, LimitValue = 5,
                    VResult = JsonConvert.SerializeObject(voltages), IResult = JsonConvert.SerializeObject(amps)
                });
                view.AddViewResultSMU(scan);
                Pump(window);
                Assert.Equal(Visibility.Visible, scanPanel.Visibility);
                Assert.Same(scan.SMUDatas[^1], metrics.DataContext);
                Scatter curve = Assert.Single(plot.Plot.GetPlottables().OfType<Scatter>());
                Assert.Equal(voltages, curve.Data.GetScatterPoints().Select(p => p.X));
                Assert.Equal(amps.Select(current => current * 1000), curve.Data.GetScatterPoints().Select(p => p.Y));
                Assert.Equal("电压 (V)", plot.Plot.Axes.Bottom.Label.Text);
                Assert.Equal("电流 (mA)", plot.Plot.Axes.Left.Label.Text);
                points.SelectedIndex = 15;
                Pump(window);
                Assert.Same(scan.SMUDatas[15], metrics.DataContext);
                Assert.Equal(scan.SMUDatas[15].Voltage, Assert.Single(plot.Plot.GetPlottables().OfType<Marker>()).X);
                Capture(view, "scan.png");

                CheckBox logYAxis = (CheckBox)view.FindName("LogYAxis");
                Assert.False(logYAxis.IsChecked == true);
                logYAxis.IsChecked = true;
                logYAxis.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Pump(window);
                Assert.True(Assert.IsType<ScottPlot.TickGenerators.NumericAutomatic>(plot.Plot.Axes.Left.TickGenerator).IntegerTicksOnly);
                curve = Assert.Single(plot.Plot.GetPlottables().OfType<Scatter>());
                Assert.True(double.IsNaN(curve.Data.GetScatterPoints()[0].Y));
                Assert.Equal(amps.Skip(1).Select(current => Math.Log10(current * 1000)), curve.Data.GetScatterPoints().Skip(1).Select(p => p.Y));
                Assert.Equal(Math.Log10(scan.SMUDatas[15].Current), Assert.Single(plot.Plot.GetPlottables().OfType<Marker>()).Y);
                Assert.Equal(amps, scan.IList);
                points.SelectedIndex = 0;
                Assert.Empty(plot.Plot.GetPlottables().OfType<Marker>());
                Assert.Same(scan.SMUDatas[0], metrics.DataContext);
                Capture(view, "scan-log.png");

                ((ComboBox)view.FindName("AxisChoice")).SelectedIndex = 1;
                curve = Assert.Single(plot.Plot.GetPlottables().OfType<Scatter>());
                Assert.Equal(voltages.Skip(1).Select(Math.Log10), curve.Data.GetScatterPoints().Skip(1).Select(p => p.Y));
                logYAxis.IsChecked = false;
                logYAxis.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                curve = Assert.Single(plot.Plot.GetPlottables().OfType<Scatter>());
                Assert.Equal(voltages, curve.Data.GetScatterPoints().Select(p => p.Y));
                Assert.False(Assert.IsType<ScottPlot.TickGenerators.NumericAutomatic>(plot.Plot.Axes.Left.TickGenerator).IntegerTicksOnly);
                Assert.Equal(0, Assert.Single(plot.Plot.GetPlottables().OfType<Marker>()).Y);
                Assert.Equal(amps, scan.IList);
                view.AddViewResultSMU(new(MeasurementType.Voltage, 1, [-1, 0], [-0.001, 0]));
                logYAxis.IsChecked = true;
                logYAxis.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Pump(window);
                Assert.Empty(plot.Plot.GetPlottables().OfType<Marker>());
                Assert.Contains("没有可显示的正值", ((TextBlock)view.FindName("PlotNote")).Text);
                Capture(view, "scan-log-empty.png");
                view.AddViewResultSMU(new(MeasurementType.Voltage, 32, [1, 2, 0, 4, 8, -1, 16, 32],
                    [0.001, 0.002, 0.003, 0.004, 0.005, 0.006, 0.007, 0.008]));
                Pump(window);
                curve = Assert.Single(plot.Plot.GetPlottables().OfType<Scatter>());
                Assert.True(double.IsNaN(curve.Data.GetScatterPoints()[2].Y));
                Assert.True(double.IsNaN(curve.Data.GetScatterPoints()[5].Y));
                Capture(view, "scan-log-gaps.png");
                logYAxis.IsChecked = false;
                logYAxis.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                history.SelectedItem = point;
                Assert.False(follow.IsChecked);
                ViewResultSMU next = new(new SMUResultModel { IsSourceV = true, VResult = 2, IResult = 12 });
                view.AddViewResultSMU(next);
                Assert.Same(point, history.SelectedItem);
                Assert.Empty(plot.Plot.GetPlottables());
                // Readings must not jump while reviewing an older record.
                Assert.Same(point.SMUDatas[0], metrics.DataContext);
                // Changing insertion direction does not change which capture is the latest.
                ViewSMUConfig.Instance.InsertAtBeginning = !insertAtBeginning;
                follow.IsChecked = true;
                follow.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert.Same(next, history.SelectedItem);
                Assert.True(follow.IsChecked);
                ViewResultSMU otherScan = new(MeasurementType.Voltage, 5, [1, 2], [0.001, 0.002]);
                view.AddViewResultSMU(otherScan);
                Assert.Same(otherScan, history.SelectedItem);
                Assert.Same(otherScan, insertAtBeginning ? view.ViewResults[^1] : view.ViewResults[0]);
                Assert.True(follow.IsChecked);
                history.SelectedItems.Add(scan);
                CheckBox compare = (CheckBox)view.FindName("CompareScans");
                compare.IsChecked = true;
                compare.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert.Equal(2, plot.Plot.GetPlottables().OfType<Scatter>().Count());
                System.Windows.Input.ApplicationCommands.Delete.Execute(null, history);
                Assert.DoesNotContain(scan, view.ViewResults);
                Assert.DoesNotContain(otherScan, view.ViewResults);
                Assert.Contains(next, view.ViewResults);
                view.ClearResults();
                Pump(window);
                Assert.Null(metrics.DataContext);
                Assert.Null(points.ItemsSource);
                Assert.Empty(plot.Plot.GetPlottables());
                Assert.Equal(Visibility.Visible, ((StackPanel)view.FindName("EmptyState")).Visibility);
                Capture(view, "empty.png");
            }
            finally
            {
                if (window != null) { window.Content = null; window.Close(); }
                foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
                ConfigService.SetInstance(previous);
            }
        });
    }

    [Fact]
    public void CsvKeepsMilliampsAndNativeResistanceAcrossPointAndScanResults()
    {
        ViewResultSMU scan = new(MeasurementType.Voltage, 5, [1.9, 2], [0.012, 0.02]);
        ViewResultSMU point = new(new SMUResultModel
        {
            IsSourceV = true, VResult = 1.9f, IResult = 12,
            LocalDetails = JsonConvert.SerializeObject(new LocalSmuDetails(new LocalSmuSettings { Measurement = SmuMeasurement.Resistance }, [1.9], [123.456], [0], "test"))
        });
        string path = Path.GetTempFileName();
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            ViewSMU.ExportToCsv([point, scan], path);
            string[] lines = File.ReadAllLines(path);
            Assert.Contains("电流(mA)", lines[0]);
            Assert.Equal(4, lines.Length);
            Assert.Equal("12", lines[1].Split(',')[12]);
            Assert.Equal("12", lines[2].Split(',')[12]);
            Assert.Equal("123.456", lines[1].Split(',')[13]);
            Assert.All(lines, line => Assert.Equal(15, line.Split(',').Length));
        }
        finally { CultureInfo.CurrentCulture = previous; File.Delete(path); }
    }

    private static void Pump(Window window)
    {
        window.UpdateLayout();
        DispatcherFrame frame = new();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static void Capture(FrameworkElement view, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("CV_SMU_VIEW_PREVIEW_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        RenderTargetBitmap bitmap = new((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.Combine(directory, name));
        encoder.Save(output);
    }
}
