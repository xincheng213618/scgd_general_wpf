using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing
{
    internal sealed class FlowRuntimeActivitySnapshot
    {
        public bool HasObservedRun { get; init; }

        public bool EngineRunning { get; init; }

        public DateTimeOffset? EngineStartedAt { get; init; }

        public string LastRunStatus { get; init; } = string.Empty;

        public long? LastRunDurationMilliseconds { get; init; }
    }

    internal static class FlowRuntimeActivityRegistry
    {
        private sealed record ActiveRun(string SerialNumber, DateTimeOffset StartedAt);

        private static readonly object SyncRoot = new();
        private static readonly Dictionary<FlowControl, ActiveRun> Active =
            new(ReferenceEqualityComparer.Instance);
        private static bool _hasObservedRun;
        private static string _lastSerialNumber = string.Empty;
        private static string _lastRunStatus = string.Empty;
        private static long? _lastRunDurationMilliseconds;

        internal static void MarkStarted(FlowControl owner, string serialNumber)
        {
            ArgumentNullException.ThrowIfNull(owner);
            lock (SyncRoot)
            {
                Active[owner] = new ActiveRun(serialNumber, DateTimeOffset.UtcNow);
                _hasObservedRun = true;
            }
        }

        internal static void MarkCompleted(FlowControl owner, FlowStatus status, long durationMilliseconds)
        {
            ArgumentNullException.ThrowIfNull(owner);
            lock (SyncRoot)
            {
                Active.TryGetValue(owner, out ActiveRun? run);
                Active.Remove(owner);
                _hasObservedRun = true;
                _lastSerialNumber = run?.SerialNumber ?? string.Empty;
                _lastRunStatus = status.ToString();
                _lastRunDurationMilliseconds = durationMilliseconds > 0 ? durationMilliseconds : null;
            }
        }

        internal static void UpdateFinalOutcome(string? serialNumber, FlowStatus status, long durationMilliseconds)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
                return;
            lock (SyncRoot)
            {
                if (!string.Equals(serialNumber, _lastSerialNumber, StringComparison.Ordinal))
                    return;
                _lastRunStatus = status.ToString();
                _lastRunDurationMilliseconds = durationMilliseconds > 0 ? durationMilliseconds : null;
            }
        }

        internal static FlowRuntimeActivitySnapshot Capture()
        {
            lock (SyncRoot)
            {
                DateTimeOffset? startedAt = Active.Count == 0
                    ? null
                    : Active.Values.Min(item => item.StartedAt);
                return new FlowRuntimeActivitySnapshot
                {
                    HasObservedRun = _hasObservedRun,
                    EngineRunning = Active.Count > 0,
                    EngineStartedAt = startedAt,
                    LastRunStatus = _lastRunStatus,
                    LastRunDurationMilliseconds = _lastRunDurationMilliseconds,
                };
            }
        }
    }
}
