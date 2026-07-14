using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ColorVision.UI;

namespace ColorVision.Copilot
{
    public enum CopilotAgentMode
    {
        Chat,
        Auto,
        Explain,
        Web,
        Code,
        Diagnose,
    }

    public sealed class CopilotAgentToolInput
    {
        public static CopilotAgentToolInput Empty { get; } = new();

        public IReadOnlyDictionary<string, object?> Arguments { get; init; } = new Dictionary<string, object?>();

        public string Query { get; init; } = string.Empty;

        public string Path { get; init; } = string.Empty;

        public int? StartLine { get; init; }

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
        public string UserText { get; init; } = string.Empty;

        public CopilotProfileConfig Profile { get; init; } = null!;

        public IReadOnlyList<CopilotRequestMessage> History { get; init; } = Array.Empty<CopilotRequestMessage>();

        public IReadOnlyList<CopilotAttachmentItem> Attachments { get; init; } = Array.Empty<CopilotAttachmentItem>();

        public IReadOnlyList<CopilotContextItem> ContextItems { get; init; } = Array.Empty<CopilotContextItem>();

        public IReadOnlyList<string> SearchRootPaths { get; init; } = Array.Empty<string>();

        public string ActiveDocumentPath { get; init; } = string.Empty;

        public IReadOnlyList<CopilotProjectInstructionDocument> ProjectInstructions { get; init; } = Array.Empty<CopilotProjectInstructionDocument>();

        public IReadOnlyList<string> ReadableLocalFilePaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> ReadableLocalDirectoryPaths { get; init; } = Array.Empty<string>();

        public bool PreferBatchReadLocalFiles { get; init; }

        public CopilotAgentMode Mode { get; init; } = CopilotAgentMode.Auto;

        public CopilotAgentSessionCheckpoint? SessionCheckpoint { get; init; }

        public CopilotAgentRecoveryRequest? Recovery { get; init; }

        public CopilotAgentRunControl? RunControl { get; init; }

        public CopilotAgentRunBudgetOverride? RunBudgetOverride { get; init; }

        public IReadOnlyList<CopilotMcpClientServerConfig> ExternalMcpServers { get; init; } = Array.Empty<CopilotMcpClientServerConfig>();

        public bool ForceExternalMcpToolRefresh { get; init; }
    }

    public sealed class CopilotToolResult
    {
        public string ToolName { get; init; } = string.Empty;

        public bool Success { get; init; }

        public string Summary { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;

        public string ErrorMessage { get; init; } = string.Empty;

        public CopilotToolFailureKind FailureKind { get; init; }

        public CopilotToolApprovalInfo? Approval { get; init; }

        public IReadOnlyList<string> SuggestedReadableLocalFilePaths { get; init; } = Array.Empty<string>();
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

        public CopilotToolApprovalInfo? Approval { get; init; }

        public IReadOnlyList<string> SuggestedReadableLocalFilePaths { get; init; } = Array.Empty<string>();

        public static CopilotToolObservation FromResult(CopilotToolResult? result)
        {
            return new CopilotToolObservation
            {
                Success = result?.Success ?? false,
                Summary = result?.Summary ?? string.Empty,
                Content = result?.Content ?? string.Empty,
                ErrorMessage = result?.ErrorMessage ?? string.Empty,
                FailureKind = result?.FailureKind ?? CopilotToolFailureKind.None,
                Approval = result?.Approval,
                SuggestedReadableLocalFilePaths = result?.SuggestedReadableLocalFilePaths ?? Array.Empty<string>(),
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
        ToolResult,
        ReasoningDelta,
        AnswerDelta,
        Error,
        Completed,
        CheckpointReady,
        CheckpointUpdated,
    }

    public sealed class CopilotAgentEvent
    {
        public CopilotAgentEventType Type { get; init; }

        public string Text { get; init; } = string.Empty;

        public CopilotToolResult? ToolResult { get; init; }

        public CopilotToolExecutionInfo? ToolExecution { get; init; }

        public CopilotAgentSessionCheckpoint? SessionCheckpoint { get; init; }

        public CopilotAgentTaskLedgerSnapshot? TaskLedger { get; init; }

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

        public static CopilotAgentEvent RuntimeDiagnostic(string text)
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.RuntimeDiagnostic,
                Text = text ?? string.Empty,
            };
        }

        public static CopilotAgentEvent FromToolResult(CopilotToolResult result, CopilotToolExecutionInfo? execution = null)
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.ToolResult,
                Text = result?.Summary ?? string.Empty,
                ToolResult = result,
                ToolExecution = execution,
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

        public bool UsedEstimatedUsage { get; init; }

        public bool BudgetExhausted { get; init; }

        public int MaxToolCalls { get; init; }

        public int ToolCalls { get; init; }

        public bool ToolBudgetExhausted { get; init; }

        public int MaxAgentPasses { get; init; }

        public long TotalDurationMs { get; init; }

        public long ElapsedMs { get; init; }

        public bool TimeBudgetExhausted { get; init; }
    }
}
