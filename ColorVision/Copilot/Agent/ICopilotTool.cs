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

        CopilotToolAccess Access => CopilotToolAccess.ReadOnly;

        CopilotToolRiskLevel RiskLevel => Access == CopilotToolAccess.ReadOnly ? CopilotToolRiskLevel.Low : CopilotToolRiskLevel.Medium;

        CopilotToolApprovalMode ApprovalMode => Access == CopilotToolAccess.ReadOnly ? CopilotToolApprovalMode.Never : CopilotToolApprovalMode.Conditional;

        CopilotToolIdempotency Idempotency => Access == CopilotToolAccess.ReadOnly ? CopilotToolIdempotency.Idempotent : CopilotToolIdempotency.Unknown;

        CopilotToolConcurrencyMode ConcurrencyMode => Access == CopilotToolAccess.ReadOnly && Idempotency == CopilotToolIdempotency.Idempotent
            ? CopilotToolConcurrencyMode.SharedRead
            : CopilotToolConcurrencyMode.Exclusive;

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
    /// Implemented by tools whose side effect can run after Microsoft Agent Framework has
    /// already collected an explicit approval for the exact function call.
    /// </summary>
    public interface ICopilotFrameworkApprovedTool : ICopilotTool
    {
        Task<CopilotToolResult> ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken);
    }
}
