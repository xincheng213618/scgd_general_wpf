using ColorVision.UI.Menus;
using Spectrum.Menus;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Spectrum.DirectSpectrometer;

public class MenuDirectSpectrometerWindow : SpectrumMenuIBase
{
    public override string OwnerGuid => MenuItemConstants.Help;
    public override int Order => 10005;
    public override string Header => "光谱仪直连测试";

    public override void Execute()
    {
        if (DirectSpectrometerWindow.Instance is { IsLoaded: true } existingWindow)
        {
            if (existingWindow.WindowState == WindowState.Minimized)
                existingWindow.WindowState = WindowState.Normal;
            existingWindow.Activate();
            return;
        }

        new DirectSpectrometerWindow
        {
            Owner = Application.Current.GetActiveWindow(),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        }.Show();
    }
}

public partial class DirectSpectrometerWindow : Window, IDisposable
{
    internal static DirectSpectrometerWindow? Instance { get; private set; }
    private const int BufferSize = 8192;
    private readonly double[] _wavelengthBuffer = new double[BufferSize];
    private readonly double[] _spectrumBuffer = new double[BufferSize];
    private readonly List<string> _uiLogs = new();
    private double[] _plotWavelengths = Array.Empty<double>();
    private double[] _plotSpectrum = Array.Empty<double>();
    private int _spectrometerCount = -1;
    private int _selectedIndex;
    private bool _isOpen;
    private bool _isContinuousRunning;
    private bool _allowWindowClose;
    private bool _isWindowClosing;
    private CancellationTokenSource? _continuousCts;
    private Task? _continuousTask;
    private Task<bool>? _closeSpectrometerTask;

    public DirectSpectrometerWindow()
    {
        InitializeComponent();
        Instance = this;
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
        bool sessionAcquired = false;
        try
        {
            if (_isOpen)
            {
                AppendLog("设备已经打开");
                return;
            }

            if (_closeSpectrometerTask is { IsCompleted: false })
            {
                AppendLog("设备正在关闭，请稍后再打开");
                return;
            }

            sessionAcquired = SpectrometerNativeSession.TryAcquire(SpectrometerNativeSessionOwner.Direct);
            if (!sessionAcquired)
            {
                AppendLog("光谱仪驱动当前不可用；请先断开主光谱仪。若刚才释放失败，请重启程序");
                StatusTextBlock.Text = "驱动不可用";
                return;
            }

            _closeSpectrometerTask = null;
            var version = DirectSpectrometerLogger.Measure("SA_GetAPIVersion", SpectrometerApi.GetApiVersion);
            AppendLog($"API Version: {version}");
            _spectrometerCount = DirectSpectrometerLogger.Measure("SA_OpenSpectrometers", SpectrometerApi.SA_OpenSpectrometers);
            if (_spectrometerCount < 0)
            {
                SpectrometerNativeSession.Release(SpectrometerNativeSessionOwner.Direct);
                sessionAcquired = false;
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
            if (sessionAcquired && !_isOpen)
                SpectrometerNativeSession.Release(SpectrometerNativeSessionOwner.Direct);
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

            var setIntResult = DirectSpectrometerLogger.Measure($"SA_SetIntegrationTime({_selectedIndex}, {integrationTimeUs})", () => SpectrometerApi.SA_SetIntegrationTime(_selectedIndex, integrationTimeUs));
            var setAvgResult = DirectSpectrometerLogger.Measure($"SA_SetAverageTimes({_selectedIndex}, {averageTimes})", () => SpectrometerApi.SA_SetAverageTimes(_selectedIndex, averageTimes));

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
                var setIntResult = DirectSpectrometerLogger.Measure($"SA_SetIntegrationTime({_selectedIndex}, {integrationTimeUs})", () => SpectrometerApi.SA_SetIntegrationTime(_selectedIndex, integrationTimeUs));
                var setAvgResult = DirectSpectrometerLogger.Measure($"SA_SetAverageTimes({_selectedIndex}, {averageTimes})", () => SpectrometerApi.SA_SetAverageTimes(_selectedIndex, averageTimes));

                int spectrumCount = BufferSize;
                var getResult = DirectSpectrometerLogger.Measure($"SA_GetSpectum({_selectedIndex}) [Int={integrationTimeMs:F3}ms/{integrationTimeUs}us, Avg={averageTimes}]", () => SpectrometerApi.SA_GetSpectum(_selectedIndex, _spectrumBuffer, ref spectrumCount), out var elapsedMilliseconds);

                AppendLog($"Timing | Int={integrationTimeMs:F3}ms | Avg={averageTimes} | SetIntRet={setIntResult} | SetAvgRet={setAvgResult} | GetRet={getResult} | Points={spectrumCount} | Measured={elapsedMilliseconds}ms");
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
            SetContinuousMode(true);
            ApplyCurrentSettings();
            var integrationTimeMs = ParsePositiveDouble(IntegrationTimeTextBox.Text, "积分时间");
            var averageTimes = ParsePositiveInt(AverageTimesTextBox.Text, "平均次数");
            var intervalMs = ParsePositiveDouble(IntervalTextBox.Text, "间隔");
            AppendLog($"开始连续测试 | Int={integrationTimeMs:F3}ms | Avg={averageTimes} | Interval={intervalMs:F0}ms");

            _continuousTask = RunContinuousCaptureAsync(integrationTimeMs, averageTimes, intervalMs, _continuousCts.Token);
            await _continuousTask;
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
            _continuousTask = null;
            _isContinuousRunning = false;
            RunContinuousButton.Content = "连续测试";
            SetContinuousMode(false);
        }
    }

    private void SetContinuousMode(bool running)
    {
        OpenButton.IsEnabled = !running;
        ApplyButton.IsEnabled = !running;
        GetSpectrumButton.IsEnabled = !running;
        RunTimingButton.IsEnabled = !running;
        AutoRampButton.IsEnabled = !running;
        IntegrationTimeTextBox.IsEnabled = !running;
        AverageTimesTextBox.IsEnabled = !running;
        IntervalTextBox.IsEnabled = !running;
    }

    private Task RunContinuousCaptureAsync(double integrationTimeMs, int averageTimes, double intervalMs, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var round = 0;
            var localSpectrum = new double[BufferSize];
            while (!cancellationToken.IsCancellationRequested)
            {
                round++;
                int spectrumCount = BufferSize;
                var roundStarted = Stopwatch.GetTimestamp();
                var result = DirectSpectrometerLogger.Measure($"SA_GetSpectum({_selectedIndex}) [Continuous #{round}]", () => SpectrometerApi.SA_GetSpectum(_selectedIndex, localSpectrum, ref spectrumCount), out var measured);

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
                    var roundElapsedMs = Stopwatch.GetElapsedTime(roundStarted).TotalMilliseconds;
                    var sleepMs = (int)Math.Max(0, intervalMs - roundElapsedMs);
                    if (sleepMs > 0)
                    {
                        cancellationToken.WaitHandle.WaitOne(sleepMs);
                    }
                }
            }
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

    private async void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        await CloseSpectrometerAsync();
    }

