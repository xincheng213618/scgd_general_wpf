using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotApprovalScope(
        string ConversationId,
        string ActiveAgentConversationId,
        string ActiveAgentTaskId,
        string WorkspacePath)
    {
        public CopilotConfirmationReviewContext ToReviewContext()
        {
            var conversationId = ConversationId ?? string.Empty;
            var taskId = string.Equals(
                conversationId,
                ActiveAgentConversationId ?? string.Empty,
                StringComparison.Ordinal)
                ? ActiveAgentTaskId ?? string.Empty
                : string.Empty;
            return new CopilotConfirmationReviewContext(
                conversationId,
                taskId,
                WorkspacePath ?? string.Empty);
        }
    }

    internal readonly record struct CopilotApprovalPendingProjection(
        int VisibleCount,
        int TotalPendingCount);

    internal enum CopilotApprovalDecisionOutcome
    {
        Invalid,
        Approved,
        ApprovedAndExecuted,
        Rejected,
        ExecutionFailed,
    }

    internal readonly record struct CopilotApprovalDecisionResult(
        CopilotApprovalDecisionOutcome Outcome,
        bool Success,
        bool ExecutedImmediately,
        string Message)
    {
        public static CopilotApprovalDecisionResult Invalid(string message) => new(
            CopilotApprovalDecisionOutcome.Invalid,
            Success: false,
            ExecutedImmediately: false,
            message ?? string.Empty);
    }

    internal sealed record CopilotApprovalActionTransition(
        string ActionId,
        string AgentCallId,
        CopilotApprovalSourceKind SourceKind,
        string ConversationId,
        ConfirmableActionStatus Status,
        bool ResumesAgentOnApproval,
        bool? ExecutionSucceeded,
        string ExecutionResultText,
        DateTimeOffset? CompletedAt,
        DateTimeOffset ObservedAtUtc)
    {
        public static CopilotApprovalActionTransition Capture(
            ConfirmableAction action,
            DateTimeOffset observedAtUtc)
        {
            ArgumentNullException.ThrowIfNull(action);
            return new CopilotApprovalActionTransition(
                action.ActionId,
                action.AgentCallId,
                action.RequestContext.SourceKind,
                action.RequestContext.ConversationId,
                action.Status,
                action.ResumesAgentOnApproval,
                action.ExecutionSucceeded,
                action.ExecutionResultText,
                action.CompletedAt,
                observedAtUtc);
        }
    }

    internal sealed class CopilotApprovalActionTransitionEventArgs : EventArgs
    {
        public CopilotApprovalActionTransitionEventArgs(CopilotApprovalActionTransition transition)
        {
            Transition = transition ?? throw new ArgumentNullException(nameof(transition));
        }

        public CopilotApprovalActionTransition Transition { get; }
    }

    internal readonly record struct CopilotApprovalTraceUpdateResult(
        int UpdatedConversationCount,
        int UpdatedMessageCount,
        int UpdatedTraceCount)
    {
        public bool StateChanged => UpdatedTraceCount > 0;
    }

    internal sealed class CopilotApprovalCoordinator : IDisposable
    {
        private readonly ICopilotApprovalStore _store;
        private readonly CopilotChatState _state;
        private int _disposeState;

        public CopilotApprovalCoordinator(
            ICopilotApprovalStore store,
            CopilotChatState state)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _store.ActionsChanged += Store_ActionsChanged;
            _store.ActionStatusChanged += Store_ActionStatusChanged;
        }

        public event EventHandler? PendingActionsInvalidated;

        public event EventHandler<CopilotApprovalActionTransitionEventArgs>? ActionTransitioned;

        public ObservableCollection<ConfirmableAction> PendingActions { get; } = new();

        public CopilotApprovalPendingProjection RefreshPendingActions(string? conversationId)
        {
            var actions = _store.GetPendingActionsForConversation(conversationId);
            PendingActions.Clear();
            foreach (var action in actions)
                PendingActions.Add(action);

            return new CopilotApprovalPendingProjection(
                PendingActions.Count,
                _store.PendingCount);
        }

        public bool HasPendingActionsForConversation(string? conversationId) =>
            _store.GetPendingActionsForConversation(conversationId).Count > 0;

        public CopilotApprovalEligibility Evaluate(
            ConfirmableAction? action,
            CopilotApprovalScope scope)
        {
            if (action == null)
            {
                return CopilotApprovalEligibility.Denied(
                    CopilotApprovalEligibilityReason.ActionNotFound,
                    "The action id was not found.");
            }

            return _store.ValidateForReview(action.ActionId, scope.ToReviewContext());
        }

        public async Task<CopilotApprovalDecisionResult> ApproveAsync(
            ConfirmableAction? action,
            CopilotApprovalScope scope,
            CancellationToken cancellationToken)
        {
            var eligibility = Evaluate(action, scope);
            if (!eligibility.CanReview || action == null)
                return CopilotApprovalDecisionResult.Invalid(eligibility.Message);

            var result = await CopilotMcpConfirmationDecision.ApproveAsync(
                _store,
                action,
                scope.ToReviewContext(),
                cancellationToken).ConfigureAwait(false);
            var outcome = result.Success
                ? result.ExecutedImmediately
                    ? CopilotApprovalDecisionOutcome.ApprovedAndExecuted
                    : CopilotApprovalDecisionOutcome.Approved
                : result.ExecutedImmediately
                    ? CopilotApprovalDecisionOutcome.ExecutionFailed
                    : CopilotApprovalDecisionOutcome.Invalid;
            return new CopilotApprovalDecisionResult(
                outcome,
                result.Success,
                result.ExecutedImmediately,
                result.Message);
        }

        public CopilotApprovalDecisionResult Reject(
            ConfirmableAction? action,
            CopilotApprovalScope scope)
        {
            var eligibility = Evaluate(action, scope);
            if (!eligibility.CanReview || action == null)
                return CopilotApprovalDecisionResult.Invalid(eligibility.Message);

            var rejected = _store.Reject(
                action.ActionId,
                scope.ToReviewContext(),
                out var message);
            return new CopilotApprovalDecisionResult(
                rejected
                    ? CopilotApprovalDecisionOutcome.Rejected
                    : CopilotApprovalDecisionOutcome.Invalid,
                rejected,
                ExecutedImmediately: false,
                message);
        }

        public CopilotApprovalTraceUpdateResult ApplyTransition(
            CopilotApprovalActionTransition transition)
        {
            ArgumentNullException.ThrowIfNull(transition);
            if (string.IsNullOrWhiteSpace(transition.AgentCallId))
                return default;

            var conversations = transition.SourceKind == CopilotApprovalSourceKind.InAppAgent
                    && !string.IsNullOrWhiteSpace(transition.ConversationId)
                ? (_state.Conversations ?? new ObservableCollection<CopilotConversationRecord>())
                    .Where(conversation => string.Equals(
                        conversation.Id,
                        transition.ConversationId,
                        StringComparison.Ordinal))
                : _state.Conversations ?? new ObservableCollection<CopilotConversationRecord>();
            var updatedConversationIds = new HashSet<string>(StringComparer.Ordinal);
            var updatedMessages = 0;
            var updatedTraces = 0;
            foreach (var conversation in conversations)
            {
                foreach (var message in conversation.Messages ?? new ObservableCollection<CopilotChatMessage>())
                {
                    var trace = message.AgentTraceEntries?.FirstOrDefault(entry =>
                        string.Equals(entry.CallId, transition.AgentCallId, StringComparison.Ordinal)
                        || (!string.IsNullOrWhiteSpace(entry.ApprovalActionId)
                            && string.Equals(
                                entry.ApprovalActionId,
                                transition.ActionId,
                                StringComparison.OrdinalIgnoreCase)));
                    if (trace == null)
                        continue;

                    ApplyTransition(message, trace, transition);
                    updatedConversationIds.Add(conversation.Id);
                    updatedMessages++;
                    updatedTraces++;
                }
            }

            return new CopilotApprovalTraceUpdateResult(
                updatedConversationIds.Count,
                updatedMessages,
                updatedTraces);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposeState, 1) != 0)
                return;

            _store.ActionsChanged -= Store_ActionsChanged;
            _store.ActionStatusChanged -= Store_ActionStatusChanged;
        }

        private static void ApplyTransition(
            CopilotChatMessage message,
            CopilotAgentTraceEntry trace,
            CopilotApprovalActionTransition transition)
        {
            switch (transition.Status)
            {
                case ConfirmableActionStatus.Pending:
                case ConfirmableActionStatus.Approved:
                    trace.State = CopilotToolExecutionState.AwaitingApproval;
                    break;
                case ConfirmableActionStatus.Executing:
                    trace.State = CopilotToolExecutionState.Running;
                    message.MarkThinkingStarted();
                    message.IsExecutionInProgress = true;
                    break;
                case ConfirmableActionStatus.Rejected:
                    trace.State = CopilotToolExecutionState.Denied;
                    trace.CompletedAtUtc = transition.CompletedAt ?? transition.ObservedAtUtc;
                    trace.ErrorMessage = "The user rejected this approval request.";
                    message.IsExecutionInProgress = false;
                    message.MarkThinkingCompleted();
                    break;
                case ConfirmableActionStatus.Expired:
                    trace.State = CopilotToolExecutionState.TimedOut;
                    trace.CompletedAtUtc = transition.CompletedAt ?? transition.ObservedAtUtc;
                    trace.ErrorMessage = "The approval request expired before a decision was recorded.";
                    message.IsExecutionInProgress = false;
                    message.MarkThinkingCompleted();
                    break;
                case ConfirmableActionStatus.Cancelled:
                    trace.State = CopilotToolExecutionState.Cancelled;
                    trace.CompletedAtUtc = transition.CompletedAt ?? transition.ObservedAtUtc;
                    trace.ErrorMessage = CopilotAgentTraceEntry.Sanitize(transition.ExecutionResultText);
                    message.IsExecutionInProgress = false;
                    message.MarkThinkingCompleted();
                    break;
                case ConfirmableActionStatus.Executed:
                    if (transition.ResumesAgentOnApproval)
                        break;

                    trace.State = transition.ExecutionSucceeded == true
                        ? CopilotToolExecutionState.Completed
                        : CopilotToolExecutionState.Failed;
                    trace.CompletedAtUtc = transition.CompletedAt ?? transition.ObservedAtUtc;
                    trace.ResultSummary = transition.ExecutionSucceeded == true
                        ? CopilotAgentTraceEntry.Sanitize(transition.ExecutionResultText)
                        : trace.ResultSummary;
                    trace.ErrorMessage = transition.ExecutionSucceeded == false
                        ? CopilotAgentTraceEntry.Sanitize(transition.ExecutionResultText)
                        : string.Empty;
                    message.IsExecutionInProgress = false;
                    message.MarkThinkingCompleted();
                    break;
            }

            trace.ApprovalActionId = transition.ActionId;
            if (trace.CompletedAtUtc != null && trace.StartedAtUtc != default)
            {
                trace.DurationMs = Math.Max(
                    trace.DurationMs,
                    (long)Math.Max(
                        0,
                        (trace.CompletedAtUtc.Value - trace.StartedAtUtc).TotalMilliseconds));
            }
            message.RebuildExecutionContentFromAgentTrace();
        }

        private void Store_ActionsChanged(object? sender, EventArgs e)
        {
            PendingActionsInvalidated?.Invoke(this, EventArgs.Empty);
        }

        private void Store_ActionStatusChanged(
            object? sender,
            ConfirmableActionChangedEventArgs e)
        {
            var transition = CopilotApprovalActionTransition.Capture(
                e.Action,
                DateTimeOffset.UtcNow);
            ActionTransitioned?.Invoke(
                this,
                new CopilotApprovalActionTransitionEventArgs(transition));
        }
    }
}
