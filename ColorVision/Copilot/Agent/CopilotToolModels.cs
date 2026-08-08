using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ColorVision.UI;

namespace ColorVision.Copilot
{
    public sealed class CopilotAgentToolInput
    {
        public static CopilotAgentToolInput Empty { get; } = new();

        public IReadOnlyDictionary<string, object?> Arguments { get; init; } = new Dictionary<string, object?>();

        public string Query { get; init; } = string.Empty;

        public string Path { get; init; } = string.Empty;

        public string Cursor { get; init; } = string.Empty;

        public int? StartLine { get; init; }

        public int? StartColumn { get; init; }

        public int? EndLine { get; init; }

        public string GetStableArgumentsJson()
        {
            if (Arguments.Count == 0)
                return string.Empty;

            var ordered = Arguments
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            return JsonSerializer.Serialize(ordered);
        }
    }

    public sealed class CopilotAgentRequest
    {
        public string ConversationId { get; init; } = string.Empty;

        public string TaskId { get; init; } = string.Empty;

        public string WorkspacePath { get; init; } = string.Empty;

        public string UserText { get; init; } = string.Empty;

        public string TaskIntentText { get; init; } = string.Empty;

        public string ActiveGoalText { get; init; } = string.Empty;

        public CopilotWorkspaceReviewTargetContext? WorkspaceReviewTarget { get; init; }

        public CopilotProfileConfig Profile { get; init; } = null!;

        public IReadOnlyList<CopilotRequestMessage> History { get; init; } = Array.Empty<CopilotRequestMessage>();

        public IReadOnlyList<CopilotAttachmentItem> Attachments { get; init; } = Array.Empty<CopilotAttachmentItem>();

        public IReadOnlyList<CopilotContextItem> ContextItems { get; init; } = Array.Empty<CopilotContextItem>();

        public IReadOnlyList<string> SearchRootPaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> TrustedProjectRootPaths { get; init; } = Array.Empty<string>();

        public string ActiveDocumentPath { get; init; } = string.Empty;

        public string ConfiguredDeveloperInstructions { get; init; } = string.Empty;

        internal CopilotCodexWebSearchMode CodexWebSearchMode { get; init; } =
            CopilotCodexWebSearchMode.Unspecified;

        public IReadOnlyList<CopilotProjectInstructionDocument> ProjectInstructions { get; init; } = Array.Empty<CopilotProjectInstructionDocument>();

        public IReadOnlyList<string> ReadableLocalFilePaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> ReadableLocalDirectoryPaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> WritableLocalRootPaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> WritableLocalFilePaths { get; init; } = Array.Empty<string>();

        public bool PreferBatchReadLocalFiles { get; init; }

        public CopilotShellKind PreferredShell { get; init; } = CopilotShellKind.Auto;

        public CopilotAgentMode Mode { get; init; } = CopilotAgentMode.Auto;

        public CopilotAgentSessionCheckpoint? SessionCheckpoint { get; init; }

        public CopilotAgentRecoveryRequest? Recovery { get; init; }

        public CopilotAgentRunControl? RunControl { get; init; }

        public CopilotAgentRunBudgetDefaults? RunBudgetDefaults { get; init; }

        public CopilotAgentRunBudgetOverride? RunBudgetOverride { get; init; }

        public IReadOnlyDictionary<string, CopilotAgentSkillOverrideState> SkillOverrides { get; init; } = new Dictionary<string, CopilotAgentSkillOverrideState>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, CopilotAgentSkillOverrideState> SkillPathOverrides { get; init; } = new Dictionary<string, CopilotAgentSkillOverrideState>(StringComparer.OrdinalIgnoreCase);

        public CopilotAgentSkillReference? AgentSkillReference { get; init; }

        public IReadOnlyList<CopilotMcpClientServerConfig> ExternalMcpServers { get; init; } = Array.Empty<CopilotMcpClientServerConfig>();

        public bool ForceExternalMcpToolRefresh { get; init; }

        public CopilotAgentAccessContext AccessContext { get; init; } = new();

        internal CopilotExecutionScope RuntimeExecutionScope { get; set; } = CopilotExecutionScope.Empty;

        internal string RuntimeRoleInstructions { get; init; } = string.Empty;

        internal CopilotAgentHarnessFeatures HarnessFeatures { get; init; } = CopilotAgentHarnessFeatures.Full;

        internal CopilotAgentRuntimePurpose RuntimePurpose { get; init; }

        internal IReadOnlyList<string> RequiredSuccessfulToolNames { get; init; } = Array.Empty<string>();

