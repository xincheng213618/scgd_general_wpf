using ColorVision.UI.Desktop.Operations;

namespace ColorVision.UI.Tests
{
    public sealed class OperationsTriageServiceTests
    {
        [Fact]
        public void BuildReturnsOnlyFixedActionsAndPreservesPrivilegedCoSignBoundary()
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
                ServiceHealth("stopped", healthy: false, maintenanceSupported: true));

            Assert.Equal("critical", report.State);
            Assert.Contains(report.Findings, item => item.FindingId == "recent-abnormal-events");
            Assert.Contains(report.Findings, item => item.FindingId == "message-service-events");
            Assert.Contains(report.Findings, item => item.FindingId == "desktop-window-hidden");
            Assert.Contains(report.Findings, item => item.FindingId == "pending-operations-jobs");

            OperationsTriageAction[] actions = report.Findings.SelectMany(item => item.Actions).ToArray();
            string[] allowedActionIds =
            [
                OperationsTriageActionIds.ViewRecentEvents,
                OperationsTriageActionIds.ShowMainWindow,
                OperationsTriageActionIds.ReviewJobs,
                OperationsTriageActionIds.RequestMqttRestart,
            ];
            Assert.All(actions, action => Assert.Contains(action.ActionId, allowedActionIds));
            OperationsTriageAction restart = Assert.Single(actions,
                action => action.ActionId == OperationsTriageActionIds.RequestMqttRestart);
            Assert.Equal(OperationsRiskLevels.Privileged, restart.RiskLevel);
            Assert.True(restart.RequiresConfirmation);
            Assert.True(restart.RequiresLocalCoSign);
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
