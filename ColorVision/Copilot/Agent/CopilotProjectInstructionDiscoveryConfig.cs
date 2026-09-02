using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Copilot
{
    [Flags]
    internal enum CopilotProjectInstructionConfigSources
    {
        None = 0,
        CodexHome = 1,
        TrustedProject = 2,
    }

    internal enum CopilotCodexProjectTrustLevel
    {
        Unspecified,
        Trusted,
        Untrusted,
        Invalid,
    }

    internal enum CopilotCodexWebSearchMode
    {
        Unspecified,
        Disabled,
        Cached,
        Indexed,
        Live,
    }

    internal readonly record struct CopilotCodexWebSearchConfigState(
        bool HasOverride,
        CopilotCodexWebSearchMode Mode,
        CopilotProjectInstructionConfigSources Source)
    {
        public bool HasAnyAssignment => HasOverride;

        public CopilotCodexWebSearchConfigState Apply(
            CopilotCodexWebSearchConfigState layer,
            CopilotProjectInstructionConfigSources source) => layer.HasOverride
                ? layer with { Source = source }
                : this;
    }

    internal enum CopilotCodexSandboxMode
    {
        Unspecified,
        ReadOnly,
        WorkspaceWrite,
        DangerFullAccess,
    }

    internal static class CopilotCodexSandboxModeSelection
    {
        public static bool TryParse(string? value, out CopilotCodexSandboxMode mode)
        {
            mode = (value ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "read-only" => CopilotCodexSandboxMode.ReadOnly,
                "workspace-write" => CopilotCodexSandboxMode.WorkspaceWrite,
                "danger-full-access" => CopilotCodexSandboxMode.DangerFullAccess,
                _ => CopilotCodexSandboxMode.Unspecified,
            };
            return mode != CopilotCodexSandboxMode.Unspecified;
        }

        public static string GetConfigToken(CopilotCodexSandboxMode mode) => mode switch
        {
            CopilotCodexSandboxMode.ReadOnly => "read-only",
            CopilotCodexSandboxMode.WorkspaceWrite => "workspace-write",
            CopilotCodexSandboxMode.DangerFullAccess => "danger-full-access",
            _ => "未配置",
        };

        public static bool IsReadOnly(CopilotCodexSandboxMode mode) =>
            mode == CopilotCodexSandboxMode.ReadOnly;

        public static string GetEffectiveLabel(CopilotCodexSandboxMode mode) => mode switch
        {
            CopilotCodexSandboxMode.ReadOnly => "只读能力上限；写工具不暴露，旧计划或注入写调用也会拒绝",
            CopilotCodexSandboxMode.WorkspaceWrite => "不扩大 ColorVision 原生可写根、访问模式或审批权限",
            CopilotCodexSandboxMode.DangerFullAccess => "不映射为提权；仍受 ColorVision 原生访问与审批边界约束",
            _ => "未配置；保留 ColorVision 原生访问与审批边界",
        };
    }

    internal static class CopilotCodexWebSearchModeSelection
    {
        public static bool TryParse(string? value, out CopilotCodexWebSearchMode mode)
        {
            mode = (value ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "disabled" => CopilotCodexWebSearchMode.Disabled,
                "cached" => CopilotCodexWebSearchMode.Cached,
                "indexed" => CopilotCodexWebSearchMode.Indexed,
                "live" => CopilotCodexWebSearchMode.Live,
                _ => CopilotCodexWebSearchMode.Unspecified,
            };
            return mode != CopilotCodexWebSearchMode.Unspecified;
        }

        public static string GetConfigToken(CopilotCodexWebSearchMode mode) => mode switch
        {
            CopilotCodexWebSearchMode.Disabled => "disabled",
            CopilotCodexWebSearchMode.Cached => "cached",
            CopilotCodexWebSearchMode.Indexed => "indexed",
            CopilotCodexWebSearchMode.Live => "live",
            _ => "未配置",
        };

        public static bool AllowsLiveSearch(CopilotCodexWebSearchMode mode) => mode is
            CopilotCodexWebSearchMode.Unspecified or CopilotCodexWebSearchMode.Live;

        public static string GetEffectiveLabel(CopilotCodexWebSearchMode mode) => mode switch
        {
            CopilotCodexWebSearchMode.Disabled => "已禁用实时公网检索",
            CopilotCodexWebSearchMode.Cached => "不支持 cached 后端；已保守禁用实时公网检索",
            CopilotCodexWebSearchMode.Indexed => "不支持 indexed 后端；已保守禁用实时公网检索",
            CopilotCodexWebSearchMode.Live => "已允许按请求意图实时公网检索",
            _ => "未配置；保留 ColorVision 按请求意图实时检索",
        };
    }

    internal enum CopilotModelAutoCompactTokenLimitScope
    {
        Unspecified,
        Total,
        BodyAfterPrefix,
    }

    internal static class CopilotModelAutoCompactTokenLimitScopeSelection
    {
        public static bool TryParse(
            string? value,
            out CopilotModelAutoCompactTokenLimitScope scope)
        {
            scope = (value ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "total" => CopilotModelAutoCompactTokenLimitScope.Total,
                "body_after_prefix" => CopilotModelAutoCompactTokenLimitScope.BodyAfterPrefix,
                _ => CopilotModelAutoCompactTokenLimitScope.Unspecified,
            };
            return scope != CopilotModelAutoCompactTokenLimitScope.Unspecified;
        }

        public static string GetConfigToken(CopilotModelAutoCompactTokenLimitScope scope) => scope switch
        {
            CopilotModelAutoCompactTokenLimitScope.BodyAfterPrefix => "body_after_prefix",
            _ => "total",
        };
    }

    internal sealed record CopilotProjectInstructionDiscoveryOptions(
        int MaximumBytes,
        IReadOnlyList<string> FallbackFileNames,
        bool HasMaximumBytesOverride,
        bool HasFallbackFileNamesOverride,
        CopilotProjectInstructionConfigSources ConfigSources = CopilotProjectInstructionConfigSources.None,
        CopilotCodexProjectTrustLevel ProjectTrustLevel = CopilotCodexProjectTrustLevel.Unspecified)
    {
        public IReadOnlyList<string> ProjectRootMarkers { get; init; } =
            CopilotProjectInstructionDiscoveryConfig.DefaultProjectRootMarkers;

        public bool HasProjectRootMarkersOverride { get; init; }

        public IReadOnlyList<string> AppliedProjectConfigFilePaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> AppliedExecPolicyFilePaths { get; init; } = Array.Empty<string>();

        internal IReadOnlyList<CopilotCodexExecPolicyRule> ConfiguredExecPolicyRules { get; init; } =
            Array.Empty<CopilotCodexExecPolicyRule>();

        internal IReadOnlyList<CopilotCodexExecPolicyIssue> ConfiguredExecPolicyIssues { get; init; } =
            Array.Empty<CopilotCodexExecPolicyIssue>();

        public string DeveloperInstructions { get; init; } = string.Empty;

        public bool HasDeveloperInstructionsOverride { get; init; }

        public CopilotProjectInstructionConfigSources DeveloperInstructionsSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public bool ConfiguredPersonalityEnabled { get; init; } = true;

        public bool HasPersonalityEnabledOverride { get; init; }

        public CopilotProjectInstructionConfigSources PersonalityEnabledSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public CopilotResponsePersonality ConfiguredPersonality { get; init; } =
            CopilotResponsePersonality.None;

        public bool HasPersonalityOverride { get; init; }

        public CopilotProjectInstructionConfigSources PersonalitySource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public CopilotCodexWebSearchMode ConfiguredWebSearchMode { get; init; } =
            CopilotCodexWebSearchMode.Unspecified;

        public bool HasWebSearchModeOverride { get; init; }

        internal CopilotCodexWebSearchConfigState WebSearchConfigState { get; init; }

        public CopilotProjectInstructionConfigSources WebSearchModeSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public CopilotCodexSandboxMode ConfiguredSandboxMode { get; init; } =
            CopilotCodexSandboxMode.Unspecified;

        public bool HasSandboxModeOverride { get; init; }

        public CopilotProjectInstructionConfigSources SandboxModeSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public CopilotCodexApprovalPolicy ConfiguredApprovalPolicy { get; init; } =
            CopilotCodexApprovalPolicy.Unspecified;

        public bool HasApprovalPolicyOverride { get; init; }

        public CopilotProjectInstructionConfigSources ApprovalPolicySource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        internal CopilotCodexApprovalsReviewer ConfiguredApprovalsReviewer { get; init; } =
            CopilotCodexApprovalsReviewer.Unspecified;

        public bool HasApprovalsReviewerOverride { get; init; }

        public CopilotProjectInstructionConfigSources ApprovalsReviewerSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public bool ConfiguredGuardianApprovalEnabled { get; init; } = true;

        public bool HasGuardianApprovalEnabledOverride { get; init; }

        public CopilotProjectInstructionConfigSources GuardianApprovalEnabledSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        internal CopilotCodexApprovalsReviewer EffectiveApprovalsReviewer =>
            ConfiguredGuardianApprovalEnabled
                ? ConfiguredApprovalsReviewer
                : CopilotCodexApprovalsReviewer.User;

        public string ConfiguredAutoReviewPolicy { get; init; } = string.Empty;

        public bool HasAutoReviewPolicyOverride { get; init; }

        public CopilotProjectInstructionConfigSources AutoReviewPolicySource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public string ConfiguredModel { get; init; } = string.Empty;

        public bool HasModelOverride { get; init; }

        public CopilotProjectInstructionConfigSources ModelSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public string ConfiguredReviewModel { get; init; } = string.Empty;

        public bool HasReviewModelOverride { get; init; }

        public CopilotProjectInstructionConfigSources ReviewModelSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public bool ConfiguredPreventIdleSleep { get; init; }

        public bool HasPreventIdleSleepOverride { get; init; }

        public CopilotProjectInstructionConfigSources PreventIdleSleepSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public bool ConfiguredShellToolEnabled { get; init; } = true;

        public bool HasShellToolEnabledOverride { get; init; }

        public CopilotProjectInstructionConfigSources ShellToolEnabledSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public bool ConfiguredHooksEnabled { get; init; } = true;

        public bool HasHooksEnabledOverride { get; init; }

        public CopilotProjectInstructionConfigSources HooksEnabledSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public bool ConfiguredPluginsEnabled { get; init; } = true;

        public bool HasPluginsEnabledOverride { get; init; }

        public CopilotProjectInstructionConfigSources PluginsEnabledSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public bool ConfiguredErrorOnToolCollisions { get; init; }

        public bool HasErrorOnToolCollisionsOverride { get; init; }

        public CopilotProjectInstructionConfigSources ErrorOnToolCollisionsSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public bool ConfiguredMentionsV2Enabled { get; init; } = true;

        public bool HasMentionsV2EnabledOverride { get; init; }

        public CopilotProjectInstructionConfigSources MentionsV2EnabledSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public bool ConfiguredSkillMcpDependencyInstallEnabled { get; init; } = true;

        public bool HasSkillMcpDependencyInstallEnabledOverride { get; init; }

        public CopilotProjectInstructionConfigSources SkillMcpDependencyInstallEnabledSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        internal CopilotCodexShellEnvironmentPolicy ConfiguredShellEnvironmentPolicy { get; init; } =
            CopilotCodexShellEnvironmentPolicy.Default;

        public bool HasShellEnvironmentPolicyOverride { get; init; }

        public CopilotProjectInstructionConfigSources ShellEnvironmentPolicySources { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public string ShellEnvironmentPolicyError { get; init; } = string.Empty;

        public bool ConfiguredGoalsEnabled { get; init; } = true;

        public bool HasGoalsEnabledOverride { get; init; }

        public CopilotProjectInstructionConfigSources GoalsEnabledSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public bool ConfiguredDefaultModeRequestUserInputEnabled { get; init; }

        public bool HasDefaultModeRequestUserInputEnabledOverride { get; init; }

        public CopilotProjectInstructionConfigSources DefaultModeRequestUserInputEnabledSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public bool ConfiguredExperimentalRequestUserInputEnabled { get; init; } = true;

        public bool HasExperimentalRequestUserInputEnabledOverride { get; init; }

        public CopilotProjectInstructionConfigSources ExperimentalRequestUserInputEnabledSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public bool ConfiguredUpdatePlanEnabled { get; init; } = true;

        public bool HasUpdatePlanEnabledOverride { get; init; }

        public CopilotProjectInstructionConfigSources UpdatePlanEnabledSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public bool ConfiguredIncludePermissionsInstructions { get; init; } = true;

        public bool HasIncludePermissionsInstructionsOverride { get; init; }

        public CopilotProjectInstructionConfigSources IncludePermissionsInstructionsSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public bool ConfiguredIncludeCollaborationModeInstructions { get; init; } = true;

        public bool HasIncludeCollaborationModeInstructionsOverride { get; init; }

        public CopilotProjectInstructionConfigSources IncludeCollaborationModeInstructionsSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public bool ConfiguredIncludeEnvironmentContext { get; init; } = true;

        public bool HasIncludeEnvironmentContextOverride { get; init; }

        public CopilotProjectInstructionConfigSources IncludeEnvironmentContextSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public bool ConfiguredIncludeSkillInstructions { get; init; } = true;

        public bool HasIncludeSkillInstructionsOverride { get; init; }

        public CopilotProjectInstructionConfigSources IncludeSkillInstructionsSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public bool ConfiguredMultiAgentEnabled { get; init; } = true;

        public bool HasMultiAgentEnabledOverride { get; init; }

        public CopilotProjectInstructionConfigSources MultiAgentEnabledSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public bool ConfiguredAgentsEnabled { get; init; } = true;

        public bool HasAgentsEnabledOverride { get; init; }

        public CopilotProjectInstructionConfigSources AgentsEnabledSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public bool ConfiguredInterruptMessageEnabled { get; init; } = true;

        public bool HasInterruptMessageOverride { get; init; }

        public CopilotProjectInstructionConfigSources InterruptMessageSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public int ConfiguredMaximumConcurrentSubagentRuns { get; init; } =
            CopilotSubagentCoordinator.DefaultMaximumConcurrentRuns;

        public bool HasMaximumConcurrentSubagentRunsOverride { get; init; }

        public CopilotProjectInstructionConfigSources MaximumConcurrentSubagentRunsSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public string ConfiguredDefaultSubagentModel { get; init; } = string.Empty;

        public bool HasDefaultSubagentModelOverride { get; init; }

        public CopilotProjectInstructionConfigSources DefaultSubagentModelSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public CopilotCodexReasoningEffort ConfiguredDefaultSubagentReasoningEffort { get; init; } =
            CopilotCodexReasoningEffort.Unspecified;

        public bool HasDefaultSubagentReasoningEffortOverride { get; init; }

        public CopilotProjectInstructionConfigSources DefaultSubagentReasoningEffortSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public int ConfiguredModelContextWindowTokens { get; init; }

        public bool HasModelContextWindowOverride { get; init; }

        public CopilotProjectInstructionConfigSources ModelContextWindowSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public int ConfiguredToolOutputTokenLimit { get; init; }

        public bool HasToolOutputTokenLimitOverride { get; init; }

        public CopilotProjectInstructionConfigSources ToolOutputTokenLimitSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public CopilotCodexReasoningEffort ConfiguredModelReasoningEffort { get; init; } =
            CopilotCodexReasoningEffort.Unspecified;

        public bool HasModelReasoningEffortOverride { get; init; }

        public CopilotProjectInstructionConfigSources ModelReasoningEffortSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public CopilotCodexReasoningEffort ConfiguredPlanModeReasoningEffort { get; init; } =
            CopilotCodexReasoningEffort.Unspecified;

        public bool HasPlanModeReasoningEffortOverride { get; init; }

        public CopilotProjectInstructionConfigSources PlanModeReasoningEffortSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public CopilotCodexReasoningSummary ConfiguredModelReasoningSummary { get; init; } =
            CopilotCodexReasoningSummary.Unspecified;

        public bool HasModelReasoningSummaryOverride { get; init; }

        public CopilotProjectInstructionConfigSources ModelReasoningSummarySource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public bool ConfiguredModelSupportsReasoningSummaries { get; init; }

        public bool HasModelSupportsReasoningSummariesOverride { get; init; }

        public CopilotProjectInstructionConfigSources ModelSupportsReasoningSummariesSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public bool ConfiguredHideAgentReasoning { get; init; }

        public bool HasHideAgentReasoningOverride { get; init; }

        public CopilotProjectInstructionConfigSources HideAgentReasoningSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public bool ConfiguredFastModeEnabled { get; init; } = true;

        public bool HasFastModeEnabledOverride { get; init; }

        public CopilotProjectInstructionConfigSources FastModeEnabledSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public string ConfiguredServiceTier { get; init; } = string.Empty;

        public bool HasServiceTierOverride { get; init; }

        public CopilotProjectInstructionConfigSources ServiceTierSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public CopilotCodexModelVerbosity ConfiguredModelVerbosity { get; init; } =
            CopilotCodexModelVerbosity.Unspecified;

        public bool HasModelVerbosityOverride { get; init; }

        public CopilotProjectInstructionConfigSources ModelVerbositySource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public int ResolveContextWindowTokens(int fallbackTokens)
        {
            return HasModelContextWindowOverride
                ? ConfiguredModelContextWindowTokens
                : Math.Clamp(
                    fallbackTokens,
                    CopilotAgentTokenBudget.MinimumContextWindowTokens,
                    CopilotAgentTokenBudget.MaximumContextWindowTokens);
        }

        public int ConfiguredModelAutoCompactTokenLimit { get; init; }

        public bool HasModelAutoCompactTokenLimitOverride { get; init; }

        public CopilotProjectInstructionConfigSources ModelAutoCompactTokenLimitSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public CopilotModelAutoCompactTokenLimitScope ConfiguredModelAutoCompactTokenLimitScope { get; init; } =
            CopilotModelAutoCompactTokenLimitScope.Unspecified;

        public bool HasModelAutoCompactTokenLimitScopeOverride { get; init; }

        public CopilotProjectInstructionConfigSources ModelAutoCompactTokenLimitScopeSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        public CopilotModelAutoCompactTokenLimitScope EffectiveModelAutoCompactTokenLimitScope =>
            HasModelAutoCompactTokenLimitScopeOverride
                ? ConfiguredModelAutoCompactTokenLimitScope
                : CopilotModelAutoCompactTokenLimitScope.Total;

        internal string ConfiguredModelInstructions { get; init; } = string.Empty;

        internal bool HasModelInstructionsInlineOverride { get; init; }

        internal CopilotProjectInstructionConfigSources ModelInstructionsInlineSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        internal string ConfiguredModelInstructionsFileContent { get; init; } = string.Empty;

        internal bool HasModelInstructionsFileOverride { get; init; }

        internal CopilotProjectInstructionConfigSources ModelInstructionsFileSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        internal string ConfiguredModelInstructionsSourceFilePath { get; init; } = string.Empty;

        public string ModelInstructions => HasModelInstructionsFileOverride
            ? ConfiguredModelInstructionsFileContent.Trim()
            : ConfiguredModelInstructions.Trim();

        public bool HasModelInstructionsOverride => HasModelInstructionsInlineOverride
            || HasModelInstructionsFileOverride;

        public bool HasEffectiveModelInstructions => ModelInstructions.Length > 0;

        public bool ModelInstructionsUsesFile => HasModelInstructionsFileOverride;

        public CopilotProjectInstructionConfigSources ModelInstructionsSource => ModelInstructionsUsesFile
            ? ModelInstructionsFileSource
            : ModelInstructionsInlineSource;

        public string ModelInstructionsSourceFilePath => ModelInstructionsUsesFile
            && HasEffectiveModelInstructions
            ? ConfiguredModelInstructionsSourceFilePath
            : string.Empty;

        internal string ConfiguredCompactPrompt { get; init; } = string.Empty;

        internal bool HasCompactPromptInlineOverride { get; init; }

        internal CopilotProjectInstructionConfigSources CompactPromptInlineSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        internal string ConfiguredCompactPromptFileContent { get; init; } = string.Empty;

        internal bool HasCompactPromptFileOverride { get; init; }

        internal CopilotProjectInstructionConfigSources CompactPromptFileSource { get; init; } =
            CopilotProjectInstructionConfigSources.None;

        internal string ConfiguredCompactPromptSourceFilePath { get; init; } = string.Empty;

        public string CompactPrompt
        {
            get
            {
                var inline = ConfiguredCompactPrompt.Trim();
                if (inline.Length > 0)
                    return inline;
                return ConfiguredCompactPromptFileContent.Trim();
            }
        }

        public bool HasCompactPromptOverride => HasCompactPromptInlineOverride
            || HasCompactPromptFileOverride;

        public CopilotProjectInstructionConfigSources CompactPromptSource
        {
            get
            {
                if (ConfiguredCompactPrompt.Trim().Length > 0)
                    return CompactPromptInlineSource;
                if (ConfiguredCompactPromptFileContent.Trim().Length > 0)
                    return CompactPromptFileSource;
                if (HasCompactPromptFileOverride)
                    return CompactPromptFileSource;
                return HasCompactPromptInlineOverride
                    ? CompactPromptInlineSource
                    : CopilotProjectInstructionConfigSources.None;
            }
        }

        public string CompactPromptSourceFilePath =>
            CompactPromptUsesFile
                ? ConfiguredCompactPromptSourceFilePath
                : string.Empty;

        public bool CompactPromptUsesFile => ConfiguredCompactPrompt.Trim().Length == 0
            && HasCompactPromptFileOverride;

        public bool UsesCodexConfig => ConfigSources != CopilotProjectInstructionConfigSources.None
            || HasMaximumBytesOverride
            || HasFallbackFileNamesOverride
            || HasProjectRootMarkersOverride
            || HasDeveloperInstructionsOverride
            || HasPersonalityEnabledOverride
            || HasPersonalityOverride
            || HasWebSearchModeOverride
            || HasSandboxModeOverride
            || HasApprovalPolicyOverride
            || HasApprovalsReviewerOverride
            || HasGuardianApprovalEnabledOverride
            || HasAutoReviewPolicyOverride
            || HasModelOverride
            || HasReviewModelOverride
            || HasPreventIdleSleepOverride
            || HasShellToolEnabledOverride
            || HasHooksEnabledOverride
            || HasPluginsEnabledOverride
            || HasErrorOnToolCollisionsOverride
            || HasMentionsV2EnabledOverride
            || HasSkillMcpDependencyInstallEnabledOverride
            || HasShellEnvironmentPolicyOverride
            || HasGoalsEnabledOverride
            || HasDefaultModeRequestUserInputEnabledOverride
            || HasExperimentalRequestUserInputEnabledOverride
            || HasUpdatePlanEnabledOverride
            || HasIncludePermissionsInstructionsOverride
            || HasIncludeCollaborationModeInstructionsOverride
            || HasIncludeEnvironmentContextOverride
            || HasIncludeSkillInstructionsOverride
            || HasMultiAgentEnabledOverride
            || HasAgentsEnabledOverride
            || HasInterruptMessageOverride
            || HasMaximumConcurrentSubagentRunsOverride
            || HasDefaultSubagentModelOverride
            || HasDefaultSubagentReasoningEffortOverride
            || HasModelContextWindowOverride
            || HasToolOutputTokenLimitOverride
            || HasModelReasoningEffortOverride
            || HasPlanModeReasoningEffortOverride
            || HasModelReasoningSummaryOverride
            || HasModelSupportsReasoningSummariesOverride
            || HasHideAgentReasoningOverride
            || HasFastModeEnabledOverride
            || HasServiceTierOverride
            || HasModelVerbosityOverride
            || HasModelAutoCompactTokenLimitOverride
            || HasModelAutoCompactTokenLimitScopeOverride
            || HasModelInstructionsOverride
            || HasCompactPromptOverride
            || ConfiguredExecPolicyRules.Count > 0
            || ConfiguredExecPolicyIssues.Count > 0;

        public string ConfigSourceLabel => ConfigSources switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml",
            CopilotProjectInstructionConfigSources.CodexHome | CopilotProjectInstructionConfigSources.TrustedProject =>
                "Codex Home + 受信项目 .codex/config.toml",
            _ => UsesCodexConfig ? "Codex config.toml" : "ColorVision 默认",
        };

        public bool AllowsProjectCodexConfig => ProjectTrustLevel == CopilotCodexProjectTrustLevel.Trusted;

        public string DeveloperInstructionsSourceLabel => FormatSourceLabel(DeveloperInstructionsSource);

        public string PersonalitySourceLabel => FormatSourceLabel(PersonalitySource, "personality");

        public string PersonalityEnabledSourceLabel => FormatSourceLabel(PersonalityEnabledSource, "features.personality");

        public string WebSearchModeSourceLabel => HasWebSearchModeOverride
            ? FormatSourceLabel(WebSearchModeSource, "web_search")
            : string.Empty;

        public string SandboxModeSourceLabel => FormatSourceLabel(SandboxModeSource, "sandbox_mode");

        public string ApprovalPolicySourceLabel => FormatSourceLabel(ApprovalPolicySource, "approval_policy");

        public string ShellEnvironmentPolicySourceLabel => FormatSourceLabel(
            ShellEnvironmentPolicySources,
            "shell_environment_policy",
            allowCombined: true);

        public string ApprovalsReviewerSourceLabel => FormatSourceLabel(ApprovalsReviewerSource, "approvals_reviewer");

        public string GuardianApprovalEnabledSourceLabel => FormatSourceLabel(GuardianApprovalEnabledSource, "features.guardian_approval");

        public string AutoReviewPolicySourceLabel => FormatSourceLabel(AutoReviewPolicySource, "auto_review.policy");

        public string ModelSourceLabel => FormatSourceLabel(ModelSource, "model");

        public string ReviewModelSourceLabel => FormatSourceLabel(ReviewModelSource, "review_model");

        public string PreventIdleSleepSourceLabel => FormatSourceLabel(PreventIdleSleepSource, "features.prevent_idle_sleep");

        public string ShellToolEnabledSourceLabel => FormatSourceLabel(ShellToolEnabledSource, "features.shell_tool");

        public string HooksEnabledSourceLabel => FormatSourceLabel(HooksEnabledSource, "features.hooks");

        public string PluginsEnabledSourceLabel => FormatSourceLabel(PluginsEnabledSource, "features.plugins");

        public string ErrorOnToolCollisionsSourceLabel => FormatSourceLabel(ErrorOnToolCollisionsSource, "features.tool_registry.error_on_tool_collisions");

        public string MentionsV2EnabledSourceLabel => FormatSourceLabel(MentionsV2EnabledSource, "features.mentions_v2");

        public string SkillMcpDependencyInstallEnabledSourceLabel => FormatSourceLabel(SkillMcpDependencyInstallEnabledSource, "features.skill_mcp_dependency_install");

        public string GoalsEnabledSourceLabel => FormatSourceLabel(GoalsEnabledSource, "features.goals");

        public string DefaultModeRequestUserInputEnabledSourceLabel => FormatSourceLabel(DefaultModeRequestUserInputEnabledSource, "features.default_mode_request_user_input");

        public string ExperimentalRequestUserInputEnabledSourceLabel => FormatSourceLabel(ExperimentalRequestUserInputEnabledSource, "tools.experimental_request_user_input.enabled");

        public string UpdatePlanEnabledSourceLabel => FormatSourceLabel(UpdatePlanEnabledSource, "tools.update_plan.enabled");

        public string IncludePermissionsInstructionsSourceLabel => FormatSourceLabel(IncludePermissionsInstructionsSource, "include_permissions_instructions");

        public string IncludeCollaborationModeInstructionsSourceLabel => FormatSourceLabel(IncludeCollaborationModeInstructionsSource, "include_collaboration_mode_instructions");

        public string IncludeEnvironmentContextSourceLabel => FormatSourceLabel(IncludeEnvironmentContextSource, "include_environment_context");

        public string IncludeSkillInstructionsSourceLabel => FormatSourceLabel(IncludeSkillInstructionsSource, "skills.include_instructions");

        public string MultiAgentEnabledSourceLabel => FormatSourceLabel(MultiAgentEnabledSource, "features.multi_agent");

        public bool EffectiveAgentsEnabled => ConfiguredMultiAgentEnabled
            && ConfiguredAgentsEnabled;

        public string AgentsEnabledSourceLabel => FormatSourceLabel(AgentsEnabledSource, "agents.enabled");

        public string InterruptMessageSourceLabel => FormatSourceLabel(InterruptMessageSource, "agents.interrupt_message");

        public string MaximumConcurrentSubagentRunsSourceLabel => FormatSourceLabel(MaximumConcurrentSubagentRunsSource, "agents concurrency");

        public string DefaultSubagentModelSourceLabel => FormatSourceLabel(DefaultSubagentModelSource, "agents.default_subagent_model");

        public string DefaultSubagentReasoningEffortSourceLabel => FormatSourceLabel(DefaultSubagentReasoningEffortSource, "agents.default_subagent_reasoning_effort");

        public string ModelContextWindowSourceLabel => FormatSourceLabel(ModelContextWindowSource, "model_context_window");

        public string ToolOutputTokenLimitSourceLabel => FormatSourceLabel(ToolOutputTokenLimitSource, "tool_output_token_limit");

        public string ModelReasoningEffortSourceLabel => FormatSourceLabel(ModelReasoningEffortSource, "model_reasoning_effort");

        public string PlanModeReasoningEffortSourceLabel => FormatSourceLabel(PlanModeReasoningEffortSource, "plan_mode_reasoning_effort");

        public string ModelReasoningSummarySourceLabel => FormatSourceLabel(ModelReasoningSummarySource, "model_reasoning_summary");

        public string ModelSupportsReasoningSummariesSourceLabel => FormatSourceLabel(ModelSupportsReasoningSummariesSource, "model_supports_reasoning_summaries");

        public string HideAgentReasoningSourceLabel => FormatSourceLabel(HideAgentReasoningSource, "hide_agent_reasoning");

        public string ServiceTierSourceLabel => FormatSourceLabel(ServiceTierSource, "service_tier");

        public string FastModeEnabledSourceLabel => FormatSourceLabel(FastModeEnabledSource, "features.fast_mode");

        public string ModelVerbositySourceLabel => FormatSourceLabel(ModelVerbositySource, "model_verbosity");

        public string ModelAutoCompactTokenLimitSourceLabel => FormatSourceLabel(ModelAutoCompactTokenLimitSource, "model_auto_compact_token_limit");

        public string ModelAutoCompactTokenLimitScopeSourceLabel => FormatSourceLabel(ModelAutoCompactTokenLimitScopeSource, "model_auto_compact_token_limit_scope");

        public string ModelInstructionsSourceLabel
        {
            get
            {
                var layer = ModelInstructionsSource switch
                {
                    CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml",
                    CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml",
                    _ => string.Empty,
                };
                if (layer.Length == 0)
                    return string.Empty;
                return layer + (ModelInstructionsUsesFile
                    ? " model_instructions_file"
                    : " instructions");
            }
        }

        public string CompactPromptSourceLabel
        {
            get
            {
                var layer = CompactPromptSource switch
                {
                    CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml",
                    CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml",
                    _ => string.Empty,
                };
                if (layer.Length == 0)
                    return string.Empty;
                return layer + (CompactPromptUsesFile
                    ? " experimental_compact_prompt_file"
                    : " compact_prompt");
            }
        }

        public string ProjectTrustLabel => ProjectTrustLevel switch
        {
            CopilotCodexProjectTrustLevel.Trusted => "Codex Home trust_level=trusted",
            CopilotCodexProjectTrustLevel.Untrusted =>
                "Codex Home trust_level=untrusted；已跳过项目 .codex/config.toml",
            CopilotCodexProjectTrustLevel.Invalid =>
                "Codex Home trust_level 无效；已保守跳过项目 .codex/config.toml",
            _ => "项目目录信任未决定；已跳过项目 .codex/config.toml",
        };

        private static string FormatSourceLabel(
            CopilotProjectInstructionConfigSources source,
            string configKey = "",
            bool allowCombined = false)
        {
            var prefix = source switch
            {
                CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml",
                CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml",
                CopilotProjectInstructionConfigSources.CodexHome
                    | CopilotProjectInstructionConfigSources.TrustedProject when allowCombined =>
                    "Codex Home + 受信项目 .codex/config.toml",
                _ => string.Empty,
            };
            return prefix.Length == 0 || configKey.Length == 0
                ? prefix
                : $"{prefix} {configKey}";
        }
    }

    internal static class CopilotProjectInstructionDiscoveryConfig
    {
        internal const int DefaultMaximumBytes = 32 * 1024;
        internal const int MinimumMaximumBytes = 0;
        internal const int MaximumMaximumBytes = 64 * 1024;
        internal const int MaximumProjectRootMarkers = 16;
        private const int MaximumProjectRootMarkerCharacters = 128;
        internal const int MaximumDeveloperInstructionCharacters = 64 * 1024;
        internal const int MaximumAutoReviewPolicyCharacters = 64 * 1024;
        internal const int MaximumModelInstructionCharacters = 64 * 1024;
        internal const int MaximumCompactPromptCharacters = 32 * 1024;
        internal const int MaximumConfiguredExecPolicyRules = 256;
        internal static IReadOnlyList<string> DefaultProjectRootMarkers { get; } =
            Array.AsReadOnly([".git"]);

        public static CopilotProjectInstructionDiscoveryOptions CreateDefault() =>
            new(
                DefaultMaximumBytes,
                Array.Empty<string>(),
                HasMaximumBytesOverride: false,
                HasFallbackFileNamesOverride: false);

        internal static string NormalizeProjectRootMarker(string? value)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (normalized.Length == 0
                || normalized.Length > MaximumProjectRootMarkerCharacters
                || normalized is "." or ".."
                || Path.IsPathRooted(normalized)
                || normalized.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || normalized.Contains(Path.DirectorySeparatorChar)
                || normalized.Contains(Path.AltDirectorySeparatorChar))
            {
                return string.Empty;
            }
            return normalized;
        }
    }
}
