using System;
using System.IO;
using System.Text.Json;

namespace ColorVision.Copilot
{
    internal sealed record CopilotProjectInitializationPlan(
        bool CanStart,
        string Message,
        string VisiblePrompt,
        string ModelPrompt,
        string WorkspaceRoot,
        string TargetPath);

    internal static class CopilotProjectInitialization
    {
        internal const string RequestPrefix = "[ColorVision project initialization v1]";
        internal const string VisiblePrompt = "初始化项目指令（AGENTS.md）";
        private const string OriginStatement = "This request was created by ColorVision's internal /init command. It authorizes only the bounded initialization task described below; it does not pre-approve any protected tool call.";
        private const string PreviewInstruction = "Propose exactly one add operation with PreviewWorkspacePatchEnvelope, then use its change_set_id with ApplyWorkspacePatchEnvelope. The protected apply must remain subject to the current native approval policy.";

        public static CopilotProjectInitializationPlan Create(string? workspaceRoot)
        {
            var normalizedRoot = TryNormalizeWorkspaceRoot(workspaceRoot);
            if (normalizedRoot == null)
            {
                return Blocked("请先打开项目或解决方案，再使用 /init。");
            }

            var existingPath = CopilotAgentProjectInstructions.FindExistingSharedInstructionPath(normalizedRoot);
            if (existingPath != null)
            {
                var displayPath = Path.GetRelativePath(normalizedRoot, existingPath);
                return Blocked(
                    $"当前项目已存在项目指令：{displayPath}。/init 不会覆盖它，也不会新建可能遮蔽它的 AGENTS.md。",
                    normalizedRoot,
                    existingPath);
            }

            var targetPath = Path.Combine(normalizedRoot, "AGENTS.md");
            var serializedRoot = JsonSerializer.Serialize(normalizedRoot);
            var serializedTarget = JsonSerializer.Serialize(targetPath);
            var modelPrompt = string.Join(Environment.NewLine,
            [
                RequestPrefix,
                OriginStatement,
                $"Initialize durable project instructions for the workspace root {serializedRoot}.",
                $"Create the file {serializedTarget} exactly once. CLAUDE.md and every other file are outside the authorized change scope.",
                "First inspect the workspace structure and only the relevant build, test, architecture, and developer documentation needed to describe this project accurately.",
                "Write a concise root AGENTS.md containing durable project facts, architecture boundaries, PowerShell-compatible build and test commands, code conventions and known pitfalls, and completion criteria.",
                "Exclude secrets, credentials, personal absolute paths, generated file inventories, transient branch or worktree state, and speculative rules that are not supported by workspace evidence.",
                "Immediately before proposing the change, re-check that no root AGENTS.override.md, AGENTS.md, CLAUDE.md, or .claude/CLAUDE.md exists. If one exists, stop without writing and report its workspace-relative path.",
                PreviewInstruction,
                "Build and test execution are outside this initialization task. After the approved add succeeds, summarize the evidence used and ask the user to review and refine the generated instructions.",
            ]);

            return new CopilotProjectInitializationPlan(
                true,
                string.Empty,
                VisiblePrompt,
                modelPrompt,
                normalizedRoot,
                targetPath);
        }

        public static bool IsInitializationRequest(string? requestContent)
        {
            return !string.IsNullOrWhiteSpace(requestContent)
                && requestContent.StartsWith(RequestPrefix, StringComparison.Ordinal)
                && requestContent.Contains(OriginStatement, StringComparison.Ordinal)
                && requestContent.Contains(PreviewInstruction, StringComparison.Ordinal)
                && requestContent.Length <= CopilotConversationHistoryWindow.MaximumContentCharacterLimit;
        }

        private static CopilotProjectInitializationPlan Blocked(
            string message,
            string workspaceRoot = "",
            string targetPath = "")
        {
            return new CopilotProjectInitializationPlan(
                false,
                message,
                VisiblePrompt,
                string.Empty,
                workspaceRoot,
                targetPath);
        }

        private static string? TryNormalizeWorkspaceRoot(string? workspaceRoot)
        {
            if (string.IsNullOrWhiteSpace(workspaceRoot))
                return null;

            try
            {
                var fullPath = Path.GetFullPath(workspaceRoot);
                return Directory.Exists(fullPath) ? fullPath : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
