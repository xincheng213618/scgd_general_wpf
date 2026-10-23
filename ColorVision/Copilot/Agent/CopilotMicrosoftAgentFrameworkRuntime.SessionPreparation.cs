#pragma warning disable MAAI001
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotMicrosoftAgentFrameworkRuntime
    {
        private sealed class AgentSessionPreparation : IDisposable
        {
            public required AgentSession Session { get; init; }

            public required bool SessionResumed { get; init; }

            public required SteeringRegistration SteeringRegistration { get; init; }

            public required LiveCheckpointPublisher LiveCheckpointPublisher { get; init; }

            public required CopilotAgentTaskLedgerSnapshot RecoveredTaskLedger { get; init; }

            public required IReadOnlyList<CopilotRequestMessage> PromptMessages { get; init; }

            public void Dispose() => SteeringRegistration.Dispose();
        }

        private async Task<AgentSessionPreparation> PrepareAgentSessionAsync(
            CopilotAgentRequest request,
            CopilotAgentSessionCheckpoint? requestedCheckpoint,
            CopilotAgentCheckpointCompatibility? checkpointCompatibility,
            bool requiresCheckpointReplan,
            CopilotCapabilityCatalogSnapshot capabilitySnapshot,
            IReadOnlyList<string> availableToolNames,
            CopilotAgentEnvironmentContext? checkpointEnvironmentContext,
            CopilotToolExecutionHookRegistrySnapshot hookSurfaceSnapshot,
            IReadOnlyList<CopilotAgentEvidenceArtifact> previousEvidenceArtifacts,
            AIAgent agent,
            HarnessToolBridge bridge,
            TodoProvider? todoProvider,
            AgentModeProvider? modeProvider,
            MessageInjectingChatClient messageInjector,
            CopilotAgentTaskEventJournalBuilder taskEventJournalBuilder,
            Action<CopilotAgentEvent> emit,
            StringBuilder answerText,
            IReadOnlyList<CopilotRequestMessage> preparedPromptMessages,
            CancellationToken cancellationToken)
        {
            SteeringRegistration? steeringRegistration = null;
            try
            {
                var sessionResumed = false;
                IReadOnlyList<CopilotSteeringMessageSnapshot> restoredSteeringMessages = Array.Empty<CopilotSteeringMessageSnapshot>();
                AgentSession session;
                if (checkpointCompatibility?.CanResume == true && requestedCheckpoint != null)
                {
                    try
                    {
                        using var checkpointDocument = JsonDocument.Parse(requestedCheckpoint.SerializedSessionJson);
                        session = await agent.DeserializeSessionAsync(checkpointDocument.RootElement.Clone(), null, cancellationToken);
                        // Only the top-level host recovers unconfirmed input into a draft.
                        // Delegated runs retain their own pending coordination messages.
                        var recoverSteeringToDraft = request.RuntimePurpose == CopilotAgentRuntimePurpose.Standard
                            && string.IsNullOrWhiteSpace(request.RuntimeExecutionScope.ParentRunId);
                        var recoveredSteeringCount = 0;
                        if (recoverSteeringToDraft)
                        {
                            recoveredSteeringCount = await DiscardRecoveredSteeringAsync(messageInjector, session, cancellationToken);
                        }
                        else
                        {
                            var pendingMessages = await messageInjector.GetPendingMessagesAsync(session, cancellationToken);
                            restoredSteeringMessages = pendingMessages
                                .Where(message => IsOwnedSteeringMessage(message) && !string.IsNullOrWhiteSpace(message.Text))
                                .DistinctBy(message => message.MessageId, StringComparer.Ordinal)
                                .Select(message => new CopilotSteeringMessageSnapshot(message.MessageId!, message.Text))
                                .ToArray();
                        }
                        sessionResumed = true;
                        taskEventJournalBuilder.RecordSessionResumed();
                        emit(CopilotAgentEvent.RuntimeDiagnostic("Agent Framework session resumed from the persisted conversation checkpoint."));
                        if (recoveredSteeringCount > 0)
                        {
                            emit(CopilotAgentEvent.RuntimeDiagnostic(
                                $"Removed {recoveredSteeringCount} unconfirmed steering instruction(s) from the resumed delivery queue; host draft recovery owns their resubmission."));
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        restoredSteeringMessages = Array.Empty<CopilotSteeringMessageSnapshot>();
                        taskEventJournalBuilder.RecordReplanRequired(CopilotAgentCheckpointCompatibilityKind.Invalid);
                        emit(CopilotAgentEvent.RuntimeDiagnostic($"Agent session checkpoint could not be resumed; starting a fresh session ({CopilotUserFacingErrorFormatter.Sanitize(ex.Message)})."));
                        session = await agent.CreateSessionAsync(cancellationToken);
                    }
                }
                else
                {
                    if (requiresCheckpointReplan)
                    {
                        taskEventJournalBuilder.RecordReplanRequired(checkpointCompatibility!.Kind);
                        emit(CopilotAgentEvent.RuntimeDiagnostic(FormatCapabilityReplanDiagnostic(checkpointCompatibility)));
                    }
                    session = await agent.CreateSessionAsync(cancellationToken);
                }

                steeringRegistration = RegisterSteeringContext(
                    request.ConversationId,
                    request.TaskId,
                    messageInjector,
                    session,
                    restoredSteeringMessages,
                    taskEventJournalBuilder,
                    bridge.NotifyUserInputAccepted);
                var liveCheckpointPublisher = new LiveCheckpointPublisher(
                    request,
                    requestedCheckpoint,
                    capabilitySnapshot,
                    availableToolNames,
                    checkpointEnvironmentContext,
                    hookSurfaceSnapshot,
                    previousEvidenceArtifacts,
                    bridge,
                    todoProvider,
                    modeProvider,
                    taskEventJournalBuilder,
                    emit,
                    sessionResumed,
                    () => answerText.ToString(),
                    steeringRegistration);

                var recoveredTaskLedger = await CaptureTaskLedgerAsync(
                    todoProvider,
                    modeProvider,
                    session,
                    sessionResumed,
                    cancellationToken);
                taskEventJournalBuilder.RecordTaskLedger(recoveredTaskLedger, sessionResumed ? "recovered" : "initial");
                if (await liveCheckpointPublisher.TryPublishAsync(agent, session, cancellationToken, recoveredTaskLedger))
                    emit(CopilotAgentEvent.CheckpointReady());
                if (sessionResumed)
                {
                    emit(CopilotAgentEvent.RuntimeDiagnostic(
                        FormatTaskLedgerDiagnostic("Agent task ledger recovered", recoveredTaskLedger)
                        + " Persisted tasks are planning state, not authorization; protected tools require a fresh exact-call approval."));
                }

                var sessionMemory = requestedCheckpoint?.SessionConversationMemory
                    ?? requestedCheckpoint?.ConversationMemory;
                IReadOnlyList<CopilotRequestMessage> unseenVisibleHistory = sessionResumed && requestedCheckpoint != null
                    ? CopilotAgentConversationMemory.SelectUnseenVisibleTail(sessionMemory, request.History)
                    : Array.Empty<CopilotRequestMessage>();
                var promptMessages = sessionResumed
                    ? CopilotAgentConversationMemory.MergeIntoPreparedPrompt(
                        CopilotAgentConversationMemory.SelectUnseenVisibleTail(
                            sessionMemory, requestedCheckpoint!.ConversationMemory),
                        unseenVisibleHistory.Concat(preparedPromptMessages.TakeLast(1)).ToArray())
                    : preparedPromptMessages;
                if (unseenVisibleHistory.Count > 0)
                {
                    emit(CopilotAgentEvent.RuntimeDiagnostic(
                        $"Agent session reconciled {unseenVisibleHistory.Count} visible conversation message(s) newer than the persisted checkpoint."));
                }

                var sessionResumeFailed = !sessionResumed
                    && requestedCheckpoint != null
                    && checkpointCompatibility?.CanResume == true;
                if (!sessionResumed
                    && (requiresCheckpointReplan || sessionResumeFailed)
                    && requestedCheckpoint?.ConversationMemory.Count > 0)
                {
                    promptMessages = CopilotAgentConversationMemory.MergeIntoPreparedPrompt(
                        requestedCheckpoint.ConversationMemory,
                        preparedPromptMessages);
                    emit(CopilotAgentEvent.RuntimeDiagnostic(
                        $"Agent task session was reset, but {requestedCheckpoint.ConversationMemory.Count} bounded conversation memory message(s) were restored for continuity."));
                }
                var sessionWasReset = !sessionResumed && (requiresCheckpointReplan || sessionResumeFailed);
                var recoveryEvidencePrompt = sessionWasReset
                    ? CopilotAgentEvidenceArtifacts.BuildRecoveryPrompt(previousEvidenceArtifacts, capabilitySnapshot)
                    : string.Empty;
                if (!string.IsNullOrWhiteSpace(recoveryEvidencePrompt))
                {
                    promptMessages = InsertEvidenceMessageBeforeCurrentUser(promptMessages, recoveryEvidencePrompt);
                    emit(CopilotAgentEvent.RuntimeDiagnostic($"Agent recovery checkpoint contained {previousEvidenceArtifacts.Count} evidence artifact(s); bounded untrusted historical context was supplied."));
                }
                var attemptedToolRecoveryPrompt = sessionWasReset && requestedCheckpoint != null
                    ? CopilotAgentTaskEventJournal.BuildAttemptedToolRecoveryPrompt(
                        requestedCheckpoint.TaskEventJournal)
                    : string.Empty;
                if (!string.IsNullOrWhiteSpace(attemptedToolRecoveryPrompt))
                {
                    promptMessages = InsertEvidenceMessageBeforeCurrentUser(
                        promptMessages,
                        attemptedToolRecoveryPrompt);
                    emit(CopilotAgentEvent.RuntimeDiagnostic(
                        "Agent recovery checkpoint supplied bounded recent attempted-tool metadata so the rebuilt session can avoid replaying completed or denied operations."));
                }

                return new AgentSessionPreparation
                {
                    Session = session,
                    SessionResumed = sessionResumed,
                    SteeringRegistration = steeringRegistration,
                    LiveCheckpointPublisher = liveCheckpointPublisher,
                    RecoveredTaskLedger = recoveredTaskLedger,
                    PromptMessages = promptMessages,
                };
            }
            catch
            {
                steeringRegistration?.Dispose();
                throw;
            }
        }

        private static async Task<int> DiscardRecoveredSteeringAsync(
            MessageInjectingChatClient messageInjector,
            AgentSession session,
            CancellationToken cancellationToken)
        {
            // Run before registering producers for this restored session. The host returns
            // unconfirmed steering to the draft; its old delivery queue must not resend it.
            var pending = await messageInjector.GetPendingMessagesAsync(session, cancellationToken);
            var retained = pending.Where(message => !IsOwnedSteeringMessage(message)).ToArray();
            var removedCount = pending.Count - retained.Length;
            if (removedCount == 0)
                return 0;

            // Framework 1.24 exposes queue snapshots and enqueue, but no removal API.
            // This persisted key also appears in the supported 1.21 session format.
            // Fail into normal session rebuilding if that compatibility boundary changes.
            const string pendingQueueKey = "MessageInjectingChatClient.PendingInjectedMessages";
            if (!session.StateBag.TryRemoveValue(pendingQueueKey))
                throw new InvalidOperationException("The restored message-injection queue could not be isolated.");

            // Background observations and hook context have no draft-recovery counterpart.
            // Keep their messages, including identifiers and order, in the resumed queue.
            if (retained.Length > 0)
                await messageInjector.EnqueueMessagesAsync(session, retained, cancellationToken);
            return removedCount;
        }

        private static bool IsOwnedSteeringMessage(ChatMessage message) =>
            message.Role == ChatRole.User
            && message.MessageId?.StartsWith(SteeringMessageIdPrefix, StringComparison.Ordinal) == true;
    }
}
