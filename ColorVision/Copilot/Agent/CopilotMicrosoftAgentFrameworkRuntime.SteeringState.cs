#pragma warning disable MAAI001
using Microsoft.Agents.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotMicrosoftAgentFrameworkRuntime
    {
        private sealed class ActiveSteeringContext(
            string conversationId,
            string taskId,
            MessageInjectingChatClient messageInjector,
            AgentSession session,
            IReadOnlyList<CopilotSteeringMessageSnapshot> restoredSteeringMessages,
            CopilotAgentTaskEventJournalBuilder taskEventJournal,
            Action onInputAccepted)
        {
            private readonly object _syncRoot = new();
            // Restored delegated input is already in the framework queue. Track its original
            // IDs before accepting new input, without enqueueing it or recording acceptance again.
            private readonly List<TrackedSteeringMessage> _undeliveredSteeringMessages = restoredSteeringMessages
                .Select(message => new TrackedSteeringMessage(message.MessageId, message.Text)).ToList();
            private readonly List<string> _deliveredSteeringMessages = new();

            public string ConversationId { get; } = conversationId;

            public string TaskId { get; } = taskId;

            public MessageInjectingChatClient MessageInjector { get; } = messageInjector;

            public AgentSession Session { get; } = session;

            public CopilotAgentTaskEventJournalBuilder TaskEventJournal { get; } = taskEventJournal;

            public bool TryEnqueueSteeringMessage(
                Microsoft.Extensions.AI.ChatMessage message,
                string normalizedText)
            {
                var messageId = message.MessageId
                    ?? throw new ArgumentException(
                        "Tracked steering messages require an identifier.",
                        nameof(message));
                lock (_syncRoot)
                {
                    if (_undeliveredSteeringMessages.Count
                            >= CopilotSteeringMessagePolicy.MaximumPendingMessages
                        || _undeliveredSteeringMessages.Sum(item => item.Text.Length)
                            + normalizedText.Length
                            > CopilotSteeringMessagePolicy.MaximumPendingCharacters)
                    {
                        return false;
                    }

                    MessageInjector.EnqueueMessagesAsync(
                        Session,
                        [message],
                        CancellationToken.None).GetAwaiter().GetResult();
                    TaskEventJournal.RecordSteering(normalizedText);
                    _undeliveredSteeringMessages.Add(new TrackedSteeringMessage(
                        messageId,
                        normalizedText));
                    onInputAccepted();
                    return true;
                }
            }

            public IReadOnlyList<CopilotSteeringMessageSnapshot> RecordStoredSteeringMessages(
                IReadOnlyList<string> storedUserMessageIds)
            {
                var storedMessageIds = storedUserMessageIds.ToHashSet(StringComparer.Ordinal);
                var deliveredMessages = new List<string>();
                var deliveredSnapshots = new List<CopilotSteeringMessageSnapshot>();
                lock (_syncRoot)
                {
                    foreach (var message in _undeliveredSteeringMessages.ToArray())
                    {
                        if (!storedMessageIds.Contains(message.MessageId))
                            continue;

                        _undeliveredSteeringMessages.Remove(message);
                        TaskEventJournal.RecordSteeringDelivered(message.Text);
                        deliveredMessages.Add(message.Text);
                        deliveredSnapshots.Add(new CopilotSteeringMessageSnapshot(
                            message.MessageId,
                            message.Text));
                    }
                    if (deliveredMessages.Count > 0)
                    {
                        var boundedMessages = CopilotAgentConversationMemory
                            .SelectBoundedUserFollowUps(
                                _deliveredSteeringMessages.Concat(deliveredMessages));
                        _deliveredSteeringMessages.Clear();
                        _deliveredSteeringMessages.AddRange(boundedMessages);
                    }
                }
                return deliveredSnapshots;
            }

            public IReadOnlyList<string> GetDeliveredSteeringMessages()
            {
                lock (_syncRoot)
                {
                    return _deliveredSteeringMessages.ToArray();
                }
            }

            public IReadOnlyList<CopilotSteeringMessageSnapshot> GetUndeliveredSteeringMessages()
            {
                lock (_syncRoot)
                {
                    return _undeliveredSteeringMessages
                        .Select(message => new CopilotSteeringMessageSnapshot(
                            message.MessageId,
                            message.Text))
                        .ToArray();
                }
            }

            private sealed class TrackedSteeringMessage(
                string messageId,
                string text)
            {
                public string MessageId { get; } = messageId;

                public string Text { get; } = text;
            }
        }

        private sealed class SteeringRegistration(CopilotMicrosoftAgentFrameworkRuntime owner, ActiveSteeringContext context) : IDisposable
        {
            private CopilotMicrosoftAgentFrameworkRuntime? _owner = owner;

            public void StopAcceptingInput()
            {
                Interlocked.Exchange(ref _owner, null)?.ClearSteeringContext(context);
            }

            public IReadOnlyList<CopilotSteeringMessageSnapshot> RecordStoredSteeringMessages(
                IReadOnlyList<string> storedUserMessageIds) =>
                context.RecordStoredSteeringMessages(storedUserMessageIds);

            public IReadOnlyList<string> GetDeliveredSteeringMessages() =>
                context.GetDeliveredSteeringMessages();

            public IReadOnlyList<CopilotSteeringMessageSnapshot> GetUndeliveredSteeringMessages() =>
                context.GetUndeliveredSteeringMessages();

            public void Dispose() => StopAcceptingInput();
        }
    }
}
