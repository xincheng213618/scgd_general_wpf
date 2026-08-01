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
        private sealed class LiveCheckpointPublisher
        {
            private readonly CopilotAgentRequest _request;
            private readonly CopilotAgentSessionCheckpoint? _requestedCheckpoint;
            private readonly CopilotCapabilityCatalogSnapshot _capabilitySnapshot;
            private readonly IReadOnlyList<string> _availableToolNames;
            private readonly CopilotAgentEnvironmentContext _environmentContext;
            private readonly CopilotToolExecutionHookRegistrySnapshot _hookSurfaceSnapshot;
            private readonly IReadOnlyList<CopilotAgentEvidenceArtifact> _previousEvidenceArtifacts;
            private readonly HarnessToolBridge _bridge;
            private readonly TodoProvider? _todoProvider;
            private readonly AgentModeProvider? _modeProvider;
            private readonly CopilotAgentTaskEventJournalBuilder _taskEventJournalBuilder;
            private readonly Action<CopilotAgentEvent> _emit;
            private readonly bool _sessionResumed;
            private readonly Func<string> _answerText;
            private readonly Func<IReadOnlyList<string>> _deliveredSteeringMessages;
            private CopilotAgentSessionCheckpoint? _latestCheckpoint;
            private CopilotAgentTaskLedgerSnapshot? _latestTaskLedger;
            private int _publishing;

            public LiveCheckpointPublisher(
                CopilotAgentRequest request,
                CopilotAgentSessionCheckpoint? requestedCheckpoint,
                CopilotCapabilityCatalogSnapshot capabilitySnapshot,
                IReadOnlyList<string> availableToolNames,
                CopilotAgentEnvironmentContext environmentContext,
                CopilotToolExecutionHookRegistrySnapshot hookSurfaceSnapshot,
                IReadOnlyList<CopilotAgentEvidenceArtifact> previousEvidenceArtifacts,
                HarnessToolBridge bridge,
                TodoProvider? todoProvider,
                AgentModeProvider? modeProvider,
                CopilotAgentTaskEventJournalBuilder taskEventJournalBuilder,
                Action<CopilotAgentEvent> emit,
                bool sessionResumed,
                Func<string> answerText,
                Func<IReadOnlyList<string>> deliveredSteeringMessages)
            {
                _request = request;
                _requestedCheckpoint = requestedCheckpoint;
                _capabilitySnapshot = capabilitySnapshot;
                _availableToolNames = availableToolNames;
                _environmentContext = environmentContext;
                _hookSurfaceSnapshot = hookSurfaceSnapshot;
                _previousEvidenceArtifacts = previousEvidenceArtifacts;
                _bridge = bridge;
                _todoProvider = todoProvider;
                _modeProvider = modeProvider;
                _taskEventJournalBuilder = taskEventJournalBuilder;
                _emit = emit;
                _sessionResumed = sessionResumed;
                _answerText = answerText;
                _deliveredSteeringMessages = deliveredSteeringMessages;
            }

            public CopilotAgentSessionCheckpoint? LatestCheckpoint => Volatile.Read(ref _latestCheckpoint);

            public CopilotAgentTaskLedgerSnapshot? LatestTaskLedger => Volatile.Read(ref _latestTaskLedger);

            public async ValueTask<bool> TryPublishAsync(
                AIAgent agent,
                AgentSession session,
                CancellationToken cancellationToken,
                CopilotAgentTaskLedgerSnapshot? knownTaskLedger = null)
            {
                ArgumentNullException.ThrowIfNull(agent);
                ArgumentNullException.ThrowIfNull(session);
                if (Interlocked.CompareExchange(ref _publishing, 1, 0) != 0)
                    return false;
                try
                {
                    var taskLedger = knownTaskLedger
                        ?? await CaptureTaskLedgerAsync(_todoProvider, _modeProvider, session, _sessionResumed, cancellationToken);
                    if (knownTaskLedger == null)
                        _taskEventJournalBuilder.RecordTaskLedger(taskLedger, "checkpoint");

                    var evidenceArtifacts = CopilotAgentEvidenceArtifacts.Merge(
                        _previousEvidenceArtifacts,
                        _bridge.StepRecords,
                        _capabilitySnapshot,
                        DateTimeOffset.UtcNow);
                    var serializedSession = await agent.SerializeSessionAsync(session, null, cancellationToken);
                    var conversationMemory = CopilotAgentConversationMemory.Merge(
                        _requestedCheckpoint?.ConversationMemory,
                        _request.History,
                        _request.UserText,
                        _answerText(),
                        _deliveredSteeringMessages());
                    var checkpoint = CopilotAgentSessionCheckpoint.Create(
                        _request.Profile,
                        serializedSession.GetRawText(),
                        _capabilitySnapshot,
                        evidenceArtifacts,
                        _taskEventJournalBuilder.Snapshot(),
                        _availableToolNames,
                        conversationMemory,
                        _environmentContext,
                        _request.TaskIntentText,
                        _hookSurfaceSnapshot);
                    if (checkpoint == null)
                    {
                        _emit(CopilotAgentEvent.RuntimeDiagnostic("Incremental Agent checkpoint was rejected because the serialized state was invalid."));
                        return false;
                    }

                    Volatile.Write(ref _latestTaskLedger, taskLedger);
                    Volatile.Write(ref _latestCheckpoint, checkpoint);
                    _emit(CopilotAgentEvent.CheckpointUpdated(checkpoint, taskLedger));
                    return true;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _emit(CopilotAgentEvent.RuntimeDiagnostic(
                        $"Incremental Agent checkpoint could not be saved ({CopilotAgentTraceEntry.Sanitize(ex.Message)})."));
                    return false;
                }
                finally
                {
                    Volatile.Write(ref _publishing, 0);
                }
            }
        }

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
