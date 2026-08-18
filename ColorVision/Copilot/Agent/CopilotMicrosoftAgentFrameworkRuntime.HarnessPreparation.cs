#pragma warning disable MAAI001
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotMicrosoftAgentFrameworkRuntime
    {
        private sealed class HarnessPolicyPreparation : IDisposable
        {
            public CopilotAgentExecutionContract ExecutionContract { get; init; } = null!;

            public IList<AITool> FrameworkTools { get; init; } = null!;

            public CopilotAgentPreparedPrompt PreparedPrompt { get; init; } = null!;

            public CopilotAgentTokenBudget TokenBudget { get; init; } = null!;

            public ContextWindowCompactionStrategy CompactionStrategy { get; init; } = null!;

            public bool TaskLedgerAvailable { get; init; }

            public bool TaskLedgerEnabled { get; init; }

            public bool AgentModeEnabled { get; init; }

            public bool MinimalDelegatedFinalization { get; init; }

            public CopilotAgentSkills AgentSkills { get; init; } = null!;

            public bool AgentSkillsEnabled { get; init; }

            public void Dispose() => AgentSkills.Dispose();
        }

        private HarnessPolicyPreparation PrepareHarnessPolicy(
            CopilotAgentRequest request,
            CopilotAgentRunBudget runBudget,
            IReadOnlyList<ICopilotTool> availableTools,
            HarnessToolBridge bridge,
            Action<CopilotAgentEvent> emit)
        {
            var executionContract = CopilotAgentExecutionContract.Create(request, availableTools);
            if (executionContract.IsRequired)
            {
                emit(CopilotAgentEvent.RuntimeDiagnostic(
                    $"Agent execution contract enabled · {executionContract.Description} · accepted tools: {string.Join(", ", executionContract.AcceptedToolNames)}."));
            }
            var frameworkTools = bridge.CreateFunctions();
            if (IsRequestUserInputToolEnabled(request))
            {
                frameworkTools.Add(new HarnessToolBridge.UserQuestionAIFunction(
                    _userQuestionCoordinator,
                    request,
                    emit,
                    bridge.TryPublishInteractionCheckpointAsync));
            }
            var tokenBudget = CopilotAgentTokenBudget.Create(request.Profile, runBudget);
            var compactionStrategy = new ContextWindowCompactionStrategy(
                tokenBudget.ContextWindowTokens,
                request.Profile.MaxTokens);
            var autonomousTaskPasses = runBudget.MaxAgentPasses;
            var taskLedgerAvailable = IsTaskLedgerAvailable(request);
            var taskLedgerEnabled = IsUpdatePlanToolEnabled(request);
            var agentModeEnabled = taskLedgerEnabled && request.HarnessFeatures.HasFlag(CopilotAgentHarnessFeatures.AgentMode);
            var minimalDelegatedFinalization = CanUseMinimalDelegatedFinalizationInstructions(
                request,
                availableTools,
                taskLedgerEnabled,
                agentModeEnabled);
            var preparedPrompt = _contextBuilder.BuildHarnessMessages(
                request,
                Array.Empty<CopilotAgentStepRecord>(),
                minimalDelegatedFinalization);
            var skillsFeatureEnabled = request.HarnessFeatures.HasFlag(CopilotAgentHarnessFeatures.Skills);
            var historicalExplicitOnlySkillNames = skillsFeatureEnabled
                ? _skillUsageStore.GetSnapshot().HistoricalExplicitOnlySkills.Select(entry => entry.Name).ToArray()
                : Array.Empty<string>();
            var agentSkills = skillsFeatureEnabled
                ? CopilotAgentSkills.Create(
                    request,
                    historicalExplicitOnlySkillNames,
                    tokenBudget.ContextWindowTokens,
                    includeAutomaticInstructions: request.CodexIncludeSkillInstructions)
                : CopilotAgentSkills.Disabled();
            try
            {
                var agentSkillsEnabled = skillsFeatureEnabled && agentSkills.IsEnabled;
                emit(CopilotAgentEvent.RuntimeDiagnostic(
                    $"Agent budgets · input {tokenBudget.InputBudgetTokens:N0} tokens · request {tokenBudget.RequestTokenBudget:N0} tokens · tools {runBudget.MaxToolCalls} · passes {runBudget.MaxAgentPasses} · total time {FormatDuration(runBudget.TotalDuration)}."));
                if (runBudget.NarrowEvidenceResultLimit > 0)
                {
                    emit(CopilotAgentEvent.RuntimeDiagnostic(
                        $"Adaptive evidence budget · the request asks for {runBudget.NarrowEvidenceResultLimit} bounded result(s); stop after collecting that many high-confidence findings with enough evidence."));
                }
                emit(CopilotAgentEvent.RuntimeDiagnostic(!skillsFeatureEnabled
                    ? "Agent Skills disabled by the isolated runtime tool surface."
                    : agentSkillsEnabled
                        ? agentSkills.BuildStartupDiagnostic()
                        : "Agent Skills enabled · no trusted project or built-in skills were discovered."));
                var projectInstructionCount = request.ProjectInstructions.Count(document => document?.IsStructurallyValid() == true);
                if (projectInstructionCount > 0)
                    emit(CopilotAgentEvent.RuntimeDiagnostic($"Project instructions enabled · {projectInstructionCount} scoped workspace instruction document(s)."));
                if (!string.IsNullOrWhiteSpace(request.ActiveGoalText))
                    emit(CopilotAgentEvent.RuntimeDiagnostic($"Active conversation goal bound · {request.ActiveGoalText.Length:N0} character(s) · completion constraint only, never authorization."));
                emit(CopilotAgentEvent.RuntimeDiagnostic(preparedPrompt.ContextProvenance.FormatDiagnostic()));

                return new HarnessPolicyPreparation
                {
                    ExecutionContract = executionContract,
                    FrameworkTools = frameworkTools,
                    PreparedPrompt = preparedPrompt,
                    TokenBudget = tokenBudget,
                    CompactionStrategy = compactionStrategy,
                    TaskLedgerAvailable = taskLedgerAvailable,
                    TaskLedgerEnabled = taskLedgerEnabled,
                    AgentModeEnabled = agentModeEnabled,
                    MinimalDelegatedFinalization = minimalDelegatedFinalization,
                    AgentSkills = agentSkills,
                    AgentSkillsEnabled = agentSkillsEnabled,
                };
            }
            catch
            {
                agentSkills.Dispose();
                throw;
            }
        }

        internal static bool IsRequestUserInputToolEnabled(CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return request.CodexExperimentalRequestUserInputEnabled
                && (request.Mode == CopilotAgentMode.Plan
                    || request.CodexDefaultModeRequestUserInputEnabled)
                && request.RuntimePurpose == CopilotAgentRuntimePurpose.Standard;
        }

        internal static bool IsTaskLedgerAvailable(CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return request.CodexUpdatePlanEnabled
                && request.HarnessFeatures.HasFlag(CopilotAgentHarnessFeatures.TaskLedger);
        }

        internal static bool IsUpdatePlanToolEnabled(CopilotAgentRequest request) =>
            IsTaskLedgerAvailable(request) && CopilotToolIntentPolicy.NeedsTaskLedger(request);

        internal static IReadOnlyList<string> BuildCheckpointToolNames(
            CopilotAgentRequest request,
            IReadOnlyList<string> availableToolNames)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(availableToolNames);
            var names = availableToolNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .ToList();
            if (IsRequestUserInputToolEnabled(request))
                names.Add("AskUserQuestion");
            if (IsUpdatePlanToolEnabled(request))
                names.Add("update_plan");
            return names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }
}
