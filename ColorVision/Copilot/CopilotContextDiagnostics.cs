using System;
using System.Globalization;
using System.Text;

namespace ColorVision.Copilot
{
    public sealed class CopilotContextDiagnosticSnapshot
    {
        public string ProfileLabel { get; init; } = string.Empty;

        public CopilotAgentMode Mode { get; init; }

        public int SystemPromptCharacters { get; init; }

        public int SourceHistoryMessages { get; init; }

        public int RetainedHistoryMessages { get; init; }

        public int SourceHistoryCharacters { get; init; }

        public int RetainedHistoryCharacters { get; init; }

        public int AttachmentCount { get; init; }

        public int FileAttachmentCount { get; init; }

        public int ImageAttachmentCount { get; init; }

        public int WebAttachmentCount { get; init; }

        public bool HasLiveWindowContext { get; init; }

        public bool AgentContextEnabled { get; init; }

        public int ProjectInstructionDocuments { get; init; }

        public int ProjectInstructionPromptCharacters { get; init; }

        public long RecordedSkillRuns { get; init; }

        public int TrackedSkills { get; init; }

        public int HistoricalExplicitOnlySkills { get; init; }

        public int RegisteredCapabilities { get; init; }

        public int EnabledExternalMcpServers { get; init; }
    }

    public static class CopilotContextDiagnostics
    {
        public const string Command = "/context";

        public static bool IsCommand(string? text)
        {
            return string.Equals((text ?? string.Empty).Trim(), Command, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsCommandPrefix(string? text)
        {
            var normalized = (text ?? string.Empty).Trim();
            return normalized.Length > 0
                && normalized.Length < Command.Length
                && Command.StartsWith(normalized, StringComparison.OrdinalIgnoreCase);
        }

        public static string Format(CopilotContextDiagnosticSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            var builder = new StringBuilder();
            builder.AppendLine("Copilot 上下文快照");
            builder.AppendLine("本地诊断：未调用模型、工具或 MCP，也不会加入模型历史。");
            builder.AppendLine();
            builder.Append("模型：").AppendLine(string.IsNullOrWhiteSpace(snapshot.ProfileLabel) ? "未选择" : snapshot.ProfileLabel.Trim());
            builder.Append("模式：").AppendLine(snapshot.Mode.ToString());
            builder.Append("系统提示：").Append(FormatCount(snapshot.SystemPromptCharacters)).AppendLine(" 字符");
            builder.Append("对话历史：");
            if (snapshot.SourceHistoryMessages <= 0)
            {
                builder.AppendLine("无");
            }
            else
            {
                builder.Append(FormatCount(snapshot.RetainedHistoryMessages))
                    .Append('/')
                    .Append(FormatCount(snapshot.SourceHistoryMessages))
                    .Append(" 条，")
                    .Append(FormatCount(snapshot.RetainedHistoryCharacters))
                    .Append('/')
                    .Append(FormatCount(snapshot.SourceHistoryCharacters))
                    .AppendLine(" 字符保留");
            }
            builder.Append("历史预算：最多 ")
                .Append(CopilotConversationHistoryWindow.DefaultMaximumMessages)
                .Append(" 条 / ")
                .Append(FormatCount(CopilotConversationHistoryWindow.DefaultMaximumCharacters))
                .Append(" 字符 / 单条 ")
                .Append(FormatCount(CopilotConversationHistoryWindow.DefaultMaximumContentCharacters))
                .AppendLine(" 字符");
            builder.Append("附件：").AppendLine(FormatAttachments(snapshot));
            builder.Append("窗口上下文：").AppendLine(snapshot.HasLiveWindowContext ? "已提供" : "无");
            builder.AppendLine();

            if (!snapshot.AgentContextEnabled)
            {
                builder.AppendLine("Agent 扩展：当前 Chat 模式不注入项目指令、Skills 或 MCP 工具。");
                return builder.ToString().TrimEnd();
            }

            builder.Append("项目指令：")
                .Append(FormatCount(snapshot.ProjectInstructionDocuments))
                .Append(" 个文档，序列化提示 ")
                .Append(FormatCount(snapshot.ProjectInstructionPromptCharacters))
                .AppendLine(" 字符");
            builder.Append("Agent Skills：")
                .Append(FormatCount(snapshot.TrackedSkills))
                .Append(" 个已跟踪，")
                .Append(FormatCount(snapshot.HistoricalExplicitOnlySkills))
                .Append(" 个低使用率仅显式调用，统计运行 ")
                .Append(FormatCount(snapshot.RecordedSkillRuns))
                .AppendLine(" 次");
            builder.Append("Skill 预算：下一请求最多 ")
                .Append(CopilotAgentSkills.MaxActiveSkills)
                .Append(" 个相关 Skill / ")
                .Append(FormatCount(CopilotAgentSkills.MaxAdvertisedSkillCharacters))
                .AppendLine(" 元数据字符");
            builder.Append("能力目录：")
                .Append(FormatCount(snapshot.RegisteredCapabilities))
                .AppendLine(" 个已注册能力；实际工具仍按请求过滤");
            builder.Append("外部 MCP：")
                .Append(FormatCount(snapshot.EnabledExternalMcpServers))
                .AppendLine(" 个启用服务；仅在 Agent 请求中发现工具");
            return builder.ToString().TrimEnd();
        }

        private static string FormatAttachments(CopilotContextDiagnosticSnapshot snapshot)
        {
            var count = Math.Max(0, snapshot.AttachmentCount);
            if (count == 0)
                return "无";

            return $"{FormatCount(count)} 个（文件 {FormatCount(snapshot.FileAttachmentCount)}，图片 {FormatCount(snapshot.ImageAttachmentCount)}，网页 {FormatCount(snapshot.WebAttachmentCount)}，其他 {FormatCount(count - snapshot.FileAttachmentCount - snapshot.ImageAttachmentCount - snapshot.WebAttachmentCount)}）";
        }

        private static string FormatCount(long value)
        {
            return Math.Max(0, value).ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}
