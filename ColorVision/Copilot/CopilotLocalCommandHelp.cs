using System;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal static class CopilotLocalCommandHelp
    {
        private const int MaximumQueryLength = 128;

        private static readonly (string Title, CopilotLocalCommandKind[] Kinds)[] Sections =
        [
            ("帮助", [CopilotLocalCommandKind.Help, CopilotLocalCommandKind.Shortcuts]),
            ("状态与诊断",
            [
                CopilotLocalCommandKind.Status,
                CopilotLocalCommandKind.Doctor,
                CopilotLocalCommandKind.Tasks,
                CopilotLocalCommandKind.TaskLog,
                CopilotLocalCommandKind.Queue,
                CopilotLocalCommandKind.Approve,
                CopilotLocalCommandKind.Usage,
                CopilotLocalCommandKind.Statistics,
                CopilotLocalCommandKind.Context,
                CopilotLocalCommandKind.Permissions,
                CopilotLocalCommandKind.Hooks,
                CopilotLocalCommandKind.Skills,
                CopilotLocalCommandKind.Mcp,
            ]),
            ("工作区与 Agent",
            [
                CopilotLocalCommandKind.InitializeProject,
                CopilotLocalCommandKind.Mention,
                CopilotLocalCommandKind.Diff,
                CopilotLocalCommandKind.Compact,
                CopilotLocalCommandKind.Review,
                CopilotLocalCommandKind.Verify,
                CopilotLocalCommandKind.Plan,
                CopilotLocalCommandKind.ViewPlan,
                CopilotLocalCommandKind.Goal,
            ]),
            ("会话与输出",
            [
                CopilotLocalCommandKind.Recap,
                CopilotLocalCommandKind.ResumeConversation,
                CopilotLocalCommandKind.ArchiveConversation,
                CopilotLocalCommandKind.UnarchiveConversation,
                CopilotLocalCommandKind.RenameConversation,
                CopilotLocalCommandKind.RewindConversation,
                CopilotLocalCommandKind.NavigateTurn,
                CopilotLocalCommandKind.SearchPromptHistory,
                CopilotLocalCommandKind.PromptSuggestions,
                CopilotLocalCommandKind.Transcript,
                CopilotLocalCommandKind.Timestamps,
                CopilotLocalCommandKind.CompactMode,
                CopilotLocalCommandKind.MultilineComposer,
                CopilotLocalCommandKind.CopyResponse,
                CopilotLocalCommandKind.ExportConversation,
                CopilotLocalCommandKind.FindInConversation,
                CopilotLocalCommandKind.NewConversation,
                CopilotLocalCommandKind.ClearConversation,
                CopilotLocalCommandKind.ForkConversation,
                CopilotLocalCommandKind.SideQuestion,
                CopilotLocalCommandKind.Feedback,
            ]),
            ("模型与推理",
            [
                CopilotLocalCommandKind.SelectModel,
                CopilotLocalCommandKind.SelectReasoning,
                CopilotLocalCommandKind.SelectPersonality,
            ]),
        ];

        public static string Format(string? query)
        {
            var normalized = NormalizeQuery(query);
            if (normalized.Length == 0)
                return FormatOverview();

            var command = CopilotLocalCommandCatalog.All.FirstOrDefault(item =>
                string.Equals(item.Name, normalized, StringComparison.OrdinalIgnoreCase));
            return command == null
                ? $"未找到命令“{normalized}”。输入 /help 查看全部命令，或输入 / 按名称过滤。"
                : FormatCommand(command);
        }

        private static string FormatOverview()
        {
            var commands = CopilotLocalCommandCatalog.All;
            var builder = new StringBuilder();
            builder.AppendLine($"Copilot 命令 · {commands.Count}");
            builder.AppendLine();
            builder.AppendLine("输入 / 可按名称过滤；输入 /help <命令> 查看详情。");
            builder.AppendLine("◎ Agent 运行中可立即执行；· 当前任务结束后执行。");

            foreach (var (title, kinds) in Sections)
            {
                builder.AppendLine();
                builder.AppendLine(title);
                foreach (var command in commands.Where(item => kinds.Contains(item.Kind)))
                {
                    var availability = command.AvailableWhileAgentRuns ? '◎' : '·';
                    builder.Append(availability)
                        .Append(' ')
                        .Append(command.Usage)
                        .Append(" — ")
                        .AppendLine(command.Description);
                }
            }

            builder.AppendLine();
            builder.Append("动态 Skill 不计入固定命令：使用 /skills 查看，按 $name 或 /name 调用。");
            return builder.ToString();
        }

        private static string FormatCommand(CopilotLocalCommand command)
        {
            var builder = new StringBuilder();
            builder.AppendLine(command.Usage);
            builder.AppendLine();
            builder.AppendLine(command.Description);
            builder.Append("参数：")
                .AppendLine(command.AcceptsArguments ? "可选" : "无");
            builder.Append("Agent 运行中：")
                .Append(command.AvailableWhileAgentRuns ? "可立即执行" : "当前任务结束后执行");
            return builder.ToString();
        }

        private static string NormalizeQuery(string? query)
        {
            var normalized = string.Join(
                " ",
                (query ?? string.Empty).Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            if (normalized.Length == 0)
                return string.Empty;

            if (normalized[0] != '/')
                normalized = "/" + normalized;
            return normalized.Length > MaximumQueryLength
                ? normalized[..MaximumQueryLength]
                : normalized;
        }
    }
}
