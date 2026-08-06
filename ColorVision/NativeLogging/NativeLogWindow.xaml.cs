using ColorVision.Themes;
using ColorVision.UI.LogImp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.NativeLogging;

public partial class NativeLogWindow : Window, IDisposable
{
    private const int PendingCapacity = 8192;
    private const int MaxDrainBatchSize = 512;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(100);

    private readonly NativeLogWindowSession _session;
    private readonly DispatcherTimer _flushTimer;
    private string? _lastOperationStatus;
    private bool _isLoaded;

    public NativeLogWindow()
        : this(new NativeLogCaptureController())
    {
    }

    internal NativeLogWindow(INativeLogCaptureController controller)
    {
        _session = new NativeLogWindowSession(controller, PendingCapacity);
        _flushTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = FlushInterval,
        };
        _flushTimer.Tick += FlushTimer_Tick;

        InitializeComponent();
        this.ApplyCaption();
        Title = NativeLogText.Title;
        LevelComboBox.ItemsSource = Enum.GetValues<NativeLogSeverity>();
        LevelComboBox.SelectedItem = NativeLogSeverity.Info;
        UpdateControls();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = true;
        _flushTimer.Start();
        UpdateControls();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        Dispose();
    }

    public void Dispose()
    {
        _flushTimer.Stop();
        _flushTimer.Tick -= FlushTimer_Tick;
        _session.Dispose();
        GC.SuppressFinalize(this);
    }

    private void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session.IsCapturing)
        {
            _session.Stop();
            _lastOperationStatus = null;
        }
        else
        {
            NativeLogOperationResult result = _session.Start(GetSelectedLevel());
            _lastOperationStatus = result.Success
                ? result.Message
                : $"{NativeLogText.StartFailed}: {result.Message}";
        }

        UpdateControls();
    }

    private void PauseToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _session.IsPaused = PauseToggleButton.IsChecked == true;
        UpdateControls();
    }

    private void LevelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || !_session.IsCapturing)
        {
            return;
        }

        NativeLogOperationResult result = _session.SetLevel(GetSelectedLevel());
        _lastOperationStatus = result.Success
            ? result.Message
            : $"{NativeLogText.LevelFailed}: {result.Message}";
        UpdateControls();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _session.Clear();
        LogViewer.Clear();
        _lastOperationStatus = null;
        UpdateStatus();
    }

    private void FlushTimer_Tick(object? sender, EventArgs e)
    {
        NativeLogDrainBatch batch = _session.Drain(MaxDrainBatchSize);
        if (batch.Entries.Count > 0)
        {
            List<LogEntry> entries = batch.Entries.Select(entry => entry.ToLogEntry()).ToList();
            LogViewer.AppendEntries(entries, latestAtTop: false, autoScroll: AutoScrollCheckBox.IsChecked == true);
        }

        UpdateStatus(batch.RemainingCount, batch.DroppedCount);
    }

    private NativeLogSeverity GetSelectedLevel()
    {
        return LevelComboBox.SelectedItem is NativeLogSeverity level
            ? level
            : NativeLogSeverity.Info;
    }

    private void UpdateControls()
    {
        bool isCapturing = _session.IsCapturing;
        StartStopButton.Content = isCapturing ? NativeLogText.Stop : NativeLogText.Start;
        PauseToggleButton.Content = _session.IsPaused ? NativeLogText.Resume : NativeLogText.Pause;
        PauseToggleButton.IsEnabled = isCapturing;
        LevelComboBox.IsEnabled = true;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        NativeLogBufferSnapshot snapshot = _session.GetBufferSnapshot();
        UpdateStatus(snapshot.PendingCount, snapshot.DroppedCount);
    }

    private void UpdateStatus(int pendingCount, long droppedCount)
    {
        string state = !_session.IsCapturing
            ? NativeLogText.Off
            : _session.IsPaused
                ? NativeLogText.Paused
                : NativeLogText.Capturing;
        string counts = $"{NativeLogText.Pending}: {pendingCount:N0}  |  {NativeLogText.Dropped}: {droppedCount:N0}";
        StatusTextBlock.Text = string.IsNullOrWhiteSpace(_lastOperationStatus)
            ? $"{state}  |  {counts}"
            : $"{state}  |  {counts}  |  {_lastOperationStatus}";
    }
}
