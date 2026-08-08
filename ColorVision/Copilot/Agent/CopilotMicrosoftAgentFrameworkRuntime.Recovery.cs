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
        private async Task<CopilotAgentRunResult> RecoverFinalAnswerOnlyAsync(
            CopilotAgentRequest request,
            CopilotAgentSessionCheckpoint checkpoint,
            CopilotCapabilityCatalogSnapshot capabilitySnapshot,
            CopilotAgentTaskEventJournalBuilder taskEventJournalBuilder,
            Action<CopilotAgentEvent> emit,
            CopilotAgentRunBudget runBudget,
            Stopwatch stopwatch,
            CancellationTokenSource timeBudgetCancellation,
            CancellationToken callerCancellationToken,
            CancellationToken cancellationToken)
        {
            emit(CopilotAgentEvent.Status("Agent Framework is retrying only the final answer with every tool disabled."));
            emit(CopilotAgentEvent.RuntimeDiagnostic("Final-answer-only recovery bypassed tool discovery, Harness execution, approvals, and task replay."));

            var preparedPrompt = _contextBuilder.BuildAnswerMessages(request, Array.Empty<CopilotAgentStepRecord>());
            IReadOnlyList<CopilotRequestMessage> promptMessages = CopilotAgentConversationMemory
                .MergeIntoPreparedPrompt(checkpoint.ConversationMemory, preparedPrompt.Messages);
            var evidencePrompt = CopilotAgentEvidenceArtifacts.BuildRecoveryPrompt(checkpoint.EvidenceArtifacts, capabilitySnapshot);
            if (!string.IsNullOrWhiteSpace(evidencePrompt))
                promptMessages = InsertEvidenceMessageBeforeCurrentUser(promptMessages, evidencePrompt);
            var runOutcomePrompt = CopilotAgentTaskEventJournal.BuildFinalAnswerRecoveryPrompt(checkpoint.TaskEventJournal);
            if (!string.IsNullOrWhiteSpace(runOutcomePrompt))
                promptMessages = InsertEvidenceMessageBeforeCurrentUser(promptMessages, runOutcomePrompt);
            promptMessages = promptMessages.Append(new CopilotRequestMessage(
                "user",
                "# Final-answer-only recovery\n"
                + "Return the missing user-facing final answer using only the supplied conversation and persisted evidence. Every tool is unavailable: do not request a tool, repeat an operation, claim a fresh verification, or treat historical evidence as authorization. Clearly distinguish verified results from stale or incomplete evidence."))
                .ToArray();
            IReadOnlyList<Microsoft.Extensions.AI.ChatMessage> messages = CopilotRequestMessageSequence
                .Normalize(promptMessages)
                .Select(ToFrameworkMessage)
                .ToArray();

            var tokenBudget = CopilotAgentTokenBudget.Create(request.Profile, runBudget);
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
                    $"Agent token budget exhausted after {snapshot.ProviderCalls} provider call(s); final-answer-only recovery stopped without invoking tools.")),
                snapshot => emit(CopilotAgentEvent.BudgetUpdated(runBudget.CreateSnapshot(
                    snapshot,
                    stopwatch.Elapsed,
                    toolCalls: 0,
                    timeBudgetExhausted: false))));
            var retryChatClient = new CopilotProviderRetryChatClient(
                chatClient,
                retry =>
                {
                    chatClient.RecordProviderRetry(retry);
                    emit(CopilotAgentEvent.FromProviderRetry(retry));
                });
            using var contextRecoveryChatClient = new CopilotContextWindowRecoveryChatClient(
                retryChatClient,
                tokenBudget.InputBudgetTokens,
                recoveryInfo =>
                {
                    chatClient.RecordContextRecovery(recoveryInfo);
                    emit(CopilotAgentEvent.RuntimeDiagnostic(recoveryInfo.ToDiagnosticText()));
                });

            var usage = CopilotTokenUsage.Empty;
            var finalAnswer = string.Empty;
            var timeBudgetExhausted = false;
            var contextWindowExceeded = false;
            var outputLengthLimited = false;
            var outputContentFiltered = false;
            var outputFinishReasonIncomplete = false;
            try
            {
                var response = await contextRecoveryChatClient.GetResponseAsync(
                    messages,
                    BuildFinalAnswerOptions(request),
                    cancellationToken);
                foreach (var usageContent in response.Messages.SelectMany(message => message.Contents).OfType<UsageContent>())
                    usage = usage.Add(ToCopilotUsage(usageContent.Details));
                finalAnswer = ExtractFinalAnswerText(response);
                outputLengthLimited = IsLengthLimitedOutput(response.FinishReason);
                outputContentFiltered = IsContentFilteredOutput(response.FinishReason);
                outputFinishReasonIncomplete = IsUnexpectedIncompleteOutput(response.FinishReason);
                if (!string.IsNullOrWhiteSpace(finalAnswer))
                    emit(CopilotAgentEvent.AnswerDelta(finalAnswer));
                else
                    emit(CopilotAgentEvent.RuntimeDiagnostic("Final-answer-only recovery returned no displayable text."));
                if (outputLengthLimited)
                    emit(CopilotAgentEvent.RuntimeDiagnostic("Final-answer-only recovery reached the provider output limit; partial text was retained and the checkpoint remains recoverable."));
                else if (outputContentFiltered)
                    emit(CopilotAgentEvent.RuntimeDiagnostic("Final-answer-only recovery was stopped by the provider content filter; allowed partial text was retained and the checkpoint remains recoverable."));
                else if (outputFinishReasonIncomplete)
                    emit(CopilotAgentEvent.RuntimeDiagnostic("Final-answer-only recovery ended with an explicit non-success finish reason; partial text was retained and the checkpoint remains recoverable."));
            }
            catch (OperationCanceledException) when (timeBudgetCancellation.IsCancellationRequested && !callerCancellationToken.IsCancellationRequested)
            {
                timeBudgetExhausted = true;
                emit(CopilotAgentEvent.RuntimeDiagnostic($"Final-answer-only recovery exhausted its total-time budget after {FormatDuration(stopwatch.Elapsed)}."));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (CopilotAgentContextWindowExceededException ex)
            {
                contextWindowExceeded = true;
                emit(CopilotAgentEvent.RuntimeDiagnostic(
                    $"Final-answer-only recovery was rejected locally because its estimated input ({ex.EstimatedInputTokens:N0} tokens) exceeded the configured input window ({ex.InputBudgetTokens:N0} tokens)."));
            }
            catch (CopilotAgentContextWindowRecoveryExhaustedException ex)
            {
                contextWindowExceeded = true;
                emit(CopilotAgentEvent.RuntimeDiagnostic(
                    $"Final-answer-only context recovery stopped after one bounded compaction attempt"
                    + $" ({ex.OriginalMessageCount} → {ex.CompactedMessageCount} messages"
                    + $" · estimated input {ex.EstimatedInputTokensBefore:N0} → {ex.EstimatedInputTokensAfter:N0} tokens"
                    + $" · target {ex.TargetInputTokens:N0})."));
            }
            catch (Exception ex)
            {
                emit(CopilotAgentEvent.RuntimeDiagnostic($"Final-answer-only recovery failed ({CopilotAgentTraceEntry.Sanitize(ex.Message)})."));
            }

            var hasDisplayableFinalAnswer = !string.IsNullOrWhiteSpace(finalAnswer);
            var hasFinalAnswer = hasDisplayableFinalAnswer
                && !outputLengthLimited
                && !outputContentFiltered
                && !outputFinishReasonIncomplete;
            if (!hasFinalAnswer)
            {
                emit(CopilotAgentEvent.AnswerDelta(hasDisplayableFinalAnswer
                    ? outputLengthLimited
                        ? "\n\n最终回答再次达到模型输出上限；已保留以上部分内容，可以稍后再次重试最终回答。"
                        : outputContentFiltered
                            ? "\n\n最终回答被提供商内容策略提前停止；已保留以上允许返回的内容。"
                            : "\n\n最终回答以未确认完成的提供商状态结束；已保留以上部分内容，可以稍后再次重试最终回答。"
                    : contextWindowExceeded
                        ? "最终回答所需上下文超过当前模型窗口，请缩短会话或附件内容后重试；已保存的上下文和工具结果没有被重放。"
                        : timeBudgetExhausted
                            ? "最终回答生成达到本轮时间预算。已保存的上下文和工具结果没有被重放，可以稍后再次重试最终回答。"
                            : "模型仍未返回可显示的最终回答。已保存的上下文和工具结果没有被重放，可以稍后再次重试最终回答。"));
            }

            var budgetSnapshot = runBudget.CreateSnapshot(
                chatClient.Snapshot,
                stopwatch.Elapsed,
                toolCalls: 0,
                timeBudgetExhausted);
            var taskLedger = new CopilotAgentTaskLedgerSnapshot
            {
                Mode = "execute",
                ResumedFromCheckpoint = true,
            };
            var budgetExhausted = timeBudgetExhausted || budgetSnapshot.BudgetExhausted;
            var stopReason = hasFinalAnswer
                ? CopilotAgentStopReason.Completed
                : contextWindowExceeded
                    ? CopilotAgentStopReason.ProviderFailure
                    : budgetExhausted
                        ? CopilotAgentStopReason.BudgetExhausted
                        : CopilotAgentStopReason.IncompleteOutput;
            IReadOnlyList<CopilotAgentBlockerSnapshot> blockers = hasFinalAnswer
                ? Array.Empty<CopilotAgentBlockerSnapshot>()
                : [CreateProviderOutputBlocker(
                    timeBudgetExhausted,
                    requestBudgetExhausted: budgetSnapshot.BudgetExhausted && !timeBudgetExhausted && !contextWindowExceeded,
                    contextWindowExceeded,
                    outputLengthLimited,
                    outputContentFiltered,
                    outputFinishReasonIncomplete)];
            taskEventJournalBuilder.RecordTaskLedger(taskLedger, "final-answer-only");
            foreach (var blocker in blockers)
                taskEventJournalBuilder.RecordBlocker(blocker);
            taskEventJournalBuilder.RecordStop(stopReason);
            var taskEventJournal = taskEventJournalBuilder.Snapshot();
            emit(CopilotAgentEvent.RuntimeDiagnostic(
                $"Final-answer-only recovery used {budgetSnapshot.ConsumedTokens:N0}/{budgetSnapshot.RequestTokenBudget:N0} tokens across {budgetSnapshot.ProviderCalls} provider call(s) · tools 0/{budgetSnapshot.MaxToolCalls}."));
            emit(CopilotAgentEvent.RuntimeDiagnostic($"Agent stop reason · {stopReason}."));

            CopilotAgentSessionCheckpoint? sessionCheckpoint = null;
            if (!hasFinalAnswer)
            {
                var conversationMemory = CopilotAgentConversationMemory.Merge(
                    checkpoint.ConversationMemory,
                    request.History,
                    request.UserText,
                    finalAnswer);
                sessionCheckpoint = CopyCheckpointWithOutcome(checkpoint, taskEventJournal, conversationMemory);
                if (sessionCheckpoint == null)
                    emit(CopilotAgentEvent.RuntimeDiagnostic("The final-answer recovery checkpoint could not be refreshed; retry metadata was not saved."));
            }
            else
            {
                emit(CopilotAgentEvent.RuntimeDiagnostic("The missing final answer was recovered. The old executable session checkpoint was retired so a later turn cannot resume before this answer."));
            }

            emit(CopilotAgentEvent.Completed());
            return new CopilotAgentRunResult
            {
                PreparedUserMessageContent = preparedPrompt.PreparedUserMessageContent,
                Usage = usage,
                Budget = budgetSnapshot,
                TaskLedger = taskLedger,
                StopReason = stopReason,
                Blockers = blockers,
                TaskEventJournal = taskEventJournal,
                SessionCheckpoint = sessionCheckpoint,
            };
        }

        private static CopilotAgentBlockerSnapshot CreateProviderOutputBlocker(
            bool timeBudgetExhausted,
            bool requestBudgetExhausted = false,
            bool contextWindowExceeded = false,
            bool outputLengthLimited = false,
            bool outputContentFiltered = false,
            bool outputFinishReasonIncomplete = false)
        {
            return new CopilotAgentBlockerSnapshot
            {
                Kind = CopilotAgentBlockerKind.ProviderOutput,
                Code = timeBudgetExhausted
                    ? "provider_output_timeout"
                    : contextWindowExceeded
                        ? "provider_context_window"
                        : outputLengthLimited
                            ? "provider_output_length"
                            : outputContentFiltered
                                ? "provider_content_filtered"
                                : outputFinishReasonIncomplete
                                    ? "provider_output_finish_reason"
                                : requestBudgetExhausted
                                    ? "provider_output_budget"
                                    : "provider_empty_output",
                Summary = timeBudgetExhausted
                    ? "The provider did not complete the Agent final answer before its time budget expired."
                    : contextWindowExceeded
                        ? "The provider rejected the request as larger than its actual context window after one bounded compaction recovery."
                        : outputLengthLimited
                            ? "The provider reached its maximum output length before the Agent final answer completed."
                            : outputContentFiltered
                                ? "The provider content policy stopped the Agent final answer."
                                : outputFinishReasonIncomplete
                                    ? "The provider ended the Agent final answer with an explicit non-success finish reason."
                                : requestBudgetExhausted
                                    ? "The Agent request budget was exhausted before a final answer was produced."
                                    : "The model returned no final answer after the bounded finalization attempt.",
                RequiresUserInput = true,
            };
        }

        private static CopilotAgentBlockerSnapshot CreateProviderInterruptionBlocker()
        {
            return new CopilotAgentBlockerSnapshot
            {
                Kind = CopilotAgentBlockerKind.ProviderOutput,
                Code = "provider_interrupted",
                Summary = "The provider stream ended after material Agent progress; the current session was checkpointed before any tool replay.",
                RequiresUserInput = true,
            };
        }

        private static CopilotAgentSessionCheckpoint? CopyCheckpointWithOutcome(
            CopilotAgentSessionCheckpoint checkpoint,
            CopilotAgentTaskEventJournalSnapshot taskEventJournal,
            IReadOnlyList<CopilotRequestMessage> conversationMemory)
        {
            var copy = new CopilotAgentSessionCheckpoint(checkpoint)
            {
                ProfileKey = checkpoint.ProfileKey,
                CapabilityCatalogRevision = checkpoint.CapabilityCatalogRevision,
                Capabilities = (checkpoint.Capabilities ?? Array.Empty<CopilotAgentCheckpointCapability>()).ToArray(),
                ToolSurfaceVersion = checkpoint.ToolSurfaceVersion,
                AvailableToolNames = (checkpoint.AvailableToolNames ?? Array.Empty<string>()).ToArray(),
                EnvironmentVersion = checkpoint.EnvironmentVersion,
                EnvironmentFingerprint = checkpoint.EnvironmentFingerprint,
                EvidenceArtifacts = (checkpoint.EvidenceArtifacts ?? Array.Empty<CopilotAgentEvidenceArtifact>()).ToArray(),
                ConversationMemory = conversationMemory.ToArray(),
                TaskIntentText = checkpoint.TaskIntentText,
                TaskEventJournal = taskEventJournal,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };
            return copy.IsStructurallyValid() ? copy : null;
        }

    }
}
