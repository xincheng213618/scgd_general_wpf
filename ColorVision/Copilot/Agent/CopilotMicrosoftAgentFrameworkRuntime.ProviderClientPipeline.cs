#pragma warning disable MAAI001
using System;
using System.Diagnostics;
using System.Threading;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotMicrosoftAgentFrameworkRuntime
    {
        private sealed class ProviderClientPipeline : IDisposable
        {
            public CopilotTokenBudgetChatClient ChatClient { get; set; } = null!;

            public CopilotContextWindowRecoveryChatClient ContextRecoveryChatClient { get; set; } = null!;

            public CopilotUnknownToolCallTrackingChatClient TrackingChatClient { get; set; } = null!;

            public CopilotAgentToolSurfaceMetrics ToolSurface { get; set; }

            public bool UsedDelegatedDirectAnswer { get; set; }

            public void Dispose() => TrackingChatClient.Dispose();
        }

        private ProviderClientPipeline CreateProviderClientPipeline(
            CopilotAgentRequest request,
            CopilotAgentRunBudget runBudget,
            Stopwatch stopwatch,
            CopilotAgentTokenBudget tokenBudget,
            HarnessToolBridge bridge,
            Action<CopilotAgentEvent> emit,
            bool taskLedgerEnabled)
        {
            var pipeline = new ProviderClientPipeline();
            var providerInactivityTimeouts =
                CopilotProviderInactivityPolicy.Resolve(request.Profile);
            var providerChatClient = new CopilotProviderInactivityChatClient(
                new CopilotCancellationGuardChatClient(
                    _chatClientFactory(request.Profile)),
                providerInactivityTimeouts.FirstResponseTimeout,
                providerInactivityTimeouts.StreamingUpdateTimeout);
            var chatClient = new CopilotTokenBudgetChatClient(
                providerChatClient,
                tokenBudget,
                snapshot => emit(CopilotAgentEvent.RuntimeDiagnostic(
                    $"Agent token budget exhausted after {snapshot.ProviderCalls} provider call(s); the model loop was stopped without replaying tools.")),
                snapshot => emit(CopilotAgentEvent.BudgetUpdated(runBudget.CreateSnapshot(
                    snapshot,
                    stopwatch.Elapsed,
                    bridge.StepRecords.Count,
                    timeBudgetExhausted: false,
                    bridge.ToolBudgetExhausted,
                    pipeline.UsedDelegatedDirectAnswer,
                    pipeline.ToolSurface))));
            var retryChatClient = new CopilotProviderRetryChatClient(
                chatClient,
                retry =>
                {
                    chatClient.RecordProviderRetry(retry);
                    emit(CopilotAgentEvent.FromProviderRetry(retry));
                });
            var contextRecoveryChatClient = new CopilotContextWindowRecoveryChatClient(
                retryChatClient,
                tokenBudget.InputBudgetTokens,
                recoveryInfo =>
                {
                    chatClient.RecordContextRecovery(recoveryInfo);
                    emit(CopilotAgentEvent.RuntimeDiagnostic(recoveryInfo.ToDiagnosticText()));
                });
            var delegatedDirectAnswerChatClient = new CopilotDelegatedDirectAnswerChatClient(
                contextRecoveryChatClient,
                request,
                () => bridge.StepRecords,
                taskLedgerEnabled,
                () =>
                {
                    pipeline.UsedDelegatedDirectAnswer = true;
                    emit(CopilotAgentEvent.RuntimeDiagnostic(
                        "The explicit completed DelegateExplore result was returned directly without a second parent provider call."));
                });
            var explicitDelegationDispatchChatClient = new CopilotExplicitDelegationDispatchChatClient(
                delegatedDirectAnswerChatClient,
                request,
                HarnessToolBridge.ToFunctionName("DelegateExplore"),
                taskLedgerEnabled,
                () => emit(CopilotAgentEvent.RuntimeDiagnostic(
                    "The explicit exclusive DelegateExplore request was dispatched directly without a parent provider planning call.")));
            pipeline.ChatClient = chatClient;
            pipeline.ContextRecoveryChatClient = contextRecoveryChatClient;
            pipeline.TrackingChatClient = new CopilotUnknownToolCallTrackingChatClient(
                explicitDelegationDispatchChatClient,
                bridge.RecordUnknownToolCall);
            return pipeline;
        }
    }
}
