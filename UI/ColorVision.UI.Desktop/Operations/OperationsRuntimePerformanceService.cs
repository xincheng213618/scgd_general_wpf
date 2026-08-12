using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.UI.Desktop.Operations
{
    public sealed class OperationsUiResponsivenessSnapshot
    {
        public bool Available { get; init; }

        public string State { get; init; } = "unavailable";

        public long? LatencyMilliseconds { get; init; }
    }

    public sealed class OperationsGcSnapshot
    {
        public int Gen0Collections { get; init; }

        public int Gen1Collections { get; init; }

        public int Gen2Collections { get; init; }
    }

    public sealed class OperationsRuntimePerformanceSnapshot
    {
        public DateTimeOffset CapturedAt { get; init; }

        public int SampleMilliseconds { get; init; }

        public double CpuPercent { get; init; }

        public double WorkingSetMb { get; init; }

        public double PrivateMemoryMb { get; init; }

        public double ManagedHeapMb { get; init; }

        public int ThreadCount { get; init; }

        public int HandleCount { get; init; }

        public OperationsGcSnapshot GarbageCollection { get; init; } = new();

        public OperationsUiResponsivenessSnapshot MainUi { get; init; } = new();

        public string PrivacyNotice { get; init; } =
            "This snapshot contains aggregate process counters only. It excludes process identifiers, names, paths, command lines, host names, user names, network addresses, window content, and application data.";
    }

    public interface IOperationsRuntimePerformanceProvider
    {
        OperationsRuntimePerformanceSnapshot Capture();
    }

    public sealed class OperationsRuntimePerformanceService : IOperationsRuntimePerformanceProvider
    {
        public const int DefaultSampleMilliseconds = 300;
        private const int UiProbeTimeoutMilliseconds = 1000;
        private const double BytesPerMegabyte = 1024d * 1024d;
        private readonly Func<OperationsUiResponsivenessSnapshot> _uiProbe;

        public OperationsRuntimePerformanceService(Func<OperationsUiResponsivenessSnapshot>? uiProbe = null)
        {
            _uiProbe = uiProbe ?? CaptureMainUiResponsiveness;
        }

        public OperationsRuntimePerformanceSnapshot Capture() => Capture(DefaultSampleMilliseconds);

        public OperationsRuntimePerformanceSnapshot Capture(int sampleMilliseconds)
        {
            if (sampleMilliseconds is < 10 or > 2000)
                throw new ArgumentOutOfRangeException(nameof(sampleMilliseconds));

            using Process process = Process.GetCurrentProcess();
            process.Refresh();
            TimeSpan cpuBefore = process.TotalProcessorTime;
            long sampleStarted = Stopwatch.GetTimestamp();
            Thread.Sleep(sampleMilliseconds);
            TimeSpan elapsed = Stopwatch.GetElapsedTime(sampleStarted);
            process.Refresh();

            return new OperationsRuntimePerformanceSnapshot
            {
                CapturedAt = DateTimeOffset.UtcNow,
                SampleMilliseconds = (int)Math.Max(1, Math.Round(elapsed.TotalMilliseconds)),
                CpuPercent = CalculateCpuPercent(
                    process.TotalProcessorTime - cpuBefore, elapsed, Environment.ProcessorCount),
                WorkingSetMb = Megabytes(process.WorkingSet64),
                PrivateMemoryMb = Megabytes(process.PrivateMemorySize64),
                ManagedHeapMb = Megabytes(GC.GetTotalMemory(forceFullCollection: false)),
                ThreadCount = process.Threads.Count,
                HandleCount = process.HandleCount,
                GarbageCollection = new OperationsGcSnapshot
                {
                    Gen0Collections = GC.CollectionCount(0),
                    Gen1Collections = GC.CollectionCount(1),
                    Gen2Collections = GC.CollectionCount(2),
                },
                MainUi = SafeCaptureUiResponsiveness(),
            };
        }

        public static double CalculateCpuPercent(TimeSpan processorTime, TimeSpan elapsed, int processorCount)
        {
            if (processorTime <= TimeSpan.Zero || elapsed <= TimeSpan.Zero || processorCount <= 0)
                return 0;
            double value = processorTime.TotalMilliseconds / (elapsed.TotalMilliseconds * processorCount) * 100d;
            return Math.Round(Math.Clamp(value, 0d, 100d), 1);
        }

        private OperationsUiResponsivenessSnapshot SafeCaptureUiResponsiveness()
        {
            try
            {
                return _uiProbe();
            }
            catch
            {
                return UnavailableUiSnapshot();
            }
        }

        private static OperationsUiResponsivenessSnapshot CaptureMainUiResponsiveness()
        {
            Dispatcher? dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                return UnavailableUiSnapshot();
            if (dispatcher.CheckAccess())
            {
                return new OperationsUiResponsivenessSnapshot
                {
                    Available = true,
                    State = "responsive",
                    LatencyMilliseconds = 0,
                };
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            DispatcherOperation operation = dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Send);
            if (!operation.Task.Wait(UiProbeTimeoutMilliseconds))
            {
                return new OperationsUiResponsivenessSnapshot
                {
                    Available = true,
                    State = "unresponsive",
                };
            }
            stopwatch.Stop();
            long latency = Math.Max(0, stopwatch.ElapsedMilliseconds);
            return new OperationsUiResponsivenessSnapshot
            {
                Available = true,
                State = latency >= 250 ? "slow" : "responsive",
                LatencyMilliseconds = latency,
            };
        }

        private static OperationsUiResponsivenessSnapshot UnavailableUiSnapshot() => new()
        {
            Available = false,
            State = "unavailable",
        };

        private static double Megabytes(long bytes) => Math.Round(Math.Max(0, bytes) / BytesPerMegabyte, 1);
    }
}
