#pragma warning disable MAAI001
using System;
using System.Collections.Generic;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotMicrosoftAgentFrameworkRuntime
    {
        private bool ApplyFinalAnswerQualityGates(
            CopilotAgentRequest request,
            Action<CopilotAgentEvent> emit,
            HarnessToolBridge bridge,
            IReadOnlyList<ICopilotTool> availableTools,
            Func<string> answerText,
            CopilotAgentControlIntent controlIntent,
            bool timeBudgetExhausted,
            bool providerInterrupted,
            bool contextWindowExceeded,
            bool automaticReviewCircuitBreakerTripped,
            bool hasModelFinalAnswer,
            bool outputLengthLimitReached,
            bool outputContentFiltered,
            bool outputFinishReasonIncomplete)
        {
            if (controlIntent == CopilotAgentControlIntent.None
                && !timeBudgetExhausted
                && !providerInterrupted
                && !contextWindowExceeded
                && !automaticReviewCircuitBreakerTripped
                && !hasModelFinalAnswer)
            {
                var partialAnswerPrefix = answerText().Length > 0 ? "\n\n" : string.Empty;
                emit(CopilotAgentEvent.AnswerDelta(outputContentFiltered
                    ? partialAnswerPrefix + "最终回答被提供商内容策略提前停止；已保留以上允许返回的内容，可调整请求后重试最终回答。"
                    : outputLengthLimitReached
                        ? partialAnswerPrefix + "最终回答达到模型输出上限；已保留以上部分内容，可稍后重试最终回答。"
                        : outputFinishReasonIncomplete
                            ? partialAnswerPrefix + "最终回答以未确认完成的提供商状态结束；已保留以上部分内容，可稍后重试最终回答。"
                            : "模型没有返回可显示的最终回答。本轮上下文和工具执行记录已经保留，可使用“重试最终回答”仅重新生成总结，不会再次调用工具。"));
            }
            if (controlIntent == CopilotAgentControlIntent.None
                && !timeBudgetExhausted
                && !providerInterrupted
                && hasModelFinalAnswer
                && CopilotNarrowEvidenceAnswerPolicy.TryGetUnsupportedFindingReason(
                    request,
                    answerText(),
                    out var unsupportedFindingReason))
            {
                emit(CopilotAgentEvent.RuntimeDiagnostic(
                    $"Narrow evidence quality gate rejected an unsupported finding ({unsupportedFindingReason}); the answer was replaced with an explicit no-verified-finding result."));
                emit(CopilotAgentEvent.AnswerReset());
                emit(CopilotAgentEvent.AnswerDelta(CopilotNarrowEvidenceAnswerPolicy.BuildNoVerifiedFindingAnswer(request)));
                hasModelFinalAnswer = true;
            }
            if (controlIntent == CopilotAgentControlIntent.None
                && !timeBudgetExhausted
                && hasModelFinalAnswer)
            {
                var sourceAppendix = CopilotWebEvidenceSourceLedger.BuildMissingSourceAppendix(
                    bridge.StepRecords,
                    availableTools,
                    answerText());
                if (!string.IsNullOrWhiteSpace(sourceAppendix))
                {
                    emit(CopilotAgentEvent.AnswerDelta(sourceAppendix));
                    emit(CopilotAgentEvent.RuntimeDiagnostic("The model used web evidence without citing a returned URL; a bounded source ledger was appended to the final answer."));
                }
            }

            return hasModelFinalAnswer;
        }
    }
}
