using System.Text.Json.Serialization;

namespace ColorVision.UI.Desktop.Operations
{
    public static class OperationsRiskLevels
    {
        public const string ReadOnly = "read-only";
        public const string LowRisk = "low-risk";
        public const string ApprovalRequired = "approval-required";
        public const string Privileged = "privileged";
    }

    public sealed class OperationsCapabilityDescriptor
    {
        public string SchemaVersion { get; init; } = OperationsCapabilityCatalog.SchemaVersion;

        public string Id { get; init; } = string.Empty;

        public string Version { get; init; } = "1.0.0";

        public string Title { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string Category { get; init; } = string.Empty;

        public string Provider { get; init; } = string.Empty;

        public string RiskLevel { get; init; } = OperationsRiskLevels.ReadOnly;

        public string Permission { get; init; } = string.Empty;

        public IReadOnlyList<string> DiscoverableOn { get; init; } = Array.Empty<string>();

        public object InputSchema { get; init; } = new { type = "object", additionalProperties = false };

        public object OutputSchema { get; init; } = new { type = "object" };

        public int TimeoutMs { get; init; } = 5000;

        public string Idempotency { get; init; } = "safe";

        public bool SupportsCancellation { get; init; }

        public OperationsApprovalPolicy Approval { get; init; } = OperationsApprovalPolicy.None;

        public OperationsAuditPolicy Audit { get; init; } = new();

        public OperationsExecutionPolicy Execution { get; init; } = new();

        public string RollbackCapability { get; init; } = string.Empty;

        public IReadOnlyList<string> Evidence { get; init; } = Array.Empty<string>();

        public bool Available { get; init; }

        public string BlockedReason { get; init; } = string.Empty;
    }

    public sealed class OperationsApprovalPolicy
    {
        public static OperationsApprovalPolicy None { get; } = new();

        public string Mode { get; init; } = "none";

        public int TtlSeconds { get; init; }

        public bool SingleUse { get; init; }

        public bool RequiresBiometric { get; init; }

        public bool RequiresLocalCoSign { get; init; }
    }

    public sealed class OperationsAuditPolicy
    {
        public bool Required { get; init; } = true;

        public IReadOnlyList<string> Redact { get; init; } = Array.Empty<string>();

        public int RetentionDays { get; init; } = 30;
    }

    public sealed class OperationsExecutionPolicy
    {
        public string Target { get; init; } = "desktop";

        public string BrokerCapability { get; init; } = string.Empty;
    }
}
