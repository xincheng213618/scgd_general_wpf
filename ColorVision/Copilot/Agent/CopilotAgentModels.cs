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
            init => _stepRecords = FreezeStepRecords(value);
        }
        private IReadOnlyList<CopilotAgentStepRecord> _stepRecords = Array.Empty<CopilotAgentStepRecord>();

        public CopilotTokenUsage Usage { get; init; } = CopilotTokenUsage.Empty;

        public CopilotAgentBudgetSnapshot Budget { get; init; } = new();

        public CopilotAgentTaskLedgerSnapshot TaskLedger
        {
            get => _taskLedger;
            init => _taskLedger = CopilotAgentTaskLedgerSnapshot.CreateSnapshot(value, normalize: false);
        }
        private CopilotAgentTaskLedgerSnapshot _taskLedger =
            CopilotAgentTaskLedgerSnapshot.CreateSnapshot(source: null, normalize: false);

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

        private static IReadOnlyList<CopilotAgentStepRecord> FreezeStepRecords(
            IReadOnlyList<CopilotAgentStepRecord>? values)
        {
            if (values == null || values.Count == 0)
                return Array.Empty<CopilotAgentStepRecord>();

            return Array.AsReadOnly(values
                .Select(CreateStepRecordSnapshot)
                .ToArray());
        }

        private static CopilotAgentStepRecord CreateStepRecordSnapshot(CopilotAgentStepRecord? source)
        {
            source ??= new CopilotAgentStepRecord();
            var sourceToolCall = source.ToolCall ?? new CopilotToolCall();
            var toolInput = CopilotAgentToolInputSnapshot.TryCreate(
                sourceToolCall.ToolInput,
                out var toolInputSnapshot,
                out _)
                    ? toolInputSnapshot
                    : CopilotAgentToolInput.Empty;
            return new CopilotAgentStepRecord
            {
                Round = source.Round,
                ToolCall = new CopilotToolCall
                {
                    ToolName = sourceToolCall.ToolName ?? string.Empty,
                    ToolInput = toolInput,
                    Reason = sourceToolCall.Reason ?? string.Empty,
                    IsFallback = sourceToolCall.IsFallback,
                },
                Observation = CreateObservationSnapshot(source.Observation),
                ModelObservation = source.ModelObservation == null
                    ? null
                    : CreateObservationSnapshot(source.ModelObservation),
                ModelToolResult = source.ModelToolResult ?? string.Empty,
                Execution = source.Execution ?? new CopilotToolExecutionInfo(),
                SuppressModelOutput = source.SuppressModelOutput,
            };
        }

        private static CopilotToolObservation CreateObservationSnapshot(CopilotToolObservation? source)
        {
            source ??= new CopilotToolObservation();
            return new CopilotToolObservation
            {
                Success = source.Success,
                Summary = source.Summary ?? string.Empty,
                Content = source.Content ?? string.Empty,
                ErrorMessage = source.ErrorMessage ?? string.Empty,
                FailureKind = source.FailureKind,
                FailureCode = source.FailureCode ?? string.Empty,
                ProcessOperation = source.ProcessOperation ?? string.Empty,
                ProcessExitCode = source.ProcessExitCode,
                ProcessTimedOut = source.ProcessTimedOut,
                Approval = source.Approval,
                SuggestedReadableLocalFilePaths = Freeze(source.SuggestedReadableLocalFilePaths),
                AttemptedLocalFilePaths = Freeze(source.AttemptedLocalFilePaths),
                SuccessfullyReadLocalFilePaths = Freeze(source.SuccessfullyReadLocalFilePaths),
                LocalFileReadScopes = Freeze(source.LocalFileReadScopes),
                DelegatedRunUsage = source.DelegatedRunUsage,
                DelegatedAnswer = source.DelegatedAnswer,
            };
        }

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

        public string Mode
        {
            get => _mode;
            set
            {
                ThrowIfDetached();
                _mode = value;
            }
        }
        private string _mode = string.Empty;

        public bool ResumedFromCheckpoint
        {
            get => _resumedFromCheckpoint;
            set
            {
                ThrowIfDetached();
                _resumedFromCheckpoint = value;
            }
        }
        private bool _resumedFromCheckpoint;

        public IReadOnlyList<CopilotAgentTaskItem> Items
        {
            get => _items;
            set
            {
                ThrowIfDetached();
                _items = value;
            }
        }
        private IReadOnlyList<CopilotAgentTaskItem> _items = Array.Empty<CopilotAgentTaskItem>();

        private bool _isDetachedSnapshot;

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
                snapshot.Detach();
                return snapshot;
            }
            catch
            {
                var snapshot = new CopilotAgentTaskLedgerSnapshot();
                if (normalize)
                    snapshot.EnsureValid();
                snapshot.Detach();
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
            if (_isDetachedSnapshot)
                return false;

            var originalMode = _mode;
            var originalItems = _items;
            _mode = string.Equals(_mode, "plan", StringComparison.OrdinalIgnoreCase) ? "plan" : "execute";
            var changed = !string.Equals(originalMode, _mode, StringComparison.Ordinal) || originalItems == null;
            var normalizedItems = new List<CopilotAgentTaskItem>();
            var observedIds = new HashSet<int>();
            foreach (var item in _items ?? Array.Empty<CopilotAgentTaskItem>())
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

            _items = Array.AsReadOnly(normalizedItems.ToArray());
            return changed || originalItems?.Count != _items.Count;
        }

        private void Detach()
        {
            foreach (var item in _items)
                item?.Detach();
            _isDetachedSnapshot = true;
        }

        private void ThrowIfDetached()
        {
            if (_isDetachedSnapshot)
                throw new InvalidOperationException("A detached Agent task ledger snapshot cannot be modified.");
        }

    }

    public sealed class CopilotAgentTaskItem
    {
        public const int MaxTitleLength = 256;
        public const int MaxDescriptionLength = 2_048;

        public int Id
        {
            get => _id;
            set
            {
                ThrowIfDetached();
                _id = value;
            }
        }
        private int _id;

        public string Title
        {
            get => _title;
            set
            {
                ThrowIfDetached();
                _title = value;
            }
        }
        private string _title = string.Empty;

        public string Description
        {
            get => _description;
            set
            {
                ThrowIfDetached();
                _description = value;
            }
        }
        private string _description = string.Empty;

        public bool IsComplete
        {
            get => _isComplete;
            set
            {
                ThrowIfDetached();
                _isComplete = value;
            }
        }
        private bool _isComplete;
        private bool _isDetachedSnapshot;

        internal bool Normalize()
        {
            var originalId = _id;
            var originalTitle = _title;
            var originalDescription = _description;
            _id = Math.Max(0, _id);
            _title = BoundText(_title, MaxTitleLength);
            _description = BoundText(_description, MaxDescriptionLength);
            return originalId != _id
                || !string.Equals(originalTitle, _title, StringComparison.Ordinal)
                || !string.Equals(originalDescription, _description, StringComparison.Ordinal);
        }

        internal void Detach()
        {
            _isDetachedSnapshot = true;
        }

        private void ThrowIfDetached()
        {
            if (_isDetachedSnapshot)
                throw new InvalidOperationException("A detached Agent task item snapshot cannot be modified.");
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
