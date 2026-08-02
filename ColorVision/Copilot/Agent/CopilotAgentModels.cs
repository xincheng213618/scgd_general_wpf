using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ColorVision.UI;

namespace ColorVision.Copilot
{

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
