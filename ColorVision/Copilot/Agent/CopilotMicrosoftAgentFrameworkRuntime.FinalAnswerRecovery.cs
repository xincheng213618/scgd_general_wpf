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
        private readonly record struct FinalAnswerRecoveryResult(
            CopilotTokenUsage Usage,
            bool OutputLengthLimitReached,
            bool OutputContentFiltered,
            bool OutputFinishReasonIncomplete,
            bool HasModelFinalAnswer,
            CopilotAgentBlockerSnapshot? ProviderFailure);

        private async Task<FinalAnswerRecoveryResult> RecoverFinalAnswerAsync(
            CopilotAgentRequest request,
            IReadOnlyList<string> deliveredSteeringMessages,
            Action<CopilotAgentEvent> emit,
            HarnessToolBridge bridge,
            TodoProvider? todoProvider,
            AgentModeProvider? modeProvider,
            AgentSession session,
            bool sessionResumed,
            CopilotContextWindowRecoveryChatClient contextRecoveryChatClient,
            CancellationToken cancellationToken,
            bool toolBudgetForcedFinalization,
            bool answerHasContent,
            CopilotTokenUsage usage,
            bool outputLengthLimitReached,
            bool outputContentFiltered,
            bool outputFinishReasonIncomplete)
        {
            emit(CopilotAgentEvent.RuntimeDiagnostic(toolBudgetForcedFinalization
                ? "The tool-enabled Agent loop reached its hard limit; starting one bounded finalization call with business tools disabled."
                : "Agent Framework returned no displayable final answer; starting one bounded finalization call with business tools disabled."));
            var repairLedger = await CaptureTaskLedgerAsync(
                todoProvider,
                modeProvider,
                session,
                sessionResumed,
                cancellationToken);
            var repairPrompt = _contextBuilder.BuildAnswerMessages(request, bridge.StepRecords);
            var repairInstruction = "# Final answer recovery\n"
                + (toolBudgetForcedFinalization
                    ? "The tool-enabled Agent loop reached its hard tool-call limit. Provide the final answer now using only the supplied request, context, and collected tool observations. Do not request or call tools. Do not claim unfinished work is complete; state remaining work or a concrete blocker when applicable.\n"
                    : "The Agent loop ended without displayable final text. Provide the final answer now using only the supplied request, context, and tool observations. Do not request or call tools. Do not claim unfinished work is complete; state remaining work or a concrete blocker when applicable.\n")
                + CodeFindingEvidenceInstruction + "\n"
                + FormatTaskLedgerDiagnostic("Current task ledger", repairLedger);
            var repairMessages = CopilotRequestMessageSequence
                .Normalize(repairPrompt.Messages
                    .Concat(deliveredSteeringMessages.Select(message => new CopilotRequestMessage("user", message)))
                    .Append(new CopilotRequestMessage("user", repairInstruction)))
                .Select(ToFrameworkMessage)
                .ToArray();
            var hasModelFinalAnswer = false;
            CopilotAgentBlockerSnapshot? providerFailure = null;
            try
            {
                var repairResponse = await contextRecoveryChatClient.GetResponseAsync(
                    repairMessages,
                    BuildFinalAnswerOptions(request),
                    cancellationToken);
                usage = usage.Add(CopilotTokenBudgetChatClient.ExtractResponseUsage(repairResponse));
                var repairLengthLimited = IsLengthLimitedOutput(repairResponse.FinishReason);
                var repairContentFiltered = IsContentFilteredOutput(repairResponse.FinishReason);
                var repairFinishReasonIncomplete = IsUnexpectedIncompleteOutput(repairResponse.FinishReason);
                var repairedText = ExtractFinalAnswerText(repairResponse);
                outputLengthLimitReached = repairLengthLimited;
                outputContentFiltered = repairContentFiltered;
                outputFinishReasonIncomplete = repairFinishReasonIncomplete;
                if (repairLengthLimited)
                {
                    emit(CopilotAgentEvent.RuntimeDiagnostic(
                        "The bounded no-tools finalization call also reached its maximum output length; allowed partial text was retained without replacing earlier output."));
                }
                else if (repairContentFiltered)
                {
                    emit(CopilotAgentEvent.RuntimeDiagnostic(
                        "The bounded no-tools finalization call was stopped by the provider content filter; filtered replacement text was not accepted as complete and earlier partial output was retained."));
                }
                else if (repairFinishReasonIncomplete)
                {
                    emit(CopilotAgentEvent.RuntimeDiagnostic(
                        "The bounded no-tools finalization call ended with an explicit non-success finish reason; replacement text was not accepted as complete and earlier partial output was retained."));
                }
                if (!repairLengthLimited
                    && !repairContentFiltered
                    && !repairFinishReasonIncomplete
                    && !string.IsNullOrWhiteSpace(repairedText))
                {
                    if (answerHasContent)
                        emit(CopilotAgentEvent.AnswerReset());
                    emit(CopilotAgentEvent.AnswerDelta(repairedText));
                    answerHasContent = true;
                    hasModelFinalAnswer = true;
                    emit(CopilotAgentEvent.RuntimeDiagnostic("The bounded no-tools finalization call produced the final answer."));
                }
                else if (!string.IsNullOrWhiteSpace(repairedText))
                {
                    if (!answerHasContent)
                    {
                        emit(CopilotAgentEvent.AnswerDelta(repairedText));
                        answerHasContent = true;
                    }
                }
                else
                {
                    emit(CopilotAgentEvent.RuntimeDiagnostic("The bounded no-tools finalization call also returned no displayable text."));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is CopilotAgentContextWindowExceededException or CopilotAgentContextWindowRecoveryExhaustedException
                || CopilotProviderRetryChatClient.IsProviderInterruption(ex, cancellationToken))
            {
                providerFailure = ex is CopilotAgentContextWindowExceededException or CopilotAgentContextWindowRecoveryExhaustedException
                    ? CreateProviderOutputBlocker(timeBudgetExhausted: false, contextWindowExceeded: true)
                    : CreateProviderFailureBlocker(ex);
                outputLengthLimitReached = false;
                outputContentFiltered = false;
                outputFinishReasonIncomplete = false;
                emit(CopilotAgentEvent.RuntimeDiagnostic("The bounded no-tools finalization call failed: " + providerFailure.Summary));
                if (!answerHasContent)
                    emit(CopilotAgentEvent.AnswerDelta(providerFailure.Summary + " 已取得的工具结果正在保存，可处理原因后重试最终回答。"));
            }
            catch (Exception ex)
            {
                emit(CopilotAgentEvent.RuntimeDiagnostic($"The bounded no-tools finalization call failed ({CopilotUserFacingErrorFormatter.Sanitize(ex.Message, request.Profile.ApiKey)})."));
            }

            return new FinalAnswerRecoveryResult(
                usage,
                outputLengthLimitReached,
                outputContentFiltered,
                outputFinishReasonIncomplete,
                hasModelFinalAnswer,
                providerFailure);
        }
    }
}