    private void LoadWavelength()
    {
        int spectrumCount = BufferSize;
        var result = DirectSpectrometerLogger.Measure($"SA_GetWavelength({_selectedIndex})", () => SpectrometerApi.SA_GetWavelength(_selectedIndex, _wavelengthBuffer, ref spectrumCount));
        AppendLog($"获取波长返回: {result}，点数: {spectrumCount}");
    }

    private void ApplyCurrentSettings()
    {
        var integrationTimeMs = ParsePositiveDouble(IntegrationTimeTextBox.Text, "积分时间");
        var integrationTimeUs = ConvertMsToUs(integrationTimeMs);
        var averageTimes = ParsePositiveInt(AverageTimesTextBox.Text, "平均次数");

        var setIntResult = DirectSpectrometerLogger.Measure($"SA_SetIntegrationTime({_selectedIndex}, {integrationTimeUs})", () => SpectrometerApi.SA_SetIntegrationTime(_selectedIndex, integrationTimeUs));
        var setAvgResult = DirectSpectrometerLogger.Measure($"SA_SetAverageTimes({_selectedIndex}, {averageTimes})", () => SpectrometerApi.SA_SetAverageTimes(_selectedIndex, averageTimes));

        AppendLog($"采集前设置 | IntegrationTime={integrationTimeMs:F3}ms | SetIntegrationTime={setIntResult} | SetAverageTimes={setAvgResult}");
    }

    private void AcquireAndPlotSpectrum()
    {
        int spectrumCount = BufferSize;
        var result = DirectSpectrometerLogger.Measure($"SA_GetSpectum({_selectedIndex})", () => SpectrometerApi.SA_GetSpectum(_selectedIndex, _spectrumBuffer, ref spectrumCount), out var elapsedMilliseconds);

        AppendLog($"获取光谱返回: {result}，点数: {spectrumCount}，耗时: {elapsedMilliseconds}ms");

        if (spectrumCount <= 0)
        {
            throw new InvalidOperationException("返回点数 <= 0");
        }

        PlotSpectrum(spectrumCount, elapsedMilliseconds);
    }

