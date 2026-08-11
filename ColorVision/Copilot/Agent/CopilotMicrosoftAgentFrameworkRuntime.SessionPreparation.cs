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
                AgentSession session;
                if (checkpointCompatibility?.CanResume == true && requestedCheckpoint != null)
                {
                    try
                    {
                        using var checkpointDocument = JsonDocument.Parse(requestedCheckpoint.SerializedSessionJson);
                        session = await agent.DeserializeSessionAsync(checkpointDocument.RootElement.Clone(), null, cancellationToken);
                        sessionResumed = true;
                        taskEventJournalBuilder.RecordSessionResumed();
                        emit(CopilotAgentEvent.RuntimeDiagnostic("Agent Framework session resumed from the persisted conversation checkpoint."));
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
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
                    taskEventJournalBuilder);
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
                    steeringRegistration.GetDeliveredSteeringMessages);

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

                IReadOnlyList<CopilotRequestMessage> unseenVisibleHistory = sessionResumed && requestedCheckpoint != null
                    ? CopilotAgentConversationMemory.SelectUnseenVisibleTail(requestedCheckpoint.ConversationMemory, request.History)
                    : Array.Empty<CopilotRequestMessage>();
                var promptMessages = sessionResumed
                    ? unseenVisibleHistory.Concat(preparedPromptMessages.TakeLast(1)).ToArray()
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
    }
}
