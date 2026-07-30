using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ColorVision.UI;

namespace ColorVision.Copilot
{
    [Flags]
    internal enum CopilotAgentHarnessFeatures
    {
        None = 0,
        TaskLedger = 1,
        AgentMode = 2,
        Skills = 4,
        Full = TaskLedger | AgentMode | Skills,
    }

    internal enum CopilotAgentRuntimePurpose
    {
        Standard,
        DelegatedEvidenceFinalization,
    }

    public enum CopilotAgentMode
    {
        Chat,
        Auto,
        Explain,
        Web,
        Code,
        Review,
        Diagnose,
        Plan,
    }

    public enum CopilotAgentAccessMode
    {
        ConfirmProtectedActions,
        FullAccess,
    }

    public sealed class CopilotAgentAccessContext
    {
        internal static readonly TimeSpan MaximumFullAccessLifetime = TimeSpan.FromMinutes(15);
        private readonly object _syncRoot = new();
        private FullAccessGrant? _grant;

        public CopilotAgentAccessMode Mode
        {
            get
            {
                lock (_syncRoot)
                    return IsGrantCurrentNoLock(DateTimeOffset.UtcNow)
                        ? CopilotAgentAccessMode.FullAccess
                        : CopilotAgentAccessMode.ConfirmProtectedActions;
            }
        }

        public bool IsPreparedForNextTask
        {
            get
            {
                lock (_syncRoot)
                    return IsGrantCurrentNoLock(DateTimeOffset.UtcNow)
                        && string.IsNullOrWhiteSpace(_grant!.TaskId);
            }
        }

        public string GrantedTaskId
        {
            get
            {
                lock (_syncRoot)
                    return IsGrantCurrentNoLock(DateTimeOffset.UtcNow) ? _grant!.TaskId : string.Empty;
            }
        }

        public string WorkspacePath
        {
            get
            {
                lock (_syncRoot)
                    return IsGrantCurrentNoLock(DateTimeOffset.UtcNow) ? _grant!.WorkspacePath : string.Empty;
            }
        }

        public DateTimeOffset? ExpiresAtUtc
        {
            get
            {
                lock (_syncRoot)
                    return IsGrantCurrentNoLock(DateTimeOffset.UtcNow) ? _grant!.ExpiresAtUtc : null;
            }
        }

        public bool AllowsUnattendedProtectedActions
        {
            get
            {
                lock (_syncRoot)
                    return IsGrantCurrentNoLock(DateTimeOffset.UtcNow)
                        && !string.IsNullOrWhiteSpace(_grant!.TaskId);
            }
        }

        internal void PrepareFullAccess(
            string conversationId,
            string workspacePath,
            string? taskId,
            DateTimeOffset expiresAtUtc)
        {
            var normalizedConversationId = NormalizeIdentifier(conversationId);
            var normalizedTaskId = NormalizeIdentifier(taskId);
            var normalizedWorkspacePath = NormalizeWorkspacePath(workspacePath);
            var now = DateTimeOffset.UtcNow;
            var normalizedExpiresAtUtc = expiresAtUtc.ToUniversalTime();
            if (normalizedExpiresAtUtc > now.Add(MaximumFullAccessLifetime))
                normalizedExpiresAtUtc = now.Add(MaximumFullAccessLifetime);
            lock (_syncRoot)
            {
                _grant = string.IsNullOrWhiteSpace(normalizedConversationId)
                    || string.IsNullOrWhiteSpace(normalizedWorkspacePath)
                    || normalizedExpiresAtUtc <= now
                    ? null
                    : new FullAccessGrant(
                        normalizedConversationId,
                        normalizedTaskId,
                        normalizedWorkspacePath,
                        normalizedExpiresAtUtc);
            }
        }

        internal bool BindToTask(string conversationId, string taskId, string workspacePath)
        {
            var normalizedConversationId = NormalizeIdentifier(conversationId);
            var normalizedTaskId = NormalizeIdentifier(taskId);
            var normalizedWorkspacePath = NormalizeWorkspacePath(workspacePath);
            lock (_syncRoot)
            {
                if (!IsGrantCurrentNoLock(DateTimeOffset.UtcNow))
                {
                    _grant = null;
                    return false;
                }

                if (string.IsNullOrWhiteSpace(normalizedTaskId)
                    || !string.Equals(_grant!.ConversationId, normalizedConversationId, StringComparison.Ordinal)
                    || !WorkspaceMatches(_grant.WorkspacePath, normalizedWorkspacePath))
                {
                    _grant = null;
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(_grant.TaskId))
                    return string.Equals(_grant.TaskId, normalizedTaskId, StringComparison.Ordinal);

                _grant = _grant with { TaskId = normalizedTaskId };
                return true;
            }
        }