    private void PlotSpectrum(double[] spectrumData, int spectrumCount, long elapsedMilliseconds)
    {
        var pointCount = Math.Min(spectrumCount, Math.Min(spectrumData.Length, _wavelengthBuffer.Length));
        if (pointCount <= 0)
        {
            return;
        }

        if (_plotSpectrum.Length != pointCount)
        {
            _plotSpectrum = new double[pointCount];
            _plotWavelengths = new double[pointCount];
        }

        Array.Copy(_wavelengthBuffer, _plotWavelengths, pointCount);
        var minY = spectrumData[0];
        var maxY = spectrumData[0];
        var maxIndex = 0;
        for (var index = 0; index < pointCount; index++)
        {
            var value = spectrumData[index];
            _plotSpectrum[index] = value;
            if (value < minY)
            {
                minY = value;
            }
            if (value > maxY)
            {
                maxY = value;
                maxIndex = index;
            }
        }

        SpectrumPlot.Plot.Clear();
        var scatter = SpectrumPlot.Plot.Add.Scatter(_plotWavelengths, _plotSpectrum);
        scatter.LineWidth = 1;
        SpectrumPlot.Plot.Title($"Spectrum - Points={pointCount} - Time={elapsedMilliseconds}ms");
        SpectrumPlot.Plot.XLabel("Wavelength (nm)");
        SpectrumPlot.Plot.YLabel("Intensity");
        SpectrumPlot.Plot.Axes.AutoScale();
        SpectrumPlot.Refresh();

        var peakX = _plotWavelengths[maxIndex];
        AppendLog($"光谱统计 | Min={minY:F3} | Max={maxY:F3} | PeakWavelength={peakX:F3}nm");
        StatusTextBlock.Text = $"采集完成 | Points={pointCount} | Time={elapsedMilliseconds}ms";
    }

    private void PlotSpectrum(int spectrumCount, long elapsedMilliseconds)
    {
        PlotSpectrum(_spectrumBuffer, spectrumCount, elapsedMilliseconds);
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

    private Task<bool> CloseSpectrometerAsync()
    {
        if (_closeSpectrometerTask is { IsCompleted: false })
        {
            return _closeSpectrometerTask;
        }

        _closeSpectrometerTask = CloseSpectrometerCoreAsync();
        return _closeSpectrometerTask;
    }

    private async Task<bool> CloseSpectrometerCoreAsync()
    {
        var shouldClose = _isOpen;
        try
        {
            _continuousCts?.Cancel();
            var continuousTask = _continuousTask;
            if (continuousTask != null)
            {
                try
                {
                    await continuousTask;
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    DirectSpectrometerLogger.Error("Continuous capture stopped with an error while closing", ex);
                }
            }

            if (!shouldClose)
            {
                AppendLog("设备已经关闭");
                DirectSpectrometerLogger.Flush();
                return true;
            }

            DirectSpectrometerLogger.Measure("SA_CloseSpectrometers", () =>
            {
                SpectrometerApi.SA_CloseSpectrometers();
                return "OK";
            });

            _isOpen = false;
            SpectrometerNativeSession.Release(SpectrometerNativeSessionOwner.Direct);
            _spectrometerCount = -1;
            _selectedIndex = 0;
            AppendLog("设备已关闭");
            StatusTextBlock.Text = "设备已关闭";
            DirectSpectrometerLogger.Flush();
            return true;
        }
        catch (Exception ex)
        {
            AppendLog($"关闭设备失败: {ex.Message}");
            DirectSpectrometerLogger.Error("CloseSpectrometer failed", ex);
            return false;
        }
    }

    private void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        _uiLogs.Add(line);
        if (_uiLogs.Count > 1000)
        {
            _uiLogs.RemoveRange(0, 100);
            LogTextBox.Text = string.Join(Environment.NewLine, _uiLogs) + Environment.NewLine;
        }
        else
        {
            LogTextBox.AppendText(line + Environment.NewLine);
        }

        LogTextBox.CaretIndex = LogTextBox.Text.Length;
        LogTextBox.ScrollToEnd();
        DirectSpectrometerLogger.Info(message);
    }

    protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_allowWindowClose)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        base.OnClosing(e);
        e.Cancel = true;
        if (_isWindowClosing)
        {
            return;
        }

        _isWindowClosing = true;
        IsEnabled = false;
        bool closed = await CloseSpectrometerAsync();
        if (!closed)
        {
            _isWindowClosing = false;
            IsEnabled = true;
            return;
        }
        _allowWindowClose = true;
        _ = Dispatcher.BeginInvoke(new Action(Close));
    }

    protected override void OnClosed(EventArgs e)
    {
        Dispose();
        base.OnClosed(e);
    }

    public void Dispose()
    {
        _continuousCts?.Dispose();
        _continuousCts = null;
        DirectSpectrometerLogger.Close();
        if (ReferenceEquals(Instance, this))
            Instance = null;
        GC.SuppressFinalize(this);
    }
}
