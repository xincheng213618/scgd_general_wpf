using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision;

/// <summary>Opt-in, bounded Dispatcher observations from main-window factory entry to first render.</summary>
internal sealed class StartupUiTrace
{
    private const int MaximumOperations = 2048;
    private readonly object sync = new();
    private readonly DispatcherHooks hooks;
    private readonly Dictionary<DispatcherOperation, OperationRecord> operations = new();
    private readonly long startedAt = Stopwatch.GetTimestamp();
    private Window? window;
    private long? factoryReturnedAt;
    private long? showReturnedAt;
    private long? loadedAt;
    private long? capturedAt;
    private int droppedHookEvents;
    private bool loadedBeforeObserve;
    private bool stopped;
    private bool reported;

    internal static StartupUiTrace? Current { get; private set; }

    private StartupUiTrace(Dispatcher dispatcher) => hooks = dispatcher.Hooks;

    internal static StartupUiTrace? Start(Dispatcher dispatcher)
    {
        if (Environment.GetEnvironmentVariable("COLORVISION_STARTUP_TRACE") != "1")
            return null;

        Current?.Abort();
        StartupUiTrace? trace = null;
        try
        {
            trace = new StartupUiTrace(dispatcher);
            trace.hooks.OperationPosted += trace.OnOperationPosted;
            trace.hooks.OperationStarted += trace.OnOperationStarted;
            trace.hooks.OperationCompleted += trace.OnOperationCompleted;
            trace.hooks.OperationAborted += trace.OnOperationAborted;
            Current = trace;
            return trace;
        }
        catch
        {
            trace?.Abort();
            return null;
        }
    }

    internal void Observe(Window observedWindow)
    {
        lock (sync)
        {
            if (stopped)
                return;
            factoryReturnedAt = Stopwatch.GetTimestamp();
            window = observedWindow;
            loadedBeforeObserve = observedWindow.IsLoaded;
            if (loadedBeforeObserve)
                loadedAt = factoryReturnedAt;
        }
        observedWindow.Loaded += OnLoaded;
        observedWindow.Closed += OnClosed;
    }

