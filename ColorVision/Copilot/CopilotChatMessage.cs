using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Copilot
{
    public enum CopilotChatRole
    {
        User,
        Assistant,
    }

    public enum CopilotAttachmentType
    {
        File,
        Context,
        Image,
        WebPage,
    }

    public readonly record struct CopilotStreamDelta(string ReasoningContent, string Content)
    {
        public static CopilotStreamDelta Empty => new(string.Empty, string.Empty);

        public bool HasReasoning => !string.IsNullOrWhiteSpace(ReasoningContent);

        public bool HasContent => !string.IsNullOrWhiteSpace(Content);

        public bool HasAny => HasReasoning || HasContent;
    }

    public readonly record struct CopilotTokenUsage(int InputTokens, int OutputTokens, int TotalTokens)
    {
        public static CopilotTokenUsage Empty => new(0, 0, 0);

        public bool HasAny => InputTokens > 0 || OutputTokens > 0 || TotalTokens > 0;

        public int EffectiveTotalTokens => TotalTokens > 0 ? TotalTokens : Math.Max(0, InputTokens) + Math.Max(0, OutputTokens);

        public CopilotTokenUsage MergeProgress(CopilotTokenUsage other)
        {
            if (!HasAny)
                return other;

            if (!other.HasAny)
                return this;

            var inputTokens = other.InputTokens > 0 ? Math.Max(InputTokens, other.InputTokens) : InputTokens;
            var outputTokens = other.OutputTokens > 0 ? Math.Max(OutputTokens, other.OutputTokens) : OutputTokens;
            var totalTokens = other.TotalTokens > 0
                ? Math.Max(EffectiveTotalTokens, other.TotalTokens)
                : Math.Max(0, inputTokens) + Math.Max(0, outputTokens);

            return new CopilotTokenUsage(inputTokens, outputTokens, totalTokens);
        }

        public CopilotTokenUsage Add(CopilotTokenUsage other)
        {
            if (!HasAny)
                return other;

            if (!other.HasAny)
                return this;

            var inputTokens = Math.Max(0, InputTokens) + Math.Max(0, other.InputTokens);
            var outputTokens = Math.Max(0, OutputTokens) + Math.Max(0, other.OutputTokens);
            return new CopilotTokenUsage(inputTokens, outputTokens, inputTokens + outputTokens);
        }

        public static string FormatCount(int value)
        {
            var normalized = Math.Max(0, value);
            return normalized >= 1000
                ? $"{normalized / 1000d:0.#}k"
                : normalized.ToString();
        }
    }

    public readonly record struct CopilotChatReply(CopilotStreamDelta Delta, CopilotTokenUsage Usage)
    {
        public static CopilotChatReply Empty => new(CopilotStreamDelta.Empty, CopilotTokenUsage.Empty);

        public string ReasoningContent => Delta.ReasoningContent;

        public string Content => Delta.Content;
    }

    public sealed class CopilotChatMessage : ViewModelBase
    {
        private static readonly char[] ExecutionLineSeparators = { '\r', '\n' };
        private static readonly string[] ExecutionBlockSeparators = { "\r\n\r\n", "\n\n", "\r\r" };

        public CopilotChatMessage()
        {
        }

        public CopilotChatMessage(CopilotChatRole role, string content)
        {
            Role = role;
            _content = content ?? string.Empty;
            CreatedAt = DateTime.Now;
        }

        public CopilotChatRole Role
        {
            get => _role;
            set
            {
                if (SetProperty(ref _role, value))
                {
                    OnPropertyChanged(nameof(IsUser));
                    OnPropertyChanged(nameof(Header));
                }
            }
        }
        private CopilotChatRole _role;

        [JsonIgnore]
        public bool IsUser => Role == CopilotChatRole.User;

        [JsonIgnore]
        public string Header => IsUser ? CopilotUiText.UserHeader : string.IsNullOrWhiteSpace(AssistantName) ? "AI" : AssistantName;

        public string AssistantName
        {
            get => _assistantName;
            set
            {
                if (SetProperty(ref _assistantName, value ?? string.Empty))
                    OnPropertyChanged(nameof(Header));
            }
        }
        private string _assistantName = string.Empty;

        public DateTime CreatedAt
        {
            get => _createdAt;
            set
            {
                if (SetProperty(ref _createdAt, value))
                    OnPropertyChanged(nameof(TimeLabel));
            }
        }
        private DateTime _createdAt = DateTime.Now;

        [JsonIgnore]
        public string TimeLabel => CreatedAt.ToString("HH:mm");

        public string Content
        {
            get => _content;
            set => SetProperty(ref _content, value ?? string.Empty);
        }
        private string _content;

        public string RequestContent
        {
            get => _requestContent;
            set => SetProperty(ref _requestContent, value ?? string.Empty);
        }
        private string _requestContent = string.Empty;

        public CopilotAgentMode RequestMode
        {
            get => _requestMode;
            set => SetProperty(ref _requestMode, value);
        }
        private CopilotAgentMode _requestMode = CopilotAgentMode.Chat;

        [JsonIgnore]
        public string ModelContent => string.IsNullOrWhiteSpace(RequestContent) ? Content : RequestContent;

        public string ExecutionContent
        {
            get => _executionContent;
            set
            {
                if (SetProperty(ref _executionContent, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(HasExecutionTrace));
                    OnPropertyChanged(nameof(HasExecutionFailures));
                    OnPropertyChanged(nameof(HasThinkingTrace));
                    OnPropertyChanged(nameof(ThinkingContent));
                    OnPropertyChanged(nameof(HasThinkingContent));
                    OnPropertyChanged(nameof(LegacyThinkingContent));
                    OnPropertyChanged(nameof(HasLegacyThinkingContent));
                    OnPropertyChanged(nameof(ExecutionSummary));
                    OnPropertyChanged(nameof(ExecutionSummaryToolTip));
                    OnPropertyChanged(nameof(ThinkingSummaryToolTip));
                }
            }
        }
        private string _executionContent = string.Empty;

        public ObservableCollection<CopilotAgentTraceEntry> AgentTraceEntries { get; set; } = new();

        public CopilotAgentTaskLedgerSnapshot AgentTaskLedger
        {
            get => _agentTaskLedger;
            set
            {
                var normalized = value ?? new CopilotAgentTaskLedgerSnapshot();
                normalized.EnsureValid();
                if (SetProperty(ref _agentTaskLedger, normalized))
                    OnAgentTaskStateChanged();
            }
        }
        private CopilotAgentTaskLedgerSnapshot _agentTaskLedger = new();

        public CopilotAgentStopReason AgentStopReason
        {
            get => _agentStopReason;
            set
            {
                var normalized = Enum.IsDefined(value) ? value : CopilotAgentStopReason.None;
                if (SetProperty(ref _agentStopReason, normalized))
                    OnAgentTaskStateChanged();
            }
        }
        private CopilotAgentStopReason _agentStopReason;

        public IReadOnlyList<CopilotAgentBlockerSnapshot> AgentBlockers
        {
            get => _agentBlockers;
            set
            {
                var normalized = (value ?? Array.Empty<CopilotAgentBlockerSnapshot>())
                    .Where(item => item?.IsStructurallyValid() == true)
                    .Take(8)
                    .ToArray();
                if (SetProperty(ref _agentBlockers, normalized))
                    OnAgentTaskStateChanged();
            }
        }
        private IReadOnlyList<CopilotAgentBlockerSnapshot> _agentBlockers = Array.Empty<CopilotAgentBlockerSnapshot>();

        [JsonIgnore]
        public CopilotAgentRecoveryRequest? RecoveryRequest { get; set; }

        [JsonIgnore]
        public bool HasAgentTaskLedger => !IsUser && AgentTaskLedger.TotalCount > 0;

        [JsonIgnore]
        public bool HasAgentTaskState => !IsUser && (HasAgentTaskLedger || HasAgentBlockers || HasRecoverableAgentTasks);

        [JsonIgnore]
        public bool HasIncompleteAgentTasks => HasAgentTaskLedger && AgentTaskLedger.RemainingCount > 0;

        [JsonIgnore]
        public bool HasRecoverableFinalAnswer => !HasIncompleteAgentTasks
            && (AgentStopReason == CopilotAgentStopReason.Interrupted
                || (AgentStopReason is (CopilotAgentStopReason.IncompleteOutput
                        or CopilotAgentStopReason.BudgetExhausted
                        or CopilotAgentStopReason.ProviderFailure)
                    && AgentBlockers.Any(blocker => blocker?.Kind == CopilotAgentBlockerKind.ProviderOutput)));

        [JsonIgnore]
        public bool HasRecoverableAgentTasks => (HasIncompleteAgentTasks
                && AgentStopReason is CopilotAgentStopReason.BudgetExhausted or CopilotAgentStopReason.TaskPassLimit or CopilotAgentStopReason.Paused)
            || (HasIncompleteAgentTasks && AgentStopReason == CopilotAgentStopReason.Interrupted)
            || HasRecoverableFinalAnswer;

        [JsonIgnore]
        public string AgentRecoveryActionLabel => HasRecoverableFinalAnswer
                ? "重试最终回答"
                : AgentTraceEntries?.LastOrDefault(entry => entry != null
            && entry.IsFailure
            && entry.RetryEligible
            && entry.Access == CopilotToolAccess.ReadOnly
            && entry.Idempotency == CopilotToolIdempotency.Idempotent) != null
                ? "重试只读检查"
                : "继续任务";

        [JsonIgnore]
        public string AgentRecoveryToolTip => HasRecoverableFinalAnswer
            ? "仅使用已保存的上下文和证据生成最终回答；不会再次调用工具"
            : "从当前 AgentSession 继续未完成任务；写操作仍需重新审批";

        [JsonIgnore]
        public bool HasAgentBlockers => !IsUser && AgentBlockers.Count > 0;

        [JsonIgnore]
        public string AgentBlockerLabel
        {
            get
            {
                if (AgentBlockers.Count == 0)
                    return string.Empty;
                var blocker = AgentBlockers[0];
                return blocker.Kind switch
                {
                    CopilotAgentBlockerKind.UserDecision => "需要您的决定",
                    CopilotAgentBlockerKind.Approval => "操作未获批准",
                    CopilotAgentBlockerKind.ProviderOutput when blocker.Code == "provider_interrupted" => "模型连接中断",
                    CopilotAgentBlockerKind.ProviderOutput => "模型未返回最终回答",
                    _ when !string.IsNullOrWhiteSpace(blocker.ToolName) => $"{blocker.ToolName} 无法继续",
                    _ => "任务暂时受阻",
                };
            }
        }

        [JsonIgnore]
        public string AgentTaskModeLabel => string.Equals(AgentTaskLedger.Mode, "plan", StringComparison.OrdinalIgnoreCase) ? "计划" : "执行";

        [JsonIgnore]
        public string AgentTaskProgressLabel => $"{AgentTaskLedger.CompletedCount}/{AgentTaskLedger.TotalCount} 已完成";

        [JsonIgnore]
        public string AgentStopReasonLabel => AgentStopReason switch
        {
            CopilotAgentStopReason.Completed => "任务完成",
            CopilotAgentStopReason.AwaitingUser => "等待用户决定",
            CopilotAgentStopReason.ApprovalDenied => "审批未通过",
            CopilotAgentStopReason.BudgetExhausted => "本轮预算已用尽",
            CopilotAgentStopReason.TaskPassLimit => "达到本轮继续上限",
            CopilotAgentStopReason.Blocked => "任务受阻",
            CopilotAgentStopReason.Paused => "任务已暂停",
            CopilotAgentStopReason.Cancelled => "任务已取消",
            CopilotAgentStopReason.IncompleteOutput => "未收到最终回答",
            CopilotAgentStopReason.ProviderFailure => "模型连接中断",
            CopilotAgentStopReason.Interrupted => "应用中断后可恢复",
            _ => "Agent 已停止",
        };

        [JsonIgnore]
        public string AgentTaskSummaryToolTip => $"Agent 任务 · {AgentTaskModeLabel} · {AgentTaskProgressLabel}{Environment.NewLine}{AgentStopReasonLabel}";

        [JsonIgnore]
        public bool HasExecutionTrace => !string.IsNullOrWhiteSpace(ExecutionContent);

        [JsonIgnore]
        public bool HasExecutionFailures => AgentTraceEntries.Any(entry => entry != null
                && entry.IsVisibleInActivity
                && IsFailedTraceState(entry.State))
            || AnalyzeExecutionTrace(FilterDisplayableExecutionContent(ExecutionContent)).FailedCount > 0;

        public bool IsExecutionExpanded
        {
            get => _isExecutionExpanded;
            set => SetProperty(ref _isExecutionExpanded, value);
        }
        private bool _isExecutionExpanded = true;

        public bool IsExecutionInProgress
        {
            get => _isExecutionInProgress;
            set
            {
                if (SetProperty(ref _isExecutionInProgress, value))
                {
                    OnPropertyChanged(nameof(IsThinkingInProgress));
                    OnPropertyChanged(nameof(HasThinkingTrace));
                    OnPropertyChanged(nameof(ThinkingHeader));
                    OnPropertyChanged(nameof(ThinkingSummaryToolTip));
                    OnPropertyChanged(nameof(ExecutionHeader));
                    OnPropertyChanged(nameof(ExecutionSummary));
                    OnPropertyChanged(nameof(ExecutionSummaryToolTip));
                }
            }
        }
        private bool _isExecutionInProgress;

        [JsonIgnore]
        public string ExecutionHeader => IsExecutionInProgress ? CopilotUiText.ExecutionInProgressHeader : CopilotUiText.ExecutionHeader;

        [JsonIgnore]
        public string ExecutionSummary
        {
            get
            {
                var visibleEntries = VisibleAgentTraceEntries;
                return visibleEntries.Count > 0
                    ? BuildAgentTraceSummary(visibleEntries, IsExecutionInProgress)
                    : BuildExecutionSummary(FilterDisplayableExecutionContent(ExecutionContent), IsExecutionInProgress);
            }
        }

        [JsonIgnore]
        public string ExecutionSummaryToolTip
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ExecutionContent))
                    return ExecutionSummary;

                return $"{ExecutionHeader}: {ExecutionSummary}{Environment.NewLine}{Environment.NewLine}{TrimForTooltip(ExecutionContent)}";
            }
        }

        public string ReasoningContent
        {
            get => _reasoningContent;
            set
            {
                if (SetProperty(ref _reasoningContent, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(HasReasoning));
                    OnPropertyChanged(nameof(HasThinkingTrace));
                    OnPropertyChanged(nameof(ThinkingContent));
                    OnPropertyChanged(nameof(HasThinkingContent));
                    OnPropertyChanged(nameof(LegacyThinkingContent));
                    OnPropertyChanged(nameof(HasLegacyThinkingContent));
                    OnPropertyChanged(nameof(ThinkingSummaryToolTip));
                }
            }
        }
        private string _reasoningContent = string.Empty;

        [JsonIgnore]
        public bool HasReasoning => !string.IsNullOrWhiteSpace(ReasoningContent);

        public bool IsReasoningExpanded
        {
            get => _isReasoningExpanded;
            set => SetProperty(ref _isReasoningExpanded, value);
        }
        private bool _isReasoningExpanded = true;

        public bool IsThinkingExpanded
        {
            get => _isThinkingExpanded;
            set => SetProperty(ref _isThinkingExpanded, value);
        }
        private bool _isThinkingExpanded;

        public bool IsReasoningInProgress
        {
            get => _isReasoningInProgress;
            set
            {
                if (SetProperty(ref _isReasoningInProgress, value))
                {
                    OnPropertyChanged(nameof(IsThinkingInProgress));
                    OnPropertyChanged(nameof(HasThinkingTrace));
                    OnPropertyChanged(nameof(ThinkingHeader));
                    OnPropertyChanged(nameof(ThinkingSummaryToolTip));
                    OnPropertyChanged(nameof(ReasoningHeader));
                }
            }
        }
        private bool _isReasoningInProgress;

        [JsonIgnore]
        public string ReasoningHeader => IsReasoningInProgress ? CopilotUiText.ReasoningInProgressHeader : CopilotUiText.ReasoningHeader;

        public DateTime ThinkingStartedAt
        {
            get => _thinkingStartedAt;
            set
            {
                if (SetProperty(ref _thinkingStartedAt, value))
                {
                    OnPropertyChanged(nameof(ThinkingHeader));
                    OnPropertyChanged(nameof(ThinkingSummaryToolTip));
                }
            }
        }
        private DateTime _thinkingStartedAt;

        public DateTime ThinkingCompletedAt
        {
            get => _thinkingCompletedAt;
            set
            {
                if (SetProperty(ref _thinkingCompletedAt, value))
                {
                    OnPropertyChanged(nameof(ThinkingHeader));
                    OnPropertyChanged(nameof(ThinkingSummaryToolTip));
                }
            }
        }
        private DateTime _thinkingCompletedAt;

        [JsonIgnore]
        public bool IsThinkingInProgress => _isProcessingInProgress || IsExecutionInProgress || IsReasoningInProgress;

        private bool _isProcessingInProgress;

        [JsonIgnore]
        public bool HasThinkingTrace => HasExecutionTrace || HasReasoning || IsThinkingInProgress || ThinkingStartedAt != default;

        [JsonIgnore]
        public bool HasThinkingContent => !string.IsNullOrWhiteSpace(ThinkingContent);

        [JsonIgnore]
        public bool HasAgentTraceEntries => VisibleAgentTraceEntries.Count > 0;

        [JsonIgnore]
        public IReadOnlyList<CopilotAgentTraceEntry> VisibleAgentTraceEntries => AgentTraceEntries
            .Where(entry => entry != null && entry.IsVisibleInActivity)
            .ToArray();

        [JsonIgnore]
        public string LegacyThinkingContent => HasAgentTraceEntries ? string.Empty : BuildThinkingContent(ExecutionContent, ReasoningContent);

        [JsonIgnore]
        public bool HasLegacyThinkingContent => !string.IsNullOrWhiteSpace(LegacyThinkingContent);

        [JsonIgnore]
        public string ThinkingHeader
        {
            get
            {
                if (IsThinkingInProgress)
                    return CopilotUiText.ProcessingHeader;

                var elapsed = FormatCompletedProcessingElapsed();
                return string.IsNullOrWhiteSpace(elapsed)
                    ? CopilotUiText.ProcessedHeader
                    : $"{CopilotUiText.ProcessedHeader} {elapsed}";
            }
        }

        [JsonIgnore]
        public string ThinkingContent => HasAgentTraceEntries
            ? string.Join(Environment.NewLine, VisibleAgentTraceEntries.Select(entry => entry.ActivityLabel))
            : LegacyThinkingContent;

        [JsonIgnore]
        public string ThinkingSummaryToolTip => ThinkingHeader;

        public void MarkThinkingStarted()
        {
            _isProcessingInProgress = true;

            if (ThinkingStartedAt == default)
                ThinkingStartedAt = DateTime.Now;

            ThinkingCompletedAt = default;
            IsThinkingExpanded = true;
            OnPropertyChanged(nameof(IsThinkingInProgress));
            OnPropertyChanged(nameof(HasThinkingTrace));
            OnPropertyChanged(nameof(HasThinkingContent));
            OnPropertyChanged(nameof(ThinkingHeader));
            OnPropertyChanged(nameof(ThinkingSummaryToolTip));
        }

        public void MarkThinkingCompleted()
        {
            _isProcessingInProgress = false;

            if (ThinkingStartedAt == default)
                ThinkingStartedAt = CreatedAt == default ? DateTime.Now : CreatedAt;

            if (ThinkingCompletedAt == default)
                ThinkingCompletedAt = DateTime.Now;

            IsThinkingExpanded = false;
            OnPropertyChanged(nameof(IsThinkingInProgress));
            OnPropertyChanged(nameof(HasThinkingTrace));
            OnPropertyChanged(nameof(HasThinkingContent));
            OnPropertyChanged(nameof(ThinkingHeader));
            OnPropertyChanged(nameof(ThinkingSummaryToolTip));
        }

        public bool EnsureValid()
        {
            var changed = false;

            if (CreatedAt == default)
            {
                CreatedAt = DateTime.Now;
                changed = true;
            }

            if (_content == null)
            {
                Content = string.Empty;
                changed = true;
            }

            if (_reasoningContent == null)
            {
                ReasoningContent = string.Empty;
                changed = true;
            }

            if (_executionContent == null)
            {
                ExecutionContent = string.Empty;
                changed = true;
            }

            if (AgentTraceEntries == null)
            {
                AgentTraceEntries = new ObservableCollection<CopilotAgentTraceEntry>();
                changed = true;
            }

            if (_agentTaskLedger == null)
            {
                _agentTaskLedger = new CopilotAgentTaskLedgerSnapshot();
                changed = true;
            }
            else
            {
                changed |= _agentTaskLedger.EnsureValid();
            }

            if (!Enum.IsDefined(AgentStopReason))
            {
                AgentStopReason = CopilotAgentStopReason.None;
                changed = true;
            }

            var validBlockers = (_agentBlockers ?? Array.Empty<CopilotAgentBlockerSnapshot>())
                .Where(item => item?.IsStructurallyValid() == true)
                .Take(8)
                .ToArray();
            if (_agentBlockers == null || validBlockers.Length != _agentBlockers.Count)
            {
                _agentBlockers = validBlockers;
                changed = true;
            }

            for (var index = AgentTraceEntries.Count - 1; index >= 0; index--)
            {
                if (AgentTraceEntries[index] != null)
                    continue;

                AgentTraceEntries.RemoveAt(index);
                changed = true;
            }

            var recoveredAtUtc = DateTimeOffset.UtcNow;
            foreach (var entry in AgentTraceEntries)
                changed |= entry.EnsureValid(recoveredAtUtc);

            if (AgentTraceEntries.Count > 0)
            {
                var previousExecutionContent = ExecutionContent;
                RebuildExecutionContentFromAgentTrace();
                changed |= !string.Equals(previousExecutionContent, ExecutionContent, StringComparison.Ordinal);
            }

            if (IsExecutionInProgress || IsReasoningInProgress)
            {
                IsExecutionInProgress = false;
                IsReasoningInProgress = false;
                _isProcessingInProgress = false;
                if (ThinkingCompletedAt == default)
                    ThinkingCompletedAt = DateTime.Now;
                changed = true;
            }

            if (!IsThinkingInProgress && HasThinkingTrace)
            {
                IsThinkingExpanded = false;
                OnPropertyChanged(nameof(IsThinkingExpanded));
                OnPropertyChanged(nameof(ThinkingHeader));
            }

            if (_requestContent == null)
            {
                RequestContent = string.Empty;
                changed = true;
            }

            if (_assistantName == null)
            {
                AssistantName = string.Empty;
                changed = true;
            }

            if (!Enum.IsDefined(RequestMode))
            {
                RequestMode = CopilotAgentMode.Chat;
                changed = true;
            }

            return changed;
        }

        private void OnAgentTaskStateChanged()
        {
            OnPropertyChanged(nameof(HasAgentTaskLedger));
            OnPropertyChanged(nameof(HasAgentTaskState));
            OnPropertyChanged(nameof(HasIncompleteAgentTasks));
            OnPropertyChanged(nameof(HasRecoverableFinalAnswer));
            OnPropertyChanged(nameof(HasRecoverableAgentTasks));
            OnPropertyChanged(nameof(AgentRecoveryActionLabel));
            OnPropertyChanged(nameof(AgentRecoveryToolTip));
            OnPropertyChanged(nameof(HasAgentBlockers));
            OnPropertyChanged(nameof(AgentBlockerLabel));
            OnPropertyChanged(nameof(AgentTaskModeLabel));
            OnPropertyChanged(nameof(AgentTaskProgressLabel));
            OnPropertyChanged(nameof(AgentStopReasonLabel));
            OnPropertyChanged(nameof(AgentTaskSummaryToolTip));
        }

        public void UpsertAgentTrace(CopilotAgentTraceEntry traceEntry)
        {
            ArgumentNullException.ThrowIfNull(traceEntry);
            AgentTraceEntries ??= new ObservableCollection<CopilotAgentTraceEntry>();

            var index = AgentTraceEntries
                .Select((entry, entryIndex) => new { entry, entryIndex })
                .FirstOrDefault(item => !string.IsNullOrWhiteSpace(traceEntry.CallId)
                    && string.Equals(item.entry.CallId, traceEntry.CallId, StringComparison.Ordinal))
                ?.entryIndex ?? -1;

            if (index >= 0)
                AgentTraceEntries[index] = traceEntry;
            else
                AgentTraceEntries.Add(traceEntry);

            RebuildExecutionContentFromAgentTrace();
            OnPropertyChanged(nameof(AgentRecoveryActionLabel));
            OnPropertyChanged(nameof(AgentRecoveryToolTip));
        }

        public void RebuildExecutionContentFromAgentTrace()
        {
            if (AgentTraceEntries == null || AgentTraceEntries.Count == 0)
                return;

            var blocks = AgentTraceEntries
                .Where(entry => entry != null)
                .Select(BuildAgentTraceBlock)
                .Where(block => !string.IsNullOrWhiteSpace(block));
            ExecutionContent = string.Join(Environment.NewLine + Environment.NewLine, blocks);
            OnPropertyChanged(nameof(HasAgentTraceEntries));
            OnPropertyChanged(nameof(VisibleAgentTraceEntries));
            OnPropertyChanged(nameof(ThinkingContent));
            OnPropertyChanged(nameof(HasThinkingContent));
            OnPropertyChanged(nameof(LegacyThinkingContent));
            OnPropertyChanged(nameof(HasLegacyThinkingContent));
            OnPropertyChanged(nameof(HasExecutionFailures));
            OnPropertyChanged(nameof(ExecutionSummary));
            OnPropertyChanged(nameof(ExecutionSummaryToolTip));
        }

        private static string BuildAgentTraceBlock(CopilotAgentTraceEntry entry)
        {
            return entry.DiagnosticDetails;
        }

        private static string BuildAgentTraceSummary(IReadOnlyList<CopilotAgentTraceEntry> entries, bool isInProgress)
        {
            var failedCount = entries.Count(entry => entry != null && IsFailedTraceState(entry.State));
            var latestTool = entries.LastOrDefault(entry => entry != null)?.ToolName ?? string.Empty;
            var isAwaitingApproval = entries.Any(entry => entry?.State == CopilotToolExecutionState.AwaitingApproval);
            var hasRunningTool = entries.Any(entry => entry?.State is CopilotToolExecutionState.Pending or CopilotToolExecutionState.Running);
            var builder = new StringBuilder(isAwaitingApproval
                ? "Awaiting approval"
                : (isInProgress || hasRunningTool ? "Running" : "Completed"));
            builder.Append(" - ").Append(entries.Count).Append(entries.Count == 1 ? " tool" : " tools");
            if (failedCount > 0)
                builder.Append(" - ").Append(failedCount).Append(" failed");
            if (!string.IsNullOrWhiteSpace(latestTool))
                builder.Append(" - latest ").Append(TrimForInline(latestTool));
            return builder.ToString();
        }

        private static bool IsFailedTraceState(CopilotToolExecutionState state)
        {
            return state is CopilotToolExecutionState.Failed
                or CopilotToolExecutionState.TimedOut
                or CopilotToolExecutionState.Denied
                or CopilotToolExecutionState.Cancelled
                or CopilotToolExecutionState.Interrupted;
        }

        private static string FormatTraceState(CopilotToolExecutionState state) => state switch
        {
            CopilotToolExecutionState.Pending => "Pending",
            CopilotToolExecutionState.Running => "Running...",
            CopilotToolExecutionState.Completed => "Completed",
            CopilotToolExecutionState.Failed => "Failed",
            CopilotToolExecutionState.TimedOut => "Timed out",
            CopilotToolExecutionState.Denied => "Denied",
            CopilotToolExecutionState.Cancelled => "Cancelled",
            CopilotToolExecutionState.Interrupted => "Interrupted",
            CopilotToolExecutionState.AwaitingApproval => "Awaiting approval",
            _ => "Unknown",
        };

        private static string FormatTraceDuration(long durationMs)
        {
            if (durationMs < 1000)
                return $"{Math.Max(0, durationMs)}ms";

            return $"{durationMs / 1000d:0.#}s";
        }

        private static string BuildThinkingContent(string? executionContent, string? reasoningContent)
        {
            var builder = new StringBuilder();
            var execution = FilterDisplayableExecutionContent(executionContent);
            var reasoning = (reasoningContent ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(execution))
                builder.AppendLine(execution);

            if (!string.IsNullOrWhiteSpace(reasoning))
            {
                if (builder.Length > 0)
                    builder.AppendLine().AppendLine();

                builder.AppendLine(CopilotUiText.ThinkingDetailsHeader);
                builder.AppendLine(reasoning);
            }

            return builder.ToString().TrimEnd();
        }

        private string FormatCompletedProcessingElapsed()
        {
            var startedAt = ThinkingStartedAt == default ? CreatedAt : ThinkingStartedAt;
            if (IsThinkingInProgress || startedAt == default || ThinkingCompletedAt == default || ThinkingCompletedAt < startedAt)
                return string.Empty;

            var elapsed = ThinkingCompletedAt - startedAt;
            if (elapsed.TotalSeconds < 1)
                return "<1s";

            var totalSeconds = Math.Max(1, (int)Math.Floor(elapsed.TotalSeconds));
            var hours = totalSeconds / 3600;
            var minutes = totalSeconds % 3600 / 60;
            var seconds = totalSeconds % 60;

            if (hours > 0)
                return $"{hours}h {minutes}m {seconds}s";

            return minutes > 0 ? $"{minutes}m {seconds}s" : $"{seconds}s";
        }

        private static string FilterDisplayableExecutionContent(string? content)
        {
            var text = (content ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var blocks = text.Split(ExecutionBlockSeparators, StringSplitOptions.RemoveEmptyEntries);
            var keptBlocks = blocks
                .Select(FilterExecutionBlock)
                .Where(block => !string.IsNullOrWhiteSpace(block))
                .ToArray();

            return string.Join(Environment.NewLine + Environment.NewLine, keptBlocks).Trim();
        }

        private static string FilterExecutionBlock(string block)
        {
            var lines = block
                .Split(ExecutionLineSeparators, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();

            if (lines.Length == 0 || IsHiddenExecutionBlock(lines))
                return string.Empty;

            var keptLines = lines.Where(line => !IsHiddenExecutionLine(line)).ToArray();
            return string.Join(Environment.NewLine, keptLines).Trim();
        }

        private static bool IsHiddenExecutionBlock(string[] lines)
        {
            if (IsFailedSearchExecutionBlock(lines))
                return true;

            return lines.All(IsHiddenExecutionLine);
        }

        private static bool IsFailedSearchExecutionBlock(string[] lines)
        {
            var mentionsSearchTool = lines.Any(line =>
                line.Contains("SearchFiles", StringComparison.OrdinalIgnoreCase)
                || line.Contains("GrepText", StringComparison.OrdinalIgnoreCase)
                || line.Contains("SearchDocs", StringComparison.OrdinalIgnoreCase)
                || line.Contains("WebSearch", StringComparison.OrdinalIgnoreCase));
            if (!mentionsSearchTool)
                return false;

            return lines.Any(line =>
                line.StartsWith("Status: Failed", StringComparison.OrdinalIgnoreCase)
                || line.Contains("] Failed", StringComparison.OrdinalIgnoreCase)
                || line.Contains("] Timed out", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsHiddenExecutionLine(string line)
        {
            return line.Equals("Analyzing task...", StringComparison.OrdinalIgnoreCase)
                || line.Equals("Generating answer...", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Round ", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Tool phase converged", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("No extra tools are needed", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Reused the context", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Agent Skills enabled", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("MCP client", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildExecutionSummary(string? content, bool isInProgress)
        {
            var text = content ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return isInProgress ? "Starting" : string.Empty;

            var (toolCount, failedCount, latestTool) = AnalyzeExecutionTrace(text);

            if (toolCount == 0)
            {
                var firstLine = text
                    .Split(ExecutionLineSeparators, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
                return isInProgress
                    ? TrimForInline(string.IsNullOrWhiteSpace(firstLine) ? "Running" : firstLine)
                    : "Trace available";
            }

            var builder = new StringBuilder(isInProgress ? "Running" : "Completed");
            builder.Append(" - ").Append(toolCount).Append(toolCount == 1 ? " tool" : " tools");

            if (failedCount > 0)
                builder.Append(" - ").Append(failedCount).Append(" failed");

            if (!string.IsNullOrWhiteSpace(latestTool))
                builder.Append(" - latest ").Append(TrimForInline(latestTool));

            return builder.ToString();
        }

        private static (int ToolCount, int FailedCount, string LatestTool) AnalyzeExecutionTrace(string? content)
        {
            var toolCount = 0;
            var failedCount = 0;
            var latestTool = string.Empty;

            foreach (var rawLine in (content ?? string.Empty).Split(ExecutionLineSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                if (line.Length > 2 && line[0] == '[')
                {
                    var closeIndex = line.IndexOf(']');
                    if (closeIndex > 1)
                    {
                        latestTool = line[1..closeIndex].Trim();
                        toolCount++;
                    }
                }

                if (line.StartsWith("Status:", StringComparison.OrdinalIgnoreCase)
                    && line.Contains("Failed", StringComparison.OrdinalIgnoreCase))
                {
                    failedCount++;
                }
            }

            return (toolCount, failedCount, latestTool);
        }

        private static string TrimForInline(string value)
        {
            var text = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length <= 48 ? text : text[..48] + "...";
        }

        private static string TrimForTooltip(string value)
        {
            var text = (value ?? string.Empty).Trim();
            return text.Length <= 1600 ? text : text[..1600] + Environment.NewLine + "...";
        }
    }

    public sealed class CopilotConversationRecord : ViewModelBase
    {
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, NormalizeText(value));
        }
        private string _id = Guid.NewGuid().ToString("N");

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, NormalizeText(value));
        }
        private string _title = CopilotUiText.NewConversationTitle;

        public bool HasCustomTitle
        {
            get => _hasCustomTitle;
            set => SetProperty(ref _hasCustomTitle, value);
        }
        private bool _hasCustomTitle;

        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                if (SetProperty(ref _isPinned, value))
                {
                    OnPropertyChanged(nameof(PinLabel));
                    OnPropertyChanged(nameof(PinMenuText));
                }
            }
        }
        private bool _isPinned;

        public string PreviewText
        {
            get => _previewText;
            set => SetProperty(ref _previewText, value ?? string.Empty);
        }
        private string _previewText = CopilotUiText.EmptyConversationPreview;

        public string ProfileId
        {
            get => _profileId;
            set => SetProperty(ref _profileId, NormalizeText(value));
        }
        private string _profileId = string.Empty;

        public string ProfileDisplayName
        {
            get => _profileDisplayName;
            set => SetProperty(ref _profileDisplayName, NormalizeText(value));
        }
        private string _profileDisplayName = string.Empty;

        public int LastUsageInputTokens
        {
            get => _lastUsageInputTokens;
            set => SetProperty(ref _lastUsageInputTokens, Math.Max(0, value));
        }
        private int _lastUsageInputTokens;

        public int LastUsageOutputTokens
        {
            get => _lastUsageOutputTokens;
            set => SetProperty(ref _lastUsageOutputTokens, Math.Max(0, value));
        }
        private int _lastUsageOutputTokens;

        public int LastUsageTotalTokens
        {
            get => _lastUsageTotalTokens;
            set => SetProperty(ref _lastUsageTotalTokens, Math.Max(0, value));
        }
        private int _lastUsageTotalTokens;

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }
        private DateTime _createdAt = DateTime.Now;

        public DateTime UpdatedAt
        {
            get => _updatedAt;
            set
            {
                if (SetProperty(ref _updatedAt, value))
                    OnPropertyChanged(nameof(UpdatedLabel));
            }
        }
        private DateTime _updatedAt = DateTime.Now;

        public ObservableCollection<CopilotChatMessage> Messages { get; set; } = new();

        public ObservableCollection<CopilotAttachmentItem> Attachments { get; set; } = new();

        public CopilotAgentSessionCheckpoint? AgentSessionCheckpoint { get; set; }

        [JsonIgnore]
        public string UpdatedLabel => UpdatedAt.Date == DateTime.Today ? UpdatedAt.ToString("HH:mm") : UpdatedAt.ToString("M/d");

        [JsonIgnore]
        public string PinLabel => IsPinned ? CopilotUiText.PinnedLabel : string.Empty;

        [JsonIgnore]
        public string PinMenuText => IsPinned ? CopilotUiText.UnpinMenuText : CopilotUiText.PinMenuText;

        [JsonIgnore]
        public string AgentRunStatusLabel
        {
            get => _agentRunStatusLabel;
            internal set
            {
                if (SetProperty(ref _agentRunStatusLabel, value ?? string.Empty))
                    OnPropertyChanged(nameof(HasAgentRunStatus));
            }
        }
        private string _agentRunStatusLabel = string.Empty;

        [JsonIgnore]
        public bool HasAgentRunStatus => !string.IsNullOrWhiteSpace(AgentRunStatusLabel);

        [JsonIgnore]
        public CopilotTokenUsage LastUsage => new(LastUsageInputTokens, LastUsageOutputTokens, LastUsageTotalTokens);

        public bool EnsureValid()
        {
            var changed = false;

            if (string.IsNullOrWhiteSpace(Id))
            {
                Id = Guid.NewGuid().ToString("N");
                changed = true;
            }

            if (CreatedAt == default)
            {
                CreatedAt = DateTime.Now;
                changed = true;
            }

            if (UpdatedAt == default)
            {
                UpdatedAt = CreatedAt;
                changed = true;
            }

            Messages ??= new ObservableCollection<CopilotChatMessage>();
            Attachments ??= new ObservableCollection<CopilotAttachmentItem>();
            if (AgentSessionCheckpoint != null && !AgentSessionCheckpoint.IsStructurallyValid())
            {
                AgentSessionCheckpoint = null;
                changed = true;
            }

            foreach (var message in Messages)
            {
                changed |= message.EnsureValid();
            }

            foreach (var attachment in Attachments)
            {
                changed |= attachment.EnsureValid();
            }

            return changed;
        }

        public void Touch()
        {
            UpdatedAt = DateTime.Now;
        }

        public void SetLastUsage(CopilotTokenUsage usage)
        {
            LastUsageInputTokens = usage.InputTokens;
            LastUsageOutputTokens = usage.OutputTokens;
            LastUsageTotalTokens = usage.EffectiveTotalTokens;
        }

        public void ClearLastUsage()
        {
            LastUsageInputTokens = 0;
            LastUsageOutputTokens = 0;
            LastUsageTotalTokens = 0;
        }

        public void RefreshSummary()
        {
            var firstUserMessage = Messages.FirstOrDefault(message => message.Role == CopilotChatRole.User && !string.IsNullOrWhiteSpace(message.Content));
            var generatedTitle = firstUserMessage == null ? CopilotUiText.NewConversationTitle : BuildPreview(firstUserMessage.Content, 24);
            if (!HasCustomTitle || string.IsNullOrWhiteSpace(Title))
                Title = generatedTitle;

            var lastVisibleMessage = Messages.LastOrDefault(message => !string.IsNullOrWhiteSpace(message.Content));
            if (lastVisibleMessage != null)
            {
                PreviewText = BuildPreview(lastVisibleMessage.Content, 42);
                return;
            }

            PreviewText = Attachments.Count > 0
                ? CopilotUiText.FormatAttachmentMountedCount(Attachments.Count)
                : CopilotUiText.EmptyConversationPreview;
        }

        public void SetCustomTitle(string title)
        {
            Title = title;
            HasCustomTitle = true;
        }

        public void SetGeneratedTitle(string title)
        {
            Title = title;
            HasCustomTitle = true;
        }

        public static CopilotConversationRecord CreateEmpty(string profileId, string profileDisplayName)
        {
            return new CopilotConversationRecord
            {
                HasCustomTitle = false,
                ProfileId = profileId,
                ProfileDisplayName = profileDisplayName,
                Title = CopilotUiText.NewConversationTitle,
                PreviewText = CopilotUiText.EmptyConversationPreview,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
            };
        }

        private static string BuildPreview(string content, int maxLength)
        {
            var normalized = (content ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (normalized.Length <= maxLength)
                return normalized;

            return normalized[..maxLength] + "...";
        }

        private static string NormalizeText(string? value) => value?.Trim() ?? string.Empty;
    }

    public sealed class CopilotAttachmentItem : ViewModelBase
    {
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, NormalizeText(value));
        }
        private string _id = Guid.NewGuid().ToString("N");

        public CopilotAttachmentType Type
        {
            get => _type;
            set
            {
                if (SetProperty(ref _type, value))
                {
                    ResetPreviewImage();
                    OnPropertyChanged(nameof(BadgeText));
                    OnPropertyChanged(nameof(DisplayLabel));
                }
            }
        }
        private CopilotAttachmentType _type;

        public string Title
        {
            get => _title;
            set
            {
                if (SetProperty(ref _title, NormalizeText(value)))
                    OnPropertyChanged(nameof(DisplayLabel));
            }
        }
        private string _title = string.Empty;

        public string Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value?.Trim() ?? string.Empty))
                {
                    ResetPreviewImage();
                    OnPropertyChanged(nameof(DisplayLabel));
                    OnPropertyChanged(nameof(TooltipText));
                }
            }
        }
        private string _value = string.Empty;

        public string Source
        {
            get => _source;
            set
            {
                if (SetProperty(ref _source, value?.Trim() ?? string.Empty))
                    OnPropertyChanged(nameof(TooltipText));
            }
        }
        private string _source = string.Empty;

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }
        private DateTime _createdAt = DateTime.Now;

        [JsonIgnore]
        public string BadgeText => Type switch
        {
            CopilotAttachmentType.File => CopilotUiText.FileBadge,
            CopilotAttachmentType.Image => CopilotUiText.ImageBadge,
            CopilotAttachmentType.WebPage => CopilotUiText.WebPageBadge,
            _ => CopilotUiText.ContextBadge,
        };

        [JsonIgnore]
        public string DisplayLabel
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Title))
                    return Title;

                if (Type == CopilotAttachmentType.File || Type == CopilotAttachmentType.Image)
                    return Path.GetFileName(Value);

                if (Type == CopilotAttachmentType.WebPage)
                    return TryGetHostLabel(Source);

                return BuildPreview(Value, 20);
            }
        }

        [JsonIgnore]
        public string TooltipText => Type == CopilotAttachmentType.WebPage && !string.IsNullOrWhiteSpace(Source)
            ? Source
            : Value;

        [JsonIgnore]
        public ImageSource? PreviewImage
        {
            get
            {
                if (Type != CopilotAttachmentType.Image || string.IsNullOrWhiteSpace(Value) || !File.Exists(Value))
                    return null;

                if (_previewImage != null && string.Equals(_previewImagePath, Value, StringComparison.OrdinalIgnoreCase))
                    return _previewImage;

                try
                {
                    using var stream = new FileStream(Value, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze();

                    _previewImage = image;
                    _previewImagePath = Value;
                    return _previewImage;
                }
                catch
                {
                    return null;
                }
            }
        }

        [JsonIgnore]
        public bool HasPreviewImage => PreviewImage != null;

        [JsonIgnore]
        public bool IsImage => Type == CopilotAttachmentType.Image;

        [JsonIgnore]
        public bool IsStoredImageFile => Type == CopilotAttachmentType.Image && !string.IsNullOrWhiteSpace(Value);

        [JsonIgnore]
        public string ImageFallbackText => HasPreviewImage ? string.Empty : CopilotUiText.ImagePreviewUnavailable;

        [JsonIgnore]
        public string ImageMetaText => CreatedAt.ToString("M/d HH:mm");

        private ImageSource? _previewImage;

        private string _previewImagePath = string.Empty;

        public bool EnsureValid()
        {
            var changed = false;

            if (string.IsNullOrWhiteSpace(Id))
            {
                Id = Guid.NewGuid().ToString("N");
                changed = true;
            }

            if (CreatedAt == default)
            {
                CreatedAt = DateTime.Now;
                changed = true;
            }

            if (_value == null)
            {
                Value = string.Empty;
                changed = true;
            }

            if (_title == null)
            {
                Title = string.Empty;
                changed = true;
            }

            if (_source == null)
            {
                Source = string.Empty;
                changed = true;
            }

            return changed;
        }

        public static CopilotAttachmentItem CreateFile(string filePath)
        {
            return new CopilotAttachmentItem
            {
                Type = CopilotAttachmentType.File,
                Title = Path.GetFileName(filePath),
                Value = filePath,
                CreatedAt = DateTime.Now,
            };
        }

        public static CopilotAttachmentItem CreateContext(string text, string? title = null, string? source = null)
        {
            return new CopilotAttachmentItem
            {
                Type = CopilotAttachmentType.Context,
                Title = string.IsNullOrWhiteSpace(title) ? BuildPreview(text, 18) : title,
                Source = source ?? string.Empty,
                Value = text,
                CreatedAt = DateTime.Now,
            };
        }

        public static CopilotAttachmentItem CreateImage(string imagePath, string? title = null)
        {
            return new CopilotAttachmentItem
            {
                Type = CopilotAttachmentType.Image,
                Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(imagePath) : title,
                Value = imagePath,
                CreatedAt = DateTime.Now,
            };
        }

        public static CopilotAttachmentItem CreateWebPage(string url, string title, string content)
        {
            return new CopilotAttachmentItem
            {
                Type = CopilotAttachmentType.WebPage,
                Title = title,
                Source = url,
                Value = content,
                CreatedAt = DateTime.Now,
            };
        }

        private void ResetPreviewImage()
        {
            _previewImage = null;
            _previewImagePath = string.Empty;
            OnPropertyChanged(nameof(PreviewImage));
            OnPropertyChanged(nameof(HasPreviewImage));
            OnPropertyChanged(nameof(ImageFallbackText));
        }

        private static string BuildPreview(string content, int maxLength)
        {
            var normalized = (content ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (normalized.Length <= maxLength)
                return normalized;

            return normalized[..maxLength] + "...";
        }

        private static string TryGetHostLabel(string? value)
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
                return uri.Host;

            return BuildPreview(value ?? string.Empty, 20);
        }

        private static string NormalizeText(string? value) => value?.Trim() ?? string.Empty;
    }

    public readonly record struct CopilotRequestMessage(string Role, string Content);

    public sealed class CopilotProviderOption
    {
        public string Label { get; init; } = string.Empty;

        public CopilotProviderType Value { get; init; }
    }
}
