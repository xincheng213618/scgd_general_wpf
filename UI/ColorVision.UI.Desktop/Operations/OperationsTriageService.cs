namespace ColorVision.UI.Desktop.Operations
{
    public static class OperationsTriageActionIds
    {
        public const string ViewRecentEvents = "triage.events.view";
        public const string ShowMainWindow = "triage.window.show";
        public const string ReviewJobs = "triage.jobs.review";
        public const string ViewServiceHealth = "triage.services.view";
        public const string RequestMqttRestart = "triage.mqtt.restart.request";
        public const string ViewDeviceHealth = "triage.devices.view";
        public const string ViewMessageChannelHealth = "triage.messaging.view";
        public const string RequestMessageChannelRecovery = "triage.messaging.reconnect.request";
        public const string ViewFailureEvidence = "triage.failures.view";
    }

    public sealed class OperationsTriageAction
    {
        public string ActionId { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Kind { get; init; } = string.Empty;
        public string RiskLevel { get; init; } = OperationsRiskLevels.ReadOnly;
        public string Description { get; init; } = string.Empty;
        public bool RequiresConfirmation { get; init; }
        public bool RequiresLocalCoSign { get; init; }
    }

    public sealed class OperationsTriageFinding
    {
        public string FindingId { get; init; } = string.Empty;
        public string Severity { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public int EvidenceCount { get; init; }
        public DateTimeOffset? LatestAt { get; init; }
        public IReadOnlyList<OperationsTriageAction> Actions { get; init; } = [];
    }

    public sealed class OperationsTriageReport
    {
        public string State { get; init; } = "healthy";
        public string Summary { get; init; } = string.Empty;
        public int CriticalCount { get; init; }
        public int ErrorCount { get; init; }
        public int WarningCount { get; init; }
        public int PendingJobCount { get; init; }
        public int DeviceTotalCount { get; init; }
        public int DeviceReadyCount { get; init; }
        public int DeviceBusyCount { get; init; }
        public int DeviceClosedCount { get; init; }
        public int DeviceUnavailableCount { get; init; }
        public int DeviceAttentionCount { get; init; }
        public int DeviceOfflineCount { get; init; }
        public int DeviceUninitializedCount { get; init; }
        public int DeviceUnauthorizedCount { get; init; }
        public int DeviceUnclassifiedUnavailableCount { get; init; }
        public string MessageChannelState { get; init; } = OperationsMessageChannelStates.Unavailable;
        public bool MessageChannelConnected { get; init; }
        public bool MessageChannelSubscriptionReady { get; init; }
        public int MessageChannelRegisteredSubscriptionCount { get; init; }
        public int MessageChannelActiveSubscriptionCount { get; init; }
        public int FailureEventCount { get; init; }
        public int CrashCount { get; init; }
        public int HangCount { get; init; }
        public int FailureDumpCount { get; init; }
        public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
        public IReadOnlyList<OperationsTriageFinding> Findings { get; init; } = [];
        public string SafetyNotice { get; init; } =
            "建议仅引用有界脱敏摘要。消息通道恢复、固定 MQTT 服务重启、ColorVision 应用重启、脱敏诊断包与单次主窗口快照由已配对手机明确确认后执行；支持会话仍需电脑端本机同意。";
    }

    public static class OperationsTriageService
    {
        public static OperationsTriageReport Build(
            OperationsLogDigest digest,
            OperationsDesktopState desktop,
            int pendingJobCount,
            OperationsServiceHealthReport? serviceHealth = null,
            OperationsDeviceHealthSnapshot? deviceHealth = null,
            OperationsMessageChannelHealthSnapshot? messageChannel = null,
            OperationsFailureEvidenceSnapshot? failureEvidence = null)
        {
            int boundedPendingJobCount = Math.Max(0, pendingJobCount);
            List<OperationsTriageFinding> findings = [];

            if (serviceHealth != null)
                AddServiceHealthFindings(serviceHealth, findings);
            if (messageChannel != null)
                AddMessageChannelFinding(messageChannel, findings);
            if (deviceHealth != null)
                AddDeviceHealthFindings(deviceHealth, messageChannel, findings);
            if (failureEvidence != null)
                AddFailureEvidenceFinding(failureEvidence, findings);
            AddLogFindings(digest, findings);
            AddMessageServiceFinding(digest, serviceHealth, findings);
            AddDesktopFinding(desktop, findings);
            AddPendingJobFinding(boundedPendingJobCount, findings);

            string state = digest.CriticalCount > 0
                ? "critical"
                : findings.Count > 0 ? "attention" : "healthy";
            return new OperationsTriageReport
            {
                State = state,
                Summary = findings.Count == 0
                    ? "当前有界证据中没有需要处理的项目。"
                    : $"发现 {findings.Count} 项需要关注的状态；请先查看证据，再选择建议动作。",
                CriticalCount = digest.CriticalCount,
                ErrorCount = digest.ErrorCount,
                WarningCount = digest.WarningCount,
                PendingJobCount = boundedPendingJobCount,
                DeviceTotalCount = Math.Max(0, deviceHealth?.TotalCount ?? 0),
                DeviceReadyCount = Math.Max(0, deviceHealth?.ReadyCount ?? 0),
                DeviceBusyCount = Math.Max(0, deviceHealth?.BusyCount ?? 0),
                DeviceClosedCount = Math.Max(0, deviceHealth?.ClosedCount ?? 0),
                DeviceUnavailableCount = Math.Max(0, deviceHealth?.UnavailableCount ?? 0),
                DeviceAttentionCount = Math.Max(0, deviceHealth?.AttentionCount ?? 0),
                DeviceOfflineCount = Math.Max(0, deviceHealth?.OfflineCount ?? 0),
                DeviceUninitializedCount = Math.Max(0, deviceHealth?.UninitializedCount ?? 0),
                DeviceUnauthorizedCount = Math.Max(0, deviceHealth?.UnauthorizedCount ?? 0),
                DeviceUnclassifiedUnavailableCount = Math.Max(0, deviceHealth?.UnclassifiedUnavailableCount ?? 0),
                MessageChannelState = messageChannel?.State ?? OperationsMessageChannelStates.Unavailable,
                MessageChannelConnected = messageChannel?.Connected == true,
                MessageChannelSubscriptionReady = messageChannel?.SubscriptionReady == true,
                MessageChannelRegisteredSubscriptionCount = Math.Max(0, messageChannel?.RegisteredSubscriptionCount ?? 0),
                MessageChannelActiveSubscriptionCount = Math.Max(0, messageChannel?.ActiveSubscriptionCount ?? 0),
                FailureEventCount = Math.Max(0, failureEvidence?.FailureEventCount ?? 0),
                CrashCount = Math.Max(0, failureEvidence?.CrashCount ?? 0),
                HangCount = Math.Max(0, failureEvidence?.HangCount ?? 0),
                FailureDumpCount = Math.Max(0, failureEvidence?.DumpCount ?? 0),
                Findings = findings,
            };
        }

        private static void AddFailureEvidenceFinding(
            OperationsFailureEvidenceSnapshot failureEvidence,
            List<OperationsTriageFinding> findings)
        {
            if (!failureEvidence.Available)
            {
                findings.Add(new OperationsTriageFinding
                {
                    FindingId = "failure-evidence-unavailable",
                    Severity = "info",
                    Category = "failure-evidence",
                    Title = "崩溃与卡死证据暂不可用",
                    Summary = "当前无法读取 Windows 应用事件和本机转储；这表示证据来源不可用，不能据此判断近期没有故障。",
                    Actions = [ViewFailureEvidenceAction()],
                });
                return;
            }

            string coverageSummary = FailureEvidenceCoverageSummary(failureEvidence);
            if (!failureEvidence.HasEvidence)
            {
                if (coverageSummary.Length == 0)
                    return;

                findings.Add(new OperationsTriageFinding
                {
                    FindingId = "failure-evidence-coverage-limited",
                    Severity = "info",
                    Category = "failure-evidence",
                    Title = "崩溃与卡死证据覆盖不完整",
                    Summary = $"当前可读取来源中未发现近期故障线索。{coverageSummary}不能据此确认最近 {failureEvidence.WindowDays} 天没有故障。",
                    Actions = [ViewFailureEvidenceAction()],
                });
                return;
            }

            List<string> evidence = [];
            AddEvidence(evidence, "应用崩溃", failureEvidence.CrashCount);
            AddEvidence(evidence, "应用卡死", failureEvidence.HangCount);
            AddEvidence(evidence, ".NET 运行时失败", failureEvidence.ManagedRuntimeFailureCount);
            AddEvidence(evidence, "Windows 错误报告", failureEvidence.WindowsErrorReportCount);
            AddEvidence(evidence, "本机转储", failureEvidence.DumpCount, "个");
            findings.Add(new OperationsTriageFinding
            {
                FindingId = "recent-failure-evidence",
                Severity = failureEvidence.CrashCount > 0 || failureEvidence.HangCount > 0 ? "error" : "warning",
                Category = "failure-evidence",
                Title = "最近存在崩溃或卡死线索",
                Summary = $"最近 {failureEvidence.WindowDays} 天发现{string.Join("、", evidence)}。这些是聚合线索，可能包含同一次故障的重复记录；{coverageSummary}请结合发生时间在电脑端继续定位。",
                EvidenceCount = Math.Min(999, failureEvidence.FailureEventCount + failureEvidence.DumpCount),
                LatestAt = failureEvidence.LatestEvidenceAt,
                Actions = [ViewFailureEvidenceAction()],
            });
        }

        private static string FailureEvidenceCoverageSummary(
            OperationsFailureEvidenceSnapshot failureEvidence)
        {
            List<string> limitations = [];
            if (!failureEvidence.EventLogAvailable)
                limitations.Add("Windows 应用事件不可读取");
            else if (failureEvidence.EventScanLimited)
                limitations.Add("Windows 应用事件仅扫描安全上限内条目");
            if (!failureEvidence.DumpFolderAvailable)
                limitations.Add("本机转储目录不可读取");
            else if (failureEvidence.DumpScanLimited)
                limitations.Add("本机转储仅扫描安全上限内文件");
            return limitations.Count == 0
                ? string.Empty
                : $"证据覆盖有限：{string.Join("、", limitations)}。";
        }

        private static void AddEvidence(List<string> evidence, string title, int count, string unit = "条")
        {
            if (count > 0)
                evidence.Add($"{title} {count} {unit}");
        }

        private static void AddDeviceHealthFindings(
            OperationsDeviceHealthSnapshot deviceHealth,
            OperationsMessageChannelHealthSnapshot? messageChannel,
            List<OperationsTriageFinding> findings)
        {
            if (!deviceHealth.Available)
            {
                findings.Add(new OperationsTriageFinding
                {
                    FindingId = "device-health-unavailable",
                    Severity = "info",
                    Category = "devices",
                    Title = "检测设备状态暂不可用",
                    Summary = "当前无法取得设备注册表的类别级运行状态汇总；不会据此执行任何设备操作。",
                    Actions = [ViewDeviceHealthAction()],
                });
                return;
            }
            if (deviceHealth.AttentionCount == 0)
                return;

            string correlationSummary = messageChannel is { Available: true, AttentionRequired: true }
                ? "消息通道当前未就绪，这些设备状态可能由通道问题引起；请先处理消息通道。"
                : messageChannel is { Available: true, Connected: true, SubscriptionReady: true }
                    ? "消息通道当前正常，优先在电脑端核对具体设备进程、授权和运行状态。"
                    : "请先查看类别级汇总，再到电脑端核对具体设备。";
            string unavailableReasonSummary = DeviceUnavailableReasonSummary(deviceHealth);
            findings.Add(new OperationsTriageFinding
            {
                FindingId = "inspection-devices-attention",
                Severity = "warning",
                Category = "devices",
                Title = "检测设备存在不可用或未知状态",
                Summary = $"已加载设备 {deviceHealth.TotalCount} 台，其中不可用 {deviceHealth.UnavailableCount} 台、状态未知 {deviceHealth.UnknownCount} 台。{unavailableReasonSummary}{correlationSummary}",
                EvidenceCount = deviceHealth.AttentionCount,
                LatestAt = deviceHealth.ObservedAt,
                Actions = [ViewDeviceHealthAction()],
            });
        }

        private static string DeviceUnavailableReasonSummary(OperationsDeviceHealthSnapshot deviceHealth)
        {
            if (deviceHealth.UnavailableCount == 0)
                return string.Empty;

            List<string> reasons = [];
            AddReason(reasons, "离线", deviceHealth.OfflineCount);
            AddReason(reasons, "未初始化", deviceHealth.UninitializedCount);
            AddReason(reasons, "未授权", deviceHealth.UnauthorizedCount);
            AddReason(reasons, "未归类", deviceHealth.UnclassifiedUnavailableCount);
            return reasons.Count == 0 ? string.Empty : $"不可用原因：{string.Join("、", reasons)}。";
        }

        private static void AddReason(List<string> reasons, string title, int count)
        {
            if (count > 0)
                reasons.Add($"{title} {count} 台");
        }

        private static void AddMessageChannelFinding(
            OperationsMessageChannelHealthSnapshot messageChannel,
            List<OperationsTriageFinding> findings)
        {
            if (!messageChannel.Available)
            {
                findings.Add(new OperationsTriageFinding
                {
                    FindingId = "message-channel-health-unavailable",
                    Severity = "info",
                    Category = "message-channel",
                    Title = "消息通道状态暂不可用",
                    Summary = "当前无法取得 ColorVision 消息客户端的脱敏连接状态；不会据此自动重连或重启。",
                    Actions = [ViewMessageChannelHealthAction()],
                });
                return;
            }
            if (!messageChannel.AttentionRequired)
                return;

            string title;
            string summary;
            string severity;
            switch (messageChannel.State)
            {
                case OperationsMessageChannelStates.Unconfigured:
                    title = "消息通道尚未配置";
                    summary = "ColorVision 当前没有有效的消息服务连接配置；请在电脑端复核配置。";
                    severity = "warning";
                    break;
                case OperationsMessageChannelStates.Disconnected:
                    title = "ColorVision 未连接消息服务";
                    summary = "消息服务进程可能仍在运行，但 ColorVision 客户端当前没有建立连接；设备状态可能因此不可用。";
                    severity = "error";
                    break;
                default:
                    title = "消息订阅尚未完全恢复";
                    summary = $"ColorVision 已连接消息服务，但只恢复了 {messageChannel.ActiveSubscriptionCount}/{messageChannel.RegisteredSubscriptionCount} 个已登记订阅；请稍后刷新或在电脑端复核。";
                    severity = "warning";
                    break;
            }
            List<OperationsTriageAction> actions =
            [
                ViewMessageChannelHealthAction(),
            ];
            if (messageChannel.State is OperationsMessageChannelStates.Disconnected or OperationsMessageChannelStates.Degraded)
            {
                actions.Add(new OperationsTriageAction
                {
                    ActionId = OperationsTriageActionIds.RequestMessageChannelRecovery,
                    Title = "恢复消息通道",
                    Kind = "approval-workflow",
                    RiskLevel = OperationsRiskLevels.ApprovalRequired,
                    Description = "手机确认后只重建当前已配置的 ColorVision 消息连接并恢复已登记订阅；健康通道保持不动。",
                    RequiresConfirmation = true,
                    RequiresLocalCoSign = false,
                });
            }
            findings.Add(new OperationsTriageFinding
            {
                FindingId = "message-channel-attention",
                Severity = severity,
                Category = "message-channel",
                Title = title,
                Summary = summary,
                EvidenceCount = 1,
                LatestAt = messageChannel.ObservedAt,
                Actions = actions,
            });
        }

        private static void AddServiceHealthFindings(
            OperationsServiceHealthReport serviceHealth,
            List<OperationsTriageFinding> findings)
        {
            if (!serviceHealth.Available)
            {
                findings.Add(new OperationsTriageFinding
                {
                    FindingId = "service-health-unavailable",
                    Severity = "info",
                    Category = "services",
                    Title = "白名单服务状态暂不可用",
                    Summary = "当前无法取得 Windows 服务控制管理器状态；不会仅凭日志自动建议维护动作。",
                    Actions = [ViewServiceHealthAction()],
                });
                return;
            }

            foreach (OperationsServiceHealthItem service in serviceHealth.Services.Where(item => !item.Healthy))
            {
                List<OperationsTriageAction> actions = [ViewServiceHealthAction()];
                if (service.ServiceId == OperationsServiceIds.MqttBroker
                    && service.MaintenanceSupported
                    && service.Status is "stopped" or "paused")
                {
                    actions.Add(RestartMqttAction());
                }
                findings.Add(new OperationsTriageFinding
                {
                    FindingId = $"service-health-{service.ServiceId}",
                    Severity = service.Status is "stopped" or "paused" ? "error" : "warning",
                    Category = "services",
                    Title = $"{service.Title}状态异常",
                    Summary = ServiceHealthSummary(service),
                    EvidenceCount = 1,
                    LatestAt = service.ObservedAt,
                    Actions = actions,
                });
            }
        }

        private static void AddLogFindings(OperationsLogDigest digest, List<OperationsTriageFinding> findings)
        {
            if (!digest.Available)
            {
                findings.Add(new OperationsTriageFinding
                {
                    FindingId = "application-log-unavailable",
                    Severity = "info",
                    Category = "diagnostics",
                    Title = "近期日志摘要暂不可用",
                    Summary = "电脑端当前没有可读取的应用日志摘要。其他运行状态仍可继续检查。",
                    Actions = [ViewEventsAction()],
                });
                return;
            }

            int abnormalCount = digest.CriticalCount + digest.ErrorCount + digest.WarningCount;
            if (abnormalCount == 0)
                return;

            string severity = digest.CriticalCount > 0 ? "critical" : digest.ErrorCount > 0 ? "error" : "warning";
            findings.Add(new OperationsTriageFinding
            {
                FindingId = "recent-abnormal-events",
                Severity = severity,
                Category = "diagnostics",
                Title = digest.CriticalCount > 0 ? "近期存在严重事件" : digest.ErrorCount > 0 ? "近期存在错误事件" : "近期存在警告事件",
                Summary = $"有界日志摘要包含严重 {digest.CriticalCount} 条、错误 {digest.ErrorCount} 条、警告 {digest.WarningCount} 条。",
                EvidenceCount = abnormalCount,
                LatestAt = Latest(digest.RecentEvents),
                Actions = [ViewEventsAction()],
            });
        }

        private static void AddMessageServiceFinding(
            OperationsLogDigest digest,
            OperationsServiceHealthReport? serviceHealth,
            List<OperationsTriageFinding> findings)
        {
            OperationsAlert[] events = digest.RecentEvents
                .Where(item => item.Source == "消息服务" && item.Severity is ("warning" or "error" or "critical"))
                .ToArray();
            if (events.Length == 0)
                return;

            OperationsServiceHealthItem? mqtt = serviceHealth?.Services.FirstOrDefault(
                item => item.ServiceId == OperationsServiceIds.MqttBroker);
            List<OperationsTriageAction> actions = [ViewEventsAction()];
            string currentState = mqtt == null || !serviceHealth!.Available
                ? "当前服务状态未知，不建议仅凭日志重启。"
                : mqtt.Status == "running"
                    ? "Windows 服务控制管理器显示 MQTT 当前正在运行，请先查看事件确认是否仍在发生。"
                    : mqtt.Status == "not_applicable"
                        ? "当前使用的不是本机 MQTT 服务，请查看事件并在电脑端复核连接配置。"
                        : "Windows 服务控制管理器已确认服务异常，可在复核证据后申请维护。";
            findings.Add(new OperationsTriageFinding
            {
                FindingId = "message-service-events",
                Severity = events.Any(item => item.Severity is "error" or "critical") ? "error" : "warning",
                Category = "message-service",
                Title = "消息服务需要复核",
                Summary = $"近期脱敏摘要中有 {events.Length} 条消息服务异常事件。{currentState}",
                EvidenceCount = events.Length,
                LatestAt = Latest(events),
                Actions = actions,
            });
        }

        private static void AddDesktopFinding(OperationsDesktopState desktop, List<OperationsTriageFinding> findings)
        {
            if (!desktop.DispatcherAvailable || !desktop.Exists)
            {
                findings.Add(new OperationsTriageFinding
                {
                    FindingId = "desktop-window-unavailable",
                    Severity = "warning",
                    Category = "desktop",
                    Title = "电脑主窗口不可用",
                    Summary = "当前无法取得 ColorVision 主窗口；请在电脑端确认应用启动状态。",
                });
                return;
            }

            if (desktop.IsVisible && !string.Equals(desktop.WindowState, "Minimized", StringComparison.OrdinalIgnoreCase))
                return;

            findings.Add(new OperationsTriageFinding
            {
                FindingId = "desktop-window-hidden",
                Severity = "info",
                Category = "desktop",
                Title = "电脑主窗口当前未显示",
                Summary = "可执行已审计的低风险动作，将现有主窗口恢复并置于前台。",
                EvidenceCount = 1,
                Actions =
                [
                    new OperationsTriageAction
                    {
                        ActionId = OperationsTriageActionIds.ShowMainWindow,
                        Title = "显示电脑主窗口",
                        Kind = "immediate-audited",
                        RiskLevel = OperationsRiskLevels.LowRisk,
                        Description = "只显示现有 ColorVision 主窗口，不启动程序或执行任意命令。",
                    },
                ],
            });
        }

        private static void AddPendingJobFinding(int pendingJobCount, List<OperationsTriageFinding> findings)
        {
            if (pendingJobCount == 0)
                return;

            findings.Add(new OperationsTriageFinding
            {
                FindingId = "pending-operations-jobs",
                Severity = "info",
                Category = "approvals",
                Title = "存在待处理运维作业",
                Summary = $"当前有 {pendingJobCount} 个作业等待手机决定、必要的电脑端本机共签或执行结果。",
                EvidenceCount = pendingJobCount,
                Actions =
                [
                    new OperationsTriageAction
                    {
                        ActionId = OperationsTriageActionIds.ReviewJobs,
                        Title = "查看作业与审批",
                        Kind = "client-navigation",
                        RiskLevel = OperationsRiskLevels.ReadOnly,
                        Description = "只打开手机端作业列表；批准操作仍需单独确认。",
                    },
                ],
            });
        }

        private static OperationsTriageAction ViewEventsAction() => new()
        {
            ActionId = OperationsTriageActionIds.ViewRecentEvents,
            Title = "查看近期脱敏事件",
            Kind = "client-navigation",
            RiskLevel = OperationsRiskLevels.ReadOnly,
            Description = "打开有界脱敏日志摘要，不读取原始日志。",
        };

        private static OperationsTriageAction ViewDeviceHealthAction() => new()
        {
            ActionId = OperationsTriageActionIds.ViewDeviceHealth,
            Title = "查看设备状态概览",
            Kind = "client-navigation",
            RiskLevel = OperationsRiskLevels.ReadOnly,
            Description = "只查看固定类别的规范化运行状态计数，不返回设备身份，也不执行重连或重启。",
        };

        private static OperationsTriageAction ViewMessageChannelHealthAction() => new()
        {
            ActionId = OperationsTriageActionIds.ViewMessageChannelHealth,
            Title = "查看消息通道健康",
            Kind = "client-navigation",
            RiskLevel = OperationsRiskLevels.ReadOnly,
            Description = "只查看脱敏连接状态、订阅计数和聚合活动时间，不执行重连、重启或任意目标操作。",
        };

        private static OperationsTriageAction ViewFailureEvidenceAction() => new()
        {
            ActionId = OperationsTriageActionIds.ViewFailureEvidence,
            Title = "查看崩溃与卡死线索",
            Kind = "client-navigation",
            RiskLevel = OperationsRiskLevels.ReadOnly,
            Description = "只查看最近七天固定类别的计数与聚合时间，不返回事件正文、文件名、路径或转储内容。",
        };

        private static OperationsTriageAction RestartMqttAction() => new()
        {
            ActionId = OperationsTriageActionIds.RequestMqttRestart,
            Title = "确认并重启 MQTT",
            Kind = "approval-workflow",
            RiskLevel = OperationsRiskLevels.Privileged,
            Description = "仅通过 ServiceHost 重启固定 Mosquitto 服务；已配对手机确认后立即执行。",
            RequiresConfirmation = true,
            RequiresLocalCoSign = false,
        };

        private static OperationsTriageAction ViewServiceHealthAction() => new()
        {
            ActionId = OperationsTriageActionIds.ViewServiceHealth,
            Title = "查看白名单服务状态",
            Kind = "client-navigation",
            RiskLevel = OperationsRiskLevels.ReadOnly,
            Description = "只查看固定 ColorVision 后台服务与 MQTT 服务的规范化状态、来源和观测时间，不执行维护。",
        };

        private static string ServiceHealthSummary(OperationsServiceHealthItem service) => service.Status switch
        {
            "stopped" => "Windows 服务控制管理器确认该服务已停止。",
            "paused" => "Windows 服务控制管理器确认该服务已暂停。",
            "not_installed" => "Windows 服务控制管理器未找到该固定白名单服务。",
            "start_pending" or "stop_pending" or "continue_pending" or "pause_pending" =>
                "该服务正在切换状态，请稍后刷新后再决定是否处理。",
            _ => "当前无法确认该固定白名单服务的运行状态，请在电脑端复核。",
        };

        private static DateTimeOffset? Latest(IEnumerable<OperationsAlert> events) =>
            events.Select(item => (DateTimeOffset?)item.OccurredAt).Max();
    }
}
