using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Spectrum.DirectSpectrometer;

public partial class AutoRampWindow : Window, IDisposable
{
    public class TimingResult
    {
        public int Index { get; set; }
        public double IntegrationTimeMs { get; set; }
        public int IntegrationTimeUs { get; set; }
        public int AverageTimes { get; set; }
        public long ElapsedMs { get; set; }
        public double ExpectedMs { get; set; }
        public double ErrorMs => ElapsedMs - ExpectedMs;
        public double ErrorPercent => ExpectedMs > 0 ? ErrorMs / ExpectedMs * 100 : 0;
        public int ResultCode { get; set; }
    }

    private readonly List<TimingResult> _results = new();
    private readonly List<string> _uiLogs = new();
    private readonly int _spectrometerIndex;
    private readonly double[] _spectrumBuffer = new double[8192];
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    public AutoRampWindow(int spectrometerIndex)
    {
        InitializeComponent();
        _spectrometerIndex = spectrometerIndex;
        StatusTextBlock.Text = "就绪";
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            return;
        }

        try
        {
            var startMs = ParsePositiveDouble(StartTimeTextBox.Text, "起始积分时间");
            var endMs = ParsePositiveDouble(EndTimeTextBox.Text, "结束积分时间");
            var stepMs = ParsePositiveDouble(StepTimeTextBox.Text, "步长");
            var avgTimes = ParsePositiveInt(AvgTimesTextBox.Text, "平均次数");

            if (endMs <= startMs)
            {
                throw new InvalidOperationException("结束积分时间必须大于起始积分时间");
            }

            _results.Clear();
            ResultsDataGrid.ItemsSource = null;
            _uiLogs.Clear();
            LogTextBox.Clear();

            _isRunning = true;
            _cts = new CancellationTokenSource();
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            ExportButton.IsEnabled = false;

            AppendLog($"开始自动增长测试 | 范围: {startMs:F3}ms ~ {endMs:F3}ms | 步长: {stepMs:F3}ms | 平均次数: {avgTimes}");
            await RunAutoRampTestAsync(startMs, endMs, stepMs, avgTimes, _cts.Token);

            AppendLog("测试完成");
            StatusTextBlock.Text = $"测试完成 | 共 {_results.Count} 组数据";
        }
        catch (OperationCanceledException)
        {
            AppendLog("测试已取消");
            StatusTextBlock.Text = $"测试已取消 | 已完成 {_results.Count} 组数据";
        }
        catch (Exception ex)
        {
            AppendLog($"测试失败: {ex.Message}");
            StatusTextBlock.Text = "测试失败";
        }
        finally
        {
            _isRunning = false;
            _cts?.Dispose();
            _cts = null;
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            ExportButton.IsEnabled = _results.Count > 0;
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            _cts?.Cancel();
            AppendLog("正在请求停止测试...");
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV文件 (*.csv)|*.csv",
                FileName = $"TimingTest_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                using var writer = new StreamWriter(dialog.FileName, false, System.Text.Encoding.UTF8);
                writer.WriteLine("序号,积分时间(ms),积分时间(us),平均次数,实际耗时(ms),预期耗时(ms),误差(ms),误差比例(%),返回码");
                foreach (var result in _results)
                {
                    writer.WriteLine($"{result.Index},{result.IntegrationTimeMs:F6},{result.IntegrationTimeUs},{result.AverageTimes},{result.ElapsedMs:F2},{result.ExpectedMs:F2},{result.ErrorMs:F2},{result.ErrorPercent:F2},{result.ResultCode}");
                }

                AppendLog($"已导出到: {dialog.FileName}");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"导出失败: {ex.Message}");
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            _cts?.Cancel();
        }

        Close();
    }

    private Task RunAutoRampTestAsync(double startMs, double endMs, double stepMs, int avgTimes, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var index = 0;
            var currentMs = startMs;

            while (currentMs <= endMs && !cancellationToken.IsCancellationRequested)
            {
                var integrationTimeUs = (int)Math.Round(currentMs * 1000d, MidpointRounding.AwayFromZero);
                var expectedMs = currentMs * avgTimes;
                var setIntResult = SpectrometerApi.SA_SetIntegrationTime(_spectrometerIndex, integrationTimeUs);
                var setAvgResult = SpectrometerApi.SA_SetAverageTimes(_spectrometerIndex, avgTimes);

                if (setIntResult != 0 || setAvgResult != 0)
                {
                    Dispatcher.Invoke(() => AppendLog($"设置参数失败: Int={currentMs:F3}ms, SetIntResult={setIntResult}, SetAvgResult={setAvgResult}"));
                    break;
                }

                int spectrumCount = 8192;
                var stopwatch = Stopwatch.StartNew();
                var getResult = SpectrometerApi.SA_GetSpectum(_spectrometerIndex, _spectrumBuffer, ref spectrumCount);
                stopwatch.Stop();

                if (getResult == 0 && spectrumCount > 0)
                {
                    var calibrated = new double[spectrumCount];
                    var calibResult = SpectrometerApi.SA_NonlinearCalibration(_spectrometerIndex, _spectrumBuffer, calibrated, spectrumCount);
                    if (calibResult == 0)
                    {
                        Array.Copy(calibrated, _spectrumBuffer, spectrumCount);
                    }
                }

                var result = new TimingResult
                {
                    Index = ++index,
                    IntegrationTimeMs = currentMs,
                    IntegrationTimeUs = integrationTimeUs,
                    AverageTimes = avgTimes,
                    ElapsedMs = stopwatch.ElapsedMilliseconds,
                    ExpectedMs = expectedMs,
                    ResultCode = getResult
                };

                Dispatcher.Invoke(() =>
                {
                    _results.Add(result);
                    ResultsDataGrid.ItemsSource = null;
                    ResultsDataGrid.ItemsSource = _results;
                    ResultsDataGrid.ScrollIntoView(result);
                    AppendLog($"#{result.Index} | Int={result.IntegrationTimeMs:F3}ms | Avg={avgTimes} | Measured={result.ElapsedMs}ms | Expected={result.ExpectedMs:F2}ms | Error={result.ErrorMs:F2}ms ({result.ErrorPercent:F2}%) | Result={result.ResultCode}");
                    StatusTextBlock.Text = $"测试中... #{result.Index} | Int={result.IntegrationTimeMs:F3}ms";
                });

                currentMs += stepMs;
                if (!cancellationToken.IsCancellationRequested)
                {
                    Thread.Sleep(10);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
        }, cancellationToken);
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

    private void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        _uiLogs.Add(line);
        if (_uiLogs.Count > 500)
        {
            _uiLogs.RemoveAt(0);
        }

        LogTextBox.Text = string.Join(Environment.NewLine, _uiLogs);
        LogTextBox.CaretIndex = LogTextBox.Text.Length;
        LogTextBox.ScrollToEnd();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_isRunning)
        {
            _cts?.Cancel();
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        Dispose();
        base.OnClosed(e);
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _cts = null;
        GC.SuppressFinalize(this);
    }
}