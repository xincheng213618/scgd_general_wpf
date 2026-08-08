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

        public CopilotResponsePersonality ResponsePersonality { get; init; }

        public int SystemPromptCharacters { get; init; }

        public int ConfiguredModelInstructionsCharacters { get; init; }

        public string ConfiguredModelInstructionsSourceLabel { get; init; } = string.Empty;

        public bool HasConfiguredModelInstructionsOverride { get; init; }

        public bool ConfiguredModelInstructionsApplied { get; init; }

        public int SourceHistoryMessages { get; init; }

        public int RetainedHistoryMessages { get; init; }

        public int SourceHistoryCharacters { get; init; }

        public int RetainedHistoryCharacters { get; init; }

        public int RetainedHistoryEstimatedTokens { get; init; }

        public int HistoryMaximumMessages { get; init; }

        public int HistoryMaximumCharacters { get; init; }

        public int HistoryMaximumContentCharacters { get; init; }

        public int HistoryMaximumEstimatedTokens { get; init; }

        public int HistoryMaximumContentEstimatedTokens { get; init; }

        public int HistoryContextWindowTokens { get; init; }

        public bool AutoCompactConversationHistory { get; init; }

        public int AutoCompactThresholdPercent { get; init; }

        public int AutoCompactInstructionsCharacters { get; init; }

        public int ConfiguredCompactPromptCharacters { get; init; }

        public string ConfiguredCompactPromptSourceLabel { get; init; } = string.Empty;

        public bool HasConfiguredCompactPromptOverride { get; init; }

        public int CompactedSourceMessages { get; init; }

        public int CompactionSummaryCharacters { get; init; }

        public int CompactionRequests { get; init; }

        public CopilotTokenUsage CompactionUsage { get; init; } = CopilotTokenUsage.Empty;

        public int ConversationGoalCharacters { get; init; }

        public CopilotConversationGoalState? ConversationGoalState { get; init; }

        public long ConversationGoalTimeUsedSeconds { get; init; }

        public bool ConversationGoalContinuationDeferred { get; init; }

        public bool ConversationGoalActive { get; init; }

        public bool ConversationGoalAchieved { get; init; }

        public int AttachmentCount { get; init; }

        public int FileAttachmentCount { get; init; }

        public int ImageAttachmentCount { get; init; }

        public int WebAttachmentCount { get; init; }

        public bool HasLiveWindowContext { get; init; }

        public bool AgentContextEnabled { get; init; }

        public int ProjectInstructionDocuments { get; init; }

        public int ProjectInstructionPromptCharacters { get; init; }

        public int ProjectInstructionMaximumBytes { get; init; } = CopilotProjectInstructionDiscoveryConfig.DefaultMaximumBytes;

        public bool ProjectInstructionUsesCodexConfig { get; init; }

        public string ProjectInstructionConfigSourceLabel { get; init; } = string.Empty;

        public string ProjectInstructionProjectTrustLabel { get; init; } = string.Empty;

        public int ProjectInstructionDeveloperInstructionsCharacters { get; init; }

        public string ProjectInstructionDeveloperInstructionsSourceLabel { get; init; } = string.Empty;

        public bool ProjectInstructionHasDeveloperInstructionsOverride { get; init; }

        public IReadOnlyList<string> ProjectInstructionFallbackFileNames { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> ProjectInstructionRootMarkers { get; init; } =
            CopilotProjectInstructionDiscoveryConfig.DefaultProjectRootMarkers;

        public bool ProjectInstructionHasRootMarkersOverride { get; init; }

        public IReadOnlyList<string> ProjectInstructionAppliedProjectConfigFilePaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> TrustedProjectRootPaths { get; init; } = Array.Empty<string>();

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

        public CopilotToolExecutionHookRegistrySnapshot? ToolHookSurface { get; init; }

        public IReadOnlyList<CopilotAgentExtensionSourceSnapshot> AgentExtensions { get; init; } = Array.Empty<CopilotAgentExtensionSourceSnapshot>();

        public IReadOnlyList<CopilotAgentExtensionIssue> AgentExtensionIssues { get; init; } = Array.Empty<CopilotAgentExtensionIssue>();
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
            builder.Append("回答风格：")
                .Append(CopilotResponsePersonalitySelection.GetDisplayName(snapshot.ResponsePersonality))
                .Append('（')
                .Append(CopilotResponsePersonalitySelection.GetCommandToken(snapshot.ResponsePersonality))
                .AppendLine("）");
            builder.Append("有效系统提示：")
                .Append(FormatCount(snapshot.SystemPromptCharacters))
                .AppendLine(" 字符（已应用宿主响应规则）");
            if (snapshot.HasConfiguredModelInstructionsOverride)
            {
                builder.Append("Codex model_instructions_file：")
                    .Append(FormatCount(snapshot.ConfiguredModelInstructionsCharacters))
                    .Append(" 字符（")
                    .Append(string.IsNullOrWhiteSpace(snapshot.ConfiguredModelInstructionsSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.ConfiguredModelInstructionsSourceLabel.Trim())
                    .AppendLine(snapshot.ConfiguredModelInstructionsApplied
                        ? " 请求快照；已替换内置主体，宿主安全规则仍强制保留）"
                        : snapshot.ConfiguredModelInstructionsCharacters == 0
                            ? " 请求快照；文件为空或未安全加载，使用 Profile/内置主体）"
                            : " 请求快照；Profile 显式覆盖优先）");
            }
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
                .Append(FormatCount(snapshot.HistoryMaximumEstimatedTokens))
                .Append(" Token / 单条 ")
                .Append(FormatCount(snapshot.HistoryMaximumContentEstimatedTokens))
                .Append(" Token（混合文本估算，上下文 ")
                .Append(CopilotConversationHistoryWindow.HistoryContextPercent)
                .Append("%，窗口 ")
                .Append(FormatCount(snapshot.HistoryContextWindowTokens))
                .AppendLine(" Token）");
            builder.Append("自动压缩：");
            if (snapshot.AutoCompactConversationHistory)
            {
                builder.Append("已开启 · 活动历史达到 ")
                    .Append(snapshot.AutoCompactThresholdPercent.ToString(CultureInfo.InvariantCulture))
                    .AppendLine("% 时在发送前压缩；失败时保留原请求");
            }
            else
            {
                builder.AppendLine("已关闭");
            }
            builder.Append("压缩重点：")
                .AppendLine(snapshot.AutoCompactInstructionsCharacters > 0
                    ? $"已配置 {FormatCount(snapshot.AutoCompactInstructionsCharacters)} 字符长期要求"
                    : "使用内置默认要求");
            if (snapshot.HasConfiguredCompactPromptOverride)
            {
                builder.Append("Codex compact_prompt：")
                    .Append(FormatCount(snapshot.ConfiguredCompactPromptCharacters))
                    .Append(" 字符（")
                    .Append(string.IsNullOrWhiteSpace(snapshot.ConfiguredCompactPromptSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.ConfiguredCompactPromptSourceLabel.Trim())
                    .AppendLine(snapshot.ConfiguredCompactPromptCharacters == 0
                        ? " 请求快照；未产生非空覆盖，使用内置主体）"
                        : " 请求快照；终态完整性后缀仍由宿主强制保留）");
            }
            if (snapshot.CompactionSummaryCharacters > 0)
            {
                builder.Append("主动压缩：")
                    .Append(FormatCount(snapshot.CompactedSourceMessages))
                    .Append(" 条来源已归纳为 ")
                    .Append(FormatCount(snapshot.CompactionSummaryCharacters))
                    .AppendLine(" 字符摘要；完整记录仍保留在本地");
            }
            if (snapshot.CompactionRequests > 0)
            {
                builder.Append("压缩模型调用：")
                    .Append(FormatCount(snapshot.CompactionRequests))
                    .Append(" 次");
                if (snapshot.CompactionUsage.HasAny)
                {
                    builder.Append(" · 已返回用量累计输入 ")
                        .Append(FormatCount(snapshot.CompactionUsage.InputTokens))
                        .Append(" / 输出 ")
                        .Append(FormatCount(snapshot.CompactionUsage.OutputTokens))
                        .Append(" / 总计 ")
                        .Append(FormatCount(snapshot.CompactionUsage.EffectiveTotalTokens))
                        .AppendLine(" Token");
                }
                else
                {
                    builder.AppendLine("；Provider 未返回 Token 元数据");
                }
            }
            builder.Append("持续目标：");
            if (snapshot.ConversationGoalCharacters <= 0)
            {
                builder.AppendLine("无");
            }
            else
            {
                var goalState = snapshot.ConversationGoalState
                    ?? (snapshot.ConversationGoalActive
                        ? CopilotConversationGoalState.Active
                        : snapshot.ConversationGoalAchieved
                            ? CopilotConversationGoalState.Achieved
                            : CopilotConversationGoalState.Paused);
                builder.Append(snapshot.ConversationGoalContinuationDeferred
                        ? "待显式 Agent 任务接管"
                        : CopilotConversationGoalStateText.Format(goalState))
                    .Append(" · ")
                    .Append(FormatCount(snapshot.ConversationGoalCharacters))
                    .Append(" 字符 · 累计执行 ")
                    .Append(CopilotConversationGoalUsageText.FormatElapsed(snapshot.ConversationGoalTimeUsedSeconds))
                    .AppendLine("；仅约束完成判定，不授予操作权限");
            }
            builder.Append("附件：").AppendLine(FormatAttachments(snapshot));
            builder.Append("窗口上下文：").AppendLine(snapshot.HasLiveWindowContext ? "已提供" : "无");
            builder.AppendLine();

            if (!snapshot.AgentContextEnabled)
            {
                builder.AppendLine("Agent 扩展：当前 Chat 模式不注入个人/项目指令、Skills 或 MCP 工具。");
                AppendOptimizationSuggestions(builder, snapshot);
                return builder.ToString().TrimEnd();
            }

            builder.Append("个人/项目指令：")
                .Append(FormatCount(snapshot.ProjectInstructionDocuments))
                .Append(" 个文档，序列化提示 ")
                .Append(FormatCount(snapshot.ProjectInstructionPromptCharacters))
                .Append(" 字符；发现预算 ")
                .Append(FormatCount(snapshot.ProjectInstructionMaximumBytes))
                .Append(" UTF-8 字节")
                .AppendLine(snapshot.ProjectInstructionUsesCodexConfig
                    ? $"（{(string.IsNullOrWhiteSpace(snapshot.ProjectInstructionConfigSourceLabel) ? "Codex config.toml" : snapshot.ProjectInstructionConfigSourceLabel)} 请求快照）"
                    : "（默认）");
            if (snapshot.ProjectInstructionFallbackFileNames.Count > 0)
            {
                builder.Append("配置备用名：")
                    .AppendLine(string.Join("、", snapshot.ProjectInstructionFallbackFileNames));
            }
            if (!string.IsNullOrWhiteSpace(snapshot.ProjectInstructionProjectTrustLabel))
            {
                builder.Append("项目配置信任：")
                    .AppendLine(snapshot.ProjectInstructionProjectTrustLabel);
            }
            AppendDeveloperInstructionsSnapshot(
                builder,
                snapshot.ProjectInstructionDeveloperInstructionsCharacters,
                snapshot.ProjectInstructionDeveloperInstructionsSourceLabel,
                snapshot.ProjectInstructionHasDeveloperInstructionsOverride);
            builder.Append("项目根标记：");
            if (snapshot.ProjectInstructionRootMarkers.Count == 0)
            {
                builder.AppendLine(snapshot.ProjectInstructionHasRootMarkersOverride
                    ? "[]（Codex Home 请求快照；不向上搜索）"
                    : "[]（默认；不向上搜索）");
            }
            else
            {
                builder.Append(string.Join("、", snapshot.ProjectInstructionRootMarkers))
                    .AppendLine(snapshot.ProjectInstructionHasRootMarkersOverride
                        ? "（Codex Home 请求快照）"
                        : "（默认）");
            }
            AppendAppliedProjectConfigLayers(
                builder,
                snapshot.ProjectInstructionAppliedProjectConfigFilePaths);
            AppendTrustedProjectRoots(builder, snapshot.TrustedProjectRootPaths);
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
            AppendToolHookDetails(builder, snapshot.ToolHookSurface);
            AppendAgentExtensionDetails(builder, snapshot.AgentExtensions, snapshot.AgentExtensionIssues);
            AppendOptimizationSuggestions(builder, snapshot);
            return builder.ToString().TrimEnd();
        }

        private static void AppendDeveloperInstructionsSnapshot(
            StringBuilder builder,
            int characters,
            string? sourceLabel,
            bool hasOverride)
        {
            if (!hasOverride)
                return;

            var normalizedCharacters = Math.Max(0, characters);
            var source = string.IsNullOrWhiteSpace(sourceLabel)
                ? "Codex config.toml"
                : sourceLabel.Trim();
            builder.Append("Codex developer_instructions：")
                .Append(FormatCount(normalizedCharacters))
                .Append(" 字符（")
                .Append(source)
                .AppendLine(normalizedCharacters == 0
                    ? " 请求快照；显式清空）"
                    : " 请求快照；独立开发者指令）");
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

        private static void AppendAppliedProjectConfigLayers(
            StringBuilder builder,
            IReadOnlyList<string>? configFilePaths)
        {
            var paths = (configFilePaths ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(FormatProjectRootLabel)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (paths.Length == 0)
                return;

            builder.Append("项目配置层：")
                .Append(FormatCount(paths.Length))
                .AppendLine(" 个（项目根 → 工作目录，后者优先）");
            foreach (var path in paths.Take(8))
                builder.Append("  - ").AppendLine(path);
            if (paths.Length > 8)
                builder.Append("  - ...另有 ").Append(FormatCount(paths.Length - 8)).AppendLine(" 个配置层未展开");
        }

        private static void AppendTrustedProjectRoots(StringBuilder builder, IReadOnlyList<string>? roots)
        {
            var normalizedRoots = (roots ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(FormatProjectRootLabel)
                .Where(label => label.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToArray();
            builder.Append("受信项目根：");
            if (normalizedRoots.Length == 0)
            {
                builder.AppendLine("无");
                return;
            }

            builder.AppendLine();
            foreach (var root in normalizedRoots)
                builder.Append("  - ").AppendLine(root);
        }

        private static string FormatProjectRootLabel(string path)
        {
            try
            {
                var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
                return string.IsNullOrWhiteSpace(normalized) ? path.Trim() : normalized;
            }
            catch
            {
                return path.Trim();
            }
        }

        private static void AppendAgentExtensionDetails(
            StringBuilder builder,
            IReadOnlyList<CopilotAgentExtensionSourceSnapshot> extensions,
            IReadOnlyList<CopilotAgentExtensionIssue> issues)
        {
            extensions ??= Array.Empty<CopilotAgentExtensionSourceSnapshot>();
            issues ??= Array.Empty<CopilotAgentExtensionIssue>();
            builder.Append("业务模块扩展：")
                .Append(FormatCount(extensions.Count))
                .Append(" 个来源 / 上下文提供者 ")
                .Append(FormatCount(extensions.Sum(extension => extension.ContextProviderCount)))
                .Append(" / 工具 ")
                .Append(FormatCount(extensions.Sum(extension => extension.ActiveToolCount)))
                .Append('/')
                .Append(FormatCount(extensions.Sum(extension => extension.DeclaredToolCount)))
                .Append(" 个已激活/声明；Hook ")
                .Append(FormatCount(extensions.Sum(extension => extension.ActiveHookCount)))
                .Append('/')
                .Append(FormatCount(extensions.Sum(extension => extension.DeclaredHookCount)))
                .AppendLine(" 个已激活/声明");

            foreach (var extension in extensions.Take(12))
            {
                builder.Append("  - ")
                    .Append(FormatInlineDiagnosticText(extension.SourceName, "Unnamed extension", 120));
                if (!string.IsNullOrWhiteSpace(extension.SourceVersion))
                    builder.Append(" · v").Append(FormatInlineDiagnosticText(extension.SourceVersion, string.Empty, 64));
                builder.Append(" · context ")
                    .Append(FormatCount(extension.ContextProviderCount))
                    .Append(" · tools ")
                    .Append(FormatCount(extension.ActiveToolCount))
                    .Append('/')
                    .Append(FormatCount(extension.DeclaredToolCount))
                    .Append(" · hooks ")
                    .Append(FormatCount(extension.ActiveHookCount))
                    .Append('/')
                    .Append(FormatCount(extension.DeclaredHookCount))
                    .AppendLine();
            }
            if (extensions.Count > 12)
                builder.Append("  - ...另有 ").Append(FormatCount(extensions.Count - 12)).AppendLine(" 个来源未展开");

            foreach (var issue in issues.Take(8))
            {
                var source = FormatInlineDiagnosticText(issue.SourceId, "unknown", 120);
                var message = FormatInlineDiagnosticText(issue.Message, "No details provided.", 240);
                builder.Append("  ! ").Append(source).Append(": ").AppendLine(message);
            }
            if (issues.Count > 8)
                builder.Append("  ! ...另有 ").Append(FormatCount(issues.Count - 8)).AppendLine(" 个问题未展开");
        }

        private static void AppendToolHookDetails(
            StringBuilder builder,
            CopilotToolExecutionHookRegistrySnapshot? hookSurface)
        {
            if (hookSurface?.IsStructurallyValid() != true)
            {
                builder.AppendLine("工具 Hook：无有效运行时快照");
                return;
            }

            builder.Append("工具 Hook：")
                .Append(FormatCount(hookSurface.Entries.Count))
                .Append(" 个已生效 · revision ")
                .Append(FormatCount(hookSurface.Revision));
            if (!string.IsNullOrWhiteSpace(hookSurface.Fingerprint))
                builder.Append(" · fingerprint ").Append(hookSurface.Fingerprint[..Math.Min(12, hookSurface.Fingerprint.Length)]);
            builder.AppendLine();

            foreach (var hook in hookSurface.Entries.Take(16))
            {
                builder.Append("  - ")
                    .Append(FormatInlineDiagnosticText(hook.SourceId, "unknown", 160))
                    .Append(" · matcher ")
                    .Append(FormatInlineDiagnosticText(hook.ToolNamePattern, "*", 120))
                    .Append(" · order ")
                    .Append(hook.Order)
                    .AppendLine();
            }
            if (hookSurface.Entries.Count > 16)
                builder.Append("  - ...另有 ").Append(FormatCount(hookSurface.Entries.Count - 16)).AppendLine(" 个 Hook 未展开");
        }

        private static string FormatInlineDiagnosticText(string? value, string fallback, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            var sanitized = new StringBuilder(Math.Min(value.Length, maxLength));
            var pendingSpace = false;
            foreach (var character in value.Trim())
            {
                if (char.IsWhiteSpace(character) || char.IsControl(character))
                {
                    pendingSpace = sanitized.Length > 0;
                    continue;
                }

                if (pendingSpace)
                    sanitized.Append(' ');
                sanitized.Append(character);
                pendingSpace = false;
            }

            var result = sanitized.ToString();
            if (result.Length <= maxLength)
                return result;
            return maxLength <= 3 ? result[..maxLength] : result[..(maxLength - 3)] + "...";
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
            else if (snapshot.HistoryMaximumEstimatedTokens > 0
                && (long)snapshot.RetainedHistoryEstimatedTokens * 100 / snapshot.HistoryMaximumEstimatedTokens >= HighHistoryPressurePercent)
            {
                suggestions.Add("对话历史已使用至少 75% 的历史预算；继续长任务前可运行 /compact，避免临近上限时丢失早期细节。");
            }

            var truncatedInstructions = snapshot.ProjectInstructions.Count(document => document?.IsTruncated == true);
            if (truncatedInstructions > 0)
            {
                suggestions.Add($"{FormatCount(truncatedInstructions)} 个个人/项目指令文档已截断；请精简通用规则，或把局部规则放到更靠近目标代码的 AGENTS.md/CLAUDE.md。");
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
                return "project instructions";

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
