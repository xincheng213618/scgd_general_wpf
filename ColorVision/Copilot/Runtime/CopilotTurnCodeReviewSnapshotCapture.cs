using System;

namespace ColorVision.Copilot
{
    internal static class CopilotTurnCodeReviewSnapshotCapture
    {
        public static bool TryCapture(
            CopilotWorkspaceReviewTargetContext target,
            CopilotAgentEvent agentEvent,
            out CopilotCodeReviewSnapshot snapshot)
            => TryCaptureUpdate(target, null, agentEvent, out snapshot);

        public static bool TryCaptureUpdate(
            CopilotWorkspaceReviewTargetContext target,
            CopilotCodeReviewSnapshot? currentSnapshot,
            CopilotAgentEvent agentEvent,
            out CopilotCodeReviewSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(agentEvent);
            snapshot = null!;
            var toolResult = agentEvent.ToolResult;
            if (agentEvent.Type != CopilotAgentEventType.ToolResult
                || toolResult?.Success != true)
            {
                return false;
            }

            if (string.Equals(toolResult.ToolName, "InspectGitDiff", StringComparison.OrdinalIgnoreCase))
            {
                return CopilotGitDiffResultProtocol.TryParse(toolResult.Content, out var parsed, out _)
                    && MatchesTarget(target, parsed)
                    && CopilotCodeReviewSnapshot.TryCreate(
                        parsed,
                        agentEvent.ModelToolResult,
                        out snapshot);
            }

            return string.Equals(
                    toolResult.ToolName,
                    "SubmitCodeReviewFindings",
                    StringComparison.OrdinalIgnoreCase)
                && currentSnapshot?.IsStructurallyValid() == true
                && MatchesTarget(target, currentSnapshot)
                && currentSnapshot.TryApplyFindings(toolResult.Content, out snapshot);
        }

        public static bool MatchesTarget(
            CopilotWorkspaceReviewTargetContext target,
            CopilotGitDiffSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(snapshot);
            if (!target.IsStructurallyValid() || !snapshot.IsStructurallyValid())
                return false;

            return target.Target switch
            {
                CopilotWorkspaceReviewTarget.BaseBranch =>
                    string.Equals(snapshot.Target, "base_branch", StringComparison.Ordinal)
                    && string.Equals(snapshot.Revision, target.Revision, StringComparison.Ordinal),
                CopilotWorkspaceReviewTarget.Commit =>
                    string.Equals(snapshot.Target, "commit", StringComparison.Ordinal)
                    && string.Equals(snapshot.Revision, target.Revision, StringComparison.OrdinalIgnoreCase),
                _ =>
                    string.Equals(snapshot.Target, "working_tree", StringComparison.Ordinal)
                    && string.Equals(snapshot.Scope, "both", StringComparison.Ordinal),
            };
        }

        public static bool MatchesTarget(
            CopilotWorkspaceReviewTargetContext target,
            CopilotCodeReviewSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(snapshot);
            if (!target.IsStructurallyValid() || !snapshot.IsStructurallyValid())
                return false;

            return target.Target switch
            {
                CopilotWorkspaceReviewTarget.BaseBranch =>
                    string.Equals(snapshot.Target, "base_branch", StringComparison.Ordinal)
                    && string.Equals(snapshot.Revision, target.Revision, StringComparison.Ordinal),
                CopilotWorkspaceReviewTarget.Commit =>
                    string.Equals(snapshot.Target, "commit", StringComparison.Ordinal)
                    && string.Equals(snapshot.Revision, target.Revision, StringComparison.OrdinalIgnoreCase),
                _ =>
                    string.Equals(snapshot.Target, "working_tree", StringComparison.Ordinal)
                    && string.Equals(snapshot.Scope, "both", StringComparison.Ordinal),
            };
        }
    }
}