        internal bool Revoke(string? taskId = null)
        {
            var normalizedTaskId = NormalizeIdentifier(taskId);
            lock (_syncRoot)
            {
                if (_grant == null
                    || (!string.IsNullOrWhiteSpace(normalizedTaskId)
                        && !string.Equals(_grant.TaskId, normalizedTaskId, StringComparison.Ordinal)))
                {
                    return false;
                }

                _grant = null;
                return true;
            }
        }

        internal bool ExpireIfNeeded()
        {
            lock (_syncRoot)
            {
                if (_grant == null || _grant.ExpiresAtUtc > DateTimeOffset.UtcNow)
                    return false;

                _grant = null;
                return true;
            }
        }

        internal bool AllowsUnattendedProtectedActionsFor(
            string conversationId,
            string taskId,
            string workspacePath)
        {
            var normalizedConversationId = NormalizeIdentifier(conversationId);
            var normalizedTaskId = NormalizeIdentifier(taskId);
            var normalizedWorkspacePath = NormalizeWorkspacePath(workspacePath);
            lock (_syncRoot)
            {
                return IsGrantCurrentNoLock(DateTimeOffset.UtcNow)
                    && !string.IsNullOrWhiteSpace(normalizedTaskId)
                    && string.Equals(_grant!.ConversationId, normalizedConversationId, StringComparison.Ordinal)
                    && string.Equals(_grant.TaskId, normalizedTaskId, StringComparison.Ordinal)
                    && WorkspaceMatches(_grant.WorkspacePath, normalizedWorkspacePath);
            }
        }

        internal bool RevokeIfWorkspaceChanged(string workspacePath)
        {
            var normalizedWorkspacePath = NormalizeWorkspacePath(workspacePath);
            lock (_syncRoot)
            {
                if (!IsGrantCurrentNoLock(DateTimeOffset.UtcNow)
                    || WorkspaceMatches(_grant!.WorkspacePath, normalizedWorkspacePath))
                {
                    return false;
                }

                _grant = null;
                return true;
            }
        }

        private bool IsGrantCurrentNoLock(DateTimeOffset now)
        {
            return _grant != null && _grant.ExpiresAtUtc > now;
        }

        private static bool WorkspaceMatches(string grantedPath, string requestPath)
        {
            return string.Equals(grantedPath, requestPath, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeIdentifier(string? value)
        {
            var normalized = (value ?? string.Empty).Trim();
            return normalized.Length <= 160 ? normalized : normalized[..160];
        }

        private static string NormalizeWorkspacePath(string? value)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;

            try
            {
                return Path.GetFullPath(normalized)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return normalized.Length <= 1000 ? normalized : normalized[..1000];
            }
        }

        private sealed record FullAccessGrant(
            string ConversationId,
            string TaskId,
            string WorkspacePath,
            DateTimeOffset ExpiresAtUtc);
    }

    internal static class CopilotAgentAccessPolicy
    {
        public static bool CanAutoApprove(
            CopilotAgentRequest request,
            ICopilotTool tool,
            string currentWorkspacePath)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(tool);
            if (request.AccessContext.RevokeIfWorkspaceChanged(currentWorkspacePath))
                return false;
            if (string.IsNullOrWhiteSpace(currentWorkspacePath)
                || !WorkspacePathsMatch(request.WorkspacePath, currentWorkspacePath))
            {
                return false;
            }

            return request.AccessContext.AllowsUnattendedProtectedActionsFor(
                    request.ConversationId,
                    request.TaskId,
                    currentWorkspacePath)
                && !CopilotToolIntentPolicy.IsReadOnlyMode(request.Mode)
                && tool.Capability.RequiresNativeApproval
                && tool.Capability.AllowsTemporaryFullAccess
                && IsWriteScopeContainedByWorkspace(request, currentWorkspacePath);
        }

