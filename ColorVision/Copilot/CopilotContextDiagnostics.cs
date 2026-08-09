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

        public bool CodexPersonalityEnabled { get; init; } = true;

        public bool HasCodexPersonalityEnabledOverride { get; init; }

        public string CodexPersonalityEnabledSourceLabel { get; init; } = string.Empty;

        internal CopilotCodexWebSearchMode CodexWebSearchMode { get; init; } =
            CopilotCodexWebSearchMode.Unspecified;

        public string CodexWebSearchModeSourceLabel { get; init; } = string.Empty;

        public bool HasCodexWebSearchModeOverride { get; init; }

        internal CopilotCodexSandboxMode CodexSandboxMode { get; init; } =
            CopilotCodexSandboxMode.Unspecified;

        public string CodexSandboxModeSourceLabel { get; init; } = string.Empty;

        public bool HasCodexSandboxModeOverride { get; init; }

        internal CopilotCodexApprovalPolicy CodexApprovalPolicy { get; init; } =
            CopilotCodexApprovalPolicy.Unspecified;

        public string CodexApprovalPolicySourceLabel { get; init; } = string.Empty;

        public bool HasCodexApprovalPolicyOverride { get; init; }

        internal CopilotCodexApprovalsReviewer CodexApprovalsReviewer { get; init; } =
            CopilotCodexApprovalsReviewer.Unspecified;

        public string CodexApprovalsReviewerSourceLabel { get; init; } = string.Empty;

        public bool HasCodexApprovalsReviewerOverride { get; init; }

        public bool CodexGuardianApprovalEnabled { get; init; } = true;

        public string CodexGuardianApprovalEnabledSourceLabel { get; init; } = string.Empty;

        public bool HasCodexGuardianApprovalEnabledOverride { get; init; }

        public int CodexAutoReviewPolicyCharacters { get; init; }

        public string CodexAutoReviewPolicySourceLabel { get; init; } = string.Empty;

        public bool HasCodexAutoReviewPolicyOverride { get; init; }

        public string CodexModel { get; init; } = string.Empty;

        public bool HasCodexModelOverride { get; init; }

        public string CodexModelSourceLabel { get; init; } = string.Empty;

        public string CodexReviewModel { get; init; } = string.Empty;

        public bool HasCodexReviewModelOverride { get; init; }

        public string CodexReviewModelSourceLabel { get; init; } = string.Empty;

        public bool CodexPreventIdleSleep { get; init; }

        public bool HasCodexPreventIdleSleepOverride { get; init; }

        public string CodexPreventIdleSleepSourceLabel { get; init; } = string.Empty;

        public bool CodexShellToolEnabled { get; init; } = true;

        public bool HasCodexShellToolEnabledOverride { get; init; }

        public string CodexShellToolEnabledSourceLabel { get; init; } = string.Empty;

        public bool CodexHooksEnabled { get; init; } = true;

        public bool HasCodexHooksEnabledOverride { get; init; }

        public string CodexHooksEnabledSourceLabel { get; init; } = string.Empty;

        public bool CodexPluginsEnabled { get; init; } = true;

        public bool HasCodexPluginsEnabledOverride { get; init; }

        public string CodexPluginsEnabledSourceLabel { get; init; } = string.Empty;

        public string CodexShellEnvironmentPolicySummary { get; init; } = string.Empty;

        public bool HasCodexShellEnvironmentPolicyOverride { get; init; }

        public string CodexShellEnvironmentPolicySourceLabel { get; init; } = string.Empty;

        public string CodexShellEnvironmentPolicyError { get; init; } = string.Empty;

        public bool CodexGoalsEnabled { get; init; } = true;

        public bool HasCodexGoalsEnabledOverride { get; init; }

        public string CodexGoalsEnabledSourceLabel { get; init; } = string.Empty;

        public bool CodexDefaultModeRequestUserInputEnabled { get; init; }

        public bool HasCodexDefaultModeRequestUserInputEnabledOverride { get; init; }

        public string CodexDefaultModeRequestUserInputEnabledSourceLabel { get; init; } = string.Empty;

        public bool CodexExperimentalRequestUserInputEnabled { get; init; } = true;

        public bool HasCodexExperimentalRequestUserInputEnabledOverride { get; init; }

        public string CodexExperimentalRequestUserInputEnabledSourceLabel { get; init; } = string.Empty;

        public bool CodexUpdatePlanEnabled { get; init; } = true;

        public bool HasCodexUpdatePlanEnabledOverride { get; init; }

        public string CodexUpdatePlanEnabledSourceLabel { get; init; } = string.Empty;

        public bool CodexIncludePermissionsInstructions { get; init; } = true;

        public bool HasCodexIncludePermissionsInstructionsOverride { get; init; }

        public string CodexIncludePermissionsInstructionsSourceLabel { get; init; } = string.Empty;

        public bool CodexIncludeCollaborationModeInstructions { get; init; } = true;

        public bool HasCodexIncludeCollaborationModeInstructionsOverride { get; init; }

        public string CodexIncludeCollaborationModeInstructionsSourceLabel { get; init; } = string.Empty;

        public bool CodexIncludeEnvironmentContext { get; init; } = true;

        public bool HasCodexIncludeEnvironmentContextOverride { get; init; }

        public string CodexIncludeEnvironmentContextSourceLabel { get; init; } = string.Empty;

        public bool CodexIncludeSkillInstructions { get; init; } = true;

        public bool HasCodexIncludeSkillInstructionsOverride { get; init; }

        public string CodexIncludeSkillInstructionsSourceLabel { get; init; } = string.Empty;

        public bool CodexMultiAgentEnabled { get; init; } = true;

        public bool HasCodexMultiAgentEnabledOverride { get; init; }

        public string CodexMultiAgentEnabledSourceLabel { get; init; } = string.Empty;

        public bool CodexAgentsEnabled { get; init; } = true;

        public bool HasCodexAgentsEnabledOverride { get; init; }

        public string CodexAgentsEnabledSourceLabel { get; init; } = string.Empty;

        public bool CodexInterruptMessageEnabled { get; init; } = true;

        public bool HasCodexInterruptMessageOverride { get; init; }

        public string CodexInterruptMessageSourceLabel { get; init; } = string.Empty;

        public int CodexMaximumConcurrentSubagentRuns { get; init; } =
            CopilotSubagentCoordinator.DefaultMaximumConcurrentRuns;

        public bool HasCodexMaximumConcurrentSubagentRunsOverride { get; init; }

        public string CodexMaximumConcurrentSubagentRunsSourceLabel { get; init; } = string.Empty;

        public string CodexDefaultSubagentModel { get; init; } = string.Empty;

        public bool HasCodexDefaultSubagentModelOverride { get; init; }

        public string CodexDefaultSubagentModelSourceLabel { get; init; } = string.Empty;

        internal CopilotCodexReasoningEffort CodexDefaultSubagentReasoningEffort { get; init; } =
            CopilotCodexReasoningEffort.Unspecified;

        public bool HasCodexDefaultSubagentReasoningEffortOverride { get; init; }

        public string CodexDefaultSubagentReasoningEffortSourceLabel { get; init; } = string.Empty;

        public int ActiveSleepPreventionLeaseCount { get; init; }

        public int? SleepPreventionLastErrorCode { get; init; }

        public string SleepPreventionLastFailure { get; init; } = string.Empty;

        public int SystemPromptCharacters { get; init; }

        public int ConfiguredModelInstructionsCharacters { get; init; }

        public string ConfiguredModelInstructionsSourceLabel { get; init; } = string.Empty;

        public bool HasConfiguredModelInstructionsOverride { get; init; }

        public bool ConfiguredModelInstructionsUsesFile { get; init; }

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

        public bool HasModelContextWindowOverride { get; init; }

        public string ModelContextWindowSourceLabel { get; init; } = string.Empty;

        public int ToolOutputTokenLimit { get; init; }

        public bool HasToolOutputTokenLimitOverride { get; init; }

        public string ToolOutputTokenLimitSourceLabel { get; init; } = string.Empty;

        internal CopilotCodexReasoningEffort CodexReasoningEffort { get; init; } =
            CopilotCodexReasoningEffort.Unspecified;

        public bool HasCodexReasoningEffortOverride { get; init; }

        public string CodexReasoningEffortSourceLabel { get; init; } = string.Empty;

        internal CopilotCodexReasoningSummary CodexReasoningSummary { get; init; } =
            CopilotCodexReasoningSummary.Unspecified;

        public bool HasCodexReasoningSummaryOverride { get; init; }

        public string CodexReasoningSummarySourceLabel { get; init; } = string.Empty;

        public bool CodexModelSupportsReasoningSummaries { get; init; }

        public bool HasCodexModelSupportsReasoningSummariesOverride { get; init; }

        public string CodexModelSupportsReasoningSummariesSourceLabel { get; init; } = string.Empty;

        public bool CodexHideAgentReasoning { get; init; }

        public bool HasCodexHideAgentReasoningOverride { get; init; }

        public string CodexHideAgentReasoningSourceLabel { get; init; } = string.Empty;

        public bool CodexFastModeEnabled { get; init; } = true;

        public bool HasCodexFastModeEnabledOverride { get; init; }

        public string CodexFastModeEnabledSourceLabel { get; init; } = string.Empty;

        public string CodexServiceTier { get; init; } = string.Empty;

        public bool HasCodexServiceTierOverride { get; init; }

        public string CodexServiceTierSourceLabel { get; init; } = string.Empty;

        internal CopilotCodexModelVerbosity CodexModelVerbosity { get; init; } =
            CopilotCodexModelVerbosity.Unspecified;

        public bool HasCodexModelVerbosityOverride { get; init; }

        public string CodexModelVerbositySourceLabel { get; init; } = string.Empty;

        public bool AutoCompactConversationHistory { get; init; }

        public int AutoCompactThresholdPercent { get; init; }

        public int ConfiguredModelAutoCompactTokenLimit { get; init; }

        public bool HasModelAutoCompactTokenLimitOverride { get; init; }

        public string ModelAutoCompactTokenLimitSourceLabel { get; init; } = string.Empty;

        internal CopilotModelAutoCompactTokenLimitScope ModelAutoCompactTokenLimitScope { get; init; } =
            CopilotModelAutoCompactTokenLimitScope.Total;

        public bool HasModelAutoCompactTokenLimitScopeOverride { get; init; }

        public string ModelAutoCompactTokenLimitScopeSourceLabel { get; init; } = string.Empty;

        public int AutoCompactTotalEstimatedTokens { get; init; }

        public int AutoCompactCarriedPrefixEstimatedTokens { get; init; }

        public int AutoCompactBodyAfterPrefixEstimatedTokens { get; init; }

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
                .Append("） · 来源 ")
                .AppendLine(string.IsNullOrWhiteSpace(snapshot.ResponsePersonalitySourceLabel)
                    ? "Codex features.personality 稳定功能默认值"
                    : snapshot.ResponsePersonalitySourceLabel.Trim());
            builder.Append("Personality 功能：")
                .Append(snapshot.CodexPersonalityEnabled ? "启用" : "关闭");
            if (snapshot.HasCodexPersonalityEnabledOverride)
            {
                builder.Append("（")
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexPersonalityEnabledSourceLabel)
                        ? "Codex config.toml features.personality"
                        : snapshot.CodexPersonalityEnabledSourceLabel.Trim())
                    .Append('）');
            }
            else
            {
                builder.Append("（Codex 稳定功能默认值）");
            }
            builder.AppendLine(snapshot.CodexPersonalityEnabled
                ? " · 允许 personality 指令"
                : " · 总闸门关闭，不注入任何 personality 指令");
            builder.Append("公网检索：")
                .Append(CopilotCodexWebSearchModeSelection.GetEffectiveLabel(
                    snapshot.CodexWebSearchMode));
            if (snapshot.HasCodexWebSearchModeOverride)
            {
                builder.Append('（')
                    .Append(CopilotCodexWebSearchModeSelection.GetConfigToken(
                        snapshot.CodexWebSearchMode))
                    .Append(" · 来源 ")
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexWebSearchModeSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexWebSearchModeSourceLabel.Trim())
                    .Append('）');
            }
            builder.AppendLine();
            builder.Append("执行沙箱：")
                .Append(CopilotCodexSandboxModeSelection.GetConfigToken(
                    snapshot.CodexSandboxMode))
                .Append(" · ")
                .Append(CopilotCodexSandboxModeSelection.GetEffectiveLabel(
                    snapshot.CodexSandboxMode));
            if (snapshot.HasCodexSandboxModeOverride)
            {
                builder.Append("（来源 ")
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexSandboxModeSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexSandboxModeSourceLabel.Trim())
                    .Append(" · 提交快照）");
            }
            builder.AppendLine();
            builder.Append("审批策略：")
                .Append(CopilotCodexApprovalPolicySelection.GetConfigToken(
                    snapshot.CodexApprovalPolicy))
                .Append(" · ")
                .Append(CopilotCodexApprovalPolicySelection.GetEffectiveLabel(
                    snapshot.CodexApprovalPolicy));
            if (snapshot.HasCodexApprovalPolicyOverride)
            {
                builder.Append("（来源 ")
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexApprovalPolicySourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexApprovalPolicySourceLabel.Trim())
                    .Append(" · 提交快照）");
            }
            builder.AppendLine();
            builder.Append("自动审批复核：features.guardian_approval=")
                .Append(snapshot.CodexGuardianApprovalEnabled ? "true" : "false");
            if (snapshot.HasCodexGuardianApprovalEnabledOverride)
            {
                builder.Append("（来源 ")
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexGuardianApprovalEnabledSourceLabel)
                        ? "Codex config.toml features.guardian_approval"
                        : snapshot.CodexGuardianApprovalEnabledSourceLabel.Trim())
                    .Append(" · 提交快照）");
            }
            builder.AppendLine(snapshot.CodexGuardianApprovalEnabled
                ? " · 自动复核路由可用，仍受 approval_policy 与原生审批条件约束。"
                : " · 自动复核不可用；有效复核者固定为 user，approval_policy 与沙箱不变。");
            var effectiveApprovalsReviewer = snapshot.CodexGuardianApprovalEnabled
                ? snapshot.CodexApprovalsReviewer
                : CopilotCodexApprovalsReviewer.User;
            builder.Append("审批复核者：")
                .Append(CopilotCodexApprovalsReviewerSelection.GetConfigToken(
                    snapshot.CodexApprovalsReviewer))
                .Append(" · ");
            if (effectiveApprovalsReviewer != snapshot.CodexApprovalsReviewer)
            {
                builder.Append("有效 ")
                    .Append(CopilotCodexApprovalsReviewerSelection.GetConfigToken(
                        effectiveApprovalsReviewer))
                    .Append("；");
            }
            builder
                .Append(CopilotCodexApprovalsReviewerSelection.GetEffectiveLabel(
                    effectiveApprovalsReviewer));
            if (snapshot.HasCodexApprovalsReviewerOverride)
            {
                builder.Append("（来源 ")
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexApprovalsReviewerSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexApprovalsReviewerSourceLabel.Trim())
                    .Append(" · 提交快照）");
            }
            builder.AppendLine();
            if (snapshot.HasCodexAutoReviewPolicyOverride)
            {
                builder.Append("自动审查策略：")
                    .Append(FormatCount(snapshot.CodexAutoReviewPolicyCharacters))
                    .Append(" 字符（来源 ")
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexAutoReviewPolicySourceLabel)
                        ? "Codex config.toml auto_review.policy"
                        : snapshot.CodexAutoReviewPolicySourceLabel.Trim())
                    .AppendLine(snapshot.CodexGuardianApprovalEnabled
                        ? " · 提交快照；仅注入独立 reviewer）"
                        : " · 配置快照；guardian gate 关闭，本轮未注入 reviewer）");
            }
            if (snapshot.HasCodexModelOverride)
            {
                builder.Append("请求模型：")
                    .Append(snapshot.CodexModel)
                    .Append("（")
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexModelSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexModelSourceLabel.Trim())
                    .AppendLine(" 请求快照；沿用所选 Profile 的 Provider、端点与凭据；Review 模式的 review_model 优先）");
            }
            if (snapshot.HasCodexReviewModelOverride)
            {
                builder.Append("Review 模型：")
                    .Append(snapshot.CodexReviewModel)
                    .Append("（")
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexReviewModelSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexReviewModelSourceLabel.Trim())
                    .Append(snapshot.Mode == CopilotAgentMode.Review
                        ? " 请求快照；当前 Review 模式生效"
                        : " 请求快照；仅 Review 模式生效，当前模式不替换")
                    .AppendLine("；沿用所选 Profile 的 Provider、端点与凭据）");
            }
            if (snapshot.HasCodexPreventIdleSleepOverride)
            {
                builder.Append("活动轮次防休眠：")
                    .Append(snapshot.CodexPreventIdleSleep ? "开启" : "关闭")
                    .Append('（')
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexPreventIdleSleepSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexPreventIdleSleepSourceLabel.Trim())
                    .Append(" 提交快照");
                if (!snapshot.CodexPreventIdleSleep)
                {
                    builder.AppendLine("；不阻止系统空闲休眠）");
                }
                else if (snapshot.ActiveSleepPreventionLeaseCount > 0)
                {
                    builder.Append("；Windows Power Request 活动 ")
                        .Append(snapshot.ActiveSleepPreventionLeaseCount.ToString("N0", CultureInfo.CurrentCulture))
                        .AppendLine(" 个）");
                }
                else if (!string.IsNullOrWhiteSpace(snapshot.SleepPreventionLastFailure))
                {
                    builder.Append("；最近一次系统请求失败：")
                        .Append(snapshot.SleepPreventionLastFailure.Trim());
                    if (snapshot.SleepPreventionLastErrorCode.HasValue)
                    {
                        builder.Append("（Win32 ")
                            .Append(snapshot.SleepPreventionLastErrorCode.Value.ToString(CultureInfo.InvariantCulture))
                            .Append('）');
                    }
                    builder.AppendLine("）");
                }
                else
                {
                    builder.AppendLine("；当前无活动轮次，排队等待不占用系统请求）");
                }
            }
            builder.Append("命令工具：")
                .Append(snapshot.CodexShellToolEnabled ? "开启" : "关闭");
            if (snapshot.HasCodexShellToolEnabledOverride)
            {
                builder.Append('（')
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexShellToolEnabledSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexShellToolEnabledSourceLabel.Trim())
                    .Append(" 提交快照")
                    .Append(snapshot.CodexShellToolEnabled
                        ? "；按请求意图暴露命令启动工具）"
                        : "；命令启动工具已从目录移除，旧调用也会拒绝；已有后台命令仍可观察或停止）");
            }
            else
            {
                builder.Append("（Codex 默认开启）");
            }
            builder.AppendLine();
            builder.Append("模块扩展 Hook：")
                .Append(snapshot.CodexHooksEnabled ? "开启" : "关闭");
            if (snapshot.HasCodexHooksEnabledOverride)
            {
                builder.Append('（')
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexHooksEnabledSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexHooksEnabledSourceLabel.Trim())
                    .Append(" 提交快照；");
            }
            else
            {
                builder.Append("（Codex 默认开启；");
            }
            builder.AppendLine(snapshot.CodexHooksEnabled && snapshot.CodexPluginsEnabled
                ? "扩展授权与生命周期 Hook 可运行，内置写入安全策略始终保留）"
                : snapshot.CodexHooksEnabled
                    ? "features.plugins=false，扩展 Hook 已省略；内置写入安全策略仍保留）"
                    : "扩展授权与生命周期 Hook 已省略，内置写入安全策略仍保留）");
            builder.Append("Copilot 扩展能力：features.plugins=")
                .Append(snapshot.CodexPluginsEnabled ? "true" : "false");
            if (snapshot.HasCodexPluginsEnabledOverride)
            {
                builder.Append('（')
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexPluginsEnabledSourceLabel)
                        ? "Codex config.toml features.plugins"
                        : snapshot.CodexPluginsEnabledSourceLabel.Trim())
                    .Append(" 提交快照；");
            }
            else
            {
                builder.Append("（Codex 默认开启；");
            }
            builder.AppendLine(snapshot.CodexPluginsEnabled
                ? "模块提供的 Copilot context 与 tool 可用，扩展 Hook 仍受 features.hooks 约束）"
                : "模块提供的 Copilot context、tool 与 Hook 已排除；内置工具、外部 MCP 与主程序业务插件不受影响）");
            builder.Append("命令环境：")
                .Append(string.IsNullOrWhiteSpace(snapshot.CodexShellEnvironmentPolicySummary)
                    ? CopilotCodexShellEnvironmentPolicy.Default.BuildRedactedSummary()
                    : snapshot.CodexShellEnvironmentPolicySummary.Trim());
            if (snapshot.HasCodexShellEnvironmentPolicyOverride)
            {
                builder.Append('（')
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexShellEnvironmentPolicySourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexShellEnvironmentPolicySourceLabel.Trim())
                    .Append(" 提交快照；set 仅报告数量）");
            }
            else
            {
                builder.Append("（Codex 默认；set 仅报告数量）");
            }
            if (!string.IsNullOrWhiteSpace(snapshot.CodexShellEnvironmentPolicyError))
                builder.Append(" · ").Append(snapshot.CodexShellEnvironmentPolicyError.Trim());
            builder.AppendLine();
            builder.Append("持续目标：")
                .Append(snapshot.CodexGoalsEnabled ? "开启" : "暂停");
            if (snapshot.HasCodexGoalsEnabledOverride)
            {
                builder.Append('（')
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexGoalsEnabledSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexGoalsEnabledSourceLabel.Trim())
                    .Append(" 提交快照；")
                    .Append(snapshot.CodexGoalsEnabled
                        ? "活动目标会绑定、计数、评估并按需自动续作）"
                        : "不绑定、计数、评估或自动续作；已有记录保留，仍可查看、暂停或清除）");
            }
            else
            {
                builder.Append("（Codex 默认开启）");
            }
            builder.AppendLine();
            builder.Append("Default 模式结构化提问：")
                .Append(snapshot.CodexDefaultModeRequestUserInputEnabled ? "开放" : "关闭");
            if (snapshot.HasCodexDefaultModeRequestUserInputEnabledOverride)
            {
                builder.Append('（')
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexDefaultModeRequestUserInputEnabledSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexDefaultModeRequestUserInputEnabledSourceLabel.Trim())
                    .Append(" 提交快照；")
                    .Append(snapshot.CodexDefaultModeRequestUserInputEnabled
                        ? "AskUserQuestion 仍受全局工具开关约束）"
                        : "Plan 模式不受此 feature 影响）");
            }
            else
            {
                builder.Append("（Codex 默认关闭；Plan 模式不受影响）");
            }
            builder.AppendLine();
            AppendToolEnabled(
                builder,
                "结构化澄清工具",
                "tools.experimental_request_user_input.enabled",
                snapshot.CodexExperimentalRequestUserInputEnabled,
                snapshot.HasCodexExperimentalRequestUserInputEnabledOverride,
                snapshot.CodexExperimentalRequestUserInputEnabledSourceLabel,
                "AskUserQuestion 可用",
                "AskUserQuestion 已移除；不影响原生审批");
            AppendToolEnabled(
                builder,
                "任务清单工具",
                "tools.update_plan.enabled",
                snapshot.CodexUpdatePlanEnabled,
                snapshot.HasCodexUpdatePlanEnabledOverride,
                snapshot.CodexUpdatePlanEnabledSourceLabel,
                "复杂请求可启用任务清单与完成循环",
                "任务清单与 plan/execute 完成循环已移除");
            builder.Append("权限说明：")
                .Append(snapshot.CodexIncludePermissionsInstructions ? "注入" : "省略");
            if (snapshot.HasCodexIncludePermissionsInstructionsOverride)
            {
                builder.Append('（')
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexIncludePermissionsInstructionsSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexIncludePermissionsInstructionsSourceLabel.Trim())
                    .Append(" 提交快照；")
                    .Append(snapshot.CodexIncludePermissionsInstructions
                        ? "模型可见完整权限说明）"
                        : "仅省略模型提示；沙箱、审批、工具过滤与执行策略保持强制）");
            }
            else
            {
                builder.Append("（Codex 默认注入）");
            }
            builder.AppendLine();
            builder.Append("协作模式说明：")
                .Append(snapshot.CodexIncludeCollaborationModeInstructions ? "注入" : "省略");
            if (snapshot.HasCodexIncludeCollaborationModeInstructionsOverride)
            {
                builder.Append('（')
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexIncludeCollaborationModeInstructionsSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexIncludeCollaborationModeInstructionsSourceLabel.Trim())
                    .Append(" 提交快照；")
                    .Append(snapshot.CodexIncludeCollaborationModeInstructions
                        ? "模型可见当前 Plan/Default 语义）"
                        : "仅省略模式提示；当前模式、工具过滤、任务清单与完成循环保持不变）");
            }
            else
            {
                builder.Append("（Codex 默认注入）");
            }
            builder.AppendLine();
            builder.Append("运行环境上下文：")
                .Append(snapshot.CodexIncludeEnvironmentContext ? "注入" : "省略");
            if (snapshot.HasCodexIncludeEnvironmentContextOverride)
            {
                builder.Append('（')
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexIncludeEnvironmentContextSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexIncludeEnvironmentContextSourceLabel.Trim())
                    .Append(" 提交快照；")
                    .Append(snapshot.CodexIncludeEnvironmentContext
                        ? "模型可见请求开始时的工作目录、平台、日期、时区、路径边界与 Git 摘要）"
                        : "不向模型注入 runtime_environment；工具侧路径、沙箱与审批边界保持不变）");
            }
            else
            {
                builder.Append("（Codex 默认注入）");
            }
            builder.AppendLine();
            builder.Append("自动 Skill 说明：")
                .Append(snapshot.CodexIncludeSkillInstructions ? "注入" : "省略");
            if (snapshot.HasCodexIncludeSkillInstructionsOverride)
            {
                builder.Append('（')
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexIncludeSkillInstructionsSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexIncludeSkillInstructionsSourceLabel.Trim())
                    .Append(" 提交快照；")
                    .Append(snapshot.CodexIncludeSkillInstructions
                        ? "可按请求相关性自动选择 Skill）"
                        : "仅显式 $name 或 /name 调用可加载匹配 Skill）");
            }
            else
            {
                builder.Append("（Codex 默认注入）");
            }
            builder.AppendLine();
            builder.Append("V1 多代理功能：")
                .Append(snapshot.CodexMultiAgentEnabled ? "开启" : "关闭");
            if (snapshot.HasCodexMultiAgentEnabledOverride)
            {
                builder.Append('（')
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexMultiAgentEnabledSourceLabel)
                        ? "Codex config.toml features.multi_agent"
                        : snapshot.CodexMultiAgentEnabledSourceLabel.Trim())
                    .Append(" 提交快照）");
            }
            else
            {
                builder.Append("（Codex 稳定功能默认值）");
            }
            builder.AppendLine();
            builder.Append("Agents 配置：")
                .Append(snapshot.CodexAgentsEnabled ? "开启" : "关闭");
            if (snapshot.HasCodexAgentsEnabledOverride)
            {
                builder.Append('（')
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexAgentsEnabledSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexAgentsEnabledSourceLabel.Trim())
                    .Append(" 提交快照）");
            }
            else
            {
                builder.Append("（Codex 默认开启）");
            }
            builder.AppendLine();
            bool effectiveAgentsEnabled = snapshot.CodexMultiAgentEnabled
                && snapshot.CodexAgentsEnabled;
            builder.Append("子代理工具（有效）：")
                .Append(effectiveAgentsEnabled ? "开启" : "关闭")
                .AppendLine(effectiveAgentsEnabled
                    ? "（两个门槛均允许；可按请求意图暴露委派工具）"
                    : "（任一门槛关闭；工具已从目录移除，旧调用也会拒绝）");
            builder.Append("子代理中断消息：")
                .Append(snapshot.CodexInterruptMessageEnabled ? "记录给父 Agent" : "仅保留本地审计");
            if (snapshot.HasCodexInterruptMessageOverride)
            {
                builder.Append("（")
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexInterruptMessageSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexInterruptMessageSourceLabel.Trim())
                    .AppendLine(" 提交快照）");
            }
            else
            {
                builder.AppendLine("（Codex 默认开启）");
            }
            builder.Append("子代理并发槽位：")
                .Append(snapshot.CodexMaximumConcurrentSubagentRuns.ToString("N0", CultureInfo.CurrentCulture));
            if (snapshot.HasCodexMaximumConcurrentSubagentRunsOverride)
            {
                builder.Append('（')
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexMaximumConcurrentSubagentRunsSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexMaximumConcurrentSubagentRunsSourceLabel.Trim())
                    .Append(" 提交快照）");
            }
            else
            {
                builder.Append("（ColorVision 默认）");
            }
            builder.AppendLine("；请求级 Token 总预算独立限制");
            builder.Append("子代理默认模型：");
            if (snapshot.HasCodexDefaultSubagentModelOverride)
            {
                builder.Append(snapshot.CodexDefaultSubagentModel)
                    .Append("（")
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexDefaultSubagentModelSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexDefaultSubagentModelSourceLabel.Trim())
                    .AppendLine(" 提交快照；沿用父 Profile 的 Provider、端点与凭据）");
            }
            else
            {
                builder.AppendLine("沿用父 Profile 模型");
            }
            builder.Append("子代理默认推理强度：")
                .Append(CopilotCodexReasoningEffortSelection.GetConfigToken(
                    snapshot.CodexDefaultSubagentReasoningEffort));
            if (snapshot.HasCodexDefaultSubagentReasoningEffortOverride)
            {
                builder.Append("（")
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexDefaultSubagentReasoningEffortSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexDefaultSubagentReasoningEffortSourceLabel.Trim())
                    .AppendLine(" 提交快照；子代理官方 OpenAI Responses 生效）");
            }
            else
            {
                builder.AppendLine("（未配置；继承父请求推理强度）");
            }
            if (snapshot.HasCodexReasoningEffortOverride)
            {
                builder.Append("推理强度：")
                    .Append(CopilotCodexReasoningEffortSelection.GetConfigToken(
                        snapshot.CodexReasoningEffort))
                    .Append("（")
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexReasoningEffortSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexReasoningEffortSourceLabel.Trim())
                    .AppendLine(" 请求快照；仅 Agent 官方 OpenAI Responses 生效）");
            }
            if (snapshot.HasCodexReasoningSummaryOverride)
            {
                builder.Append("推理摘要：")
                    .Append(CopilotCodexReasoningSummarySelection.GetConfigToken(
                        snapshot.CodexReasoningSummary))
                    .Append("（")
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexReasoningSummarySourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexReasoningSummarySourceLabel.Trim())
                    .AppendLine(" 请求快照；仅 Agent 官方 OpenAI Responses 生效）");
            }
            if (snapshot.HasCodexModelSupportsReasoningSummariesOverride)
            {
                builder.Append("推理元数据能力：")
                    .Append(CopilotCodexReasoningSummarySupportSelection.GetConfigToken(
                        snapshot.CodexModelSupportsReasoningSummaries))
                    .Append("（")
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexModelSupportsReasoningSummariesSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexModelSupportsReasoningSummariesSourceLabel.Trim())
                    .Append(snapshot.CodexModelSupportsReasoningSummaries
                        ? " 请求快照；启用，摘要未配置时使用 auto；显式 none 仍关闭摘要"
                        : " 请求快照；阻断并覆盖 effort/summary")
                    .AppendLine("；仅 Agent 官方 OpenAI Responses 生效）");
            }
            if (snapshot.HasCodexHideAgentReasoningOverride)
            {
                builder.Append("推理事件展示：")
                    .Append(snapshot.CodexHideAgentReasoning ? "隐藏" : "显示")
                    .Append("（")
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexHideAgentReasoningSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexHideAgentReasoningSourceLabel.Trim())
                    .AppendLine(" 提交快照；同时作用于 Chat/Agent，仅改变用户可见输出，不改变请求、Token 计量或运行事件）");
            }
            if (snapshot.HasCodexFastModeEnabledOverride)
            {
                builder.Append("快速服务等级总闸门：")
                    .Append(snapshot.CodexFastModeEnabled ? "启用" : "关闭")
                    .Append("（")
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexFastModeEnabledSourceLabel)
                        ? "Codex config.toml features.fast_mode"
                        : snapshot.CodexFastModeEnabledSourceLabel.Trim())
                    .Append(snapshot.CodexFastModeEnabled
                        ? " 请求快照；允许 service_tier"
                        : " 请求快照；不发送任何 service_tier")
                    .AppendLine("；仅 Agent 官方 OpenAI Responses 生效）");
            }
            if (snapshot.HasCodexServiceTierOverride)
            {
                builder.Append("服务等级：")
                    .Append(snapshot.CodexServiceTier)
                    .Append(snapshot.CodexFastModeEnabled
                        ? " → 请求 " + CopilotCodexServiceTierSelection.GetRequestToken(
                            snapshot.CodexServiceTier)
                        : " → 不发送（features.fast_mode=false）")
                    .Append("（")
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexServiceTierSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexServiceTierSourceLabel.Trim())
                    .AppendLine(" 请求快照；仅 Agent 官方 OpenAI Responses 生效）");
            }
            if (snapshot.HasCodexModelVerbosityOverride)
            {
                builder.Append("回答详细度：")
                    .Append(CopilotCodexModelVerbositySelection.GetConfigToken(
                        snapshot.CodexModelVerbosity))
                    .Append("（")
                    .Append(string.IsNullOrWhiteSpace(snapshot.CodexModelVerbositySourceLabel)
                        ? "Codex config.toml"
                        : snapshot.CodexModelVerbositySourceLabel.Trim())
                    .AppendLine(" 请求快照；仅 Agent 官方 OpenAI Responses 生效）");
            }
            builder.Append("有效系统提示：")
                .Append(FormatCount(snapshot.SystemPromptCharacters))
                .AppendLine(" 字符（已应用宿主响应规则）");
            if (snapshot.HasConfiguredModelInstructionsOverride)
            {
                builder.Append("Codex ")
                    .Append(snapshot.ConfiguredModelInstructionsUsesFile
                        ? "model_instructions_file"
                        : "instructions")
                    .Append('：')
                    .Append(FormatCount(snapshot.ConfiguredModelInstructionsCharacters))
                    .Append(" 字符（")
                    .Append(string.IsNullOrWhiteSpace(snapshot.ConfiguredModelInstructionsSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.ConfiguredModelInstructionsSourceLabel.Trim())
                    .AppendLine(snapshot.ConfiguredModelInstructionsApplied
                        ? " 请求快照；已替换内置主体，宿主安全规则仍强制保留）"
                        : snapshot.ConfiguredModelInstructionsCharacters == 0
                            ? snapshot.ConfiguredModelInstructionsUsesFile
                                ? " 请求快照；文件为空或未安全加载，使用 Profile/内置主体）"
                                : " 请求快照；内联值为空或无效，使用 Profile/内置主体）"
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
            if (snapshot.HasModelContextWindowOverride)
            {
                builder.Append("Codex model_context_window：")
                    .Append(FormatCount(snapshot.HistoryContextWindowTokens))
                    .Append(" Token · 来源 ")
                    .Append(string.IsNullOrWhiteSpace(snapshot.ModelContextWindowSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.ModelContextWindowSourceLabel.Trim())
                    .AppendLine(" 请求快照；同时约束聊天历史、发送校验、自动压缩和 Agent 上下文");
            }
            builder.Append("工具结果历史预算：");
            if (snapshot.HasToolOutputTokenLimitOverride)
            {
                builder.Append("单次最多 ")
                    .Append(FormatCount(snapshot.ToolOutputTokenLimit))
                    .Append(" Token（混合文本保守估算） · 来源 ")
                    .Append(string.IsNullOrWhiteSpace(snapshot.ToolOutputTokenLimitSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.ToolOutputTokenLimitSourceLabel.Trim())
                    .AppendLine(" 请求快照");
            }
            else
            {
                builder.Append(FormatCount(CopilotFrameworkToolResultFormatter.MaxSerializedCharacters))
                    .AppendLine(" 序列化字符（ColorVision 默认）");
            }
            builder.AppendLine("范围：只压缩写入模型历史的函数结果；完整工具结果、审批记录、证据路径与审计日志保持原样。");
            builder.Append("自动压缩：");
            if (snapshot.AutoCompactConversationHistory)
            {
                builder.Append("已开启 · ");
                if (snapshot.HasModelAutoCompactTokenLimitOverride)
                {
                    var scopeToken = CopilotModelAutoCompactTokenLimitScopeSelection.GetConfigToken(
                        snapshot.ModelAutoCompactTokenLimitScope);
                    var activeTokens = snapshot.ModelAutoCompactTokenLimitScope ==
                        CopilotModelAutoCompactTokenLimitScope.BodyAfterPrefix
                            ? snapshot.AutoCompactBodyAfterPrefixEstimatedTokens
                            : snapshot.AutoCompactTotalEstimatedTokens;
                    builder.Append("Codex ")
                        .Append(scopeToken)
                        .Append(" 计量 ")
                        .Append(FormatCount(activeTokens))
                        .Append('/')
                        .Append(FormatCount(snapshot.ConfiguredModelAutoCompactTokenLimit))
                        .AppendLine(" Token；达到阈值时在发送前压缩，失败时保留原请求");
                }
                else
                {
                    builder.Append("活动历史达到 ")
                        .Append(snapshot.AutoCompactThresholdPercent.ToString(CultureInfo.InvariantCulture))
                        .AppendLine("% 时在发送前压缩；失败时保留原请求");
                }
            }
            else
            {
                builder.AppendLine("已关闭");
            }
            if (snapshot.HasModelAutoCompactTokenLimitOverride)
            {
                builder.Append("Codex model_auto_compact_token_limit：")
                    .Append(FormatCount(snapshot.ConfiguredModelAutoCompactTokenLimit))
                    .Append(" Token · 来源 ")
                    .Append(string.IsNullOrWhiteSpace(snapshot.ModelAutoCompactTokenLimitSourceLabel)
                        ? "Codex config.toml"
                        : snapshot.ModelAutoCompactTokenLimitSourceLabel.Trim())
                    .AppendLine(snapshot.AutoCompactConversationHistory
                        ? " 请求快照"
                        : " 请求快照；宿主自动压缩已关闭，当前不触发");
            }
            if (snapshot.HasModelAutoCompactTokenLimitScopeOverride)
            {
                builder.Append("Codex model_auto_compact_token_limit_scope：")
                    .Append(CopilotModelAutoCompactTokenLimitScopeSelection.GetConfigToken(
                        snapshot.ModelAutoCompactTokenLimitScope))
                    .Append(" · 来源 ")
                    .AppendLine(string.IsNullOrWhiteSpace(snapshot.ModelAutoCompactTokenLimitScopeSourceLabel)
                        ? "Codex config.toml 请求快照"
                        : snapshot.ModelAutoCompactTokenLimitScopeSourceLabel.Trim() + " 请求快照");
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
            AppendAgentExtensionDetails(
                builder,
                snapshot.AgentExtensions,
                snapshot.AgentExtensionIssues,
                snapshot.CodexPluginsEnabled);
            AppendOptimizationSuggestions(builder, snapshot);
            return builder.ToString().TrimEnd();
        }

        private static void AppendToolEnabled(
            StringBuilder builder,
            string label,
            string key,
            bool enabled,
            bool hasOverride,
            string sourceLabel,
            string enabledDescription,
            string disabledDescription)
        {
            builder.Append(label)
                .Append('：')
                .Append(enabled ? "开启" : "关闭");
            if (hasOverride)
            {
                builder.Append('（')
                    .Append(string.IsNullOrWhiteSpace(sourceLabel) ? "Codex config.toml" : sourceLabel.Trim())
                    .Append(" 提交快照；")
                    .Append(enabled ? enabledDescription : disabledDescription)
                    .Append('）');
            }
            else
            {
                builder.Append("（Codex 默认开启；")
                    .Append(key)
                    .Append("）");
            }
            builder.AppendLine();
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