        internal bool RequiresDelegatedWorkspaceEvidence { get; init; }
    }

    public sealed class CopilotDelegatedRunUsage
    {
        public string RoleId { get; init; } = string.Empty;

        public string RunId { get; init; } = string.Empty;

        public string ResumeFromRunId { get; init; } = string.Empty;

        public int RequestTokenBudget { get; init; }

        public long QueueDurationMs { get; init; }

        public CopilotAgentStopReason StopReason { get; init; }

        public int ToolCalls { get; init; }

        public int DeliveredSteeringCount { get; init; }

        public int UndeliveredSteeringCount { get; init; }

        public int PeakEstimatedInputTokens { get; init; }

        public int ProviderRetryCount { get; init; }

        public int ProviderRateLimitRetryCount { get; init; }

        public long ProviderRetryDelayMs { get; init; }

        public int ProviderFirstContentTimeoutCount { get; init; }

        public int ProviderStreamInactivityTimeoutCount { get; init; }

        public int ProviderResponseCount { get; init; }

        public long ProviderFirstResponseLatencyTotalMs { get; init; }

        public long ProviderFirstResponseLatencyMaxMs { get; init; }

        public long ProviderCallDurationTotalMs { get; init; }

        public int ProviderStreamChunkCount { get; init; }

        public int ProviderStreamInterChunkLatencyCount { get; init; }

        public long ProviderStreamInterChunkLatencyTotalMs { get; init; }

        public long ProviderStreamInterChunkLatencyMaxMs { get; init; }

        public int ContextRecoveryCount { get; init; }

        public long ContextRecoveryEstimatedInputTokensBefore { get; init; }

        public long ContextRecoveryEstimatedInputTokensAfter { get; init; }

        public CopilotTokenUsage Usage { get; init; } = CopilotTokenUsage.Empty;

        public long ConsumedTokens { get; init; }

        public int ProviderCalls { get; init; }

        public bool UsedEstimatedUsage { get; init; }

        public int RegisteredToolCount { get; init; }

        public int AvailableToolCount { get; init; }

        public int AvailableToolDefinitionCharacters { get; init; }

        public int HarnessInstructionCharacters { get; init; }
    }

    public sealed class CopilotDelegatedAnswer
    {
        public string Text { get; init; } = string.Empty;

        public CopilotAgentStopReason StopReason { get; init; }

        public bool HasSuccessfulEvidence { get; init; }

        public bool WasTruncated { get; init; }
    }

    public sealed class CopilotToolResult
    {
        public string ToolName { get; init; } = string.Empty;

        public bool Success { get; init; }

        public string Summary { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;

        public string ErrorMessage { get; init; } = string.Empty;

        public CopilotToolFailureKind FailureKind { get; init; }

        public string FailureCode { get; init; } = string.Empty;

        public CopilotToolApprovalInfo? Approval { get; init; }

        public IReadOnlyList<string> SuggestedReadableLocalFilePaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> AttemptedLocalFilePaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> SuccessfullyReadLocalFilePaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<CopilotLocalFileReadScope> LocalFileReadScopes { get; init; } = Array.Empty<CopilotLocalFileReadScope>();

        public CopilotDelegatedRunUsage? DelegatedRunUsage { get; init; }

        public CopilotDelegatedAnswer? DelegatedAnswer { get; init; }

        public bool ObservationCanRepeat { get; init; }

        public string ObservationProgressSignature { get; init; } = string.Empty;

        internal CopilotWorkspaceMutationSnapshot? WorkspaceMutation { get; init; }
    }

    internal sealed record CopilotWorkspaceMutationFileSnapshot(
        string FullPath,
        bool BeforeExists,
        string BeforeText,
        bool AfterExists,
        string AfterText);

    internal sealed record CopilotWorkspaceMutationSnapshot(
        IReadOnlyList<CopilotWorkspaceMutationFileSnapshot> Files);

    public sealed class CopilotLocalFileReadScope
    {
        public string Path { get; init; } = string.Empty;

        public int StartLine { get; init; }

        public int StartColumn { get; init; }

        public int EndLine { get; init; }

        public int EndColumn { get; init; }

        public bool WasTruncated { get; init; }

        public int ContinuationStartLine { get; init; }

        public int ContinuationStartColumn { get; init; }
    }

    public sealed class CopilotToolApprovalInfo
    {
        public string ActionId { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string RiskLevel { get; init; } = string.Empty;

        public DateTimeOffset ExpiresAtUtc { get; init; }

        public bool ExecuteOnApproval { get; init; }

        public bool ResumesAgentOnApproval { get; init; }
    }

