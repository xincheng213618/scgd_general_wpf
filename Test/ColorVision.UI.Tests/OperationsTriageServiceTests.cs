using ColorVision.UI.Desktop.Operations;

namespace ColorVision.UI.Tests
{
    public sealed class OperationsTriageServiceTests
    {
        [Fact]
        public void BuildReturnsOnlyFixedActionsAndUsesPairedConfirmationForMqttRestart()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            OperationsLogDigest digest = new()
            {
                Available = true,
                CriticalCount = 1,
                ErrorCount = 1,
                WarningCount = 2,
                RecentEvents =
                [
                    new OperationsAlert
                    {
                        AlertId = "message-error",
                        Severity = "error",
                        Source = "消息服务",
                        Summary = "redacted",
                        OccurredAt = now,
                    },
                    new OperationsAlert
                    {
                        AlertId = "application-critical",
                        Severity = "critical",
                        Source = "应用",
                        Summary = "redacted",
                        OccurredAt = now.AddMinutes(-1),
                    },
                ],
            };

            OperationsTriageReport report = OperationsTriageService.Build(
                digest, new OperationsDesktopState(true, true, false, "Minimized"), 2,
                ServiceHealth("stopped", healthy: false, maintenanceSupported: true),
                OperationsDeviceHealthSnapshotFactory.Create(
                [
                    new OperationsDeviceHealthObservation(OperationsDeviceCategories.Camera, OperationsDeviceStates.Ready),
                    new OperationsDeviceHealthObservation(OperationsDeviceCategories.Camera, OperationsDeviceStates.Unavailable),
                ]));

            Assert.Equal("critical", report.State);
            Assert.Contains(report.Findings, item => item.FindingId == "recent-abnormal-events");
            Assert.Contains(report.Findings, item => item.FindingId == "message-service-events");
            Assert.Contains(report.Findings, item => item.FindingId == "desktop-window-hidden");
            Assert.Contains(report.Findings, item => item.FindingId == "pending-operations-jobs");
            Assert.Contains(report.Findings, item => item.FindingId == "inspection-devices-attention");
            Assert.Equal(2, report.DeviceTotalCount);
            Assert.Equal(1, report.DeviceAttentionCount);

            OperationsTriageAction[] actions = report.Findings.SelectMany(item => item.Actions).ToArray();
            string[] allowedActionIds =
            [
                OperationsTriageActionIds.ViewRecentEvents,
                OperationsTriageActionIds.ShowMainWindow,
                OperationsTriageActionIds.ReviewJobs,
                OperationsTriageActionIds.RequestMqttRestart,
                OperationsTriageActionIds.ViewDeviceHealth,
            ];
            Assert.All(actions, action => Assert.Contains(action.ActionId, allowedActionIds));
            OperationsTriageAction restart = Assert.Single(actions,
                action => action.ActionId == OperationsTriageActionIds.RequestMqttRestart);
            Assert.Equal(OperationsRiskLevels.Privileged, restart.RiskLevel);
            Assert.True(restart.RequiresConfirmation);
            Assert.False(restart.RequiresLocalCoSign);
            Assert.Equal("approval-workflow", restart.Kind);
        }

        [Fact]
        public void BuildReturnsHealthyWhenBoundedEvidenceHasNoActionableFinding()
        {
            OperationsTriageReport report = OperationsTriageService.Build(
                new OperationsLogDigest { Available = true, InfoCount = 4 },
                new OperationsDesktopState(true, true, true, "Normal"),
                0);

            Assert.Equal("healthy", report.State);
            Assert.Empty(report.Findings);
            Assert.Equal(0, report.PendingJobCount);
        }

        [Fact]
        public void ClosedDeviceIsReportedWithoutBeingMisclassifiedAsAttention()
        {
            OperationsTriageReport report = OperationsTriageService.Build(
                new OperationsLogDigest { Available = true },
                new OperationsDesktopState(true, true, true, "Normal"),
                0,
                deviceHealth: OperationsDeviceHealthSnapshotFactory.Create(
                [
                    new OperationsDeviceHealthObservation(
                        OperationsDeviceCategories.Camera, OperationsDeviceStates.Closed),
                ]));

            Assert.Equal("healthy", report.State);
            Assert.Equal(1, report.DeviceClosedCount);
            Assert.Equal(0, report.DeviceAttentionCount);
            Assert.DoesNotContain(report.Findings, item => item.Category == "devices");
        }

