#pragma warning disable MAAI001
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotMicrosoftAgentFrameworkRuntime
    {
        private sealed class ActiveSteeringContext(
            string conversationId,
            string taskId,
            MessageInjectingChatClient messageInjector,
            AgentSession session,
            CopilotAgentTaskEventJournalBuilder taskEventJournal)
        {
            private readonly object _syncRoot = new();
            private readonly List<TrackedSteeringMessage> _undeliveredSteeringMessages = new();
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
                    return true;
                }
            }

            public async Task<IReadOnlyList<CopilotSteeringMessageSnapshot>> RecordDeliveredSteeringMessagesAsync(
                CancellationToken cancellationToken)
            {
                TrackedSteeringMessage[] trackedMessages;
                lock (_syncRoot)
                {
                    trackedMessages = _undeliveredSteeringMessages.ToArray();
                    if (trackedMessages.Length == 0)
                        return Array.Empty<CopilotSteeringMessageSnapshot>();
                }

                var pendingMessageIds = (await MessageInjector
                        .GetPendingMessagesAsync(Session, cancellationToken))
                    .Select(message => message.MessageId)
                    .Where(messageId => !string.IsNullOrWhiteSpace(messageId))
                    .ToHashSet(StringComparer.Ordinal);
                var deliveredMessages = new List<string>();
                var deliveredSnapshots = new List<CopilotSteeringMessageSnapshot>();
                lock (_syncRoot)
                {
                    foreach (var message in trackedMessages)
                    {
                        if (pendingMessageIds.Contains(message.MessageId)
                            || !_undeliveredSteeringMessages.Remove(message))
                        {
                            continue;
                        }

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

            public Task<IReadOnlyList<CopilotSteeringMessageSnapshot>> RecordDeliveredSteeringMessagesAsync(
                CancellationToken cancellationToken) =>
                context.RecordDeliveredSteeringMessagesAsync(cancellationToken);

            public IReadOnlyList<string> GetDeliveredSteeringMessages() =>
                context.GetDeliveredSteeringMessages();

            public IReadOnlyList<CopilotSteeringMessageSnapshot> GetUndeliveredSteeringMessages() =>
                context.GetUndeliveredSteeringMessages();

            public void Dispose() => StopAcceptingInput();
        }
    }
}
