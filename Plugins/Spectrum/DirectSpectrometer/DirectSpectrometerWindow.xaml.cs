using ColorVision.UI.Menus;
using Spectrum.Menus;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Spectrum.DirectSpectrometer;

public class MenuDirectSpectrometerWindow : SpectrumMenuIBase
{
    public override string OwnerGuid => MenuItemConstants.Help;
    public override int Order => 10005;
    public override string Header => "光谱仪直连测试";

    public override void Execute()
    {
        new DirectSpectrometerWindow
        {
            Owner = Application.Current.GetActiveWindow(),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        }.Show();
    }
}

public partial class DirectSpectrometerWindow : Window, IDisposable
{
    private const int BufferSize = 8192;
    private readonly double[] _wavelengthBuffer = new double[BufferSize];
    private readonly double[] _spectrumBuffer = new double[BufferSize];
    private readonly List<string> _uiLogs = new();
    private int _spectrometerCount = -1;
    private int _selectedIndex;
    private bool _isOpen;
    private bool _isContinuousRunning;
    private CancellationTokenSource? _continuousCts;

    public DirectSpectrometerWindow()
    {
        InitializeComponent();
        var logPath = Path.Combine(AppContext.BaseDirectory, "logs", $"spectrometer_direct_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        DirectSpectrometerLogger.Initialize(logPath);
        AppendLog($"日志文件: {logPath}");
        ConfigurePlot();
    }

    private void ConfigurePlot()
    {
        SpectrumPlot.Plot.Title("Spectrum");
        SpectrumPlot.Plot.XLabel("Wavelength (nm)");
        SpectrumPlot.Plot.YLabel("Intensity");
        SpectrumPlot.Refresh();
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var version = DirectSpectrometerLogger.Measure("SA_GetAPIVersion", SpectrometerApi.GetApiVersion);
            AppendLog($"API Version: {version}");
            var openResult = DirectSpectrometerLogger.Measure("SA_OpenSpectrometers", () => SpectrometerApi.SA_OpenSpectrometers().ToString());
            _spectrometerCount = int.Parse(openResult);
            if (_spectrometerCount < 0)
            {
                AppendLog($"打开设备失败，返回值: {_spectrometerCount}");
                StatusTextBlock.Text = "打开设备失败";
                return;
            }

            _selectedIndex = 0;
            _isOpen = true;
            var serial = DirectSpectrometerLogger.Measure($"SA_GetSerialNumber({_selectedIndex})", () => SpectrometerApi.GetSerialNumber(_selectedIndex));
            AppendLog($"打开成功，设备数量返回值: {_spectrometerCount}，当前使用索引: {_selectedIndex}，序列号: {serial}");
            StatusTextBlock.Text = $"已连接 | Index={_selectedIndex} | SN={serial}";
            LoadWavelength();
        }
        catch (Exception ex)
        {
            AppendLog($"打开设备异常: {ex.Message}");
            DirectSpectrometerLogger.Error("OpenButton_Click failed", ex);
            StatusTextBlock.Text = "打开设备异常";
        }
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureOpen())
        {
            return;
        }