        [Fact]
        public void DisconnectedMessageChannelExplainsUnavailableDeviceStates()
        {
            OperationsTriageReport report = OperationsTriageService.Build(
                new OperationsLogDigest { Available = true },
                new OperationsDesktopState(true, true, true, "Normal"),
                0,
                deviceHealth: OperationsDeviceHealthSnapshotFactory.Create(
                [
                    new OperationsDeviceHealthObservation(
                        OperationsDeviceCategories.Camera,
                        OperationsDeviceStates.Unavailable,
                        OperationsDeviceUnavailableReasons.Uninitialized),
                ]),
                messageChannel: OperationsMessageChannelHealthSnapshotFactory.Create(
                    new OperationsMessageChannelObservation(true, false, 3, 0)));

            OperationsTriageFinding channelFinding = Assert.Single(report.Findings,
                item => item.FindingId == "message-channel-attention");
            Assert.Equal("error", channelFinding.Severity);
            Assert.Contains(channelFinding.Actions,
                action => action.ActionId == OperationsTriageActionIds.ViewMessageChannelHealth);
            OperationsTriageFinding deviceFinding = Assert.Single(report.Findings,
                item => item.FindingId == "inspection-devices-attention");
            Assert.Contains("可能由通道问题引起", deviceFinding.Summary, StringComparison.Ordinal);
        }

        [Fact]
        public void ReadyMessageChannelPushesUnavailableDevicesToDeviceLayer()
        {
            OperationsTriageReport report = OperationsTriageService.Build(
                new OperationsLogDigest { Available = true },
                new OperationsDesktopState(true, true, true, "Normal"),
                0,
                deviceHealth: OperationsDeviceHealthSnapshotFactory.Create(
                [
                    new OperationsDeviceHealthObservation(
                        OperationsDeviceCategories.Camera,
                        OperationsDeviceStates.Unavailable,
                        OperationsDeviceUnavailableReasons.Uninitialized),
                ]),
                messageChannel: OperationsMessageChannelHealthSnapshotFactory.Create(
                    new OperationsMessageChannelObservation(true, true, 3, 3)));

            Assert.DoesNotContain(report.Findings, item => item.Category == "message-channel");
            OperationsTriageFinding deviceFinding = Assert.Single(report.Findings,
                item => item.FindingId == "inspection-devices-attention");
            Assert.Contains("消息通道当前正常", deviceFinding.Summary, StringComparison.Ordinal);
            Assert.Contains("未初始化 1 台", deviceFinding.Summary, StringComparison.Ordinal);
            Assert.Equal(1, report.DeviceUnavailableCount);
            Assert.Equal(1, report.DeviceUninitializedCount);
        }

        [Fact]
        public void BuildDoesNotRecommendWindowExecutionWhenWindowIsUnavailable()
        {
            OperationsTriageReport report = OperationsTriageService.Build(
                new OperationsLogDigest { Available = true },
                new OperationsDesktopState(false, false, false, "Unavailable"),
                0);

            OperationsTriageFinding finding = Assert.Single(report.Findings);
            Assert.Equal("desktop-window-unavailable", finding.FindingId);
            Assert.Empty(finding.Actions);
        }

        [Fact]
        public void RunningMqttServicePreventsLogOnlyRestartRecommendation()
        {
            OperationsTriageReport report = OperationsTriageService.Build(
                new OperationsLogDigest
                {
                    Available = true,
                    ErrorCount = 1,
                    RecentEvents =
                    [
                        new OperationsAlert
                        {
                            AlertId = "old-mqtt-error",
                            Severity = "error",
                            Source = "消息服务",
                            Summary = "redacted",
                            OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                        },
                    ],
                },
                new OperationsDesktopState(true, true, true, "Normal"),
                0,
                ServiceHealth("running", healthy: true, maintenanceSupported: true));

            OperationsTriageFinding finding = Assert.Single(report.Findings,
                item => item.FindingId == "message-service-events");
            Assert.DoesNotContain(finding.Actions,
                action => action.ActionId == OperationsTriageActionIds.RequestMqttRestart);
            Assert.Contains("正在运行", finding.Summary, StringComparison.Ordinal);
        }

        private static OperationsServiceHealthReport ServiceHealth(
            string status,
            bool healthy,
            bool maintenanceSupported) => new()
        {
            Available = true,
            AllHealthy = healthy,
            Services =
            [
                new OperationsServiceHealthItem
                {
                    ServiceId = OperationsServiceIds.MqttBroker,
                    Title = "MQTT 消息服务",
                    Status = status,
                    Installed = true,
                    Healthy = healthy,
                    MaintenanceSupported = maintenanceSupported,
                    StatusSource = "test",
                    ObservedAt = DateTimeOffset.UtcNow,
                },
            ],
        };
    }
}
