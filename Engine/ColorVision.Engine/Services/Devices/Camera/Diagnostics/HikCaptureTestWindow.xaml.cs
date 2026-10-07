using ColorVision.Themes;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera.Diagnostics;

public partial class HikCaptureTestWindow : Window
{
    private readonly ObservableCollection<HikCaptureSample> samples = new();
    private CancellationTokenSource? cancellation;
    private BlockingCollection<HikCaptureRequest>? captureRequests;
    private bool busy, connected, closeWhenFinished;
    private int run;
    private readonly string logPath;

    internal HikCaptureTestWindow(HikCaptureTestSettings settings)
    {
        InitializeComponent();
        this.ApplyCaption();
        CameraBox.Text = settings.CameraId;
        ExposureBox.Text = settings.ExposureMs.ToString(CultureInfo.CurrentCulture);
        GainBox.Text = settings.Gain.ToString(CultureInfo.CurrentCulture);
        QualityBox.SelectedIndex = (int)settings.BayerQuality;
        SamplesGrid.ItemsSource = samples;
        string logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision", "Log");
        Directory.CreateDirectory(logDirectory);
        logPath = Path.Combine(logDirectory, $"HikCaptureTest_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Environment.ProcessId}.log");
        AppendLog($"[HikCaptureTest] Ready. Log={logPath}; No automatic image files or database writes. Raw export requires a separate manual capture.");
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (busy || cancellation != null) return;
        SetBusy(true);
        string selected = CameraBox.SelectedItem is HikTestDevice device ? device.Serial : CameraBox.Text.Trim();
        try
        {
            var devices = await Task.Run(HikCaptureBenchmark.Discover);
            CameraBox.ItemsSource = devices;
            CameraBox.SelectedItem = devices.FirstOrDefault(d => d.Serial.Equals(selected, StringComparison.OrdinalIgnoreCase))
                ?? (devices.Count == 1 ? devices[0] : null);
            if (CameraBox.SelectedItem == null) CameraBox.Text = selected;
            StatusText.Text = EngineLocalization.Format($"找到 {devices.Count} 台海康相机。");
        }
        catch (Exception ex) { ReportFailure(ex); }
        finally { SetBusy(false); }
    }

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        if (busy || cancellation != null) return;
        try
        {
            if (!float.TryParse(ExposureBox.Text, out float exposure) || !float.TryParse(GainBox.Text, out float gain))
                throw new InvalidOperationException(EngineLocalization.Get("HikTest_InvalidSettings"));
            var settings = new HikCaptureTestSettings
            {
                CameraId = CameraBox.SelectedItem is HikTestDevice device ? device.Serial : CameraBox.Text.Trim(),
                ExposureMs = exposure, Gain = gain, BayerQuality = (uint)QualityBox.SelectedIndex
            };
            cancellation = new CancellationTokenSource();
            captureRequests = new BlockingCollection<HikCaptureRequest>(1);
            SetBusy(true);
            int currentRun = ++run;
            SummaryText.Text = string.Empty;
            AppendLog(EngineLocalization.Get("HikTest_Footnote"));
            var ready = new Progress<bool>(_ =>
            {
                if (cancellation?.IsCancellationRequested != false) return;
                connected = true;
                SetBusy(false);
                StatusText.Text = EngineLocalization.Get("HikTest_Connected");
                AppendLog($"[HikCaptureTest] Connected Run={currentRun}; waiting for manual capture.");
            });
            var progress = new Progress<HikCaptureSample>(sample =>
            {
                samples.Add(sample);
                UpdateSummary(currentRun);
                AppendLog(FormattableString.Invariant($"[CameraCaptureCompare] Path=MVS Run={sample.Run} Index={sample.Index} FirstFrame={sample.FirstFrame} Serial={settings.CameraId} RequestedExposureMs={settings.ExposureMs} ActualExposureUs={sample.ActualExposureUs} RequestedGain={settings.Gain} ActualGain={sample.ActualGain} Width={sample.Width} Height={sample.Height} Bits=16 Channels=3 BayerQuality={settings.BayerQuality} SetupMs={sample.SetupMs:F3} CaptureMs={sample.CaptureMs:F3} TriggerWaitMs={sample.WaitMs:F3} ConvertMs={sample.ConvertMs:F3} ThreadCpuMs={sample.ThreadCpuMs:F3} CycleMs={sample.CycleMs:F3} FrameId={sample.FrameId} RawExport={sample.RawExportPath != null}"));
                if (cancellation?.IsCancellationRequested == false)
                {
                    SetBusy(false);
                    StatusText.Text = sample.RawExportPath == null ? EngineLocalization.Get("HikTest_Connected")
                        : EngineLocalization.Format($"原始帧已保存：{sample.RawExportPath}");
                }
            });
            var log = new Progress<string>(AppendLog);
            await Task.Run(() => HikCaptureBenchmark.Run(settings, currentRun, captureRequests, ready, progress, log, cancellation.Token));
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = EngineLocalization.Get("HikTest_Cancelled");
            AppendLog($"[HikCaptureTest] Cancelled Run={run}; {SummaryText.Text}");
        }
        catch (Exception ex) { ReportFailure(ex); }
        finally
        {
            cancellation?.Dispose();
            cancellation = null;
            captureRequests?.Dispose();
            captureRequests = null;
            connected = false;
            SetBusy(false);
        }
    }

    private void Capture_Click(object sender, RoutedEventArgs e)
        => QueueCapture(new HikCaptureRequest());

    private void SaveRaw_Click(object sender, RoutedEventArgs e)
    {
        if (!connected || busy || cancellation?.IsCancellationRequested != false) return;
        var dialog = new SaveFileDialog
        {
            Title = EngineLocalization.Get("HikTest_SaveRaw"),
            Filter = "Bayer16 + JSON (*.zip)|*.zip", DefaultExt = ".zip", AddExtension = true,
            FileName = $"HikBayer_{DateTime.Now:yyyyMMdd_HHmmss_fff}.zip"
        };
        if (dialog.ShowDialog(this) == true) QueueCapture(new HikCaptureRequest(dialog.FileName));
    }

    private void QueueCapture(HikCaptureRequest request)
    {
        if (!connected || busy || cancellation?.IsCancellationRequested != false) return;
        SetBusy(true);
        captureRequests!.Add(request);
    }

    private void UpdateSummary(int currentRun)
    {
        var rows = samples.Where(s => s.Run == currentRun).ToArray();
        double[] warm = rows.Where(s => !s.FirstFrame).Select(s => s.CaptureMs).Order().ToArray();
        if (warm.Length == 0)
        {
            SummaryText.Text = EngineLocalization.Format($"首次取图 {rows[0].CaptureMs:F3} ms；等待后续样本。");
            return;
        }
        double median = warm.Length % 2 == 0 ? (warm[warm.Length / 2 - 1] + warm[warm.Length / 2]) / 2 : warm[warm.Length / 2];
        double p95 = warm[(int)Math.Ceiling(warm.Length * 0.95) - 1];
        SummaryText.Text = EngineLocalization.Format($"首次 {rows[0].CaptureMs:F3} ms；后续 {warm.Length} 帧：中位数 {median:F3} ms，P95 {p95:F3} ms，平均 {warm.Average():F3} ms。");
    }

    private void ReportFailure(Exception ex)
    {
        StatusText.Text = ex.Message;
        AppendLog($"ERROR: {ex}");
        if (ex is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
            AppendLog(EngineLocalization.Get("HikTest_SdkRequired"));
    }

    private void AppendLog(string message)
    {
        string line = $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}";
        DetailsBox.AppendText(line);
        DetailsBox.ScrollToEnd();
        try { File.AppendAllText(logPath, line, new UTF8Encoding(false)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            cancellation?.Cancel();
            DetailsBox.AppendText($"LOG WRITE FAILED: {ex.Message}{Environment.NewLine}");
            StatusText.Text = ex.Message;
        }
    }

    private void SetBusy(bool value)
    {
        busy = value;
        SettingsPanel.IsEnabled = RunButton.IsEnabled = !value && cancellation == null;
        CopyButton.IsEnabled = !value;
        SaveRawButton.IsEnabled = CaptureButton.IsEnabled = connected && !value && cancellation?.IsCancellationRequested == false;
        StopButton.IsEnabled = cancellation?.IsCancellationRequested == false;
        if (value) StatusText.Text = EngineLocalization.Get("HikTest_Running");
        else if (closeWhenFinished) Close();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        cancellation?.Cancel();
        SetBusy(true);
        StatusText.Text = EngineLocalization.Get("HikTest_Stopping");
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var report = new StringBuilder(DetailsBox.Text);
        report.AppendLine().AppendLine("run,index,first_frame,actual_exposure_us,actual_gain,cycle_ms,setup_ms,capture_ms,trigger_wait_ms,convert_ms,thread_cpu_ms,frame_id,width,height,format,raw_export");
        foreach (var sample in samples)
            report.AppendLine(FormattableString.Invariant($"{sample.Run},{sample.Index},{sample.FirstFrame},{sample.ActualExposureUs},{sample.ActualGain},{sample.CycleMs:F3},{sample.SetupMs:F3},{sample.CaptureMs:F3},{sample.WaitMs:F3},{sample.ConvertMs:F3},{sample.ThreadCpuMs:F3},{sample.FrameId},{sample.Width},{sample.Height},RGB48,{sample.RawExportPath != null}"));
        try { Clipboard.SetText(report.ToString()); StatusText.Text = EngineLocalization.Get("HikTest_Copied"); }
        catch (Exception ex) { ReportFailure(ex); }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!busy && cancellation == null) return;
        e.Cancel = true;
        closeWhenFinished = true;
        cancellation?.Cancel();
        StatusText.Text = EngineLocalization.Get("HikTest_Stopping");
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        AppendLog("[HikCaptureTest] Window closed.");
    }
}
