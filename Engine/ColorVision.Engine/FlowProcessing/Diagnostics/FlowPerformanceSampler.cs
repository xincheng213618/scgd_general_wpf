using ColorVision.FileIO;
using log4net;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace ColorVision.Engine.FlowProcessing.Diagnostics;

/// <summary>Queues background samples on flow completion, at most once per 30 seconds; no polling task or timer.</summary>
internal sealed class FlowPerformanceSampler(Func<FlowPerformanceReading> read, TimeProvider clock)
{
    private static readonly ILog log = LogManager.GetLogger(typeof(FlowPerformanceSampler));
    private static readonly FlowPerformanceSampler Shared = new(ReadCurrent, TimeProvider.System);
    private static readonly FlowPerformanceSamplingWorker Worker = new(Shared, TimeProvider.System, WriteSample,
        static callback => ThreadPool.UnsafeQueueUserWorkItem(static action => action(), callback, preferLocal: false));
    private readonly object gate = new();
    private bool attempted;
    private long attemptedAt;
    private long previousAt;
    private FlowPerformanceReading? previous;

    internal static void QueueIfDue(string serialNumber, string? flowName, int? batchId, string? startNodeName)
    {
        // Counter queries, JSON formatting and appenders all run outside the completion callback.
        if (log.IsInfoEnabled) Worker.TryQueue(serialNumber, flowName, batchId, startNodeName);
    }

    private static void WriteSample(FlowPerformanceSample sample)
    {
        if (!log.IsInfoEnabled) return;
        try
        {
            log.Info(JsonConvert.SerializeObject(new
            {
                Event = "FlowPerformanceSample", sample.Request.SerialNumber, sample.Request.FlowName,
                sample.Request.BatchId, sample.Request.StartNodeName, ProcessId = Environment.ProcessId,
                RequestedAt = sample.Request.RequestedAt, sample.SampledAt, sample.QueueDelayMs, sample.Metrics,
            }));
        }
        catch (Exception ex) { log.Debug("Writing flow performance sample failed.", ex); }
    }

    internal bool TrySample(out FlowPerformanceMetrics? metrics)
    {
        metrics = null;
        // Diagnostics must not queue competing flow completions behind a process query.
        if (!Monitor.TryEnter(gate)) return false;
        try
        {
            long now = clock.GetTimestamp();
            if (attempted && clock.GetElapsedTime(attemptedAt, now) < TimeSpan.FromSeconds(30)) return false;
            attempted = true;
            attemptedAt = now;
            long readStarted = Stopwatch.GetTimestamp();
            FlowPerformanceReading current = read();
            double? seconds = previous == null ? null : clock.GetElapsedTime(previousAt, now).TotalSeconds;
            metrics = FlowPerformanceMetrics.Create(current, previous, seconds,
                Stopwatch.GetElapsedTime(readStarted).TotalMilliseconds);
            previous = current;
            previousAt = now;
            return true;
        }
        catch (Exception ex)
        {
            log.Debug("Reading flow performance sample failed; execution continues.", ex);
            return false;
        }
        finally { Monitor.Exit(gate); }
    }

