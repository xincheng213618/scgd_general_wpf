using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ColorVision.UI;

namespace ColorVision.Copilot
{


    public enum CopilotAgentEventType
    {
        Status,
        RuntimeDiagnostic,
        BudgetUpdated,
        ToolStarted,
        ToolProgress,
        ToolResult,
        ReasoningDelta,
        AnswerDelta,
        AnswerReset,
        SteeringDelivered,
        SteeringRecovery,
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

        public CopilotAgentBudgetSnapshot? Budget { get; init; }

        public CopilotAgentSessionCheckpoint? SessionCheckpoint { get; init; }

        public CopilotAgentTaskLedgerSnapshot? TaskLedger { get; init; }

        public CopilotUserQuestionSnapshot? UserQuestion { get; init; }

        public IReadOnlyList<CopilotSteeringMessageSnapshot> SteeringMessages { get; init; } =
            Array.Empty<CopilotSteeringMessageSnapshot>();

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

        public static CopilotAgentEvent BudgetUpdated(CopilotAgentBudgetSnapshot budget)
        {
            ArgumentNullException.ThrowIfNull(budget);
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.BudgetUpdated,
                Budget = budget,
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

        public static CopilotAgentEvent SteeringDelivered(IEnumerable<CopilotSteeringMessageSnapshot> messages)
        {
            var deliveredMessages = CreateSteeringMessages(messages, nameof(messages));
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.SteeringDelivered,
                Text = $"Agent provider acknowledged {deliveredMessages.Count} queued user steering instruction(s).",
                SteeringMessages = deliveredMessages,
            };
        }

        public static CopilotAgentEvent SteeringRecovery(IEnumerable<CopilotSteeringMessageSnapshot> messages)
        {
            var recoveryMessages = CreateSteeringMessages(messages, nameof(messages));

            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.SteeringRecovery,
                Text = $"Agent stopped before delivering {recoveryMessages.Count} queued user steering instruction(s); the input was returned to the conversation draft.",
                SteeringMessages = recoveryMessages,
            };
        }

        private static IReadOnlyList<CopilotSteeringMessageSnapshot> CreateSteeringMessages(
            IEnumerable<CopilotSteeringMessageSnapshot> messages,
            string parameterName)
        {
            ArgumentNullException.ThrowIfNull(messages);
            var recoveryMessages = CopilotSteeringMessagePolicy.SelectForRecovery(messages);
            if (recoveryMessages.Count == 0)
                throw new ArgumentException("Steering events require at least one bounded message.", parameterName);
            return recoveryMessages;
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

    internal static class CopilotSteeringMessagePolicy
    {
        internal const int MaximumMessageCharacters = 16_000;
        internal const int MaximumIdentifierCharacters = 128;
        internal const int MaximumPendingMessages = 8;
        internal const int MaximumPendingCharacters = 32_000;

        internal static IReadOnlyList<string> SelectForRecovery(IEnumerable<string>? messages)
        {
            var selected = new List<string>(MaximumPendingMessages);
            var characterCount = 0;
            foreach (var message in messages ?? Array.Empty<string>())
            {
                var normalized = (message ?? string.Empty).Trim();
                if (normalized.Length == 0 || normalized.Length > MaximumMessageCharacters)
                    continue;
                if (selected.Count >= MaximumPendingMessages
                    || characterCount + normalized.Length > MaximumPendingCharacters)
                {
                    break;
                }

                selected.Add(normalized);
                characterCount += normalized.Length;
            }
            return selected.ToArray();
        }

        internal static IReadOnlyList<CopilotSteeringMessageSnapshot> SelectForRecovery(
            IEnumerable<CopilotSteeringMessageSnapshot>? messages)
        {
            var selected = new List<CopilotSteeringMessageSnapshot>(MaximumPendingMessages);
            var seenMessageIds = new HashSet<string>(StringComparer.Ordinal);
            var characterCount = 0;
            foreach (var message in messages ?? Array.Empty<CopilotSteeringMessageSnapshot>())
            {
                var messageId = (message?.MessageId ?? string.Empty).Trim();
                var text = (message?.Text ?? string.Empty).Trim();
                if (messageId.Length is 0 or > MaximumIdentifierCharacters
                    || text.Length is 0 or > MaximumMessageCharacters
                    || !seenMessageIds.Add(messageId))
                {
                    continue;
                }
                if (selected.Count >= MaximumPendingMessages
                    || characterCount + text.Length > MaximumPendingCharacters)
                {
                    break;
                }

                selected.Add(new CopilotSteeringMessageSnapshot(messageId, text));
                characterCount += text.Length;
            }
            return selected.ToArray();
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