    internal void MarkShowReturned()
    {
        lock (sync)
        {
            if (!stopped)
                showReturnedAt = Stopwatch.GetTimestamp();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        lock (sync)
        {
            if (!stopped)
                loadedAt ??= Stopwatch.GetTimestamp();
        }
    }

    private void OnClosed(object? sender, EventArgs e) => Abort();
    private void OnOperationPosted(object? sender, DispatcherHookEventArgs e) => Record(e.Operation, HookEvent.Posted);
    private void OnOperationStarted(object? sender, DispatcherHookEventArgs e) => Record(e.Operation, HookEvent.Started);
    private void OnOperationCompleted(object? sender, DispatcherHookEventArgs e) => Record(e.Operation, HookEvent.Completed);
    private void OnOperationAborted(object? sender, DispatcherHookEventArgs e) => Record(e.Operation, HookEvent.Aborted);

    private void Record(DispatcherOperation operation, HookEvent hookEvent)
    {
        long timestamp = Stopwatch.GetTimestamp();
        lock (sync)
        {
            if (stopped)
                return;
            if (!operations.TryGetValue(operation, out OperationRecord? record))
            {
                if (operations.Count >= MaximumOperations)
                {
                    droppedHookEvents++;
                    return;
                }
                record = new OperationRecord(operations.Count + 1, operation.Priority);
                operations.Add(operation, record);
            }

            switch (hookEvent)
            {
                case HookEvent.Posted:
                    record.PostedAt ??= timestamp;
                    break;
                case HookEvent.Started:
                    record.StartedAt ??= timestamp;
                    record.Priority = operation.Priority;
                    break;
                case HookEvent.Completed:
                    record.CompletedAt ??= timestamp;
                    break;
                case HookEvent.Aborted:
                    record.AbortedAt ??= timestamp;
                    break;
            }
        }
    }

    // Called before any other work in MainWindow_ContentRendered. No logging or serialization here.
    internal void CaptureAndStop()
    {
        long timestamp = Stopwatch.GetTimestamp();
        lock (sync)
        {
            if (stopped)
                return;
            capturedAt = timestamp;
            stopped = true;
        }
        Detach();
    }

    internal void Abort()
    {
        lock (sync)
        {
            stopped = true;
            operations.Clear();
        }
        Detach();
    }

    private void Detach()
    {
        hooks.OperationPosted -= OnOperationPosted;
        hooks.OperationStarted -= OnOperationStarted;
        hooks.OperationCompleted -= OnOperationCompleted;
        hooks.OperationAborted -= OnOperationAborted;
        if (window != null)
        {
            window.Loaded -= OnLoaded;
            window.Closed -= OnClosed;
            window = null;
        }
        if (ReferenceEquals(Current, this))
            Current = null;
    }

    internal void WriteReport()
    {
        long capture;
        lock (sync)
        {
            if (!stopped || reported || capturedAt is not long value)
                return;
            reported = true;
            capture = value;
        }

        try
        {
            List<OperationSnapshot> snapshots = operations.Values
                .Select(record => Snapshot(record, capture))
                .ToList();
            OperationSnapshot? contentRenderedOperation = snapshots.Where(operation => operation.ActiveAtCapture)
                .OrderByDescending(operation => operation.StartedMs)
                .ThenByDescending(operation => operation.Id)
                .FirstOrDefault();
            double endMs = RelativeMs(capture);
            double? showMs = RelativeMs(showReturnedAt);
            var report = new
            {
                SchemaVersion = 1,
                ProcessId = Environment.ProcessId,
                Clock = "Stopwatch monotonic; milliseconds relative to trace start before main-window factory",
                Markers = new
                {
                    FactoryStartedMs = 0d,
                    FactoryReturnedMs = RelativeMs(factoryReturnedAt),
                    ShowReturnedMs = showMs,
                    LoadedMs = RelativeMs(loadedAt),
                    LoadedBeforeObserve = loadedBeforeObserve,
                    ContentRenderedMs = endMs
                },
                MaximumOperations,
                RecordedOperations = snapshots.Count,
                DroppedHookEvents = droppedHookEvents,
                ContentRenderedOperationId = contentRenderedOperation?.Id,
                ContentRenderedOperation = contentRenderedOperation,
                ContentRenderedOperationInference = "Latest observed Started operation still active at capture; nested Dispatcher frames can make this inference ambiguous. No private delegate reflection is used.",
                ActiveOperationIdsAtCapture = snapshots.Where(operation => operation.ActiveAtCapture).Select(operation => operation.Id).ToArray(),
                Priorities = snapshots.GroupBy(operation => operation.Priority).Select(group => new
                {
                    Priority = group.Key,
                    Observed = group.Count(),
                    Posted = group.Count(operation => operation.PostedMs.HasValue),
                    Started = group.Count(operation => operation.StartedMs.HasValue),
                    Completed = group.Count(operation => operation.CompletedMs.HasValue),
                    Aborted = group.Count(operation => operation.AbortedMs.HasValue),
                    ActiveAtCapture = group.Count(operation => operation.ActiveAtCapture),
                    ExecutionMs = group.Sum(operation => operation.ExecutionMs ?? 0)
                }).ToArray(),
                SlowestQueueOperations = snapshots.Where(operation => operation.QueueMs.HasValue)
                    .OrderByDescending(operation => operation.QueueMs).Take(20).ToArray(),
                SlowestExecutionOperations = snapshots.Where(operation => operation.ExecutionMs.HasValue)
                    .OrderByDescending(operation => operation.ExecutionMs).Take(20).ToArray(),
                TailCoverage = showMs.HasValue ? GetCoverage(snapshots, showMs.Value, endMs) : null,
                Limitations = new[]
                {
                    "ContentRendered marks entry into WPF's content-rendered event (normally dispatched at Input priority); it is not confirmation of actual GPU presentation. The observed operation priority is reported separately.",
                    "Dispatcher Hooks do not cover Task continuations that run after OperationCompleted, native message work, or operations whose Started event predates subscription.",
                    "Unfinished observed operations are clipped to ContentRendered capture. Nested operation durations can overlap; only merged interval coverage is additive.",
                    "A Posted event may arrive after Started. Negative or missing Posted-to-Started durations are reported as unknown (null), not zero.",
                    "Uncovered tail time is not evidence of idle time, GPU work, a hung window, or a specific cause. Dropped events reduce coverage.",
                    "Tracing adds hook/lock overhead; enabled compact-chrome diagnostic logging also has observer cost. Serialization and file output happen after StopAndReport."
                }
            };

            string? requestedPath = Environment.GetEnvironmentVariable("COLORVISION_STARTUP_TRACE_FILE");
            string path = string.IsNullOrWhiteSpace(requestedPath)
                ? Path.Combine(Path.GetTempPath(), $"ColorVisionStartupTrace-{Environment.ProcessId}.json")
                : Path.GetFullPath(requestedPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            log4net.LogManager.GetLogger(typeof(StartupUiTrace)).Info($"Startup UI trace written to {path}");
        }
        catch (Exception ex)
        {
            log4net.LogManager.GetLogger(typeof(StartupUiTrace)).Warn("Startup UI trace could not be written.", ex);
        }
        finally
        {
            operations.Clear();
        }
    }

    private OperationSnapshot Snapshot(OperationRecord record, long capture)
    {
        long? posted = AtOrBefore(record.PostedAt, capture);
        long? started = AtOrBefore(record.StartedAt, capture);
        long? completed = AtOrBefore(record.CompletedAt, capture);
        long? aborted = AtOrBefore(record.AbortedAt, capture);
        long? ended = completed ?? aborted;
        bool active = started.HasValue && !ended.HasValue;
        return new OperationSnapshot(
            record.Id,
            record.Priority.ToString(),
            RelativeMs(posted),
            RelativeMs(started),
            RelativeMs(completed),
            RelativeMs(aborted),
            posted.HasValue && started.HasValue && started.Value >= posted.Value ? DurationMs(posted.Value, started.Value) : null,
            started.HasValue ? DurationMs(started.Value, ended ?? capture) : null,
            posted.HasValue && !started.HasValue && !ended.HasValue ? DurationMs(posted.Value, capture) : null,
            active);
    }

    private static CoverageReport GetCoverage(IEnumerable<OperationSnapshot> snapshots, double fromMs, double toMs)
    {
        var intervals = snapshots.Where(operation => operation.StartedMs.HasValue && operation.ExecutionMs.HasValue)
            .Select(operation => new ExecutionInterval(
                Math.Max(fromMs, operation.StartedMs!.Value),
                Math.Min(toMs, operation.StartedMs!.Value + operation.ExecutionMs!.Value)))
            .Where(interval => interval.EndMs > interval.StartMs)
            .OrderBy(interval => interval.StartMs)
            .ToList();
        var merged = new List<ExecutionInterval>();
        foreach (ExecutionInterval interval in intervals)
        {
            if (merged.Count > 0 && interval.StartMs <= merged[^1].EndMs)
                merged[^1] = merged[^1] with { EndMs = Math.Max(merged[^1].EndMs, interval.EndMs) };
            else
                merged.Add(interval);
        }
        double coveredMs = merged.Sum(interval => interval.EndMs - interval.StartMs);
        return new CoverageReport(fromMs, toMs, toMs - fromMs, coveredMs, Math.Max(0, toMs - fromMs - coveredMs), merged);
    }

    private static long? AtOrBefore(long? timestamp, long capture) => timestamp is long value && value <= capture ? value : null;
    private double RelativeMs(long timestamp) => DurationMs(startedAt, timestamp);
    private double? RelativeMs(long? timestamp) => timestamp.HasValue ? RelativeMs(timestamp.Value) : null;
    private static double DurationMs(long from, long to) => (to - from) * 1000d / Stopwatch.Frequency;

    private enum HookEvent { Posted, Started, Completed, Aborted }

    private sealed class OperationRecord(int id, DispatcherPriority priority)
    {
        internal int Id { get; } = id;
        internal DispatcherPriority Priority { get; set; } = priority;
        internal long? PostedAt { get; set; }
        internal long? StartedAt { get; set; }
        internal long? CompletedAt { get; set; }
        internal long? AbortedAt { get; set; }
    }

    private sealed record OperationSnapshot(int Id, string Priority, double? PostedMs, double? StartedMs,
        double? CompletedMs, double? AbortedMs, double? QueueMs, double? ExecutionMs,
        double? PendingQueueMsAtCapture, bool ActiveAtCapture);
    private sealed record ExecutionInterval(double StartMs, double EndMs);
    private sealed record CoverageReport(double FromMs, double ToMs, double DurationMs, double CoveredExecutionMs,
        double UncoveredMs, IReadOnlyList<ExecutionInterval> MergedExecutionIntervals);
}
