using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public enum CopilotToolAccess
    {
        ReadOnly,
        Write,
    }

    public enum CopilotToolRiskLevel
    {
        Low,
        Medium,
        High,
    }

    public enum CopilotToolApprovalMode
    {
        Never,
        Conditional,
        Always,
    }

    public enum CopilotToolIdempotency
    {
        Unknown,
        Idempotent,
        NonIdempotent,
    }

    public enum CopilotToolConcurrencyMode
    {
        SharedRead,
        Exclusive,
    }

    public interface ICopilotTool
    {
        string Name { get; }

        string Description { get; }

        CopilotToolCapabilityDescriptor Capability => new()
        {
            Access = Access,
            RiskLevel = RiskLevel,
            ApprovalMode = ApprovalMode,
            Idempotency = Idempotency,
            ConcurrencyMode = ConcurrencyMode,
            ExecutionTimeout = ExecutionTimeout,
            EvidenceMode = EvidenceMode,
            AllowsTemporaryFullAccess = AllowsTemporaryFullAccess,
        };

        CopilotToolAccess Access => CopilotToolAccess.ReadOnly;

        CopilotToolRiskLevel RiskLevel => Access == CopilotToolAccess.ReadOnly ? CopilotToolRiskLevel.Low : CopilotToolRiskLevel.Medium;

        CopilotToolApprovalMode ApprovalMode => Access == CopilotToolAccess.ReadOnly ? CopilotToolApprovalMode.Never : CopilotToolApprovalMode.Conditional;

        CopilotToolIdempotency Idempotency => Access == CopilotToolAccess.ReadOnly ? CopilotToolIdempotency.Idempotent : CopilotToolIdempotency.Unknown;

        CopilotToolConcurrencyMode ConcurrencyMode => Access == CopilotToolAccess.ReadOnly && Idempotency == CopilotToolIdempotency.Idempotent
            ? CopilotToolConcurrencyMode.SharedRead
            : CopilotToolConcurrencyMode.Exclusive;

        CopilotToolEvidenceMode EvidenceMode => Access == CopilotToolAccess.ReadOnly
            ? CopilotToolEvidenceMode.Summary
            : CopilotToolEvidenceMode.None;

        bool AllowsTemporaryFullAccess => false;

        string GetConcurrencyKey(CopilotAgentRequest request, CopilotAgentToolInput toolInput)
        {
            if (!string.IsNullOrWhiteSpace(toolInput.Path))
                return $"path:{toolInput.Path.Trim()}";
            if (!string.IsNullOrWhiteSpace(toolInput.Query))
                return $"tool:{Name}:query:{toolInput.Query.Trim()}";
            return $"tool:{Name}";
        }

        CopilotToolInputSchema InputSchema => CopilotToolInputSchema.OptionalQuery;

        TimeSpan ExecutionTimeout => TimeSpan.FromSeconds(30);

        bool CanHandle(CopilotAgentRequest request);

        Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Marks a tool whose presence in the Agent toolset is determined by runtime capability,
    /// not by matching words in the current user message. The model receives the tool schema
    /// and decides whether to issue a structured function call.
    /// </summary>
    public interface ICopilotAgentDrivenTool : ICopilotTool
    {
        bool IsAvailable(CopilotAgentRequest request);
    }

    /// <summary>
    /// Marks a read-only observation whose identical arguments may be invoked again
    /// while the observed operation is still active. The runtime permits another
    /// attempt only when the prior result advertised a valid progress signature and
    /// that signature changed from the preceding observation.
    /// </summary>
    public interface ICopilotRepeatableObservationTool : ICopilotTool
    {
        int MaximumObservationAttempts { get; }
    }

    /// <summary>
    /// Implemented by tools whose side effect can run after Microsoft Agent Framework has
    /// already collected an explicit approval for the exact function call.
    /// </summary>
    internal interface ICopilotFrameworkApprovedTool : ICopilotTool
    {
        Task<CopilotToolResult> ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken);
    }

    public enum CopilotApprovalReversibility
    {
        Unknown,
        AutomaticUntilExpiry,
        ManualOnly,
        NotReversible,
    }

    public sealed record CopilotToolApprovalPresentation(
        string Title,
        string Description,
        string ImpactSummary = "",
        CopilotApprovalReversibility Reversibility = CopilotApprovalReversibility.Unknown,
        string ReversibilitySummary = "")
    {
        public string ReviewDetails { get; init; } = string.Empty;
    }

    public interface ICopilotFrameworkApprovalPresentation
    {
        CopilotToolApprovalPresentation CreateApprovalPresentation(CopilotAgentToolInput toolInput);
    }

    public interface ICopilotFrameworkContextualApprovalPresentation : ICopilotFrameworkApprovalPresentation
    {
        CopilotToolApprovalPresentation CreateApprovalPresentation(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput);
    }
}