    internal static FlowPerformanceReading ReadCurrent()
    {
        using Process process = Process.GetCurrentProcess();
        process.Refresh();
        ThreadPool.GetAvailableThreads(out int workers, out int ioThreads);
        GCMemoryInfo gc = GC.GetGCMemoryInfo();
        IoCounters? io = GetProcessIoCounters(process.Handle, out IoCounters counters) ? counters : null;
        SystemCpuTimes? system = GetSystemTimes(out ulong idle, out ulong kernel, out ulong user)
            ? new(idle, kernel, user) : null;
        MemoryStatus memory = new() { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        bool hasMemoryStatus = GlobalMemoryStatusEx(ref memory);
        bool hasProcessMemory = GetProcessMemoryInfo(process.Handle, out ProcessMemoryCounters processMemory, (uint)Marshal.SizeOf<ProcessMemoryCounters>());
        CVFileReadCacheSnapshot rawCache = CVFileReadCache.GetSnapshot();
        return new()
        {
            ProcessorCount = Environment.ProcessorCount, ProcessCpuTicks = process.TotalProcessorTime.Ticks,
            SystemCpu = system, IoReadBytes = io?.ReadTransferCount, IoWriteBytes = io?.WriteTransferCount,
            WorkingSetBytes = process.WorkingSet64, PrivateBytes = process.PrivateMemorySize64,
            ManagedHeapBytes = GC.GetTotalMemory(false), GcCommittedBytes = gc.TotalCommittedBytes,
            AllocatedBytes = GC.GetTotalAllocatedBytes(false), GcPausePercent = gc.PauseTimePercentage,
            GcPauseTicks = GC.GetTotalPauseDuration().Ticks, GcFragmentedBytes = gc.FragmentedBytes,
            AvailablePhysicalBytes = hasMemoryStatus ? memory.AvailablePhysical : null,
            PhysicalMemoryLoadPercent = hasMemoryStatus ? memory.MemoryLoad : null,
            PageFaultCount = hasProcessMemory ? processMemory.PageFaultCount : null,
            CvRawCacheBytes = rawCache.CapacityBytes, CvRawCacheEntries = rawCache.Entries.Count,
            CvRawCacheReaders = rawCache.ActiveReaders,
            Gen0 = GC.CollectionCount(0), Gen1 = GC.CollectionCount(1), Gen2 = GC.CollectionCount(2),
            ThreadPoolThreads = ThreadPool.ThreadCount, AvailableWorkers = workers,
            AvailableIoThreads = ioThreads, PendingWorkItems = ThreadPool.PendingWorkItemCount,
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile;
        public ulong TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessMemoryCounters
    {
        public uint Size, PageFaultCount;
        public nuint PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage;
        public nuint QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage;
    }

    [DllImport("psapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessMemoryInfo(IntPtr process, out ProcessMemoryCounters counters, uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessIoCounters(IntPtr process, out IoCounters counters);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
}

/// <summary>Allows one queued or running job, including its log write; busy completions are dropped.</summary>
internal sealed class FlowPerformanceSamplingWorker
{
    private static readonly ILog log = LogManager.GetLogger(typeof(FlowPerformanceSamplingWorker));
    private readonly FlowPerformanceSampler sampler;
    private readonly TimeProvider clock;
    private readonly Action<FlowPerformanceSample> write;
    private readonly Func<Action, bool> schedule;
    private readonly Action run;
    private readonly object gate = new();
    private bool busy;
    private bool attempted;
    private long attemptedAt;
    private FlowPerformanceSampleRequest? pending;

    internal FlowPerformanceSamplingWorker(FlowPerformanceSampler sampler, TimeProvider clock,
        Action<FlowPerformanceSample> write, Func<Action, bool> schedule)
    {
        this.sampler = sampler;
        this.clock = clock;
        this.write = write;
        this.schedule = schedule;
        run = Run;
    }

    internal bool TryQueue(string serialNumber, string? flowName, int? batchId, string? startNodeName)
    {
        if (!Monitor.TryEnter(gate)) return false;
        try
        {
            if (busy) return false;
            long now = clock.GetTimestamp();
            if (attempted && clock.GetElapsedTime(attemptedAt, now) < TimeSpan.FromSeconds(30)) return false;
            attempted = true;
            attemptedAt = now;
            busy = true;
            pending = new(serialNumber, flowName, batchId, startNodeName, clock.GetLocalNow(), now);
            if (schedule(run)) return true;
            pending = null;
            busy = false;
            return false;
        }
        catch
        {
            // A diagnostic scheduling failure must not enter synchronous logging on this caller.
            pending = null;
            busy = false;
            return false;
        }
        finally { Monitor.Exit(gate); }
    }

    private void Run()
    {
        try
        {
            FlowPerformanceSampleRequest request;
            long startedAt;
            lock (gate)
            {
                request = pending!;
                pending = null;
                // Keep the interval tied to real execution even when the thread pool was delayed.
                startedAt = clock.GetTimestamp();
                attemptedAt = startedAt;
            }
            DateTimeOffset sampledAt = clock.GetLocalNow();
            if (sampler.TrySample(out FlowPerformanceMetrics? metrics))
                write(new(request, sampledAt, Math.Round(clock.GetElapsedTime(request.RequestedTimestamp, startedAt).TotalMilliseconds, 3), metrics!));
        }
        catch (Exception ex) { log.Debug("Background flow performance sampling failed; execution continues.", ex); }
        finally
        {
            lock (gate) busy = false;
        }
    }
}

internal sealed record FlowPerformanceSampleRequest(string SerialNumber, string? FlowName, int? BatchId,
    string? StartNodeName, DateTimeOffset RequestedAt, long RequestedTimestamp);

internal sealed record FlowPerformanceSample(FlowPerformanceSampleRequest Request, DateTimeOffset SampledAt,
    double QueueDelayMs, FlowPerformanceMetrics Metrics);

internal sealed record SystemCpuTimes(ulong Idle, ulong Kernel, ulong User);

internal sealed record FlowPerformanceReading
{
    public int ProcessorCount { get; init; }
    public long ProcessCpuTicks { get; init; }
    public SystemCpuTimes? SystemCpu { get; init; }
    public ulong? IoReadBytes { get; init; }
    public ulong? IoWriteBytes { get; init; }
    public long WorkingSetBytes { get; init; }
    public long PrivateBytes { get; init; }
    public long ManagedHeapBytes { get; init; }
    public long GcCommittedBytes { get; init; }
    public long AllocatedBytes { get; init; }
    public double GcPausePercent { get; init; }
    public long? GcPauseTicks { get; init; }
    public long GcFragmentedBytes { get; init; }
    public ulong? AvailablePhysicalBytes { get; init; }
    public uint? PhysicalMemoryLoadPercent { get; init; }
    public uint? PageFaultCount { get; init; }
    public long CvRawCacheBytes { get; init; }
    public int CvRawCacheEntries { get; init; }
    public int CvRawCacheReaders { get; init; }
    public int Gen0 { get; init; }
    public int Gen1 { get; init; }
    public int Gen2 { get; init; }
    public int ThreadPoolThreads { get; init; }
    public int AvailableWorkers { get; init; }
    public int AvailableIoThreads { get; init; }
    public long PendingWorkItems { get; init; }
}

internal sealed record FlowPerformanceMetrics
{
    public double? WindowSeconds { get; init; }
    public int ProcessorCount { get; init; }
    public double? ProcessCpuPercent { get; init; }
    public double? SystemCpuPercent { get; init; }
    public double? ProcessIoReadMiBPerSecond { get; init; }
    public double? ProcessIoWriteMiBPerSecond { get; init; }
    public double WorkingSetMiB { get; init; }
    public double PrivateMiB { get; init; }
    public double ManagedHeapMiB { get; init; }
    public double GcCommittedMiB { get; init; }
    public double? AllocatedMiBPerSecond { get; init; }
    public int? Gen0Collections { get; init; }
    public int? Gen1Collections { get; init; }
    public int? Gen2Collections { get; init; }
    public double GcPausePercent { get; init; }
    public double? WindowGcPauseMs { get; init; }
    public double? WindowGcPausePercent { get; init; }
    public double GcFragmentedMiB { get; init; }
    public double? AvailablePhysicalMiB { get; init; }
    public uint? PhysicalMemoryLoadPercent { get; init; }
    public double? ProcessPageFaultsPerSecond { get; init; }
    public double CvRawCacheMiB { get; init; }
    public int CvRawCacheEntries { get; init; }
    public int CvRawCacheReaders { get; init; }
    public int ThreadPoolThreads { get; init; }
    public int AvailableWorkers { get; init; }
    public int AvailableIoThreads { get; init; }
    public long PendingWorkItems { get; init; }
    public double SamplingMs { get; init; }

    internal static FlowPerformanceMetrics Create(FlowPerformanceReading current, FlowPerformanceReading? previous, double? seconds, double samplingMs)
    {
        bool window = previous != null && seconds > 0;
        double? gcPauseMs = window && current.GcPauseTicks.HasValue && previous!.GcPauseTicks.HasValue
            && current.GcPauseTicks >= previous.GcPauseTicks
            ? (current.GcPauseTicks.Value - previous.GcPauseTicks.Value) / (double)TimeSpan.TicksPerMillisecond : null;
        double? systemCpu = null;
        if (window && current.SystemCpu is { } end && previous!.SystemCpu is { } start
            && end.Idle >= start.Idle && end.Kernel >= start.Kernel && end.User >= start.User)
        {
            double total = (end.Kernel - start.Kernel) + (double)(end.User - start.User);
            if (total > 0) systemCpu = Math.Round(Math.Clamp((total - (end.Idle - start.Idle)) * 100 / total, 0, 100), 3);
        }
        double? IoRate(ulong? end, ulong? start) => window && end.HasValue && start.HasValue && end >= start
            ? Math.Round((end.Value - start.Value) / seconds!.Value / 1048576d, 3) : null;
        return new()
        {
            WindowSeconds = window ? Math.Round(seconds!.Value, 3) : null, ProcessorCount = current.ProcessorCount,
            ProcessCpuPercent = window && current.ProcessCpuTicks >= previous!.ProcessCpuTicks
                ? Math.Round(Math.Clamp((current.ProcessCpuTicks - previous.ProcessCpuTicks) / (double)TimeSpan.TicksPerSecond
                    / seconds!.Value / Math.Max(1, current.ProcessorCount) * 100, 0, 100), 3) : null,
            SystemCpuPercent = systemCpu,
            ProcessIoReadMiBPerSecond = IoRate(current.IoReadBytes, previous?.IoReadBytes),
            ProcessIoWriteMiBPerSecond = IoRate(current.IoWriteBytes, previous?.IoWriteBytes),
            WorkingSetMiB = Math.Round(current.WorkingSetBytes / 1048576d, 3),
            PrivateMiB = Math.Round(current.PrivateBytes / 1048576d, 3),
            ManagedHeapMiB = Math.Round(current.ManagedHeapBytes / 1048576d, 3),
            GcCommittedMiB = Math.Round(current.GcCommittedBytes / 1048576d, 3),
            AllocatedMiBPerSecond = window && current.AllocatedBytes >= previous!.AllocatedBytes
                ? Math.Round((current.AllocatedBytes - previous.AllocatedBytes) / seconds!.Value / 1048576d, 3) : null,
            Gen0Collections = window ? Math.Max(0, current.Gen0 - previous!.Gen0) : null,
            Gen1Collections = window ? Math.Max(0, current.Gen1 - previous!.Gen1) : null,
            Gen2Collections = window ? Math.Max(0, current.Gen2 - previous!.Gen2) : null,
            GcPausePercent = Math.Round(current.GcPausePercent, 3), ThreadPoolThreads = current.ThreadPoolThreads,
            WindowGcPauseMs = gcPauseMs.HasValue ? Math.Round(gcPauseMs.Value, 3) : null,
            WindowGcPausePercent = gcPauseMs.HasValue ? Math.Round(gcPauseMs.Value / (seconds!.Value * 10), 3) : null,
            GcFragmentedMiB = Math.Round(current.GcFragmentedBytes / 1048576d, 3),
            AvailablePhysicalMiB = current.AvailablePhysicalBytes.HasValue
                ? Math.Round(current.AvailablePhysicalBytes.Value / 1048576d, 3) : null,
            PhysicalMemoryLoadPercent = current.PhysicalMemoryLoadPercent,
            ProcessPageFaultsPerSecond = window && current.PageFaultCount.HasValue && previous!.PageFaultCount.HasValue
                && current.PageFaultCount >= previous.PageFaultCount
                ? Math.Round((current.PageFaultCount.Value - previous.PageFaultCount.Value) / seconds!.Value, 3) : null,
            CvRawCacheMiB = Math.Round(current.CvRawCacheBytes / 1048576d, 3),
            CvRawCacheEntries = current.CvRawCacheEntries, CvRawCacheReaders = current.CvRawCacheReaders,
            AvailableWorkers = current.AvailableWorkers, AvailableIoThreads = current.AvailableIoThreads,
            PendingWorkItems = current.PendingWorkItems, SamplingMs = Math.Round(samplingMs, 3),
        };
    }
}
