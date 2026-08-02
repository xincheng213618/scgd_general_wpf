#pragma warning disable MAAI001
using Microsoft.Agents.AI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotMicrosoftAgentFrameworkRuntime
    {
        private async Task<CopilotAgentSessionCheckpoint?> SaveFinalSessionCheckpointAsync(
            CopilotAgentRequest request,
            CopilotAgentSessionCheckpoint? requestedCheckpoint,
            AIAgent agent,
            AgentSession session,
            CopilotAgentRunFinalizationScope finalization,
            CopilotCapabilityCatalogSnapshot capabilitySnapshot,
            IReadOnlyList<CopilotAgentEvidenceArtifact> evidenceArtifacts,
            CopilotAgentTaskEventJournalSnapshot taskEventJournal,
            IReadOnlyList<string> availableToolNames,
            CopilotAgentEnvironmentContext environmentContext,
            CopilotToolExecutionHookRegistrySnapshot hookSurfaceSnapshot,
            SteeringRegistration steeringRegistration,
            LiveCheckpointPublisher liveCheckpointPublisher,
            string answerText,
            CopilotAgentControlIntent controlIntent,
            Action<CopilotAgentEvent> emit)
        {
            CopilotAgentSessionCheckpoint? sessionCheckpoint = null;
            try
            {
                if (controlIntent != CopilotAgentControlIntent.Cancel)
                {
                    var serializedSession = await agent.SerializeSessionAsync(session, null, finalization.Token)
                        .AsTask()
                        .WaitAsync(finalization.Token);
                    var conversationMemory = CopilotAgentConversationMemory.Merge(
                        requestedCheckpoint?.ConversationMemory,
                        request.History,
                        request.UserText,
                        answerText,
                        steeringRegistration.GetDeliveredSteeringMessages());
                    sessionCheckpoint = CopilotAgentSessionCheckpoint.Create(
                        request.Profile,
                        serializedSession.GetRawText(),
                        capabilitySnapshot,
                        evidenceArtifacts,
                        taskEventJournal,
                        availableToolNames,
                        conversationMemory,
                        environmentContext,
                        request.TaskIntentText,
                        hookSurfaceSnapshot);
                    if (sessionCheckpoint == null)
                        emit(CopilotAgentEvent.RuntimeDiagnostic("Agent session checkpoint exceeded its session or capability persistence limit and was not saved."));
                }
            }
            catch (OperationCanceledException) when (finalization.IsTimeoutCancellationRequested)
            {
                sessionCheckpoint = liveCheckpointPublisher.LatestCheckpoint?.CopyWithTaskEventJournal(taskEventJournal);
                emit(CopilotAgentEvent.RuntimeDiagnostic(sessionCheckpoint == null
                    ? $"Agent finalization exceeded {FormatDuration(CopilotAgentRunFinalizationScope.DefaultInterruptedTimeout)}; no final checkpoint could be saved."
                    : $"Agent finalization exceeded {FormatDuration(CopilotAgentRunFinalizationScope.DefaultInterruptedTimeout)}; the latest incremental checkpoint was sealed with the final task state."));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                emit(CopilotAgentEvent.RuntimeDiagnostic($"Agent session checkpoint could not be saved ({CopilotUserFacingErrorFormatter.Sanitize(ex.Message)})."));
                sessionCheckpoint = liveCheckpointPublisher.LatestCheckpoint?.CopyWithTaskEventJournal(taskEventJournal);
                if (sessionCheckpoint != null)
                    emit(CopilotAgentEvent.RuntimeDiagnostic("The latest incremental Agent checkpoint was sealed with the final task state instead."));
            }

            return sessionCheckpoint;
        }
    }
}
