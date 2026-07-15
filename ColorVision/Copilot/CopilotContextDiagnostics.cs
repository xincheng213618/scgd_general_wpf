using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

        public int HistoryMaximumMessages { get; init; }

        public int HistoryMaximumCharacters { get; init; }

        public int HistoryMaximumContentCharacters { get; init; }

        public int HistoryContextWindowTokens { get; init; }

        public int CompactedSourceMessages { get; init; }

        public int CompactionSummaryCharacters { get; init; }

        public int AttachmentCount { get; init; }

        public int FileAttachmentCount { get; init; }

        public int ImageAttachmentCount { get; init; }

        public int WebAttachmentCount { get; init; }

        public bool HasLiveWindowContext { get; init; }

        public bool AgentContextEnabled { get; init; }

        public int ProjectInstructionDocuments { get; init; }

        public int ProjectInstructionPromptCharacters { get; init; }

        public IReadOnlyList<CopilotProjectInstructionDocument> ProjectInstructions { get; init; } = Array.Empty<CopilotProjectInstructionDocument>();

        public long RecordedSkillRuns { get; init; }

        public int TrackedSkills { get; init; }

        public int HistoricalExplicitOnlySkills { get; init; }

        public int ManualSkillOverrides { get; init; }

        public int SkillMetadataCharacterBudget { get; init; }

        public int AgentContextWindowTokens { get; init; }

        public int AgentRequestTokenBudget { get; init; }

        public int AgentMaxToolCalls { get; init; }

        public int AgentMaxPasses { get; init; }

        public int AgentTimeoutSeconds { get; init; }

        public int RegisteredCapabilities { get; init; }

        public int EnabledExternalMcpServers { get; init; }
    }

    public static class CopilotContextDiagnostics
    {
        private const int HighHistoryPressurePercent = 75;
        private const int ExternalMcpSuggestionThreshold = 4;

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
                .Append(FormatCount(snapshot.HistoryMaximumMessages))
                .Append(" 条 / ")
                .Append(FormatCount(snapshot.HistoryMaximumCharacters))
                .Append(" 字符 / 单条 ")
                .Append(FormatCount(snapshot.HistoryMaximumContentCharacters))
                .Append(" 字符（上下文 ")
                .Append(CopilotConversationHistoryWindow.HistoryContextPercent)
                .Append("%，窗口 ")
                .Append(FormatCount(snapshot.HistoryContextWindowTokens))
                .AppendLine(" Token）");
            if (snapshot.CompactionSummaryCharacters > 0)
            {
                builder.Append("主动压缩：")
                    .Append(FormatCount(snapshot.CompactedSourceMessages))
                    .Append(" 条来源已归纳为 ")
                    .Append(FormatCount(snapshot.CompactionSummaryCharacters))
                    .AppendLine(" 字符摘要；完整记录仍保留在本地");
            }
            builder.Append("附件：").AppendLine(FormatAttachments(snapshot));
            builder.Append("窗口上下文：").AppendLine(snapshot.HasLiveWindowContext ? "已提供" : "无");
            builder.AppendLine();

            if (!snapshot.AgentContextEnabled)
            {
                builder.AppendLine("Agent 扩展：当前 Chat 模式不注入项目指令、Skills 或 MCP 工具。");
                AppendOptimizationSuggestions(builder, snapshot);
                return builder.ToString().TrimEnd();
            }

            builder.Append("项目指令：")
                .Append(FormatCount(snapshot.ProjectInstructionDocuments))
                .Append(" 个文档，序列化提示 ")
                .Append(FormatCount(snapshot.ProjectInstructionPromptCharacters))
                .AppendLine(" 字符");
            AppendProjectInstructionDetails(builder, snapshot.ProjectInstructions);
            builder.Append("Agent 预算：上下文 ")
                .Append(FormatCount(snapshot.AgentContextWindowTokens))
                .Append(" Token / 累计请求 ")
                .Append(FormatCount(snapshot.AgentRequestTokenBudget))
                .Append(" Token / 工具 ")
                .Append(FormatCount(snapshot.AgentMaxToolCalls))
                .Append(" / pass ")
                .Append(FormatCount(snapshot.AgentMaxPasses))
                .Append(" / 超时 ")
                .Append(FormatCount(snapshot.AgentTimeoutSeconds))
                .AppendLine(" 秒");
            builder.Append("Agent Skills：")
                .Append(FormatCount(snapshot.TrackedSkills))
                .Append(" 个已跟踪，")
                .Append(FormatCount(snapshot.HistoricalExplicitOnlySkills))
                .Append(" 个低使用率仅显式调用，")
                .Append(FormatCount(snapshot.ManualSkillOverrides))
                .Append(" 个手动覆盖，统计运行 ")
                .Append(FormatCount(snapshot.RecordedSkillRuns))
                .AppendLine(" 次");
            builder.Append("Skill 预算：下一请求最多 ")
                .Append(CopilotAgentSkills.MaxActiveSkills)
                .Append(" 个相关 Skill / 当前 ")
                .Append(FormatCount(snapshot.SkillMetadataCharacterBudget))
                .Append(" 元数据字符（上下文 ")
                .Append(CopilotAgentSkills.SkillMetadataContextPercent)
                .Append("% / 硬上限 ")
                .Append(FormatCount(CopilotAgentSkills.MaxAdvertisedSkillCharacters))
                .AppendLine("）");
            builder.Append("能力目录：")
                .Append(FormatCount(snapshot.RegisteredCapabilities))
                .AppendLine(" 个已注册能力；实际工具仍按请求过滤");
            builder.Append("外部 MCP：")
                .Append(FormatCount(snapshot.EnabledExternalMcpServers))
                .AppendLine(" 个启用服务；仅在 Agent 请求中发现工具");
            AppendOptimizationSuggestions(builder, snapshot);
            return builder.ToString().TrimEnd();
        }

        private static void AppendProjectInstructionDetails(
            StringBuilder builder,
            IReadOnlyList<CopilotProjectInstructionDocument> documents)
        {
            foreach (var document in (documents ?? Array.Empty<CopilotProjectInstructionDocument>())
                .Where(document => document?.IsStructurallyValid() == true)
                .Take(CopilotAgentProjectInstructions.MaxDocuments))
            {
                builder.Append("  - ")
                    .Append(FormatInstructionPath(document.Path))
                    .Append(" · ")
                    .Append(FormatCount(document.Content.Length))
                    .Append(" 字符");
                if (document.IsTruncated)
                    builder.Append(" · 已截断");
                builder.AppendLine();
            }
        }

        private static void AppendOptimizationSuggestions(StringBuilder builder, CopilotContextDiagnosticSnapshot snapshot)
        {
            var suggestions = new List<string>();
            var historyWasReduced = snapshot.SourceHistoryMessages > snapshot.RetainedHistoryMessages
                || snapshot.SourceHistoryCharacters > snapshot.RetainedHistoryCharacters;
            if (historyWasReduced)
            {
                suggestions.Add("对话历史已被窗口预算裁剪；长任务建议运行 /compact，并可在命令后写明需要保留的重点。");
            }
            else if (snapshot.HistoryMaximumCharacters > 0
                && (long)snapshot.RetainedHistoryCharacters * 100 / snapshot.HistoryMaximumCharacters >= HighHistoryPressurePercent)
            {
                suggestions.Add("对话历史已使用至少 75% 的历史预算；继续长任务前可运行 /compact，避免临近上限时丢失早期细节。");
            }

            var truncatedInstructions = snapshot.ProjectInstructions.Count(document => document?.IsTruncated == true);
            if (truncatedInstructions > 0)
            {
                suggestions.Add($"{FormatCount(truncatedInstructions)} 个项目指令文档已截断；请精简通用规则，或把局部规则放到更靠近目标代码的 AGENTS.md。");
            }

            if (snapshot.AgentContextEnabled
                && snapshot.EnabledExternalMcpServers >= ExternalMcpSuggestionThreshold)
            {
                suggestions.Add($"已启用 {FormatCount(snapshot.EnabledExternalMcpServers)} 个外部 MCP 服务；可在设置中停用当前项目不需要的服务，减少工具发现和上下文噪声。");
            }

            if (suggestions.Count == 0)
                return;

            builder.AppendLine();
            builder.AppendLine("优化建议：");
            foreach (var suggestion in suggestions)
                builder.Append("- ").AppendLine(suggestion);
        }

        private static string FormatInstructionPath(string path)
        {
            var normalized = (path ?? string.Empty).Trim();
            if (normalized.Length == 0)
                return "AGENTS.md";

            var parent = Path.GetFileName(Path.GetDirectoryName(normalized));
            var fileName = Path.GetFileName(normalized);
            return string.IsNullOrWhiteSpace(parent) ? fileName : Path.Combine(parent, fileName);
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
