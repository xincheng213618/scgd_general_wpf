using System;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotTurnBudgetLifecycleState(
        CopilotAgentBudgetSnapshot? Latest)
    {
        public static CopilotTurnBudgetLifecycleState Empty => new(null);

        public CopilotTurnBudgetLifecycleState Observe(CopilotAgentEvent agentEvent)
        {
            ArgumentNullException.ThrowIfNull(agentEvent);
            if (agentEvent.Type != CopilotAgentEventType.BudgetUpdated)
                return this;

            var current = agentEvent.Budget;
            if (!IsStructurallyValid(current))
                throw new InvalidOperationException("Copilot Agent emitted an invalid budget snapshot.");
            if (Latest != null)
                RequireCovers(Latest, current!, "Copilot Agent budget snapshot moved backwards.");
            return new CopilotTurnBudgetLifecycleState(current);
        }

        public void ValidateCompletion(CopilotAgentRunResult agentRunResult)
        {
            ArgumentNullException.ThrowIfNull(agentRunResult);
            if (Latest == null)
                return;
            if (!IsStructurallyValid(agentRunResult.Budget))
                throw new InvalidOperationException("Copilot Agent completed with an invalid budget snapshot.");
            RequireCovers(
                Latest,
                agentRunResult.Budget,
                "Copilot Agent final budget did not cover its latest update.");
        }

        internal static bool IsStructurallyValid(CopilotAgentBudgetSnapshot? budget)
        {
            if (budget == null
                || budget.ContextWindowTokens < CopilotAgentTokenBudget.MinimumContextWindowTokens
                || budget.ContextWindowTokens > CopilotAgentTokenBudget.MaximumContextWindowTokens
                || budget.InputBudgetTokens <= 0
                || budget.InputBudgetTokens > budget.ContextWindowTokens
                || budget.RequestTokenBudget < CopilotAgentRunBudget.MinimumRequestTokenBudget
                || budget.RequestTokenBudget > CopilotAgentRunBudget.MaximumRequestTokenBudget
                || budget.ConsumedTokens < 0
                || budget.ProviderCalls < 0
                || budget.PeakEstimatedInputTokens < 0
                || budget.ProviderRetryCount is < 0
                || budget.ProviderRetryCount > budget.ProviderCalls
                || budget.ProviderRateLimitRetryCount is < 0
                || budget.ProviderRateLimitRetryCount > budget.ProviderRetryCount
                || budget.ProviderRetryDelayMs < 0
                || budget.ProviderFirstContentTimeoutCount is < 0
                || budget.ProviderFirstContentTimeoutCount > budget.ProviderCalls
                || budget.ProviderStreamInactivityTimeoutCount is < 0
                || budget.ProviderStreamInactivityTimeoutCount
                    > budget.ProviderCalls - budget.ProviderFirstContentTimeoutCount
                || budget.ProviderResponseCount is < 0
                || budget.ProviderResponseCount > budget.ProviderCalls
                || budget.ProviderFirstResponseLatencyTotalMs < 0
                || budget.ProviderFirstResponseLatencyMaxMs is < 0
                || budget.ProviderFirstResponseLatencyMaxMs > budget.ProviderFirstResponseLatencyTotalMs
                || budget.ProviderCallDurationTotalMs < budget.ProviderFirstResponseLatencyTotalMs
                || budget.ProviderStreamChunkCount < 0
                || budget.ProviderStreamInterChunkLatencyCount is < 0
                || budget.ProviderStreamInterChunkLatencyCount
                    > Math.Max(0, budget.ProviderStreamChunkCount - 1)
                || budget.ProviderStreamInterChunkLatencyTotalMs < 0
                || budget.ProviderStreamInterChunkLatencyMaxMs is < 0
                || budget.ProviderStreamInterChunkLatencyMaxMs > budget.ProviderStreamInterChunkLatencyTotalMs
                || budget.ContextRecoveryCount < 0
                || budget.ContextRecoveryEstimatedInputTokensBefore < 0
                || budget.ContextRecoveryEstimatedInputTokensAfter is < 0
                || budget.ContextRecoveryEstimatedInputTokensAfter
                    > budget.ContextRecoveryEstimatedInputTokensBefore
                || budget.ReportedInputTokens < 0
                || budget.ReportedOutputTokens < 0
                || budget.ReportedTotalTokens < (long)budget.ReportedInputTokens + budget.ReportedOutputTokens
                || budget.ReportedCachedInputTokens is < 0
                || budget.ReportedCachedInputTokens > budget.ReportedInputTokens
                || budget.ConsumedTokens < budget.ReportedTotalTokens
                || budget.MaxToolCalls < CopilotAgentRunBudget.MinimumToolCalls
                || budget.MaxToolCalls > CopilotAgentRunBudget.MaximumToolCalls
                || budget.ToolCalls is < 0
                || budget.ToolCalls > budget.MaxToolCalls
                || budget.RegisteredToolCount < 0
                || budget.AvailableToolCount is < 0
                || budget.AvailableToolCount > budget.RegisteredToolCount
                || budget.AvailableToolDefinitionCharacters < 0
                || budget.HarnessInstructionCharacters < 0
                || budget.NarrowEvidenceResultLimit < 0
                || budget.MaxAgentPasses < CopilotAgentRunBudget.MinimumAgentPasses
                || budget.MaxAgentPasses > CopilotAgentRunBudget.MaximumAgentPasses
                || budget.TotalDurationMs < CopilotAgentRunBudget.MinimumTotalDuration.TotalMilliseconds
                || budget.TotalDurationMs > CopilotAgentRunBudget.MaximumTotalDuration.TotalMilliseconds
                || budget.ElapsedMs < 0
                || ((budget.RequestTokenBudgetExhausted
                        || budget.ToolBudgetExhausted
                        || budget.TimeBudgetExhausted)
                    && !budget.BudgetExhausted))
            {
                return false;
            }

            return budget.ProviderResponseCount > 0
                || (budget.ProviderFirstResponseLatencyTotalMs == 0
                    && budget.ProviderFirstResponseLatencyMaxMs == 0
                    && budget.ProviderStreamChunkCount == 0
                    && budget.ProviderStreamInterChunkLatencyCount == 0
                    && budget.ProviderStreamInterChunkLatencyTotalMs == 0
                    && budget.ProviderStreamInterChunkLatencyMaxMs == 0);
        }

        private static void RequireCovers(
            CopilotAgentBudgetSnapshot previous,
            CopilotAgentBudgetSnapshot current,
            string errorMessage)
        {
            if (!HasStableLimits(previous, current)
                || current.ConsumedTokens < previous.ConsumedTokens
                || current.ProviderCalls < previous.ProviderCalls
                || current.PeakEstimatedInputTokens < previous.PeakEstimatedInputTokens
                || current.ProviderRetryCount < previous.ProviderRetryCount
                || current.ProviderRateLimitRetryCount < previous.ProviderRateLimitRetryCount
                || current.ProviderRetryDelayMs < previous.ProviderRetryDelayMs
                || current.ProviderFirstContentTimeoutCount < previous.ProviderFirstContentTimeoutCount
                || current.ProviderStreamInactivityTimeoutCount < previous.ProviderStreamInactivityTimeoutCount
                || current.ProviderResponseCount < previous.ProviderResponseCount
                || current.ProviderFirstResponseLatencyTotalMs < previous.ProviderFirstResponseLatencyTotalMs
                || current.ProviderFirstResponseLatencyMaxMs < previous.ProviderFirstResponseLatencyMaxMs
                || current.ProviderCallDurationTotalMs < previous.ProviderCallDurationTotalMs
                || current.ProviderStreamChunkCount < previous.ProviderStreamChunkCount
                || current.ProviderStreamInterChunkLatencyCount < previous.ProviderStreamInterChunkLatencyCount
                || current.ProviderStreamInterChunkLatencyTotalMs < previous.ProviderStreamInterChunkLatencyTotalMs
                || current.ProviderStreamInterChunkLatencyMaxMs < previous.ProviderStreamInterChunkLatencyMaxMs
                || current.ContextRecoveryCount < previous.ContextRecoveryCount
                || current.ContextRecoveryEstimatedInputTokensBefore < previous.ContextRecoveryEstimatedInputTokensBefore
                || current.ContextRecoveryEstimatedInputTokensAfter < previous.ContextRecoveryEstimatedInputTokensAfter
                || current.ReportedInputTokens < previous.ReportedInputTokens
                || current.ReportedOutputTokens < previous.ReportedOutputTokens
                || current.ReportedTotalTokens < previous.ReportedTotalTokens
                || EffectiveCachedInputTokens(current) < EffectiveCachedInputTokens(previous)
                || current.ToolCalls < previous.ToolCalls
                || current.ElapsedMs < previous.ElapsedMs
                || BecameFalse(previous.UsedEstimatedUsage, current.UsedEstimatedUsage)
                || BecameFalse(previous.UsedDelegatedDirectAnswer, current.UsedDelegatedDirectAnswer)
                || BecameFalse(previous.BudgetExhausted, current.BudgetExhausted)
                || BecameFalse(previous.RequestTokenBudgetExhausted, current.RequestTokenBudgetExhausted)
                || BecameFalse(previous.ToolBudgetExhausted, current.ToolBudgetExhausted)
                || BecameFalse(previous.TimeBudgetExhausted, current.TimeBudgetExhausted))
            {
                throw new InvalidOperationException(errorMessage);
            }
        }

        private static bool HasStableLimits(
            CopilotAgentBudgetSnapshot left,
            CopilotAgentBudgetSnapshot right) =>
            left.CompactionEnabled == right.CompactionEnabled
            && left.ContextWindowTokens == right.ContextWindowTokens
            && left.InputBudgetTokens == right.InputBudgetTokens
            && left.RequestTokenBudget == right.RequestTokenBudget
            && left.MaxToolCalls == right.MaxToolCalls
            && left.RegisteredToolCount == right.RegisteredToolCount
            && left.AvailableToolCount == right.AvailableToolCount
            && left.AvailableToolDefinitionCharacters == right.AvailableToolDefinitionCharacters
            && left.HarnessInstructionCharacters == right.HarnessInstructionCharacters
            && left.NarrowEvidenceResultLimit == right.NarrowEvidenceResultLimit
            && left.MaxAgentPasses == right.MaxAgentPasses
            && left.TotalDurationMs == right.TotalDurationMs;

        private static int EffectiveCachedInputTokens(CopilotAgentBudgetSnapshot budget) =>
            Math.Clamp(budget.ReportedCachedInputTokens ?? 0, 0, budget.ReportedInputTokens);

        private static bool BecameFalse(bool previous, bool current) => previous && !current;
    }
}
