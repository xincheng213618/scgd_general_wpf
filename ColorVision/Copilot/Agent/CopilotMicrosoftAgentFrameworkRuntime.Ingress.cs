#pragma warning disable MAAI001
#pragma warning disable CA1859
using Anthropic;
using Anthropic.Core;
using ColorVision.Copilot.Mcp;
using ColorVision.Solution;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AIChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotMicrosoftAgentFrameworkRuntime
    {
        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(
            string taskId,
            string message)
        {
            var normalizedTaskId = (taskId ?? string.Empty).Trim();
            var normalized = (message ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedTaskId)
                || string.IsNullOrWhiteSpace(normalized)
                || normalized.Length > CopilotSteeringMessagePolicy.MaximumMessageCharacters)
            {
                return new CopilotSteeringAdmissionResult(
                    CopilotSteeringAdmissionReason.InvalidInput);
            }

            try
            {
                lock (_backgroundOutputRoutingSyncRoot)
                {
                    if (_userQuestionCoordinator.HasPendingQuestion)
                    {
                        return new CopilotSteeringAdmissionResult(
                            CopilotSteeringAdmissionReason.PendingUserQuestion);
                    }

                    lock (_steeringSyncRoot)
                    {
                        var activeContext = _activeSteeringContext;
                        if (activeContext == null
                            || !string.Equals(
                                activeContext.TaskId,
                                normalizedTaskId,
                                StringComparison.Ordinal))
                        {
                            return new CopilotSteeringAdmissionResult(
                                CopilotSteeringAdmissionReason.NoActiveTask);
                        }

                        var steeringMessage = new Microsoft.Extensions.AI.ChatMessage(
                            ChatRole.User,
                            normalized)
                        {
                            MessageId = SteeringMessageIdPrefix
                                + Guid.NewGuid().ToString("N"),
                        };
                        if (!activeContext.TryEnqueueSteeringMessage(
                                steeringMessage,
                                normalized))
                        {
                            return new CopilotSteeringAdmissionResult(
                                CopilotSteeringAdmissionReason.QueueFull);
                        }
                        return new CopilotSteeringAdmissionResult(
                            CopilotSteeringAdmissionReason.Accepted,
                            steeringMessage.MessageId);
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                return new CopilotSteeringAdmissionResult(
                    CopilotSteeringAdmissionReason.RuntimeUnavailable);
            }
            catch (InvalidOperationException)
            {
                return new CopilotSteeringAdmissionResult(
                    CopilotSteeringAdmissionReason.RuntimeUnavailable);
            }
        }

        internal bool TryEnqueueBackgroundShellCommandCompletion(
            CopilotBackgroundShellCommandSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            lock (_backgroundOutputRoutingSyncRoot)
            {
                if (ShouldDeferBackgroundShellSignals())
                    return TryDeferBackgroundShellCommandCompletion(snapshot);
                return TryEnqueueBackgroundShellCommandCompletionCore(snapshot)
                    || TryDeferBackgroundShellCommandCompletion(snapshot);
            }
        }

        private bool TryDeferBackgroundShellCommandCompletion(
            CopilotBackgroundShellCommandSnapshot snapshot)
        {
            return _backgroundShellCompletionInbox.TryEnqueue(snapshot);
        }

        private bool TryEnqueueBackgroundShellCommandCompletionCore(
            CopilotBackgroundShellCommandSnapshot snapshot)
        {
            ActiveSteeringContext? activeContext;
            lock (_steeringSyncRoot)
                activeContext = _activeSteeringContext;

            if (activeContext == null
                || !CopilotBackgroundShellCommandAgentEvent.TryCreateMessage(
                    snapshot,
                    activeContext.ConversationId,
                    out var message))
            {
                return false;
            }

            try
            {
                activeContext.MessageInjector.EnqueueMessagesAsync(
                    activeContext.Session,
                    [
                        new Microsoft.Extensions.AI.ChatMessage(
                            ChatRole.User,
                            message),
                    ],
                    CancellationToken.None).GetAwaiter().GetResult();
                activeContext.TaskEventJournal
                    .RecordBackgroundShellCommandCompletion(snapshot);
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        internal bool TryEnqueueBackgroundShellCommandOutput(
            CopilotBackgroundShellOutputMonitorEventArgs eventArgs)
        {
            ArgumentNullException.ThrowIfNull(eventArgs);
            lock (_backgroundOutputRoutingSyncRoot)
                return TryEnqueueBackgroundShellCommandOutputCore(eventArgs);
        }

        private bool TryEnqueueBackgroundShellCommandOutputCore(
            CopilotBackgroundShellOutputMonitorEventArgs eventArgs)
        {
            if (ShouldDeferBackgroundShellSignals())
                return _backgroundShellOutputEventInbox.TryEnqueue(eventArgs);

            ActiveSteeringContext? activeContext;
            lock (_steeringSyncRoot)
            {
                activeContext = _activeSteeringContext;
                if (activeContext == null
                    || !string.Equals(
                        activeContext.ConversationId,
                        eventArgs.Monitor.ConversationId,
                        StringComparison.Ordinal))
                {
                    return _backgroundShellOutputEventInbox.TryEnqueue(
                        eventArgs);
                }
            }

            if (!CopilotBackgroundShellCommandAgentEvent
                    .TryCreateOutputMessage(
                        eventArgs,
                        activeContext.ConversationId,
                        out var message))
            {
                return false;
            }

            try
            {
                activeContext.MessageInjector.EnqueueMessagesAsync(
                    activeContext.Session,
                    [
                        new Microsoft.Extensions.AI.ChatMessage(
                            ChatRole.User,
                            message),
                    ],
                    CancellationToken.None).GetAwaiter().GetResult();
                activeContext.TaskEventJournal
                    .RecordBackgroundShellCommandOutput(eventArgs);
                return true;
            }
            catch (ObjectDisposedException)
            {
                return _backgroundShellOutputEventInbox.TryEnqueue(eventArgs);
            }
            catch (InvalidOperationException)
            {
                return _backgroundShellOutputEventInbox.TryEnqueue(eventArgs);
            }
        }

        public bool TryAnswerUserQuestion(
            string taskId,
            string requestId,
            string answer)
        {
            lock (_backgroundOutputRoutingSyncRoot)
            {
                if (!_userQuestionCoordinator.TryAnswer(
                        taskId,
                        requestId,
                        answer))
                {
                    return false;
                }

                if (!_isFrameworkApprovalPending)
                    TryTransferDeferredBackgroundShellSignalsToActiveSession();
                return true;
            }
        }

        private bool
            TryTransferDeferredBackgroundShellSignalsToActiveSession()
        {
            ActiveSteeringContext? activeContext;
            lock (_steeringSyncRoot)
                activeContext = _activeSteeringContext;
            if (activeContext == null)
                return false;

            using var delivery =
                _backgroundShellOutputEventInbox.BeginDelivery(
                    activeContext.ConversationId);
            using var completionDelivery =
                _backgroundShellCompletionInbox.BeginDelivery(
                    activeContext.ConversationId);
            var outputMessages = CreateDeferredBackgroundOutputMessages(
                delivery.Events,
                activeContext.ConversationId);
            var completions = completionDelivery.Completions;
            var completionMessages =
                CreateDeferredBackgroundCompletionMessages(
                    completions,
                    activeContext.ConversationId);
            var messages = outputMessages
                .Concat(completionMessages)
                .ToArray();
            if (messages.Length == 0)
            {
                if (delivery.Events.Count > 0)
                    delivery.Commit();
                if (completions.Count > 0)
                    completionDelivery.Commit();
                return false;
            }

            try
            {
                activeContext.MessageInjector.EnqueueMessagesAsync(
                    activeContext.Session,
                    [
                        new Microsoft.Extensions.AI.ChatMessage(
                            ChatRole.User,
                            string.Join(
                                Environment.NewLine
                                    + Environment.NewLine,
                                messages)),
                    ],
                    CancellationToken.None).GetAwaiter().GetResult();
                delivery.Commit();
                completionDelivery.Commit();
                foreach (var deferredEvent in delivery.Events)
                {
                    activeContext.TaskEventJournal
                        .RecordBackgroundShellCommandOutput(
                            deferredEvent.EventArgs);
                }
                foreach (var completion in completions)
                {
                    activeContext.TaskEventJournal
                        .RecordBackgroundShellCommandCompletion(
                            completion.Snapshot);
                }
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private bool ShouldDeferBackgroundShellSignals()
        {
            return _isFrameworkApprovalPending
                || _userQuestionCoordinator.HasPendingQuestion;
        }

        private void BeginFrameworkApprovalRouting()
        {
            lock (_backgroundOutputRoutingSyncRoot)
                _isFrameworkApprovalPending = true;
        }

        private void CompleteFrameworkApprovalRouting()
        {
            lock (_backgroundOutputRoutingSyncRoot)
            {
                _isFrameworkApprovalPending = false;
                if (!_userQuestionCoordinator.HasPendingQuestion)
                    TryTransferDeferredBackgroundShellSignalsToActiveSession();
            }
        }

        private void CancelFrameworkApprovalRouting()
        {
            lock (_backgroundOutputRoutingSyncRoot)
                _isFrameworkApprovalPending = false;
        }
    }
}
