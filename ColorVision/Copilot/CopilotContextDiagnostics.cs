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

        public string ResponsePersonalitySourceLabel { get; init; } = string.Empty;

        internal CopilotCodexSandboxMode CodexSandboxMode { get; init; } =
            CopilotCodexSandboxMode.Unspecified;

        internal CopilotCodexApprovalPolicy CodexApprovalPolicy { get; init; } =
            CopilotCodexApprovalPolicy.Unspecified;

        internal CopilotCodexApprovalsReviewer CodexApprovalsReviewer { get; init; } =
            CopilotCodexApprovalsReviewer.Unspecified;

        public bool CodexGuardianApprovalEnabled { get; init; } = true;

        public bool CodexPreventIdleSleep { get; init; }

        public bool CodexShellToolEnabled { get; init; } = true;

        public bool CodexHooksEnabled { get; init; } = true;

        public bool CodexPluginsEnabled { get; init; } = true;

        public string CodexShellEnvironmentPolicySummary { get; init; } = string.Empty;

        public bool CodexIncludePermissionsInstructions { get; init; } = true;

        public bool CodexIncludeCollaborationModeInstructions { get; init; } = true;

        public bool CodexIncludeEnvironmentContext { get; init; } = true;

        public bool CodexIncludeSkillInstructions { get; init; } = true;

        public bool CodexMultiAgentEnabled { get; init; } = true;

        public bool CodexAgentsEnabled { get; init; } = true;

        public int CodexMaximumConcurrentSubagentRuns { get; init; } =
            CopilotSubagentCoordinator.DefaultMaximumConcurrentRuns;

        public int ActiveSleepPreventionLeaseCount { get; init; }

        public int? SleepPreventionLastErrorCode { get; init; }

        public string SleepPreventionLastFailure { get; init; } = string.Empty;

        public int SystemPromptCharacters { get; init; }

        public int SourceHistoryMessages { get; init; }

        public int RetainedHistoryMessages { get; init; }

        public int SourceHistoryCharacters { get; init; }

        public int RetainedHistoryCharacters { get; init; }

        public int RetainedHistoryEstimatedTokens { get; init; }

        public int CurrentModelSurfaceMessages { get; init; }

        public int ShadowedModelSurfaceMessages { get; init; }

        public int LogOnlySurfaceMessages { get; init; }

        public bool HasCurrentCompactionSummary { get; init; }

        public int HistoryMaximumMessages { get; init; }

        public int HistoryMaximumEstimatedTokens { get; init; }

        public int HistoryMaximumContentEstimatedTokens { get; init; }

        public int HistoryContextWindowTokens { get; init; }

        public bool AutoCompactConversationHistory { get; init; }

        public int AutoCompactThresholdPercent { get; init; }

        public int AutoCompactTotalEstimatedTokens { get; init; }

        public int AutoCompactCarriedPrefixEstimatedTokens { get; init; }

        public int AutoCompactBodyAfterPrefixEstimatedTokens { get; init; }

        public int AutoCompactInstructionsCharacters { get; init; }

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

        public IReadOnlyList<string> ProjectInstructionFallbackFileNames { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> ProjectInstructionRootMarkers { get; init; } =
            CopilotProjectInstructionDiscoveryConfig.DefaultProjectRootMarkers;

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
            builder.AppendLine("这里预览当前设置与待提交上下文；运行中的任务仍使用提交时快照。");
            builder.AppendLine();
            builder.Append("模型：").AppendLine(string.IsNullOrWhiteSpace(snapshot.ProfileLabel) ? "未选择" : snapshot.ProfileLabel.Trim());
            builder.Append("模式：").AppendLine(snapshot.Mode.ToString());
            builder.Append("回答风格：")
                .Append(CopilotResponsePersonalitySelection.GetDisplayName(snapshot.ResponsePersonality))
                .Append('（')
                .Append(CopilotResponsePersonalitySelection.GetCommandToken(snapshot.ResponsePersonality))
                .Append("） · 来源 ")
                .AppendLine(string.IsNullOrWhiteSpace(snapshot.ResponsePersonalitySourceLabel)
                    ? "ColorVision 默认"
                    : snapshot.ResponsePersonalitySourceLabel.Trim());
            if (snapshot.AgentContextEnabled)
            {
                builder.AppendLine("能力与权限：ColorVision 当前快照；实际工具仍按模式、请求与授权过滤。");
                builder.Append("执行沙箱：")
                    .Append(CopilotCodexSandboxModeSelection.GetConfigToken(snapshot.CodexSandboxMode))
                    .Append(" · ")
                    .AppendLine(CopilotCodexSandboxModeSelection.GetEffectiveLabel(snapshot.CodexSandboxMode));
                builder.Append("审批策略：")
                    .Append(CopilotCodexApprovalPolicySelection.GetConfigToken(snapshot.CodexApprovalPolicy))
                    .Append(" · ")
                    .AppendLine(CopilotCodexApprovalPolicySelection.GetEffectiveLabel(snapshot.CodexApprovalPolicy));
                var effectiveApprovalsReviewer = snapshot.CodexGuardianApprovalEnabled
                    ? snapshot.CodexApprovalsReviewer
                    : CopilotCodexApprovalsReviewer.User;
                builder.Append("自动审批复核：")
                    .Append(snapshot.CodexGuardianApprovalEnabled ? "开启" : "关闭")
                    .Append(" · 有效复核者 ")
                    .Append(CopilotCodexApprovalsReviewerSelection.GetConfigToken(effectiveApprovalsReviewer))
                    .Append(" · ")
                    .AppendLine(CopilotCodexApprovalsReviewerSelection.GetEffectiveLabel(effectiveApprovalsReviewer));
                builder.Append("命令工具：").AppendLine(snapshot.CodexShellToolEnabled ? "开启" : "关闭");
                builder.Append("模块扩展 Hook：")
                    .AppendLine(snapshot.CodexHooksEnabled && snapshot.CodexPluginsEnabled ? "开启" : "关闭");
                builder.Append("Copilot 扩展能力：")
                    .AppendLine(snapshot.CodexPluginsEnabled
                        ? "开启；模块提供的上下文、工具与 Hook 按各自条件参与请求"
                        : "关闭；内置工具、外部 MCP 与主程序业务插件不受影响");
                builder.Append("命令环境：")
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexShellEnvironmentPolicySummary)
                        ? CopilotCodexShellEnvironmentPolicy.Default.BuildRedactedSummary()
                        : snapshot.CodexShellEnvironmentPolicySummary.Trim())
                    .AppendLine("（仅报告筛选规则和数量，不显示变量值）");
                builder.Append("模型权限说明：").AppendLine(snapshot.CodexIncludePermissionsInstructions ? "注入" : "省略；执行边界仍强制生效");
                builder.Append("协作模式说明：").AppendLine(snapshot.CodexIncludeCollaborationModeInstructions ? "注入" : "省略");
                builder.Append("运行环境上下文：").AppendLine(snapshot.CodexIncludeEnvironmentContext ? "注入" : "省略");
                builder.Append("自动 Skill 说明：").AppendLine(snapshot.CodexIncludeSkillInstructions ? "注入" : "仅显式调用");
                builder.Append("子代理工具：")
                    .Append(snapshot.CodexMultiAgentEnabled && snapshot.CodexAgentsEnabled ? "开启" : "关闭")
                    .Append(" · 并发槽位 ")
                    .Append(FormatCount(snapshot.CodexMaximumConcurrentSubagentRuns))
                    .AppendLine("；累计请求预算独立限制");
            }
            if (snapshot.CodexPreventIdleSleep || snapshot.ActiveSleepPreventionLeaseCount > 0
                || !string.IsNullOrWhiteSpace(snapshot.SleepPreventionLastFailure))
            {
                builder.Append("活动轮次防休眠：")
                    .Append(snapshot.CodexPreventIdleSleep ? "开启" : "关闭")
                    .Append(" · 系统请求 ")
                    .Append(FormatCount(snapshot.ActiveSleepPreventionLeaseCount));
                if (!string.IsNullOrWhiteSpace(snapshot.SleepPreventionLastFailure))
                {
                    builder.Append(" · 最近失败：")
                        .Append(FormatInlineDiagnosticText(snapshot.SleepPreventionLastFailure, string.Empty, 160));
                    if (snapshot.SleepPreventionLastErrorCode.HasValue)
                        builder.Append(" · Win32 ").Append(snapshot.SleepPreventionLastErrorCode.Value);
                }
                builder.AppendLine();
            }
            builder.Append("有效系统提示：")
                .Append(FormatCount(snapshot.SystemPromptCharacters))
                .AppendLine(" 字符（已应用宿主响应规则）");
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
            builder.Append("模型消息表面：当前 ")
                .Append(FormatCount(snapshot.CurrentModelSurfaceMessages));
            if (snapshot.HasCurrentCompactionSummary)
                builder.Append(" + 1 条压缩摘要");
            builder.Append("；已被摘要替代 ")
                .Append(FormatCount(snapshot.ShadowedModelSurfaceMessages))
                .Append("；仅本地日志 ")
                .Append(FormatCount(snapshot.LogOnlySurfaceMessages))
                .AppendLine("。");
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
            if (snapshot.AgentContextEnabled)
            {
                builder.Append("工具结果历史预算：")
                    .Append(FormatCount(CopilotFrameworkToolResultFormatter.MaxSerializedCharacters))
                    .AppendLine(" 序列化字符（ColorVision 默认）");
                builder.AppendLine("范围：只压缩写入模型历史的函数结果；完整工具结果、审批记录、证据路径与审计日志保持原样。");
            }
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
            if (snapshot.AutoCompactCarriedPrefixEstimatedTokens > 0)
            {
                builder.Append("压缩窗口计量：total ")
                    .Append(FormatCount(snapshot.AutoCompactTotalEstimatedTokens))
                    .Append(" Token；carried prefix ")
                    .Append(FormatCount(snapshot.AutoCompactCarriedPrefixEstimatedTokens))
                    .Append(" Token；body_after_prefix ")
                    .Append(FormatCount(snapshot.AutoCompactBodyAfterPrefixEstimatedTokens))
                    .AppendLine(" Token");
            }
            builder.Append("压缩重点：")
                .AppendLine(snapshot.AutoCompactInstructionsCharacters > 0
                    ? $"已配置 {FormatCount(snapshot.AutoCompactInstructionsCharacters)} 字符长期要求"
                    : "使用内置默认要求");
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

            builder.Append("个人/项目指令预览：")
                .Append(FormatCount(snapshot.ProjectInstructionDocuments))
                .Append(" 个文档，序列化提示 ")
                .Append(FormatCount(snapshot.ProjectInstructionPromptCharacters))
                .Append(" 字符；发现预算 ")
                .Append(FormatCount(snapshot.ProjectInstructionMaximumBytes))
                .AppendLine(" UTF-8 字节（ColorVision 当前快照）");
            builder.AppendLine("此处列出当前目标发现的指令；实际注入取决于后续请求的本地证据需求或工作区补丁能力。");
            if (snapshot.ProjectInstructionFallbackFileNames.Count > 0)
                builder.Append("备用文件名：").AppendLine(string.Join("、", snapshot.ProjectInstructionFallbackFileNames));
            builder.Append("项目根标记：")
                .AppendLine(snapshot.ProjectInstructionRootMarkers.Count == 0
                    ? "[]（不向上搜索）"
                    : string.Join("、", snapshot.ProjectInstructionRootMarkers));
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
            AppendAgentExtensionDetails(
                builder,
                snapshot.AgentExtensions,
                snapshot.AgentExtensionIssues,
                snapshot.CodexPluginsEnabled);
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
            IReadOnlyList<CopilotAgentExtensionIssue> issues,
            bool pluginsEnabled)
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
                .Append(" 个已激活/声明")
                .AppendLine(pluginsEnabled
                    ? string.Empty
                    : "；这些来源仍由主程序加载，但本请求已排除其 Copilot context、tool 与 Hook");

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
                var failureCode = CopilotToolFailureCode.Normalize(issue.FailureCode);
                if (failureCode.Length == 0)
                    failureCode = CopilotAgentExtensionFailureCodes.ActivationFailed;
                var message = FormatInlineDiagnosticText(issue.Message, "No details provided.", 240);
                builder.Append("  ! ")
                    .Append(source)
                    .Append(" · code ")
                    .Append(failureCode);
                if (!string.IsNullOrWhiteSpace(issue.CapabilityName))
                {
                    builder.Append(" · capability ")
                        .Append(FormatInlineDiagnosticText(issue.CapabilityName, "unknown", 120));
                }
                builder.Append(": ").AppendLine(message);
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
