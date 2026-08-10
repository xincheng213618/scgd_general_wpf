using System;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotToolExecutor
    {
        private static void AddChangedPathProjectInstructions(
            CopilotToolExecutionOutcome outcome)
        {
            var request = outcome.Invocation.AgentRequest;
            if (request.Mode != CopilotAgentMode.Review
                || request.ReviewProjectInstructionContext == null
                || outcome.Invocation.Tool is not CopilotInspectGitDiffTool
                || outcome.Execution.State != CopilotToolExecutionState.Completed
                || !outcome.Result.Success
                || !string.Equals(
                    outcome.Result.ToolName,
                    "InspectGitDiff",
                    StringComparison.Ordinal)
                || !CopilotGitDiffResultProtocol.TryParse(
                    outcome.Result.Content,
                    out var gitDiff,
                    out _))
            {
                return;
            }

            var promptBlock = request.ReviewProjectInstructionContext
                .BuildAdditionalPromptBlock(
                    request.TrustedProjectRootPaths,
                    gitDiff);
            if (promptBlock.Length == 0)
                return;

            outcome.AddModelAdditionalContext(
                promptBlock,
                CopilotAgentProjectInstructions.MaxPromptCharacters);
        }

        private static void RecordReviewEvidence(CopilotToolExecutionOutcome outcome)
        {
            var request = outcome.Invocation.AgentRequest;
            if (request.Mode != CopilotAgentMode.Review
                || request.ReviewEvidenceContext == null
                || outcome.Invocation.Tool is not CopilotInspectGitDiffTool
                || outcome.Execution.State != CopilotToolExecutionState.Completed
                || !outcome.Result.Success
                || !string.Equals(outcome.Result.ToolName, "InspectGitDiff", StringComparison.Ordinal)
                || !CopilotGitDiffResultProtocol.TryParse(outcome.Result.Content, out var gitDiff, out _)
                || !CopilotCodeReviewSnapshot.TryCreate(
                    gitDiff,
                    outcome.FormattedModelResult,
                    out var snapshot))
            {
                return;
            }

            request.ReviewEvidenceContext.RecordEvidence(snapshot);
        }
    }
}