        public static bool CanAutoReview(
            CopilotAgentRequest request,
            ICopilotTool tool,
            string currentWorkspacePath)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(tool);
            if (request.AccessContext.RevokeIfWorkspaceChanged(currentWorkspacePath))
                return false;
            if (string.IsNullOrWhiteSpace(currentWorkspacePath)
                || !WorkspacePathsMatch(request.WorkspacePath, currentWorkspacePath))
            {
                return false;
            }

            return request.AccessContext.AllowsUnattendedProtectedActionsFor(
                    request.ConversationId,
                    request.TaskId,
                    currentWorkspacePath)
                && !CopilotToolIntentPolicy.IsReadOnlyMode(request.Mode)
                && tool.Capability.RequiresNativeApproval
                && !tool.Capability.AllowsTemporaryFullAccess;
        }

        private static bool IsWriteScopeContainedByWorkspace(
            CopilotAgentRequest request,
            string workspacePath)
        {
            var writablePaths = request.WritableLocalRootPaths
                .Concat(request.WritableLocalFilePaths)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToArray();
            if (string.IsNullOrWhiteSpace(workspacePath))
                return false;
            if (writablePaths.Length == 0)
                return true;

            return writablePaths.All(path =>
                IsPathContainedByWorkspace(path, workspacePath));
        }

        private static bool WorkspacePathsMatch(string requestPath, string currentPath)
        {
            if (string.IsNullOrWhiteSpace(requestPath) || string.IsNullOrWhiteSpace(currentPath))
                return false;

            try
            {
                return string.Equals(
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(requestPath)),
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(currentPath)),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPathContainedByWorkspace(string path, string workspacePath)
        {
            string candidate;
            string workspace;
            try
            {
                candidate = Path.GetFullPath(path);
                workspace = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspacePath));
                var relativePath = Path.GetRelativePath(workspace, candidate);
                if (Path.IsPathRooted(relativePath)
                    || string.Equals(relativePath, "..", StringComparison.Ordinal)
                    || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

            var existingPath = candidate;
            while (!File.Exists(existingPath)
                && !Directory.Exists(existingPath)
                && !string.Equals(existingPath, workspace, StringComparison.OrdinalIgnoreCase))
            {
                var parentPath = Path.GetDirectoryName(existingPath);
                if (string.IsNullOrWhiteSpace(parentPath)
                    || string.Equals(parentPath, existingPath, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                existingPath = parentPath;
            }

            return CopilotWorkspaceSearchSupport.IsPathWithinRoots(existingPath, [workspace]);
        }
    }

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

        public CopilotProfileConfig Profile { get; init; } = null!;

        public IReadOnlyList<CopilotRequestMessage> History { get; init; } = Array.Empty<CopilotRequestMessage>();

        public IReadOnlyList<CopilotAttachmentItem> Attachments { get; init; } = Array.Empty<CopilotAttachmentItem>();

        public IReadOnlyList<CopilotContextItem> ContextItems { get; init; } = Array.Empty<CopilotContextItem>();

        public IReadOnlyList<string> SearchRootPaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> TrustedProjectRootPaths { get; init; } = Array.Empty<string>();

        public string ActiveDocumentPath { get; init; } = string.Empty;

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

        public int RequestTokenBudget { get; init; }

        public long QueueDurationMs { get; init; }

        public CopilotAgentStopReason StopReason { get; init; }

        public int ToolCalls { get; init; }

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
    }

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

    public enum CopilotAgentEventType
    {
        Status,
        RuntimeDiagnostic,
        ToolStarted,
        ToolProgress,
        ToolResult,
        ReasoningDelta,
        AnswerDelta,
        AnswerReset,
        Error,
        Completed,
        CheckpointReady,
        CheckpointUpdated,
        UserQuestionRequested,
        UserQuestionResolved,
    }

    public sealed class CopilotAgentEvent
    {
        public CopilotAgentEventType Type { get; init; }

        public string Text { get; init; } = string.Empty;

        public CopilotToolResult? ToolResult { get; init; }

        public CopilotToolExecutionInfo? ToolExecution { get; init; }

        public IReadOnlyList<CopilotToolExecutionHookRun> ToolExecutionHookRuns { get; init; } =
            Array.Empty<CopilotToolExecutionHookRun>();

        public CopilotToolProgressUpdate? Progress { get; init; }

        public CopilotAgentSessionCheckpoint? SessionCheckpoint { get; init; }

        public CopilotAgentTaskLedgerSnapshot? TaskLedger { get; init; }

        public CopilotUserQuestionSnapshot? UserQuestion { get; init; }

        internal CopilotProviderRetryInfo? ProviderRetry { get; init; }

        public static CopilotAgentEvent Status(string text)
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.Status,
                Text = text ?? string.Empty,
            };
        }

