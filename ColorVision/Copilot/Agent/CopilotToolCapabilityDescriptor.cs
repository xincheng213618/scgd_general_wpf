using System;

namespace ColorVision.Copilot
{
    public enum CopilotToolAuditArgumentMode
    {
        RedactedSummary,
        NamesOnly,
    }

    public enum CopilotToolEvidenceMode
    {
        None,
        Summary,
        RedactedExcerpt,
    }

    public sealed record CopilotToolCapabilityDescriptor
    {
        public static readonly TimeSpan DefaultExecutionTimeout = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan MaximumExecutionTimeout = TimeSpan.FromMinutes(10);

        public CopilotToolAccess Access { get; init; }

        public CopilotToolRiskLevel RiskLevel { get; init; }

        public CopilotToolApprovalMode ApprovalMode { get; init; }

        public CopilotToolIdempotency Idempotency { get; init; }

        public CopilotToolConcurrencyMode ConcurrencyMode { get; init; }

        public TimeSpan ExecutionTimeout { get; init; } = DefaultExecutionTimeout;

        public CopilotToolAuditArgumentMode AuditArgumentMode { get; init; } = CopilotToolAuditArgumentMode.RedactedSummary;

        public CopilotToolEvidenceMode EvidenceMode { get; init; }

        public CopilotToolConcurrencyMode EffectiveConcurrencyMode => Access == CopilotToolAccess.Write
            || Idempotency != CopilotToolIdempotency.Idempotent
                ? CopilotToolConcurrencyMode.Exclusive
                : ConcurrencyMode;

        public TimeSpan EffectiveExecutionTimeout => ExecutionTimeout <= TimeSpan.Zero
            ? DefaultExecutionTimeout
            : ExecutionTimeout > MaximumExecutionTimeout ? MaximumExecutionTimeout : ExecutionTimeout;

        public bool RequiresNativeApproval => ApprovalMode == CopilotToolApprovalMode.Always;

        public static CopilotToolCapabilityDescriptor ReadOnly(
            TimeSpan? executionTimeout = null,
            CopilotToolAuditArgumentMode auditArgumentMode = CopilotToolAuditArgumentMode.RedactedSummary,
            CopilotToolEvidenceMode evidenceMode = CopilotToolEvidenceMode.Summary)
        {
            return new CopilotToolCapabilityDescriptor
            {
                Access = CopilotToolAccess.ReadOnly,
                RiskLevel = CopilotToolRiskLevel.Low,
                ApprovalMode = CopilotToolApprovalMode.Never,
                Idempotency = CopilotToolIdempotency.Idempotent,
                ConcurrencyMode = CopilotToolConcurrencyMode.SharedRead,
                ExecutionTimeout = executionTimeout ?? DefaultExecutionTimeout,
                AuditArgumentMode = auditArgumentMode,
                EvidenceMode = evidenceMode,
            };
        }

        public static CopilotToolCapabilityDescriptor ProtectedWrite(
            CopilotToolIdempotency idempotency,
            TimeSpan? executionTimeout = null,
            CopilotToolAuditArgumentMode auditArgumentMode = CopilotToolAuditArgumentMode.RedactedSummary)
        {
            return new CopilotToolCapabilityDescriptor
            {
                Access = CopilotToolAccess.Write,
                RiskLevel = CopilotToolRiskLevel.High,
                ApprovalMode = CopilotToolApprovalMode.Always,
                Idempotency = idempotency,
                ConcurrencyMode = CopilotToolConcurrencyMode.Exclusive,
                ExecutionTimeout = executionTimeout ?? DefaultExecutionTimeout,
                AuditArgumentMode = auditArgumentMode,
                EvidenceMode = CopilotToolEvidenceMode.None,
            };
        }

        internal void Validate(string toolName)
        {
            if (!Enum.IsDefined(Access)
                || !Enum.IsDefined(RiskLevel)
                || !Enum.IsDefined(ApprovalMode)
                || !Enum.IsDefined(Idempotency)
                || !Enum.IsDefined(ConcurrencyMode)
                || !Enum.IsDefined(AuditArgumentMode)
                || !Enum.IsDefined(EvidenceMode))
            {
                throw new ArgumentException($"Copilot tool '{toolName}' has invalid capability metadata.");
            }

            if (Access == CopilotToolAccess.Write
                && RiskLevel == CopilotToolRiskLevel.High
                && ApprovalMode == CopilotToolApprovalMode.Never)
            {
                throw new ArgumentException($"Copilot tool '{toolName}' is a high-risk write capability without approval.");
            }

            if (Access == CopilotToolAccess.Write && EvidenceMode != CopilotToolEvidenceMode.None)
            {
                throw new ArgumentException($"Copilot write tool '{toolName}' cannot persist evidence artifacts.");
            }
        }
    }
}
