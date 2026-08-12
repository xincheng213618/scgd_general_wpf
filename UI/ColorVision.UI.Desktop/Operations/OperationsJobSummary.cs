namespace ColorVision.UI.Desktop.Operations
{
    public sealed class OperationsJobEvidenceSummary
    {
        public bool Available { get; init; }
        public string Kind { get; init; } = "none";
        public string Outcome { get; init; } = "pending";
    }

    public sealed class OperationsJobTimelineItem
    {
        public string Stage { get; init; } = string.Empty;
        public string State { get; init; } = string.Empty;
        public DateTimeOffset? At { get; init; }
    }

    public sealed class OperationsJobSummary
    {
        public string JobId { get; init; } = string.Empty;
        public string CapabilityId { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Target { get; init; } = string.Empty;
        public string RiskLevel { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public bool RequiresLocalCoSign { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
        public OperationsJobEvidenceSummary Evidence { get; init; } = new();
        public IReadOnlyList<OperationsJobTimelineItem> Timeline { get; init; } = [];
    }

    public static class OperationsJobSummaryFactory
    {
        public static OperationsJobSummary Create(OperationsJob job)
        {
            ArgumentNullException.ThrowIfNull(job);
            return new OperationsJobSummary
            {
                JobId = job.JobId,
                CapabilityId = job.CapabilityId,
                Title = Title(job.CapabilityId),
                Target = Target(job.CapabilityId),
                RiskLevel = job.RiskLevel,
                Status = job.Status,
                RequiresLocalCoSign = OperationsWorkStore.RequiresLocalCoSign(job),
                CreatedAt = job.CreatedAt,
                UpdatedAt = job.UpdatedAt,
                Evidence = Evidence(job),
                Timeline = Timeline(job),
            };
        }

        private static IReadOnlyList<OperationsJobTimelineItem> Timeline(OperationsJob job)
        {
            List<OperationsJobTimelineItem> items =
            [
                new() { Stage = "requested", State = "completed", At = job.CreatedAt },
            ];
            items.Add(new OperationsJobTimelineItem
            {
                Stage = "mobile_approval",
                State = job.Status switch
                {
                    "awaiting_mobile_approval" => "pending",
                    "rejected" => "rejected",
                    _ => job.DecisionAt.HasValue ? "approved" : "pending",
                },
                At = job.DecisionAt,
            });
            items.Add(new OperationsJobTimelineItem
            {
                Stage = "local_cosign",
                State = !OperationsWorkStore.RequiresLocalCoSign(job)
                    ? "not_required"
                    : job.Status switch
                {
                    "awaiting_mobile_approval" or "rejected" => "not_started",
                    "awaiting_local_cosign" => "pending",
                    "rejected_local" => "rejected",
                    _ => job.LocalCoSignedAt.HasValue ? "approved" : "not_started",
                },
                At = job.LocalCoSignedAt,
            });
            items.Add(new OperationsJobTimelineItem
            {
                Stage = "execution",
                State = job.Status switch
                {
                    "approved_local" or "approved_mobile" => "pending",
                    "executing" => "in_progress",
                    "completed" => "completed",
                    "failed" => "failed",
                    _ => "not_started",
                },
                At = job.CompletedAt ?? (job.Status is "completed" or "failed" ? job.UpdatedAt : null),
            });
            return items;
        }

        private static OperationsJobEvidenceSummary Evidence(OperationsJob job)
        {
            string evidence = job.ResultEvidenceId ?? string.Empty;
            string kind = evidence switch
            {
                string value when value.StartsWith("servicehost:", StringComparison.Ordinal) => "service-host-receipt",
                string value when value.StartsWith("servicehost_error:", StringComparison.Ordinal) => "service-host-error",
                string value when value.StartsWith(OperationsWindowSnapshotService.EvidencePrefix, StringComparison.Ordinal)
                    => "window-snapshot-receipt",
                string value when value.StartsWith("flow_cancel:", StringComparison.Ordinal)
                    => "flow-cancel-request-receipt",
                "service_not_in_operations_allowlist" => "policy-rejection",
                string value when value.Length == 32 && value.All(char.IsLetterOrDigit) => "diagnostic-bundle-receipt",
                "" => "none",
                _ => "bounded-operation-receipt",
            };
            return new OperationsJobEvidenceSummary
            {
                Available = kind != "none",
                Kind = kind,
                Outcome = job.Status switch
                {
                    "completed" => "success",
                    "failed" or "rejected" or "rejected_local" => "failed",
                    _ => "pending",
                },
            };
        }

        private static string Title(string capabilityId) => capabilityId switch
        {
            "ops.service.restart" => "重启白名单服务",
            "ops.diagnostics.bundle.create" => "生成安全诊断包",
            "ops.window.snapshot.capture" => "采集主窗口安全快照",
            "ops.flow.cancel" => "取消当前检测",
            _ => "运维作业",
        };

        private static string Target(string capabilityId) => capabilityId switch
        {
            "ops.service.restart" => "MQTT 消息服务",
            "ops.diagnostics.bundle.create" => "ColorVision 诊断摘要",
            "ops.window.snapshot.capture" => "ColorVision 主窗口",
            "ops.flow.cancel" => "当前主检测流程",
            _ => "固定运维能力",
        };
    }
}