        public static CopilotAgentEvent ToolStarted(CopilotToolExecutionInfo execution)
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.ToolStarted,
                Text = execution?.ToolName ?? string.Empty,
                ToolExecution = execution,
            };
        }

        public static CopilotAgentEvent ToolProgress(
            CopilotToolExecutionInfo execution,
            string text,
            CopilotToolProgressUpdate? progress = null)
        {
            ArgumentNullException.ThrowIfNull(execution);
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.ToolProgress,
                Text = text ?? string.Empty,
                ToolExecution = execution,
                Progress = progress,
            };
        }

        public static CopilotAgentEvent RuntimeDiagnostic(string text)
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.RuntimeDiagnostic,
                Text = text ?? string.Empty,
            };
        }

        internal static CopilotAgentEvent FromProviderRetry(CopilotProviderRetryInfo retry)
        {
            ArgumentNullException.ThrowIfNull(retry);
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.RuntimeDiagnostic,
                Text = retry.ToDiagnosticText(),
                ProviderRetry = retry,
            };
        }

        public static CopilotAgentEvent FromToolResult(
            CopilotToolResult result,
            CopilotToolExecutionInfo? execution = null,
            IReadOnlyList<CopilotToolExecutionHookRun>? hookRuns = null)
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.ToolResult,
                Text = result?.Summary ?? string.Empty,
                ToolResult = result,
                ToolExecution = execution,
                ToolExecutionHookRuns = hookRuns?.ToArray() ?? Array.Empty<CopilotToolExecutionHookRun>(),
            };
        }

        public static CopilotAgentEvent ReasoningDelta(string text)
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.ReasoningDelta,
                Text = text ?? string.Empty,
            };
        }

        public static CopilotAgentEvent AnswerDelta(string text)
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.AnswerDelta,
                Text = text ?? string.Empty,
            };
        }

        public static CopilotAgentEvent Error(string text)
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.Error,
                Text = text ?? string.Empty,
            };
        }

        public static CopilotAgentEvent Completed()
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.Completed,
            };
        }

        public static CopilotAgentEvent AnswerReset()
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.AnswerReset,
            };
        }

        public static CopilotAgentEvent CheckpointReady()
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.CheckpointReady,
            };
        }

        public static CopilotAgentEvent CheckpointUpdated(
            CopilotAgentSessionCheckpoint sessionCheckpoint,
            CopilotAgentTaskLedgerSnapshot taskLedger)
        {
            ArgumentNullException.ThrowIfNull(sessionCheckpoint);
            ArgumentNullException.ThrowIfNull(taskLedger);
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.CheckpointUpdated,
                SessionCheckpoint = sessionCheckpoint,
                TaskLedger = taskLedger,
            };
        }

        public static CopilotAgentEvent UserQuestionRequested(CopilotUserQuestionSnapshot question)
        {
            ArgumentNullException.ThrowIfNull(question);
            if (!question.IsPending || !question.IsStructurallyValid())
                throw new ArgumentException("The user question request is not structurally valid.", nameof(question));
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.UserQuestionRequested,
                UserQuestion = question,
            };
        }

        public static CopilotAgentEvent UserQuestionResolved(CopilotUserQuestionSnapshot question)
        {
            ArgumentNullException.ThrowIfNull(question);
            if (question.IsPending || !question.IsStructurallyValid())
                throw new ArgumentException("The resolved user question is not structurally valid.", nameof(question));
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.UserQuestionResolved,
                UserQuestion = question,
            };
        }
    }

    public sealed class CopilotAgentPreparedPrompt
    {
        public CopilotAgentPreparedPrompt(IReadOnlyList<CopilotRequestMessage> messages, string preparedUserMessageContent)
        {
            Messages = messages ?? Array.Empty<CopilotRequestMessage>();
            PreparedUserMessageContent = preparedUserMessageContent ?? string.Empty;
        }

        public IReadOnlyList<CopilotRequestMessage> Messages { get; }

        public string PreparedUserMessageContent { get; }
    }

    public sealed class CopilotAgentRunResult
    {
        public string PreparedUserMessageContent { get; init; } = string.Empty;

        public IReadOnlyList<CopilotAgentStepRecord> StepRecords { get; init; } = Array.Empty<CopilotAgentStepRecord>();

        public CopilotTokenUsage Usage { get; init; } = CopilotTokenUsage.Empty;

        public CopilotAgentBudgetSnapshot Budget { get; init; } = new();

        public CopilotAgentTaskLedgerSnapshot TaskLedger { get; init; } = new();

        public CopilotAgentStopReason StopReason { get; init; }

        public IReadOnlyList<CopilotAgentBlockerSnapshot> Blockers { get; init; } = Array.Empty<CopilotAgentBlockerSnapshot>();

        public CopilotAgentTaskEventJournalSnapshot TaskEventJournal { get; init; } = new();

        public CopilotAgentSessionCheckpoint? SessionCheckpoint { get; init; }
    }

    public enum CopilotAgentStopReason
    {
        None,
        Completed,
        AwaitingUser,
        ApprovalDenied,
        BudgetExhausted,
        TaskPassLimit,
        Blocked,
        Paused,
        Cancelled,
        IncompleteOutput,
        ProviderFailure,
        Interrupted,
    }

    public sealed class CopilotAgentTaskLedgerSnapshot
    {
        public string Mode { get; set; } = string.Empty;

        public bool ResumedFromCheckpoint { get; set; }

        public IReadOnlyList<CopilotAgentTaskItem> Items { get; set; } = Array.Empty<CopilotAgentTaskItem>();

        public int TotalCount => Items.Count;

        public int CompletedCount => Items.Count(item => item.IsComplete);

        public int RemainingCount => TotalCount - CompletedCount;

        public bool EnsureValid()
        {
            var originalMode = Mode;
            var originalItems = Items;
            Mode = string.Equals(Mode, "plan", StringComparison.OrdinalIgnoreCase) ? "plan" : "execute";
            var changed = !string.Equals(originalMode, Mode, StringComparison.Ordinal) || originalItems == null;
            var normalizedItems = new List<CopilotAgentTaskItem>();
            foreach (var item in Items ?? Array.Empty<CopilotAgentTaskItem>())
            {
                if (item == null)
                {
                    changed = true;
                    continue;
                }

                changed |= item.Normalize();
                normalizedItems.Add(item);
            }

            Items = normalizedItems;
            return changed || originalItems?.Count != Items.Count;
        }

    }

    public sealed class CopilotAgentTaskItem
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public bool IsComplete { get; set; }

        internal bool Normalize()
        {
            var originalId = Id;
            var originalTitle = Title;
            var originalDescription = Description;
            Id = Math.Max(0, Id);
            Title = (Title ?? string.Empty).Trim();
            Description = (Description ?? string.Empty).Trim();
            return originalId != Id
                || !string.Equals(originalTitle, Title, StringComparison.Ordinal)
                || !string.Equals(originalDescription, Description, StringComparison.Ordinal);
        }
    }

    public sealed class CopilotAgentBudgetSnapshot
    {
        public bool CompactionEnabled { get; init; }

        public int ContextWindowTokens { get; init; }

        public int InputBudgetTokens { get; init; }

        public int RequestTokenBudget { get; init; }

        public long ConsumedTokens { get; init; }

        public int ProviderCalls { get; init; }

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

        public int ReportedInputTokens { get; init; }

        public int ReportedOutputTokens { get; init; }

        public int ReportedTotalTokens { get; init; }

        public int? ReportedCachedInputTokens { get; init; }

        public bool UsedEstimatedUsage { get; init; }

        public bool UsedDelegatedDirectAnswer { get; init; }

        public bool BudgetExhausted { get; init; }

        public bool RequestTokenBudgetExhausted { get; init; }

        public int MaxToolCalls { get; init; }

        public int ToolCalls { get; init; }

        public bool ToolBudgetExhausted { get; init; }

        public int RegisteredToolCount { get; init; }

        public int AvailableToolCount { get; init; }

        public int AvailableToolDefinitionCharacters { get; init; }

        public int HarnessInstructionCharacters { get; init; }

        public int NarrowEvidenceResultLimit { get; init; }

        public int MaxAgentPasses { get; init; }

        public long TotalDurationMs { get; init; }

        public long ElapsedMs { get; init; }

        public bool TimeBudgetExhausted { get; init; }
    }
}