        try
        {
            var integrationTimeMs = ParsePositiveDouble(IntegrationTimeTextBox.Text, "积分时间");
            var integrationTimeUs = ConvertMsToUs(integrationTimeMs);
            var averageTimes = ParsePositiveInt(AverageTimesTextBox.Text, "平均次数");

            var setIntResult = DirectSpectrometerLogger.Measure($"SA_SetIntegrationTime({_selectedIndex}, {integrationTimeUs})", () => SpectrometerApi.SA_SetIntegrationTime(_selectedIndex, integrationTimeUs).ToString());
            var setAvgResult = DirectSpectrometerLogger.Measure($"SA_SetAverageTimes({_selectedIndex}, {averageTimes})", () => SpectrometerApi.SA_SetAverageTimes(_selectedIndex, averageTimes).ToString());

            AppendLog($"设置积分时间返回: {setIntResult}，积分时间: {integrationTimeMs:F3} ms ({integrationTimeUs} us)");
            AppendLog($"设置平均次数返回: {setAvgResult}，平均次数: {averageTimes}");
            StatusTextBlock.Text = $"参数已设置 | Int={integrationTimeMs:F3}ms | Avg={averageTimes}";
        }
        catch (Exception ex)
        {
            AppendLog($"设置参数失败: {ex.Message}");
            DirectSpectrometerLogger.Error("ApplyButton_Click failed", ex);
        }
    }

    private void GetSpectrumButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureOpen())
        {
            return;
        }

        try
        {
            ApplyCurrentSettings();
            AcquireAndPlotSpectrum();
        }
        catch (Exception ex)
        {
            AppendLog($"获取光谱失败: {ex.Message}");
            DirectSpectrometerLogger.Error("GetSpectrumButton_Click failed", ex);
        }
    }

    private void RunTimingButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureOpen())
        {
            return;
        }

        try
        {
            var averageTimes = ParsePositiveInt(AverageTimesTextBox.Text, "平均次数");
            var timingPlanMs = new[] { 1d, 2d, 4d, 8d, 16d, 20d, 50d, 100d };
            AppendLog("开始时序测试");

            foreach (var integrationTimeMs in timingPlanMs)
            {
                var integrationTimeUs = ConvertMsToUs(integrationTimeMs);
                var setIntResult = DirectSpectrometerLogger.Measure($"SA_SetIntegrationTime({_selectedIndex}, {integrationTimeUs})", () => SpectrometerApi.SA_SetIntegrationTime(_selectedIndex, integrationTimeUs).ToString());
                var setAvgResult = DirectSpectrometerLogger.Measure($"SA_SetAverageTimes({_selectedIndex}, {averageTimes})", () => SpectrometerApi.SA_SetAverageTimes(_selectedIndex, averageTimes).ToString());

                int spectrumCount = BufferSize;
                var stopwatch = Stopwatch.StartNew();
                var getResult = DirectSpectrometerLogger.Measure($"SA_GetSpectum({_selectedIndex}) [Int={integrationTimeMs:F3}ms/{integrationTimeUs}us, Avg={averageTimes}]", () => SpectrometerApi.SA_GetSpectum(_selectedIndex, _spectrumBuffer, ref spectrumCount).ToString());
                stopwatch.Stop();

                AppendLog($"Timing | Int={integrationTimeMs:F3}ms | Avg={averageTimes} | SetIntRet={setIntResult} | SetAvgRet={setAvgResult} | GetRet={getResult} | Points={spectrumCount} | Measured={stopwatch.ElapsedMilliseconds}ms");
            }

            AppendLog("时序测试结束");
        }
        catch (Exception ex)
        {
            AppendLog($"时序测试失败: {ex.Message}");
            DirectSpectrometerLogger.Error("RunTimingButton_Click failed", ex);
        }
    }

    private async void RunContinuousButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureOpen())
        {
            return;
        }

        if (_isContinuousRunning)
        {
            _continuousCts?.Cancel();
            AppendLog("正在请求停止连续测试");
            return;
        }

        try
        {
            _isContinuousRunning = true;
            _continuousCts = new CancellationTokenSource();
            RunContinuousButton.Content = "停止连续";
            ApplyCurrentSettings();
            var integrationTimeMs = ParsePositiveDouble(IntegrationTimeTextBox.Text, "积分时间");
            var averageTimes = ParsePositiveInt(AverageTimesTextBox.Text, "平均次数");
            var intervalMs = ParsePositiveDouble(IntervalTextBox.Text, "间隔");
            AppendLog($"开始连续测试 | Int={integrationTimeMs:F3}ms | Avg={averageTimes} | Interval={intervalMs:F0}ms");

            await RunContinuousCaptureAsync(integrationTimeMs, averageTimes, intervalMs, _continuousCts.Token);
            AppendLog("连续测试已停止");
        }
        catch (OperationCanceledException)
        {
            AppendLog("连续测试已取消");
        }
        catch (Exception ex)
        {
            AppendLog($"连续测试失败: {ex.Message}");
            DirectSpectrometerLogger.Error("RunContinuousButton_Click failed", ex);
        }
        finally
        {
            _continuousCts?.Dispose();
            _continuousCts = null;
            _isContinuousRunning = false;
            RunContinuousButton.Content = "连续测试";
        }
    }

    private Task RunContinuousCaptureAsync(double integrationTimeMs, int averageTimes, double intervalMs, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var round = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                round++;
                var localSpectrum = new double[BufferSize];
                int spectrumCount = BufferSize;
                var stopwatch = Stopwatch.StartNew();
                var result = DirectSpectrometerLogger.Measure($"SA_GetSpectum({_selectedIndex}) [Continuous #{round}]", () => SpectrometerApi.SA_GetSpectum(_selectedIndex, localSpectrum, ref spectrumCount).ToString());
                stopwatch.Stop();

                var measured = stopwatch.ElapsedMilliseconds;
                Dispatcher.Invoke(() =>
                {
                    AppendLog($"Continuous | Round={round} | Int={integrationTimeMs:F3}ms | Avg={averageTimes} | GetRet={result} | Points={spectrumCount} | Measured={measured}ms");
                    if (spectrumCount > 0)
                    {
                        PlotSpectrum(localSpectrum, spectrumCount, measured);
                    }
                });

                if (!cancellationToken.IsCancellationRequested)
                {
                    var sleepMs = (int)Math.Max(0, intervalMs - measured);
                    if (sleepMs > 0)
                    {
                        Thread.Sleep(sleepMs);
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
        }, cancellationToken);
    }

    private void AutoRampButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureOpen())
        {
            return;
        }

        try
        {
            new AutoRampWindow(_selectedIndex)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();
        }
        catch (Exception ex)
        {
            AppendLog($"打开自动增长窗口失败: {ex.Message}");
            DirectSpectrometerLogger.Error("AutoRampButton_Click failed", ex);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseSpectrometer();
    }

    private void LoadWavelength()
    {
        int spectrumCount = BufferSize;
        var result = DirectSpectrometerLogger.Measure($"SA_GetWavelength({_selectedIndex})", () => SpectrometerApi.SA_GetWavelength(_selectedIndex, _wavelengthBuffer, ref spectrumCount).ToString());
        AppendLog($"获取波长返回: {result}，点数: {spectrumCount}");
    }

    private void ApplyCurrentSettings()
    {
        var integrationTimeMs = ParsePositiveDouble(IntegrationTimeTextBox.Text, "积分时间");
        var integrationTimeUs = ConvertMsToUs(integrationTimeMs);
        var averageTimes = ParsePositiveInt(AverageTimesTextBox.Text, "平均次数");

        var setIntResult = DirectSpectrometerLogger.Measure($"SA_SetIntegrationTime({_selectedIndex}, {integrationTimeUs})", () => SpectrometerApi.SA_SetIntegrationTime(_selectedIndex, integrationTimeUs).ToString());
        var setAvgResult = DirectSpectrometerLogger.Measure($"SA_SetAverageTimes({_selectedIndex}, {averageTimes})", () => SpectrometerApi.SA_SetAverageTimes(_selectedIndex, averageTimes).ToString());

        AppendLog($"采集前设置 | IntegrationTime={integrationTimeMs:F3}ms | SetIntegrationTime={setIntResult} | SetAverageTimes={setAvgResult}");
    }

    private void AcquireAndPlotSpectrum()
    {
        int spectrumCount = BufferSize;
        var stopwatch = Stopwatch.StartNew();
        var result = DirectSpectrometerLogger.Measure($"SA_GetSpectum({_selectedIndex})", () => SpectrometerApi.SA_GetSpectum(_selectedIndex, _spectrumBuffer, ref spectrumCount).ToString());
        stopwatch.Stop();

        AppendLog($"获取光谱返回: {result}，点数: {spectrumCount}，耗时: {stopwatch.ElapsedMilliseconds}ms");

        if (spectrumCount <= 0)
        {
            throw new InvalidOperationException("返回点数 <= 0");
        }

        PlotSpectrum(spectrumCount, stopwatch.ElapsedMilliseconds);
    }

    private void PlotSpectrum(double[] spectrumData, int spectrumCount, long elapsedMilliseconds)
    {
        Array.Copy(spectrumData, _spectrumBuffer, Math.Min(spectrumCount, _spectrumBuffer.Length));
        PlotSpectrum(spectrumCount, elapsedMilliseconds);
    }

    private void PlotSpectrum(int spectrumCount, long elapsedMilliseconds)
    {
        var xs = _wavelengthBuffer.Take(spectrumCount).ToArray();
        var ys = _spectrumBuffer.Take(spectrumCount).ToArray();

        SpectrumPlot.Plot.Clear();
        var scatter = SpectrumPlot.Plot.Add.Scatter(xs, ys);
        scatter.LineWidth = 1;
        SpectrumPlot.Plot.Title($"Spectrum - Points={spectrumCount} - Time={elapsedMilliseconds}ms");
        SpectrumPlot.Plot.XLabel("Wavelength (nm)");
        SpectrumPlot.Plot.YLabel("Intensity");
        SpectrumPlot.Plot.Axes.AutoScale();
        SpectrumPlot.Refresh();

        var maxY = ys.Max();
        var minY = ys.Min();
        var maxIndex = Array.IndexOf(ys, maxY);
        var peakX = maxIndex >= 0 && maxIndex < xs.Length ? xs[maxIndex] : 0;
        AppendLog($"光谱统计 | Min={minY:F3} | Max={maxY:F3} | PeakWavelength={peakX:F3}nm");
        StatusTextBlock.Text = $"采集完成 | Points={spectrumCount} | Time={elapsedMilliseconds}ms";
    }


    private static int ConvertMsToUs(double milliseconds)
    {
        return (int)Math.Round(milliseconds * 1000d, MidpointRounding.AwayFromZero);
    }

    private static double ParsePositiveDouble(string text, string fieldName)
    {
        if (!double.TryParse(text?.Trim(), out var value) || value <= 0)
        {
            throw new InvalidOperationException($"{fieldName}必须是正数");
        }

        return value;
    }

    private static int ParsePositiveInt(string text, string fieldName)
    {
        if (!int.TryParse(text?.Trim(), out var value) || value <= 0)
        {
            throw new InvalidOperationException($"{fieldName}必须是正整数");
        }

        return value;
    }

    private bool EnsureOpen()
    {
        if (_isOpen)
        {
            return true;
        }

        AppendLog("设备未打开");
        return false;
    }

    private void CloseSpectrometer()
    {
        try
        {
            _continuousCts?.Cancel();
            _isContinuousRunning = false;
            if (!_isOpen)
            {
                AppendLog("设备已经关闭");
                return;
            }

            DirectSpectrometerLogger.Measure("SA_CloseSpectrometers", () =>
            {
                SpectrometerApi.SA_CloseSpectrometers();
                return "OK";
            });

            _isOpen = false;
            _spectrometerCount = -1;
            _selectedIndex = 0;
            AppendLog("设备已关闭");
            StatusTextBlock.Text = "设备已关闭";
        }
        catch (Exception ex)
        {
            AppendLog($"关闭设备失败: {ex.Message}");
            DirectSpectrometerLogger.Error("CloseSpectrometer failed", ex);
        }
    }

    private void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        _uiLogs.Add(line);
        if (_uiLogs.Count > 1000)
        {
            _uiLogs.RemoveAt(0);
        }

        LogTextBox.Text = string.Join(Environment.NewLine, _uiLogs);
        LogTextBox.CaretIndex = LogTextBox.Text.Length;
        LogTextBox.ScrollToEnd();
        DirectSpectrometerLogger.Info(message);
    }

    protected override void OnClosed(EventArgs e)
    {
        CloseSpectrometer();
        Dispose();
        base.OnClosed(e);
    }

    public void Dispose()
    {
        _continuousCts?.Dispose();
        _continuousCts = null;
        GC.SuppressFinalize(this);
    }
}