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
                OperationsTriageActionIds.ViewServiceHealth,
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
            OperationsTriageFinding serviceFinding = Assert.Single(report.Findings,
                item => item.FindingId == "service-health-mqtt-broker");
            Assert.Equal(OperationsTriageActionIds.ViewServiceHealth,
                serviceFinding.Actions[0].ActionId);
            Assert.Equal(OperationsRiskLevels.ReadOnly, serviceFinding.Actions[0].RiskLevel);
        }

        [Fact]
        public void UnavailableServiceHealthStillOffersReadOnlyEvidenceRefresh()
        {
            OperationsTriageReport report = OperationsTriageService.Build(
                new OperationsLogDigest { Available = true },
                new OperationsDesktopState(true, true, true, "Normal"),
                0,
                new OperationsServiceHealthReport { Available = false });

            OperationsTriageFinding finding = Assert.Single(report.Findings,
                item => item.FindingId == "service-health-unavailable");
            OperationsTriageAction action = Assert.Single(finding.Actions);
            Assert.Equal(OperationsTriageActionIds.ViewServiceHealth, action.ActionId);
            Assert.Equal(OperationsRiskLevels.ReadOnly, action.RiskLevel);
            Assert.False(action.RequiresConfirmation);
            Assert.False(action.RequiresLocalCoSign);
        }

        [Fact]
        public void UnavailableEvidenceStillOffersOnlyItsReadOnlyDetailRefresh()
        {
            OperationsTriageReport report = OperationsTriageService.Build(
                new OperationsLogDigest { Available = false },
                new OperationsDesktopState(true, true, true, "Normal"),
                0,
                deviceHealth: OperationsDeviceHealthSnapshot.CreateUnavailable(),
                messageChannel: OperationsMessageChannelHealthSnapshot.CreateUnavailable());

            AssertReadOnlyDetailAction(
                report, "device-health-unavailable", OperationsTriageActionIds.ViewDeviceHealth);
            AssertReadOnlyDetailAction(
                report, "message-channel-health-unavailable", OperationsTriageActionIds.ViewMessageChannelHealth);
            AssertReadOnlyDetailAction(
                report, "application-log-unavailable", OperationsTriageActionIds.ViewRecentEvents);
            Assert.DoesNotContain(report.Findings.SelectMany(item => item.Actions),
                action => action.ActionId == OperationsTriageActionIds.RequestMessageChannelRecovery
                    || action.ActionId == OperationsTriageActionIds.RequestMqttRestart);
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
        public void FailureEvidenceAddsOnlyReadOnlyNavigationRecommendation()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            OperationsFailureEvidenceSnapshot evidence = OperationsFailureEvidenceSnapshotFactory.Create(
                [new(now.AddMinutes(-5), OperationsFailureKinds.ApplicationHang)],
                [new(now.AddMinutes(-4))],
                eventLogAvailable: true,
                dumpFolderAvailable: true,
                observedAt: now);

            OperationsTriageReport report = OperationsTriageService.Build(
                new OperationsLogDigest { Available = true },
                new OperationsDesktopState(true, true, true, "Normal"),
                0,
                failureEvidence: evidence);

            OperationsTriageFinding finding = Assert.Single(report.Findings,
                item => item.FindingId == "recent-failure-evidence");
            Assert.Equal("error", finding.Severity);
            Assert.Equal(2, finding.EvidenceCount);
            OperationsTriageAction action = Assert.Single(finding.Actions);
            Assert.Equal(OperationsTriageActionIds.ViewFailureEvidence, action.ActionId);
            Assert.Equal(OperationsRiskLevels.ReadOnly, action.RiskLevel);
            Assert.False(action.RequiresConfirmation);
            Assert.False(action.RequiresLocalCoSign);
            Assert.Equal(1, report.HangCount);
            Assert.Equal(1, report.FailureDumpCount);
        }

        [Fact]
        public void UnavailableFailureEvidenceCannotBeMisreportedAsHealthy()
        {
            OperationsTriageReport report = OperationsTriageService.Build(
                new OperationsLogDigest { Available = true },
                new OperationsDesktopState(true, true, true, "Normal"),
                0,
                failureEvidence: OperationsFailureEvidenceSnapshot.CreateUnavailable());

            Assert.Equal("attention", report.State);
            AssertReadOnlyDetailAction(
                report, "failure-evidence-unavailable", OperationsTriageActionIds.ViewFailureEvidence);
            OperationsTriageFinding finding = Assert.Single(report.Findings);
            Assert.Contains("不能据此判断近期没有故障", finding.Summary, StringComparison.Ordinal);
        }

        [Fact]
        public void PartialFailureEvidenceCoverageKeepsNoFindingConclusionQualified()
        {
            OperationsFailureEvidenceSnapshot evidence = OperationsFailureEvidenceSnapshotFactory.Create(
                [], [], eventLogAvailable: true, dumpFolderAvailable: false);

            OperationsTriageReport report = OperationsTriageService.Build(
                new OperationsLogDigest { Available = true },
                new OperationsDesktopState(true, true, true, "Normal"),
                0,
                failureEvidence: evidence);

            OperationsTriageFinding finding = Assert.Single(report.Findings,
                item => item.FindingId == "failure-evidence-coverage-limited");
            Assert.Contains("本机转储目录不可读取", finding.Summary, StringComparison.Ordinal);
            Assert.Contains("不能据此确认", finding.Summary, StringComparison.Ordinal);
            AssertReadOnlyDetailAction(
                report, finding.FindingId, OperationsTriageActionIds.ViewFailureEvidence);
        }

        [Fact]
        public void LimitedFailureScanKeepsEvidenceAndCoverageInOneFinding()
        {
            OperationsFailureEvidenceSnapshot evidence = OperationsFailureEvidenceSnapshotFactory.Create(
                [new OperationsFailureEventObservation(
                    DateTimeOffset.UtcNow.AddMinutes(-5), OperationsFailureKinds.ApplicationCrash)],
                [],
                eventLogAvailable: true,
                dumpFolderAvailable: true,
                eventScanLimited: true);

            OperationsTriageReport report = OperationsTriageService.Build(
                new OperationsLogDigest { Available = true },
                new OperationsDesktopState(true, true, true, "Normal"),
                0,
                failureEvidence: evidence);

            OperationsTriageFinding finding = Assert.Single(report.Findings,
                item => item.FindingId == "recent-failure-evidence");
            Assert.Contains("Windows 应用事件仅扫描安全上限内条目", finding.Summary,
                StringComparison.Ordinal);
            Assert.DoesNotContain(report.Findings,
                item => item.FindingId == "failure-evidence-coverage-limited");
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
            OperationsTriageAction recovery = Assert.Single(channelFinding.Actions,
                action => action.ActionId == OperationsTriageActionIds.RequestMessageChannelRecovery);
            Assert.Equal(OperationsRiskLevels.ApprovalRequired, recovery.RiskLevel);
            Assert.True(recovery.RequiresConfirmation);
            Assert.False(recovery.RequiresLocalCoSign);
            Assert.Equal("approval-workflow", recovery.Kind);
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
            Assert.DoesNotContain(report.Findings.SelectMany(item => item.Actions),
                action => action.ActionId == OperationsTriageActionIds.RequestMessageChannelRecovery);
            OperationsTriageFinding deviceFinding = Assert.Single(report.Findings,
                item => item.FindingId == "inspection-devices-attention");
            Assert.Contains("消息通道当前正常", deviceFinding.Summary, StringComparison.Ordinal);
            Assert.Contains("未初始化 1 台", deviceFinding.Summary, StringComparison.Ordinal);
            Assert.Equal(1, report.DeviceUnavailableCount);
            Assert.Equal(1, report.DeviceUninitializedCount);
        }

        [Fact]
        public void UnconfiguredMessageChannelDoesNotRecommendRecovery()
        {
            OperationsTriageReport report = OperationsTriageService.Build(
                new OperationsLogDigest { Available = true },
                new OperationsDesktopState(true, true, true, "Normal"),
                0,
                messageChannel: OperationsMessageChannelHealthSnapshotFactory.Create(
                    new OperationsMessageChannelObservation(false, false, 0, 0)));

            OperationsTriageFinding finding = Assert.Single(report.Findings,
                item => item.FindingId == "message-channel-attention");
            Assert.Contains(finding.Actions,
                action => action.ActionId == OperationsTriageActionIds.ViewMessageChannelHealth);
            Assert.DoesNotContain(finding.Actions,
                action => action.ActionId == OperationsTriageActionIds.RequestMessageChannelRecovery);
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

        private static void AssertReadOnlyDetailAction(
            OperationsTriageReport report,
            string findingId,
            string actionId)
        {
            OperationsTriageFinding finding = Assert.Single(report.Findings,
                item => item.FindingId == findingId);
            OperationsTriageAction action = Assert.Single(finding.Actions);
            Assert.Equal(actionId, action.ActionId);
            Assert.Equal("client-navigation", action.Kind);
            Assert.Equal(OperationsRiskLevels.ReadOnly, action.RiskLevel);
            Assert.False(action.RequiresConfirmation);
            Assert.False(action.RequiresLocalCoSign);
        }
    }
}
