namespace ColorVision.UI.Desktop.Operations
{
    public sealed class OperationsFlowRuntimeSourceSnapshot
    {
        public bool Available { get; init; }

        public bool HasConfiguredFlow { get; init; }

        public bool LifecycleActive { get; init; }

        public bool EngineRunning { get; init; }

        public bool BatchIsCurrentLifecycle { get; init; }

        public bool ProgressAvailable { get; init; }

        public string BatchStatus { get; init; } = string.Empty;

        public double ProgressPercent { get; init; }

        public DateTimeOffset? BatchCreatedAt { get; init; }

        public long BatchDurationMilliseconds { get; init; }

        public string LastRunStatus { get; init; } = string.Empty;

        public long? LastRunDurationMilliseconds { get; init; }
    }

    public sealed class OperationsFlowRuntimeStatus
    {
        public bool Available { get; init; }

        public bool HasConfiguredFlow { get; init; }

        public string Phase { get; init; } = "unavailable";

        public bool IsActive { get; init; }

        public bool EngineRunning { get; init; }

        public bool ProgressAvailable { get; init; }

        public double? ProgressPercent { get; init; }

        public bool ProgressIsHistoricalEstimate { get; init; }

        public long? ElapsedMilliseconds { get; init; }

        public string LastRunStatus { get; init; } = "none";

        public long? LastRunDurationMilliseconds { get; init; }

        public DateTimeOffset ObservedAt { get; init; } = DateTimeOffset.UtcNow;

        public string PrivacyNotice { get; init; } =
            "This status contains aggregate lifecycle, progress, and outcome fields only. It excludes flow and template names, identifiers, batch serial numbers, node names, parameters, result text, and inspection data.";

        public static OperationsFlowRuntimeStatus CreateUnavailable(DateTimeOffset? observedAt = null) => new()
        {
            ObservedAt = observedAt ?? DateTimeOffset.UtcNow,
        };
    }

    public interface IOperationsFlowRuntimeStatusProvider
    {
        OperationsFlowRuntimeStatus Capture();
    }

    public sealed class UnavailableOperationsFlowRuntimeStatusProvider : IOperationsFlowRuntimeStatusProvider
    {
        public static UnavailableOperationsFlowRuntimeStatusProvider Instance { get; } = new();

        private UnavailableOperationsFlowRuntimeStatusProvider()
        {
        }

        public OperationsFlowRuntimeStatus Capture() => OperationsFlowRuntimeStatus.CreateUnavailable();
    }

    public static class OperationsFlowRuntimeStatusFactory
    {
        public static OperationsFlowRuntimeStatus Create(
            OperationsFlowRuntimeSourceSnapshot source,
            DateTimeOffset? observedAt = null)
        {
            ArgumentNullException.ThrowIfNull(source);
            DateTimeOffset now = observedAt ?? DateTimeOffset.UtcNow;
            if (!source.Available)
                return OperationsFlowRuntimeStatus.CreateUnavailable(now);

            string batchStatus = NormalizeStatus(source.BatchStatus);
            bool terminal = IsTerminal(batchStatus);
            string phase = source.LifecycleActive
                ? source.EngineRunning
                    ? "running"
                    : source.BatchIsCurrentLifecycle && terminal ? "finalizing" : "preparing"
                : "idle";
            bool progressAvailable = source.LifecycleActive && source.ProgressAvailable;
            double? progress = progressAvailable
                ? phase switch
                {
                    "preparing" => 0d,
                    "finalizing" => 100d,
                    _ => Math.Round(Math.Clamp(source.ProgressPercent, 0d, 99d), 1),
                }
                : null;
            long? elapsedMilliseconds = source.LifecycleActive
                && (source.BatchIsCurrentLifecycle || source.EngineRunning)
                && source.BatchCreatedAt != null
                ? Math.Max(0, (long)(now - source.BatchCreatedAt.Value).TotalMilliseconds)
                : null;
            string explicitLastStatus = NormalizeStatus(source.LastRunStatus);
            string lastRunStatus = explicitLastStatus != "none"
                ? explicitLastStatus
                : terminal ? batchStatus : "none";
            long? lastRunDuration = source.LastRunDurationMilliseconds is > 0
                ? source.LastRunDurationMilliseconds
                : terminal && source.BatchDurationMilliseconds > 0
                    ? source.BatchDurationMilliseconds
                    : null;

            return new OperationsFlowRuntimeStatus
            {
                Available = true,
                HasConfiguredFlow = source.HasConfiguredFlow,
                Phase = phase,
                IsActive = source.LifecycleActive,
                EngineRunning = source.EngineRunning,
                ProgressAvailable = progressAvailable,
                ProgressPercent = progress,
                ProgressIsHistoricalEstimate = phase == "running" && progress > 0,
                ElapsedMilliseconds = elapsedMilliseconds,
                LastRunStatus = lastRunStatus,
                LastRunDurationMilliseconds = lastRunDuration,
                ObservedAt = now,
            };
        }

        private static string NormalizeStatus(string status) => status.Trim().ToLowerInvariant() switch
        {
            "completed" => "completed",
            "failed" => "failed",
            "canceled" => "canceled",
            "overtime" => "timed_out",
            _ => "none",
        };

        private static bool IsTerminal(string status) =>
            status is "completed" or "failed" or "canceled" or "timed_out";
    }
}