    public sealed class CopilotToolCall
    {
        public string ToolName { get; init; } = string.Empty;

        public CopilotAgentToolInput ToolInput { get; init; } = CopilotAgentToolInput.Empty;

        public string Reason { get; init; } = string.Empty;

        public bool IsFallback { get; init; }
    }

    public sealed class CopilotToolObservation
    {
        public bool Success { get; init; }

        public string Summary { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;

        public string ErrorMessage { get; init; } = string.Empty;

        public CopilotToolFailureKind FailureKind { get; init; }

        public string FailureCode { get; init; } = string.Empty;

        public CopilotToolApprovalInfo? Approval { get; init; }

        public IReadOnlyList<string> SuggestedReadableLocalFilePaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> AttemptedLocalFilePaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> SuccessfullyReadLocalFilePaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<CopilotLocalFileReadScope> LocalFileReadScopes { get; init; } = Array.Empty<CopilotLocalFileReadScope>();

        public CopilotDelegatedRunUsage? DelegatedRunUsage { get; init; }

        public CopilotDelegatedAnswer? DelegatedAnswer { get; init; }

        public static CopilotToolObservation FromResult(CopilotToolResult? result)
        {
            return new CopilotToolObservation
            {
                Success = result?.Success ?? false,
                Summary = result?.Summary ?? string.Empty,
                Content = result?.Content ?? string.Empty,
                ErrorMessage = result?.ErrorMessage ?? string.Empty,
                FailureKind = result?.FailureKind ?? CopilotToolFailureKind.None,
                FailureCode = result?.Success == false ? CopilotToolFailureCode.Normalize(result.FailureCode) : string.Empty,
                Approval = result?.Approval,
                SuggestedReadableLocalFilePaths = result?.SuggestedReadableLocalFilePaths ?? Array.Empty<string>(),
                AttemptedLocalFilePaths = result?.AttemptedLocalFilePaths ?? Array.Empty<string>(),
                SuccessfullyReadLocalFilePaths = result?.SuccessfullyReadLocalFilePaths ?? Array.Empty<string>(),
                LocalFileReadScopes = result?.LocalFileReadScopes ?? Array.Empty<CopilotLocalFileReadScope>(),
                DelegatedRunUsage = result?.DelegatedRunUsage,
                DelegatedAnswer = result?.DelegatedAnswer,
            };
        }
    }

    public sealed class CopilotAgentStepRecord
    {
        public int Round { get; init; }

        public CopilotToolCall ToolCall { get; init; } = new();

        public CopilotToolObservation Observation { get; init; } = new();

        public CopilotToolExecutionInfo Execution { get; init; } = new();
    }

    public enum CopilotToolExecutionState
    {
        Pending,
        Running,
        Completed,
        Failed,
        TimedOut,
        Denied,
        Cancelled,
        Interrupted,
        AwaitingApproval,
    }

    public enum CopilotToolFailureKind
    {
        None,
        Unspecified,
        Validation,
        Authorization,
        NotFound,
        Conflict,
        Transient,
        Internal,
        Cancelled,
    }

    public sealed class CopilotToolExecutionInfo
    {
        public string CallId { get; init; } = string.Empty;

        public int Round { get; init; }

        public int Attempt { get; init; } = 1;

        public int MaxAttempts { get; init; } = 1;

        public string RuntimeName { get; init; } = string.Empty;

        public string ToolName { get; init; } = string.Empty;

        public CopilotToolAccess Access { get; init; }

        public CopilotToolRiskLevel RiskLevel { get; init; }

        public CopilotToolApprovalMode ApprovalMode { get; init; }

        public CopilotToolIdempotency Idempotency { get; init; }

        public CopilotToolConcurrencyMode ConcurrencyMode { get; init; }

        public string ConcurrencyKey { get; init; } = string.Empty;

        public string ApprovalActionId { get; init; } = string.Empty;

        public string ArgumentSummary { get; init; } = string.Empty;

        public CopilotToolExecutionState State { get; init; } = CopilotToolExecutionState.Pending;

        public CopilotToolFailureKind FailureKind { get; init; }

        public bool RetryEligible { get; init; }

        public DateTimeOffset StartedAtUtc { get; init; }

        public DateTimeOffset? CompletedAtUtc { get; init; }

        public long DurationMs { get; init; }

        public long QueueDurationMs { get; init; }

        public long TimeoutMs { get; init; }
    }

}
