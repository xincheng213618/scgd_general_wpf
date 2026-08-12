namespace ColorVision.UI.Desktop.Operations
{
    public sealed class OperationsLiveMonitorAlertSummary
    {
        public int Count { get; init; }

        public int WarningCount { get; init; }

        public int ErrorCount { get; init; }

        public int CriticalCount { get; init; }

        public DateTimeOffset? LatestOccurredAt { get; init; }
    }

    public sealed class OperationsLiveMonitorSnapshot
    {
        public DateTimeOffset CapturedAt { get; init; }

        public int SuggestedRefreshSeconds { get; init; } = 10;

        public OperationsFlowRuntimeStatus Flow { get; init; } =
            OperationsFlowRuntimeStatus.CreateUnavailable();

        public OperationsRuntimePerformanceSnapshot Performance { get; init; } = new();

        public OperationsDeviceHealthSnapshot Devices { get; init; } =
            OperationsDeviceHealthSnapshot.CreateUnavailable();

        public OperationsLiveMonitorAlertSummary Alerts { get; init; } = new();

        public string PrivacyNotice { get; init; } =
            "This live snapshot contains aggregate flow state, process counters, UI latency, device-category online counts, and alert counts only. It excludes flow, template, batch, node, parameter, result, process identity, host, user, endpoint, device identity, topic, configuration, log text, and inspection data.";
    }

    public static class OperationsLiveMonitorSnapshotFactory
    {
        public static OperationsLiveMonitorSnapshot Create(
            OperationsFlowRuntimeStatus flow,
            OperationsRuntimePerformanceSnapshot performance,
            IReadOnlyList<OperationsAlert> alerts,
            OperationsDeviceHealthSnapshot devices,
            DateTimeOffset? capturedAt = null)
        {
            ArgumentNullException.ThrowIfNull(flow);
            ArgumentNullException.ThrowIfNull(performance);
            ArgumentNullException.ThrowIfNull(alerts);
            ArgumentNullException.ThrowIfNull(devices);

            return new OperationsLiveMonitorSnapshot
            {
                CapturedAt = capturedAt ?? DateTimeOffset.UtcNow,
                Flow = flow,
                Performance = performance,
                Devices = devices,
                Alerts = new OperationsLiveMonitorAlertSummary
                {
                    Count = alerts.Count,
                    WarningCount = alerts.Count(item => item.Severity == "warning"),
                    ErrorCount = alerts.Count(item => item.Severity == "error"),
                    CriticalCount = alerts.Count(item => item.Severity == "critical"),
                    LatestOccurredAt = alerts.Count == 0
                        ? null
                        : alerts.Max(item => item.OccurredAt),
                },
            };
        }
    }
}
