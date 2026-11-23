using ColorVision.Themes;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Diagnostics.HostBenchmark;

public partial class HostBenchmarkWindow : Window
{
    private readonly HostBenchmarkStore store = new();
    private HostBenchmarkHistory? history;
    private CancellationTokenSource? benchmarkCancellation;
    private bool busy;
    private bool savingBenchmark;
    private bool closeAfterBenchmark;
    private bool closed;

    public HostBenchmarkWindow()
    {
        InitializeComponent();
        this.ApplyCaption();
        MaxHeight = Math.Max(MinHeight, SystemParameters.WorkArea.Height);
    }

    private HostBenchmarkRun? LatestRun => history?.Runs.FirstOrDefault();
    private HostBenchmarkRun? SelectedRun => (HistoryList.SelectedItem as HistoryRow)?.Run;

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (busy || history is not null) return;
        await PerformAsync(async () =>
        {
            var loaded = await Task.Run(store.Load);
            if (closed) return;
            ShowHistory(loaded);
            StatusText.Text = string.Empty;
        }, "读取基准测试历史失败");
    }

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        using var cancellation = new CancellationTokenSource();
        benchmarkCancellation = cancellation;
        HostBenchmarkRun? savedRun = null;
        StatusText.Text = "正在测试 CPU 和内存…";
        await PerformAsync(async () =>
        {
            var run = await HostBenchmarkRunner.RunAsync(cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            savingBenchmark = true;
            UpdateControls();
            StatusText.Text = "正在保存测试结果…";
            var updated = await Task.Run(() => store.Add(run));
            savedRun = run;
            if (closed) return;
            ShowHistory(updated, run.Id);
            StatusText.Text = string.Empty;
        }, "基准测试失败", cancellation.Token);

        benchmarkCancellation = null;
        savingBenchmark = false;
        if (closed) return;
        if (closeAfterBenchmark)
        {
            Close();
            return;
        }

        UpdateControls();
        if (savedRun is not null) await OpenResultAsync(savedRun);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => CancelBenchmark();

    private async void LatestResult_Click(object sender, RoutedEventArgs e)
    {
        if (!busy && LatestRun is { } run) await OpenResultAsync(run);
    }

    private async void OpenHistoryResult_Click(object sender, RoutedEventArgs e)
    {
        if (!busy && SelectedRun is { } run) await OpenResultAsync(run);
    }

    private async void HistoryList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (busy || e.OriginalSource is not DependencyObject source ||
            ItemsControl.ContainerFromElement(HistoryList, source) is not ListBoxItem { DataContext: HistoryRow row }) return;

        e.Handled = true;
        await OpenResultAsync(row.Run);
    }

    private async void HistoryList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || busy || SelectedRun is not { } run) return;
        e.Handled = true;
        await OpenResultAsync(run);
    }

    private async Task OpenResultAsync(HostBenchmarkRun run)
    {
        try
        {
            StatusText.Text = string.Empty;
            new HostBenchmarkResultWindow(run) { Owner = this }.ShowDialog();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"无法打开测试结果。{ex.Message}";
            return;
        }

        if (closed) return;
        await PerformAsync(async () =>
        {
            var updated = await Task.Run(store.Load);
            if (!closed) ShowHistory(updated, run.Id);
        }, "读取基准测试历史失败");
    }

    private async void MakeReference_Click(object sender, RoutedEventArgs e)
    {
        if (busy || SelectedRun is not { } run) return;
        await PerformAsync(async () =>
        {
            var updated = await Task.Run(() => store.SetReference(run.Id));
            if (closed) return;
            ShowHistory(updated, run.Id);
            StatusText.Text = "已切换导出参考记录。";
        }, "设置导出参考失败");
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (busy || SelectedRun is not { } run) return;
        var dialog = new SaveFileDialog
        {
            Title = "导出基准测试结果",
            Filter = "JSON (*.json)|*.json",
            DefaultExt = ".json",
            AddExtension = true,
            FileName = $"ColorVision-HostBenchmark-{run.MeasuredAtUtc.ToLocalTime():yyyyMMdd-HHmmss}.json"
        };
        if (dialog.ShowDialog(this) != true) return;

        string path = dialog.FileName;
        await PerformAsync(async () =>
        {
            await Task.Run(() => store.Export(path, run.Id));
            if (!closed) StatusText.Text = $"结果已导出到 {path}";
        }, "导出测试结果失败");
    }

    private async Task PerformAsync(Func<Task> operation, string errorMessage, CancellationToken cancellationToken = default)
    {
        busy = true;
        UpdateControls();
        try { await operation(); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!closed) StatusText.Text = "测试已取消。";
        }
        catch (Exception ex)
        {
            if (!closed) StatusText.Text = $"{errorMessage}。{ex.Message}";
        }
        finally
        {
            busy = false;
            if (!closed) UpdateControls();
        }
    }

    private void ShowHistory(HostBenchmarkHistory updated, Guid? selectedId = null)
    {
        history = updated;
        var rows = updated.Runs.Select(run => new HistoryRow(run, run.Id == updated.ReferenceId,
            HostBenchmarkScoring.CompareToStandard(run)?.OverallScore)).ToArray();
        HistoryList.ItemsSource = rows;
        HistoryList.SelectedItem = rows.FirstOrDefault(row => row.Run.Id == selectedId) ?? rows.FirstOrDefault();

        var latest = rows.FirstOrDefault();
        LatestResultButton.Visibility = HistoryList.Visibility = HistoryActions.Visibility =
            latest is null ? Visibility.Collapsed : Visibility.Visible;
        EmptyHistoryText.Visibility = latest is null ? Visibility.Visible : Visibility.Collapsed;
        HistoryTab.Header = latest is null ? "历史成绩" : $"历史成绩（{rows.Length}）";
        if (latest is not null)
        {
            LatestResultTimeText.Text = $"{latest.MeasuredAt:MM-dd HH:mm:ss}  ·  {latest.ShareStatus}";
            LatestResultScoreText.Text = latest.ScoreText;
        }
        UpdateControls();
    }

    private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) UpdateControls();
    }

    private void UpdateControls()
    {
        bool running = busy && benchmarkCancellation is not null;
        RunButton.IsEnabled = !busy;
        RunButton.Content = running ? (savingBenchmark ? "正在保存…" : "正在测试…") : "开始测试";
        CancelButton.Visibility = running && !savingBenchmark ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.IsEnabled = running && !savingBenchmark && benchmarkCancellation is { IsCancellationRequested: false };
        RunProgress.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        LatestResultButton.IsEnabled = !busy && LatestRun is not null;
        HistoryList.IsEnabled = !busy;
        OpenHistoryResultButton.IsEnabled = !busy && SelectedRun is not null;
        MakeReferenceButton.IsEnabled = !busy && SelectedRun is { } run && run.Id != history?.ReferenceId;
        ExportButton.IsEnabled = !busy && SelectedRun is not null;
    }

    private void CancelBenchmark()
    {
        if (savingBenchmark || benchmarkCancellation is not { IsCancellationRequested: false } cancellation) return;
        cancellation.Cancel();
        StatusText.Text = "正在取消测试…";
        UpdateControls();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (benchmarkCancellation is null) return;
        e.Cancel = true;
        closeAfterBenchmark = true;
        CancelBenchmark();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        closed = true;
        benchmarkCancellation?.Cancel();
    }

    private sealed record HistoryRow(HostBenchmarkRun Run, bool IsReference, double? Score)
    {
        public DateTime MeasuredAt => Run.MeasuredAtUtc.LocalDateTime;
        public string StartupSummary =>
            $"应用启动 {Run.AppStartedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}\nWindows 启动 {Run.ApproximateSystemStartedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}（约）";
        public string ScoreText => Score is { } score ? $"{score:N1} 分" : "—";
        public string ShareStatus => Run.PublicShare is not null ? "已分享" : Run.ShareAttempted ? "分享待重试" : "未分享";
        public string Status => IsReference ? $"{ShareStatus}  ·  导出参考" : ShareStatus;
    }
}
