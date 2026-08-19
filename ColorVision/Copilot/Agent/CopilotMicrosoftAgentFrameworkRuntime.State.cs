#pragma warning disable MAAI001
#pragma warning disable CA1859
#pragma warning disable CA1001
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
            private readonly CopilotAgentEnvironmentContext? _checkpointEnvironmentContext;
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
            private readonly SemaphoreSlim _publicationGate = new(1, 1);

            public LiveCheckpointPublisher(
                CopilotAgentRequest request,
                CopilotAgentSessionCheckpoint? requestedCheckpoint,
                CopilotCapabilityCatalogSnapshot capabilitySnapshot,
                IReadOnlyList<string> availableToolNames,
                CopilotAgentEnvironmentContext? checkpointEnvironmentContext,
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
                _checkpointEnvironmentContext = checkpointEnvironmentContext;
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
                if (!await _publicationGate.WaitAsync(
                        millisecondsTimeout: 0,
                        cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }
                try
                {
                    return await PublishCoreAsync(
                        agent,
                        session,
                        knownTaskLedger,
                        cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    _publicationGate.Release();
                }
            }

            public async ValueTask<bool> PublishForToolDispatchAsync(
                AIAgent agent,
                AgentSession session,
                CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(agent);
                ArgumentNullException.ThrowIfNull(session);
                await _publicationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    return await PublishCoreAsync(
                        agent,
                        session,
                        null,
                        cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    _publicationGate.Release();
                }
            }

            private async ValueTask<bool> PublishCoreAsync(
                AIAgent agent,
                AgentSession session,
                CopilotAgentTaskLedgerSnapshot? knownTaskLedger,
                CancellationToken cancellationToken)
            {
                try
                {
                    var taskLedger = knownTaskLedger
                        ?? await CaptureTaskLedgerAsync(
                            _todoProvider,
                            _modeProvider,
                            session,
                            _sessionResumed,
                            cancellationToken);
                    if (knownTaskLedger == null)
                        _taskEventJournalBuilder.RecordTaskLedger(taskLedger, "checkpoint");

                    var evidenceArtifacts = CopilotAgentEvidenceArtifacts.Merge(
                        _previousEvidenceArtifacts,
                        _bridge.StepRecords,
                        _capabilitySnapshot,
                        DateTimeOffset.UtcNow);
                    var serializedSession = await agent.SerializeSessionAsync(
                        session,
                        null,
                        cancellationToken);
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
                        _checkpointEnvironmentContext,
                        _request.TaskIntentText,
                        _hookSurfaceSnapshot,
                        _request.ProjectInstructions,
                        _request.ConfiguredDeveloperInstructions);
                    if (checkpoint == null)
                    {
                        _emit(CopilotAgentEvent.RuntimeDiagnostic(
                            "Incremental Agent checkpoint was rejected because the serialized state was invalid."));
                        return false;
                    }

                    var checkpointEvent =
                        CopilotAgentEvent.CheckpointUpdated(checkpoint, taskLedger);
                    CopilotAgentEventProtocol.Validate(checkpointEvent);
                    Volatile.Write(ref _latestTaskLedger, checkpointEvent.TaskLedger);
                    Volatile.Write(ref _latestCheckpoint, checkpointEvent.SessionCheckpoint);
                    _emit(checkpointEvent);
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
            }
        }

    }
}
