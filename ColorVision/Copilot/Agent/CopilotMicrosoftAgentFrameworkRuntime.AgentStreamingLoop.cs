#pragma warning disable MAAI001
#pragma warning disable CA1859
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AIChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotMicrosoftAgentFrameworkRuntime
    {
        private readonly record struct AgentStreamingLoopResult(
            CopilotTokenUsage Usage,
            CopilotAgentControlIntent ControlIntent,
            bool TimeBudgetExhausted,
            bool ProviderInterrupted,
            bool ContextWindowExceeded,
            bool ToolBudgetForcedFinalization,
            AIChatFinishReason? ProviderFinishReason);

        private async Task<AgentStreamingLoopResult> RunAgentStreamingLoopAsync(
            CopilotAgentRequest request,
            CopilotAgentRunBudget runBudget,
            Stopwatch stopwatch,
            CancellationTokenSource timeBudgetCancellation,
            CancellationToken callerCancellationToken,
            CancellationToken cancellationToken,
            CancellationToken toolBudgetCancellationToken,
            CancellationToken agentLoopCancellationToken,
            AIAgent agent,
            IReadOnlyList<ChatMessage> initialMessages,
            AgentSession session,
            HarnessToolBridge bridge,
            CopilotContextWindowRecoveryChatClient contextRecoveryChatClient,
            CopilotAgentTaskEventJournalBuilder taskEventJournalBuilder,
            Action<CopilotAgentEvent> emit,
            SteeringRegistration steeringRegistration,
            LiveCheckpointPublisher liveCheckpointPublisher,
            MessageInjectingChatClient messageInjector,
            StringBuilder answerText,
            CopilotBackgroundShellOutputDeliveryLease deferredBackgroundOutputDelivery,
            CopilotBackgroundShellCompletionDeliveryLease deferredBackgroundCompletionDelivery,
            IReadOnlyList<CopilotDeferredBackgroundShellOutputEvent> deferredBackgroundOutputEvents,
            IReadOnlyList<CopilotDeferredBackgroundShellCompletion> deferredBackgroundCompletions,
            IReadOnlyList<string> deferredBackgroundSignalMessages)
        {
            var messages = initialMessages;
            var usage = CopilotTokenUsage.Empty;
            var controlIntent = CopilotAgentControlIntent.None;
            var timeBudgetExhausted = false;
            var providerInterrupted = false;
            var contextWindowExceeded = false;
            var toolBudgetForcedFinalization = false;
            var deferredBackgroundSignalsAccepted = false;
            var frameworkApprovalAwaitingProviderUpdate = false;
            var steeringInputSealed = false;
            AIChatFinishReason? providerFinishReason = null;
            try
            {
                while (true)
                {
                    var approvalRequests = new List<ToolApprovalRequestContent>();
                    await foreach (var update in agent.RunStreamingAsync(messages, session, null, agentLoopCancellationToken))
                    {
                        agentLoopCancellationToken.ThrowIfCancellationRequested();
                        if (frameworkApprovalAwaitingProviderUpdate)
                        {
                            CompleteFrameworkApprovalRouting();
                            frameworkApprovalAwaitingProviderUpdate = false;
                        }
                        if (!deferredBackgroundSignalsAccepted
                            && deferredBackgroundSignalMessages.Count > 0)
                        {
                            deferredBackgroundOutputDelivery.Commit();
                            deferredBackgroundCompletionDelivery.Commit();
                            deferredBackgroundSignalsAccepted = true;
                            foreach (var deferredEvent in deferredBackgroundOutputEvents)
                            {
                                taskEventJournalBuilder
                                    .RecordBackgroundShellCommandOutput(
                                        deferredEvent.EventArgs);
                            }
                            foreach (var completion in deferredBackgroundCompletions)
                            {
                                taskEventJournalBuilder
                                    .RecordBackgroundShellCommandCompletion(
                                        completion.Snapshot);
                            }
                            emit(CopilotAgentEvent.RuntimeDiagnostic(
                                $"The provider produced its first update; {deferredBackgroundSignalMessages.Count} delayed background signal(s) are now marked delivered and will not be replayed."));
                        }

                        foreach (var usageContent in update.Contents.OfType<UsageContent>())
                            usage = usage.Add(ToCopilotUsage(usageContent.Details));
                        if (update.FinishReason.HasValue)
                            providerFinishReason = update.FinishReason;

                        approvalRequests.AddRange(update.Contents.OfType<ToolApprovalRequestContent>());
                        if (!string.IsNullOrEmpty(update.Text))
                            emit(CopilotAgentEvent.AnswerDelta(update.Text));
                    }

                    var deliveredSteeringMessages = await steeringRegistration
                        .RecordDeliveredSteeringMessagesAsync(
                            agentLoopCancellationToken);
                    if (deliveredSteeringMessages.Count > 0)
                    {
                        emit(CopilotAgentEvent.SteeringDelivered(deliveredSteeringMessages));
                        emit(CopilotAgentEvent.RuntimeDiagnostic(
                            $"Agent provider received {deliveredSteeringMessages.Count} queued user steering instruction(s)."));
                        await liveCheckpointPublisher.TryPublishAsync(
                            agent,
                            session,
                            agentLoopCancellationToken);
                    }

                    if (approvalRequests.Count == 0)
                    {
                        if (frameworkApprovalAwaitingProviderUpdate)
                        {
                            CancelFrameworkApprovalRouting();
                            frameworkApprovalAwaitingProviderUpdate = false;
                        }

                        if (!steeringInputSealed)
                        {
                            emit(CopilotAgentEvent.RuntimeDiagnostic(
                                "Agent provider loop completed; live steering input is now sealed."));
                            steeringRegistration.StopAcceptingInput();
                            steeringInputSealed = true;
                        }
                        var pendingInjectedMessages = await messageInjector
                            .GetPendingMessagesAsync(
                                session,
                                agentLoopCancellationToken);
                        if (pendingInjectedMessages.Count > 0)
                        {
                            emit(CopilotAgentEvent.RuntimeDiagnostic(
                                $"Agent sealed live steering input with {pendingInjectedMessages.Count} injected message(s) still pending; running the final Agent Framework drain before finalization."));
                            messages = Array.Empty<ChatMessage>();
                            continue;
                        }
                        break;
                    }

                    var approvalRouting = await RouteFrameworkApprovalsAsync(
                        approvalRequests,
                        request,
                        bridge,
                        contextRecoveryChatClient,
                        taskEventJournalBuilder,
                        emit,
                        usage,
                        cancellationToken);
                    usage = approvalRouting.Usage;
                    messages =
                    [
                        new ChatMessage(
                            ChatRole.User,
                            approvalRouting.Responses),
                    ];
                    frameworkApprovalAwaitingProviderUpdate = true;
                }
            }
            catch (OperationCanceledException) when (toolBudgetCancellationToken.IsCancellationRequested
                && !callerCancellationToken.IsCancellationRequested
                && !timeBudgetCancellation.IsCancellationRequested
                && request.RunControl?.Intent is not (CopilotAgentControlIntent.Pause or CopilotAgentControlIntent.Cancel))
            {
                toolBudgetForcedFinalization = true;
                emit(CopilotAgentEvent.RuntimeDiagnostic(
                    $"Agent reached its {runBudget.MaxToolCalls}-call tool limit; the tool-enabled loop was stopped and one bounded no-tools finalization call will summarize the collected evidence."));
            }
            catch (OperationCanceledException) when (request.RunControl?.Intent is CopilotAgentControlIntent.Pause or CopilotAgentControlIntent.Cancel
                || (timeBudgetCancellation.IsCancellationRequested && !callerCancellationToken.IsCancellationRequested))
            {
                var requestedControl = request.RunControl?.Intent ?? CopilotAgentControlIntent.None;
                if (requestedControl is CopilotAgentControlIntent.Pause or CopilotAgentControlIntent.Cancel)
                {
                    controlIntent = requestedControl;
                    taskEventJournalBuilder.RecordControl(controlIntent);
                    emit(CopilotAgentEvent.RuntimeDiagnostic(controlIntent == CopilotAgentControlIntent.Pause
                        ? "Agent pause requested; preserving the current task session checkpoint."
                        : "Agent cancellation requested; the new task session checkpoint will be discarded."));
                }
                else
                {
                    timeBudgetExhausted = timeBudgetCancellation.IsCancellationRequested && !callerCancellationToken.IsCancellationRequested;
                    emit(CopilotAgentEvent.RuntimeDiagnostic($"Agent total-time budget exhausted after {FormatDuration(stopwatch.Elapsed)}; finalizing the current task checkpoint."));
                }
            }
            catch (CopilotAgentTokenBudgetExceededException ex)
            {
                emit(CopilotAgentEvent.AnswerDelta(ex.Message));
            }
            catch (CopilotAgentContextWindowExceededException ex)
            {
                contextWindowExceeded = true;
                emit(CopilotAgentEvent.RuntimeDiagnostic(
                    $"Agent provider call was rejected locally because its estimated input ({ex.EstimatedInputTokens:N0} tokens) exceeded the configured input window ({ex.InputBudgetTokens:N0} tokens)."));
                emit(CopilotAgentEvent.AnswerDelta(ex.Message));
            }
            catch (CopilotAgentContextWindowRecoveryExhaustedException ex)
            {
                contextWindowExceeded = true;
                emit(CopilotAgentEvent.RuntimeDiagnostic(
                    $"Agent context recovery stopped after one bounded compaction attempt for the current model turn"
                    + $" ({ex.OriginalMessageCount} → {ex.CompactedMessageCount} messages"
                    + $" · estimated input {ex.EstimatedInputTokensBefore:N0} → {ex.EstimatedInputTokensAfter:N0} tokens"
                    + $" · target {ex.TargetInputTokens:N0})."));
                emit(CopilotAgentEvent.AnswerDelta(ex.Message));
            }
            catch (Exception ex) when (CopilotProviderRetryChatClient.IsProviderInterruption(ex, cancellationToken))
            {
                if (bridge.StepRecords.Count == 0 && answerText.Length == 0)
                    throw;

                providerInterrupted = true;
                if (CopilotProviderInactivityException.TryFind(
                    ex,
                    out var inactivity))
                {
                    var inactivityDescription =
                        inactivity.Phase == CopilotProviderInactivityPhase.FirstResponse
                            ? "returned no content"
                            : "returned no new stream content";
                    emit(CopilotAgentEvent.RuntimeDiagnostic(
                        $"The provider {inactivityDescription} for {FormatDuration(inactivity.TimeoutDuration)} after material Agent progress. The current Harness session will be checkpointed without replaying tools."));
                }
                else
                {
                    emit(CopilotAgentEvent.RuntimeDiagnostic(
                        "The provider stream was interrupted after material Agent progress. The current Harness session will be checkpointed without replaying tools."));
                }
                if (answerText.Length == 0)
                {
                    emit(CopilotAgentEvent.AnswerDelta(
                        "模型连接在 Agent 已取得进展后中断。当前任务状态和工具结果正在保存，可安全恢复，不会自动重放工具。"));
                }
            }
            catch
            {
                bridge.CancelOutstandingApprovals();
                throw;
            }
            finally
            {
                steeringRegistration.StopAcceptingInput();
                var undeliveredSteeringMessages = steeringRegistration.GetUndeliveredSteeringMessages();
                if (undeliveredSteeringMessages.Count > 0)
                    emit(CopilotAgentEvent.SteeringRecovery(undeliveredSteeringMessages));
            }

            bridge.CancelOutstandingApprovals();

            if (controlIntent == CopilotAgentControlIntent.None)
                timeBudgetExhausted |= timeBudgetCancellation.IsCancellationRequested && !callerCancellationToken.IsCancellationRequested;

            return new AgentStreamingLoopResult(
                usage,
                controlIntent,
                timeBudgetExhausted,
                providerInterrupted,
                contextWindowExceeded,
                toolBudgetForcedFinalization,
                providerFinishReason);
        }
    }
}
