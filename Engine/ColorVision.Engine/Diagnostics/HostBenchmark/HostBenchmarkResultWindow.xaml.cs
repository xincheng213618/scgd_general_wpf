using ColorVision.Themes;
using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.Engine.Diagnostics.HostBenchmark;

public partial class HostBenchmarkResultWindow : Window
{
    private const double StandardScore = 100;
    private const int MinimumOtherComputers = 3;
    private static readonly ILog Log = LogManager.GetLogger(typeof(HostBenchmarkResultWindow));

    private readonly HostBenchmarkRun run;
    private readonly HostBenchmarkStore store = new();
    private readonly HostBenchmarkSharing sharing = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly DispatcherTimer refreshTimer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(15) };
    private HostBenchmarkShareProfile profile = HostBenchmarkSharing.DefaultProfile;
    private HostBenchmarkComparisonSnapshot? lastComparison;
    private CancellationTokenSource? comparisonRequest;
    private DateTimeOffset lastRefreshAt;
    private double? score;
    private bool profileLoaded;
    private bool closed;
    private bool saving;
    private bool shared;
    private bool shareAttempted;
    private bool openPageFailed;

    internal HostBenchmarkResultWindow(HostBenchmarkRun run)
    {
        this.run = run;
        InitializeComponent();
        this.ApplyCaption();
        MaxHeight = Math.Max(MinHeight, SystemParameters.WorkArea.Height);

        score = HostBenchmarkScoring.CompareToStandard(run)?.OverallScore;
        shareAttempted = run.ShareAttempted;
        if (run.PublicShare is { } receipt)
        {
            shared = true;
            if (receipt.ScoringVersion == HostBenchmarkScoring.StandardVersion)
                score = receipt.OverallScore;
            ShareStatusText.Text = "成绩已公开分享。";
        }
        else if (shareAttempted)
        {
            ShareStatusText.Text = "成绩已保存在本机，可以重试分享。";
        }
        UpdateScore();
        ShowComparison(null);
        ShowHardware();
        RunDetailsText.Text = $"测试 {run.MeasuredAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}   ·   应用启动 {run.AppStartedAtUtc.ToLocalTime():HH:mm:ss}   ·   Windows 启动 {run.ApproximateSystemStartedAtUtc.ToLocalTime():MM-dd HH:mm:ss}（约）";
        MetricItems.ItemsSource = run.Metrics.Select(metric => new MetricRow(
            MetricName(metric.Id), $"中位数 {metric.MedianMilliseconds:N2} ms", $"P95 {metric.P95Milliseconds:N2} ms")).ToArray();
        UpdateShareAction();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (profileLoaded) return;
        try
        {
            profile = await Task.Run(sharing.Load, lifetime.Token);
        }
        catch (HostBenchmarkShareException ex)
        {
            if (!closed) ShareStatusText.Text = ex.Message;
        }
        catch (OperationCanceledException) when (closed) { }
        if (closed) return;

        profileLoaded = true;
        UpdateShareAction();
        refreshTimer.Tick += RefreshTimer_Tick;
        refreshTimer.Start();
        await RefreshComparisonAsync();
    }

    private async void Window_Activated(object? sender, EventArgs e)
    {
        if (DateTimeOffset.UtcNow - lastRefreshAt >= TimeSpan.FromSeconds(3))
            await RefreshComparisonAsync();
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (IsVisible && WindowState != WindowState.Minimized)
            await RefreshComparisonAsync();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        closed = true;
        refreshTimer.Stop();
        refreshTimer.Tick -= RefreshTimer_Tick;
        lifetime.Cancel();
        lifetime.Dispose();
    }

    private void ShowHardware()
    {
        CpuText.Text = string.IsNullOrWhiteSpace(run.CpuModel) ? $"CPU · {run.LogicalProcessors} 个逻辑处理器" : run.CpuModel;
        string[] adapters = run.GpuModels ?? [];
        string[] physicalAdapters = adapters.Where(name => !name.Contains("OrayIdd", StringComparison.OrdinalIgnoreCase)).ToArray();
        string gpu = string.Join(Environment.NewLine, physicalAdapters.Length > 0 ? physicalAdapters : adapters);
        GpuText.Text = gpu;
        GpuPanel.Visibility = gpu.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        MemoryText.Text = run.TotalMemoryMiB is > 0 ? $"内存  {run.TotalMemoryMiB / 1024.0:N1} GiB" : string.Empty;
        MemoryText.Visibility = MemoryText.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateScore()
    {
        ScoreText.Text = score?.ToString("N1") ?? "—";
        ScoreUnit.Visibility = score.HasValue ? Visibility.Visible : Visibility.Collapsed;
        ScoreSummaryText.Text = score switch
        {
            null => "这条历史记录无法按当前标准计分",
            > 100.05 => $"比标准参考高 {score.Value - StandardScore:N1}%",
            < 99.95 => $"比标准参考低 {StandardScore - score.Value:N1}%",
            _ => "与标准参考持平"
        };
    }

    private void ShowComparison(HostBenchmarkComparisonSnapshot? comparison)
    {
        var rows = new List<ComparisonRow>();
        if (score is { } localScore)
            rows.Add(new ComparisonRow("本机", null, null, localScore, IsLocal: true));
        rows.Add(new ComparisonRow("标准参考", null, comparison?.ReferenceName, StandardScore, IsStandard: true));

        if (comparison is not null)
        {
            double target = score ?? StandardScore;
            ComparisonRow[] actual = comparison.References
                .Select(item => CreateComparisonRow(item, isExample: false))
                .Where(item => item.Score is > 0 and <= 2000)
                .OrderBy(item => Math.Abs(Math.Log(item.Score / target))).Take(4).ToArray();
            rows.AddRange(actual);
            int missing = Math.Max(0, MinimumOtherComputers - actual.Length);
            rows.AddRange((comparison.ExampleReferences ?? []).Where(item => item.OverallScore is > 0 and <= 2000)
                .Take(missing).Select(item => CreateComparisonRow(item, isExample: true)));
        }

        double scale = rows.Max(item => item.Score) * 1.07;
        ComparisonItems.ItemsSource = rows.Select(item => item with { ScaleMaximum = scale }).ToArray();
    }

    private static ComparisonRow CreateComparisonRow(HostBenchmarkPublicReference reference, bool isExample)
    {
        string[] graphics = reference.GpuModels ?? [];
        string? memory = reference.TotalMemoryMiB is > 0 ? $"内存 {reference.TotalMemoryMiB / 1024.0:N1} GiB" : null;
        var hardwareParts = new[] { isExample ? "示例" : null, graphics.FirstOrDefault(), memory };
        string hardware = string.Join(" · ", hardwareParts.Where(part => !string.IsNullOrWhiteSpace(part)));
        string description = string.Join(" · ", new[] { reference.CpuModel, string.Join(" / ", graphics), memory }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
        string label = string.IsNullOrWhiteSpace(reference.CpuModel) ? reference.Label : reference.CpuModel;
        double value = reference.LatestScore ?? reference.OverallScore;
        double? delta = !isExample && reference.PreviousScore is { } previous ? value - previous : null;
        return new ComparisonRow(label, hardware.Length == 0 ? null : hardware, description, value, IsExample: isExample)
        {
            Trend = delta is null ? null : Math.Abs(delta.Value) < 0.05 ? "持平" : $"{delta.Value:+0.0;-0.0} 分",
            Decreased = delta is < -0.05
        };
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshComparisonAsync(force: true);

    private async Task RefreshComparisonAsync(bool force = false)
    {
        if (closed || !profileLoaded || (comparisonRequest is not null && !force)) return;
        comparisonRequest?.Cancel();
        using var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        comparisonRequest = request;
        lastRefreshAt = DateTimeOffset.UtcNow;
        RefreshButton.IsEnabled = false;
        if (lastComparison is null) ComparisonStatusText.Text = "正在读取公开成绩…";
        try
        {
            var snapshot = await sharing.LoadComparisonAsync(profile, request.Token);
            if (request.IsCancellationRequested) return;
            if (snapshot.WorkloadVersion != run.WorkloadVersion ||
                snapshot.ScoringVersion != HostBenchmarkScoring.StandardVersion || snapshot.ReferenceScore != StandardScore)
            {
                lastComparison = null;
                ShowComparison(null);
                ComparisonStatusText.Text = "榜单采用了不同的基准版本。";
                return;
            }

            lastComparison = snapshot;
            ShowComparison(snapshot);
            ComparisonStatusText.Text = snapshot.References.Length == 0
                ? "等待其他电脑分享。带有「示例」标记的条目仅用于演示。"
                : $"公开成绩 · {DateTime.Now:HH:mm:ss} 更新，有两次成绩时显示分差";
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (HostBenchmarkShareException)
        {
            if (!request.IsCancellationRequested)
                ComparisonStatusText.Text = lastComparison is null ? "暂时无法读取公开成绩。" : "暂时无法刷新，正在显示上次读取的成绩。";
        }
        finally
        {
            if (ReferenceEquals(comparisonRequest, request))
            {
                comparisonRequest = null;
                if (!closed) RefreshButton.IsEnabled = true;
            }
        }
    }

    private async void Share_Click(object sender, RoutedEventArgs e)
    {
        if (closed || !profileLoaded || saving) return;
        if (shared)
        {
            OpenSharedComparison();
            UpdateShareAction();
            return;
        }
        if (!score.HasValue) return;

        saving = true;
        UpdateShareAction();
        ShareStatusText.Text = shareAttempted ? "正在重新连接分享服务…" : "正在分享…";
        CancellationToken token = lifetime.Token;
        try
        {
            var submissionProfile = await Task.Run(() =>
            {
                RememberShareAttempt();
                token.ThrowIfCancellationRequested();
                return sharing.Load();
            }, token);
            HostBenchmarkShareResult result = await sharing.SubmitAsync(submissionProfile, run, token);
            if (!result.PublishedComparison)
            {
                if (!closed) ShareStatusText.Text = "成绩已提交，公开对比暂不可用。";
                return;
            }

            shared = true;
            try
            {
                await Task.Run(() => store.MarkPublicShare(run.Id, new(result.OverallScore, result.ScoringVersion)));
            }
            finally
            {
                if (!closed)
                {
                    profile = submissionProfile;
                    if (result.ScoringVersion == HostBenchmarkScoring.StandardVersion)
                        score = result.OverallScore;
                    UpdateScore();
                    ShowComparison(lastComparison);
                    OpenSharedComparison();
                }
            }
            if (closed) return;
            profile = await Task.Run(sharing.Load, token);
            await RefreshComparisonAsync(force: true);
        }
        catch (OperationCanceledException) when (closed) { }
        catch (HostBenchmarkShareException ex)
        {
            if (!closed && !shared) ShareStatusText.Text = $"成绩已保存在本机。{ex.Message}";
        }
        catch (Exception ex)
        {
            Log.Warn("Could not finish updating the benchmark result after sharing.", ex);
            if (!closed)
                ShareStatusText.Text = shared ? "已分享，本机暂时无法记录分享状态。" : "分享暂未成功，成绩已保存在本机。";
        }
        finally
        {
            saving = false;
            shareAttempted = true;
            if (!closed) UpdateShareAction();
        }
    }

    private void RememberShareAttempt()
    {
        try { store.MarkShareAttempted(run.Id); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException)
        {
            Log.Warn("Could not record the benchmark share attempt locally.", ex);
        }
    }

    private void OpenSharedComparison()
    {
        Uri? uri = HostBenchmarkSharing.GetComparisonPageUri(profile, run.Id);
        if (uri is not null)
        {
            try
            {
                using var browser = Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                openPageFailed = false;
                ShareStatusText.Text = "已分享，正在打开网页对比。";
                return;
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                Log.Warn("Could not open the public benchmark comparison page.", ex);
            }
        }

        openPageFailed = true;
        ShareStatusText.Text = "已分享。可重试打开对比网页。";
    }

    private void Details_Click(object sender, RoutedEventArgs e)
    {
        bool expand = DetailsPanel.Visibility != Visibility.Visible;
        DetailsPanel.Visibility = expand ? Visibility.Visible : Visibility.Collapsed;
        DetailsButton.Content = expand ? "测试详情 ▴" : "测试详情 ▾";
    }

    private void UpdateShareAction()
    {
        bool showRetry = shared ? openPageFailed : shareAttempted;
        bool actionEnabled = profileLoaded && !saving && (shared || score.HasValue);
        ShareButton.Visibility = showRetry ? Visibility.Collapsed : Visibility.Visible;
        ShareButton.IsEnabled = actionEnabled;
        ShareButtonText.Text = shared ? "打开对比网页" : saving ? "正在分享…" : "分享成绩";
        RetryShareButton.Visibility = showRetry ? Visibility.Visible : Visibility.Collapsed;
        RetryShareButton.IsEnabled = actionEnabled;
        RetryShareButtonText.Text = shared ? "重试打开网页" : saving ? "正在重试…" : "重试分享";
    }

    private static string MetricName(string id) => id switch
    {
        "memory-copy" => "整帧内存复制",
        "channel-swap" => "16 位红蓝通道交换",
        "bayer-rg16" => "BayerRG16 转换（合成输入）",
        "pixel-compute" => "全幅像素计算",
        _ => id
    };

    private sealed record ComparisonRow(string Label, string? Hardware, string? Description, double Score,
        bool IsLocal = false, bool IsStandard = false, bool IsExample = false)
    {
        public double ScaleMaximum { get; init; }
        public string? Trend { get; init; }
        public bool Decreased { get; init; }
    }

    private sealed record MetricRow(string Name, string Median, string P95);
}
