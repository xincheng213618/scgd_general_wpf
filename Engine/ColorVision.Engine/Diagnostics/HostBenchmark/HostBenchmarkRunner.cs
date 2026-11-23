using ColorVision.Common.NativeMethods;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Diagnostics.HostBenchmark;

internal sealed record HostBenchmarkMetric(string Id, double MedianMilliseconds, double P95Milliseconds, double[] SamplesMilliseconds);
internal sealed record HostBenchmarkPublicShare(double OverallScore, string ScoringVersion);

internal sealed record HostBenchmarkRun(
    Guid Id,
    string WorkloadVersion,
    DateTimeOffset MeasuredAtUtc,
    string MachineName,
    DateTimeOffset AppStartedAtUtc,
    DateTimeOffset ApproximateSystemStartedAtUtc,
    int LogicalProcessors,
    HostBenchmarkMetric[] Metrics)
{
    public string AppVersion { get; init; } = string.Empty;
    public string Runtime { get; init; } = string.Empty;
    public int AppProcessId { get; init; }
    public string? CpuModel { get; init; }
    public string[] GpuModels { get; init; } = [];
    public long? TotalMemoryMiB { get; init; }
    public long AvailableMemoryMiBBefore { get; init; }
    public long AvailableMemoryMiBAfter { get; init; }
    public int FrameWidth { get; init; } = HostBenchmarkWorkloads.Width;
    public int FrameHeight { get; init; } = HostBenchmarkWorkloads.Height;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ShareAttempted { get; init; }
    public HostBenchmarkPublicShare? PublicShare { get; init; }
}

internal static class HostBenchmarkRunner
{
    private const int WarmupRounds = 8;
    private const int SampleRounds = 21;
    private static readonly SemaphoreSlim Gate = new(1, 1);

    internal static async Task<HostBenchmarkRun> RunAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => Run(cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static HostBenchmarkRun Run(CancellationToken cancellationToken)
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        DateTimeOffset systemStartedAt = startedAt - TimeSpan.FromMilliseconds(Environment.TickCount64);
        using Process currentProcess = Process.GetCurrentProcess();
        long memoryBefore = PerformanceInfo.GetPhysicalAvailableMemoryInMiB();
        // The two full-size buffers use about 700 MiB. Avoid a sample dominated by paging.
        if (memoryBefore is >= 0 and < 1024)
            throw new InvalidOperationException("可用物理内存不足 1 GiB，无法可靠完成全幅图像基准测试。");

        HostBenchmarkMetric[] metrics = Measure(cancellationToken);
        long memoryAfter = PerformanceInfo.GetPhysicalAvailableMemoryInMiB();
        HostBenchmarkHardware hardware = HostBenchmarkHardware.Read(cancellationToken);
        return new HostBenchmarkRun(Guid.NewGuid(), HostBenchmarkWorkloads.Version, startedAt, Environment.MachineName,
            currentProcess.StartTime.ToUniversalTime(), systemStartedAt, Environment.ProcessorCount, metrics)
        {
            AppVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? string.Empty,
            Runtime = RuntimeInformation.FrameworkDescription,
            AppProcessId = currentProcess.Id,
            CpuModel = hardware.CpuModel,
            GpuModels = hardware.GpuModels,
            TotalMemoryMiB = hardware.TotalMemoryMiB,
            AvailableMemoryMiBBefore = memoryBefore,
            AvailableMemoryMiBAfter = memoryAfter
        };
    }

    private static HostBenchmarkMetric[] Measure(CancellationToken cancellationToken)
    {
        using var buffers = new HostBenchmarkWorkloads(cancellationToken);
        HostBenchmarkWorkload[] workloads = buffers.CreateCases();
        double[][] samples = workloads.Select(_ => new double[SampleRounds]).ToArray();
        for (int round = -WarmupRounds; round < SampleRounds; round++)
        {
            // Rotate the order to reduce systematic effects from the preceding workload.
            for (int index = 0; index < workloads.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int workloadIndex = (round + WarmupRounds + index) % workloads.Length;
                long start = Stopwatch.GetTimestamp();
                workloads[workloadIndex].Execute();
                double milliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                if (round >= 0)
                    samples[workloadIndex][round] = milliseconds;
            }
        }
        cancellationToken.ThrowIfCancellationRequested();

        return workloads.Select((workload, index) => Summarize(workload.Id, samples[index])).ToArray();
    }

    private static HostBenchmarkMetric Summarize(string id, double[] samples)
    {
        if (samples.Any(value => !double.IsFinite(value) || value <= 0))
            throw new InvalidOperationException($"基准项目 {id} 返回了无效计时。");

        double[] sorted = (double[])samples.Clone();
        Array.Sort(sorted);
        double median = (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2;
        double p95 = sorted[(int)Math.Ceiling(sorted.Length * 0.95) - 1];
        return new HostBenchmarkMetric(id, median, p95, samples);
    }
}

internal sealed record HostBenchmarkMetricComparison(string Id, double ReferenceMilliseconds, double Score, double TimeRatio);

internal sealed record HostBenchmarkComparison(double OverallScore, HostBenchmarkMetricComparison[] Metrics);

internal static class HostBenchmarkScoring
{
    internal const string StandardVersion = "cv-host-score-v1";
    private static readonly HostBenchmarkMetric[] StandardMetrics =
    [
        new("memory-copy", 16.45815, 0, []),
        new("channel-swap", 25.3534, 0, []),
        new("bayer-rg16", 17.43975, 0, []),
        new("pixel-compute", 23.73725, 0, [])
    ];

    internal static HostBenchmarkComparison? CompareToStandard(HostBenchmarkRun run)
    {
        if (run.WorkloadVersion != HostBenchmarkWorkloads.Version
            || run.FrameWidth != HostBenchmarkWorkloads.Width || run.FrameHeight != HostBenchmarkWorkloads.Height)
            return null;

        return Compare(run, run with { Metrics = StandardMetrics });
    }

    internal static HostBenchmarkComparison? Compare(HostBenchmarkRun run, HostBenchmarkRun? reference)
    {
        if (reference is null || !string.Equals(run.WorkloadVersion, reference.WorkloadVersion, StringComparison.Ordinal)
            || run.FrameWidth != reference.FrameWidth || run.FrameHeight != reference.FrameHeight
            || run.Metrics.Length == 0 || run.Metrics.Length != reference.Metrics.Length)
            return null;

        var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        var comparisons = new HostBenchmarkMetricComparison[run.Metrics.Length];
        for (int index = 0; index < run.Metrics.Length; index++)
        {
            HostBenchmarkMetric metric = run.Metrics[index];
            HostBenchmarkMetric[] candidates = reference.Metrics.Where(item => item.Id == metric.Id).ToArray();
            if (!seen.Add(metric.Id) || candidates.Length != 1 || !IsValidTiming(metric.MedianMilliseconds)
                || !IsValidTiming(candidates[0].MedianMilliseconds))
                return null;

            double referenceMilliseconds = candidates[0].MedianMilliseconds;
            double ratio = metric.MedianMilliseconds / referenceMilliseconds;
            double score = 100 / ratio;
            if (!IsValidTiming(ratio) || !IsValidTiming(score))
                return null;
            comparisons[index] = new HostBenchmarkMetricComparison(metric.Id, referenceMilliseconds, score, ratio);
        }

        double overall = Math.Exp(comparisons.Average(item => Math.Log(item.Score)));
        return new HostBenchmarkComparison(overall, comparisons);
    }

    private static bool IsValidTiming(double value) => double.IsFinite(value) && value > 0;
}
