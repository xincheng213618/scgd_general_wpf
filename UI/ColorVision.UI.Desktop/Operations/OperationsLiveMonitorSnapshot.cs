namespace ColorVision.UI.Desktop.Operations
{
    public sealed class OperationsLiveMonitorAlertSummary
    {
        public int Count { get; init; }

        public int WarningCount { get; init; }

        public int ErrorCount { get; init; }

        public int CriticalCount { get; init; }

        public string PrimarySource { get; init; } = string.Empty;

        public DateTimeOffset? LatestOccurredAt { get; init; }
    }

    public sealed class OperationsRelayMqttServiceSnapshot
    {
        public bool Available { get; init; }

        public string Status { get; init; } = "unknown";

        public bool MaintenanceSupported { get; init; }
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

        public OperationsMessageChannelHealthSnapshot MessageChannel { get; init; } =
            OperationsMessageChannelHealthSnapshot.CreateUnavailable();

        public OperationsRelayMqttServiceSnapshot MqttService { get; init; } = new();

        public OperationsLiveMonitorAlertSummary Alerts { get; init; } = new();

        public OperationsApplicationRecoveryStatus ApplicationRecovery { get; init; } = new();

        public string PrivacyNotice { get; init; } =
            "This live snapshot contains aggregate flow state, process counters, UI latency, normalized message-channel, fixed local MQTT service, device-category state, alert counts, and one allowlisted alert source only. It excludes flow, template, batch, node, parameter, result, process identity, host, user, endpoint, device identity, topic, payload, service identity, path, account, arguments, configuration, credentials, raw device status, log text, and inspection data.";
    }

    public static class OperationsLiveMonitorSnapshotFactory
    {
        public static OperationsLiveMonitorSnapshot Create(
            OperationsFlowRuntimeStatus flow,
            OperationsRuntimePerformanceSnapshot performance,
            IReadOnlyList<OperationsAlert> alerts,
            OperationsDeviceHealthSnapshot devices,
            DateTimeOffset? capturedAt = null,
            OperationsMessageChannelHealthSnapshot? messageChannel = null,
            OperationsApplicationRecoveryStatus? applicationRecovery = null,
            OperationsServiceHealthReport? serviceHealth = null)
        {
            ArgumentNullException.ThrowIfNull(flow);
            ArgumentNullException.ThrowIfNull(performance);
            ArgumentNullException.ThrowIfNull(alerts);
            ArgumentNullException.ThrowIfNull(devices);
            OperationsAlert? primaryAlert = alerts
                .OrderByDescending(item => AlertSeverityRank(item.Severity))
                .ThenByDescending(item => item.OccurredAt)
                .FirstOrDefault();

            return new OperationsLiveMonitorSnapshot
            {
                CapturedAt = capturedAt ?? DateTimeOffset.UtcNow,
                Flow = flow,
                Performance = performance,
                Devices = devices,
                MessageChannel = messageChannel ?? OperationsMessageChannelHealthSnapshot.CreateUnavailable(),
                MqttService = CreateMqttServiceSnapshot(serviceHealth),
                ApplicationRecovery = applicationRecovery ?? new OperationsApplicationRecoveryStatus(),
                Alerts = new OperationsLiveMonitorAlertSummary
                {
                    Count = alerts.Count,
                    WarningCount = alerts.Count(item => item.Severity == "warning"),
                    ErrorCount = alerts.Count(item => item.Severity == "error"),
                    CriticalCount = alerts.Count(item => item.Severity == "critical"),
                    PrimarySource = SafeAlertSource(primaryAlert?.Source),
                    LatestOccurredAt = alerts.Count == 0
                        ? null
                        : alerts.Max(item => item.OccurredAt),
                },
            };
        }

        private static int AlertSeverityRank(string? severity) => severity switch
        {
            "critical" => 3,
            "error" => 2,
            "warning" => 1,
            _ => 0,
        };

        private static string SafeAlertSource(string? source) => source switch
        {
            "安全运维" => source,
            "消息服务" => source,
            "设备与图像" => source,
            "流程" => source,
            "更新与下载" => source,
            "Copilot" => source,
            "服务" => source,
            "应用" => source,
            _ => string.Empty,
        };

        private static OperationsRelayMqttServiceSnapshot CreateMqttServiceSnapshot(
            OperationsServiceHealthReport? serviceHealth)
        {
            if (serviceHealth is not { Available: true })
                return new OperationsRelayMqttServiceSnapshot();

            OperationsServiceHealthItem? mqtt = serviceHealth.Services.FirstOrDefault(item =>
                string.Equals(item.ServiceId, OperationsServiceIds.MqttBroker, StringComparison.Ordinal));
            if (mqtt == null)
                return new OperationsRelayMqttServiceSnapshot();

            return new OperationsRelayMqttServiceSnapshot
            {
                Available = true,
                Status = mqtt.Status switch
                {
                    "running" => "running",
                    "stopped" => "stopped",
                    "paused" => "paused",
                    "start_pending" => "start_pending",
                    "stop_pending" => "stop_pending",
                    "continue_pending" => "continue_pending",
                    "pause_pending" => "pause_pending",
                    "not_installed" => "not_installed",
                    "not_applicable" => "not_applicable",
                    _ => "unknown",
                },
                MaintenanceSupported = mqtt.MaintenanceSupported,
            };
        }
    }
}
