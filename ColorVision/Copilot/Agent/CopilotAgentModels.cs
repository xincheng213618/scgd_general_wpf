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
        public CopilotAgentPreparedPrompt(
            IReadOnlyList<CopilotRequestMessage> messages,
            string preparedUserMessageContent)
            : this(messages, preparedUserMessageContent, CopilotContextProvenanceSnapshot.Empty)
        {
        }

        internal CopilotAgentPreparedPrompt(
            IReadOnlyList<CopilotRequestMessage> messages,
            string preparedUserMessageContent,
            CopilotContextProvenanceSnapshot contextProvenance)
        {
            Messages = Array.AsReadOnly((messages ?? Array.Empty<CopilotRequestMessage>()).ToArray());
            PreparedUserMessageContent = preparedUserMessageContent ?? string.Empty;
            ContextProvenance = contextProvenance ?? CopilotContextProvenanceSnapshot.Empty;
        }

        public IReadOnlyList<CopilotRequestMessage> Messages { get; }

        public string PreparedUserMessageContent { get; }

        internal CopilotContextProvenanceSnapshot ContextProvenance { get; }
    }

    public sealed class CopilotAgentRunResult
    {
        public string PreparedUserMessageContent { get; init; } = string.Empty;

        public IReadOnlyList<CopilotAgentStepRecord> StepRecords
        {
            get => _stepRecords;
            init => _stepRecords = Freeze(value);
        }
        private IReadOnlyList<CopilotAgentStepRecord> _stepRecords = Array.Empty<CopilotAgentStepRecord>();

        public CopilotTokenUsage Usage { get; init; } = CopilotTokenUsage.Empty;

        public CopilotAgentBudgetSnapshot Budget { get; init; } = new();

        public CopilotAgentTaskLedgerSnapshot TaskLedger
        {
            get => _taskLedger;
            init => _taskLedger = CopilotAgentTaskLedgerSnapshot.CreateSnapshot(value, normalize: false);
        }
        private CopilotAgentTaskLedgerSnapshot _taskLedger = new();

        public CopilotAgentStopReason StopReason { get; init; }

        public IReadOnlyList<CopilotAgentBlockerSnapshot> Blockers
        {
            get => _blockers;
            init => _blockers = Freeze(value);
        }
        private IReadOnlyList<CopilotAgentBlockerSnapshot> _blockers = Array.Empty<CopilotAgentBlockerSnapshot>();

        public CopilotAgentTaskEventJournalSnapshot TaskEventJournal
        {
            get => _taskEventJournal;
            init => _taskEventJournal = CopilotAgentTaskEventJournal.TryCreateSnapshot(
                value,
                out var snapshot)
                    ? snapshot
                    : new CopilotAgentTaskEventJournalSnapshot();
        }
        private CopilotAgentTaskEventJournalSnapshot _taskEventJournal = new();

        public CopilotAgentSessionCheckpoint? SessionCheckpoint
        {
            get => _sessionCheckpoint;
            init => _sessionCheckpoint = CopilotAgentSessionCheckpoint.TryCreateSnapshot(
                value,
                out var snapshot)
                    ? snapshot
                    : null;
        }
        private CopilotAgentSessionCheckpoint? _sessionCheckpoint;

        private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T>? values)
        {
            return values == null || values.Count == 0
                ? Array.Empty<T>()
                : Array.AsReadOnly(values.ToArray());
        }
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
        public const int MaxItems = 128;

        public string Mode { get; set; } = string.Empty;

        public bool ResumedFromCheckpoint { get; set; }

        public IReadOnlyList<CopilotAgentTaskItem> Items { get; set; } = Array.Empty<CopilotAgentTaskItem>();

        public int TotalCount => Items.Count;

        public int CompletedCount => Items.Count(item => item.IsComplete);

        public int RemainingCount => TotalCount - CompletedCount;

        internal static CopilotAgentTaskLedgerSnapshot CreateSnapshot(
            CopilotAgentTaskLedgerSnapshot? source,
            bool normalize)
        {
            try
            {
                var snapshot = new CopilotAgentTaskLedgerSnapshot
                {
                    Mode = source?.Mode ?? string.Empty,
                    ResumedFromCheckpoint = source?.ResumedFromCheckpoint == true,
                    Items = Array.AsReadOnly((source?.Items ?? Array.Empty<CopilotAgentTaskItem>())
                        .Take(MaxItems + 1)
                        .Select(item => item == null
                            ? null!
                            : new CopilotAgentTaskItem
                            {
                                Id = item.Id,
                                Title = item.Title,
                                Description = item.Description,
                                IsComplete = item.IsComplete,
                            })
                        .ToArray()),
                };
                if (normalize)
                    snapshot.EnsureValid();
                return snapshot;
            }
            catch
            {
                var snapshot = new CopilotAgentTaskLedgerSnapshot();
                if (normalize)
                    snapshot.EnsureValid();
                return snapshot;
            }
        }

        internal bool IsStructurallyValid()
        {
            if (Mode is not ("plan" or "execute")
                || Items == null
                || Items.Count > MaxItems)
            {
                return false;
            }

            return Items.All(item => item != null
                    && item.Id >= 0
                    && !string.IsNullOrWhiteSpace(item.Title)
                    && string.Equals(item.Title, item.Title.Trim(), StringComparison.Ordinal)
                    && item.Title.Length <= CopilotAgentTaskItem.MaxTitleLength
                    && !item.Title.Contains('\0')
                    && item.Description != null
                    && string.Equals(item.Description, item.Description.Trim(), StringComparison.Ordinal)
                    && item.Description.Length <= CopilotAgentTaskItem.MaxDescriptionLength
                    && !item.Description.Contains('\0'))
                && Items.Select(item => item.Id).Distinct().Count() == Items.Count;
        }

        public bool EnsureValid()
        {
            var originalMode = Mode;
            var originalItems = Items;
            Mode = string.Equals(Mode, "plan", StringComparison.OrdinalIgnoreCase) ? "plan" : "execute";
            var changed = !string.Equals(originalMode, Mode, StringComparison.Ordinal) || originalItems == null;
            var normalizedItems = new List<CopilotAgentTaskItem>();
            var observedIds = new HashSet<int>();
            foreach (var item in Items ?? Array.Empty<CopilotAgentTaskItem>())
            {
                if (normalizedItems.Count >= MaxItems)
                {
                    changed = true;
                    break;
                }
                if (item == null)
                {
                    changed = true;
                    continue;
                }

                changed |= item.Normalize();
                if (string.IsNullOrWhiteSpace(item.Title) || !observedIds.Add(item.Id))
                {
                    changed = true;
                    continue;
                }
                normalizedItems.Add(item);
            }

            Items = Array.AsReadOnly(normalizedItems.ToArray());
            return changed || originalItems?.Count != Items.Count;
        }

    }

    public sealed class CopilotAgentTaskItem
    {
        public const int MaxTitleLength = 256;
        public const int MaxDescriptionLength = 2_048;

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
            Title = BoundText(Title, MaxTitleLength);
            Description = BoundText(Description, MaxDescriptionLength);
            return originalId != Id
                || !string.Equals(originalTitle, Title, StringComparison.Ordinal)
                || !string.Equals(originalDescription, Description, StringComparison.Ordinal);
        }

        private static string BoundText(string? value, int maximumLength)
        {
            var normalized = (value ?? string.Empty).Replace('\0', ' ').Trim();
            return normalized.Length <= maximumLength ? normalized : normalized[..maximumLength].TrimEnd();
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
