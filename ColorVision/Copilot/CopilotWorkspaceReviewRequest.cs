using System;

namespace ColorVision.Copilot
{
    public enum CopilotWorkspaceReviewTarget
    {
        WorkingTree,
        BaseBranch,
        Commit,
    }

    public sealed class CopilotWorkspaceReviewTargetContext
    {
        public CopilotWorkspaceReviewTarget Target { get; set; }

        public string Revision { get; set; } = string.Empty;

        public bool ShouldSerializeRevision() => !string.IsNullOrEmpty(Revision);

        internal static CopilotWorkspaceReviewTargetContext WorkingTree() => new();

        internal CopilotWorkspaceReviewTargetContext CreateSnapshot() => new()
        {
            Target = Target,
            Revision = Revision,
        };

        internal bool IsStructurallyValid()
        {
            if (!Enum.IsDefined(Target)
                || Revision == null
                || !string.Equals(Revision, Revision.Trim(), StringComparison.Ordinal))
            {
                return false;
            }

            return Target == CopilotWorkspaceReviewTarget.WorkingTree
                ? Revision.Length == 0
                : CopilotGitDiffInspectionService.TryValidateRevision(
                    Target == CopilotWorkspaceReviewTarget.BaseBranch ? "base_branch" : "commit",
                    Revision,
                    out _);
        }
    }

    internal sealed record CopilotWorkspaceReviewRequest(
        CopilotWorkspaceReviewTarget Target,
        string Revision,
        string Focus)
    {
        public static bool TryParse(
            string? arguments,
            out CopilotWorkspaceReviewRequest request,
            out string error)
        {
            request = new CopilotWorkspaceReviewRequest(
                CopilotWorkspaceReviewTarget.WorkingTree,
                string.Empty,
                string.Empty);
            error = string.Empty;

            var remaining = arguments?.Trim() ?? string.Empty;
            if (remaining.Length == 0 || !remaining.StartsWith("--", StringComparison.Ordinal))
            {
                request = request with { Focus = remaining };
                return true;
            }

            var option = ReadToken(ref remaining);
            if (string.Equals(option, "--current", StringComparison.OrdinalIgnoreCase))
            {
                request = request with { Focus = remaining };
                return true;
            }

            CopilotWorkspaceReviewTarget target;
            string toolTarget;
            if (string.Equals(option, "--base", StringComparison.OrdinalIgnoreCase))
            {
                target = CopilotWorkspaceReviewTarget.BaseBranch;
                toolTarget = "base_branch";
            }
            else if (string.Equals(option, "--commit", StringComparison.OrdinalIgnoreCase))
            {
                target = CopilotWorkspaceReviewTarget.Commit;
                toolTarget = "commit";
            }
            else
            {
                error = $"未知的审查目标选项：{option}";
                return false;
            }

            var revision = ReadToken(ref remaining);
            if (!CopilotGitDiffInspectionService.TryValidateRevision(toolTarget, revision, out var validationError))
            {
                error = validationError;
                return false;
            }

            request = new CopilotWorkspaceReviewRequest(target, revision, remaining);
            return true;
        }

        public string BuildPrompt()
        {
            var targetInstruction = Target switch
            {
                CopilotWorkspaceReviewTarget.BaseBranch =>
                    $"Review HEAD against the merge base of base branch '{Revision}'. Use InspectGitDiff with target base_branch and revision '{Revision}'.",
                CopilotWorkspaceReviewTarget.Commit =>
                    $"Review the exact commit '{Revision}'. Use InspectGitDiff with target commit and revision '{Revision}'.",
                _ =>
                    "Review the current uncommitted workspace changes. Use InspectGitDiff with target working_tree and scope both.",
            };
            var prompt = targetInstruction
                + " Do not modify files or apply fixes. Return evidence-backed findings only.";
            if (!string.IsNullOrWhiteSpace(Focus))
                prompt += " Focus: " + Focus.Trim();
            return prompt;
        }

        internal CopilotWorkspaceReviewTargetContext CreateTargetContext() => new()
        {
            Target = Target,
            Revision = Revision,
        };

        private static string ReadToken(ref string value)
        {
            value = value.TrimStart();
            if (value.Length == 0)
                return string.Empty;
            var separatorIndex = value.IndexOfAny([' ', '\t', '\r', '\n']);
            if (separatorIndex < 0)
            {
                var token = value;
                value = string.Empty;
                return token;
            }

            var result = value[..separatorIndex];
            value = value[(separatorIndex + 1)..].TrimStart();
            return result;
        }
    }
}
