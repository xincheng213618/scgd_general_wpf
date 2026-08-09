using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

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

    internal enum CopilotCodexWebSearchConfigKey
    {
        None,
        WebSearch,
        FeaturesWebSearch,
        FeaturesWebSearchCached,
        FeaturesWebSearchRequest,
    }

    internal readonly record struct CopilotCodexWebSearchModeResolution(
        CopilotCodexWebSearchMode Mode,
        bool HasOverride,
        CopilotCodexWebSearchConfigKey ConfigKey,
        CopilotProjectInstructionConfigSources Source);

    internal readonly record struct CopilotCodexWebSearchConfigState(
        bool HasCanonicalModeOverride,
        CopilotCodexWebSearchMode CanonicalMode,
        CopilotProjectInstructionConfigSources CanonicalModeSource,
        bool? LegacyWebSearchEnabled,
        CopilotProjectInstructionConfigSources LegacyWebSearchSource,
        bool? LegacyWebSearchCachedEnabled,
        CopilotProjectInstructionConfigSources LegacyWebSearchCachedSource,
        bool? LegacyWebSearchRequestEnabled,
        CopilotProjectInstructionConfigSources LegacyWebSearchRequestSource)
    {
        public bool HasAnyAssignment => HasCanonicalModeOverride
            || LegacyWebSearchEnabled.HasValue
            || LegacyWebSearchCachedEnabled.HasValue
            || LegacyWebSearchRequestEnabled.HasValue;

        public CopilotCodexWebSearchConfigState Apply(
            CopilotCodexWebSearchConfigState layer,
            CopilotProjectInstructionConfigSources source) => this with
        {
            HasCanonicalModeOverride = layer.HasCanonicalModeOverride
                || HasCanonicalModeOverride,
            CanonicalMode = layer.HasCanonicalModeOverride
                ? layer.CanonicalMode
                : CanonicalMode,
            CanonicalModeSource = layer.HasCanonicalModeOverride
                ? source
                : CanonicalModeSource,
            LegacyWebSearchEnabled = layer.LegacyWebSearchEnabled.HasValue
                ? layer.LegacyWebSearchEnabled
                : LegacyWebSearchEnabled,
            LegacyWebSearchSource = layer.LegacyWebSearchEnabled.HasValue
                ? source
                : LegacyWebSearchSource,
            LegacyWebSearchCachedEnabled = layer.LegacyWebSearchCachedEnabled.HasValue
                ? layer.LegacyWebSearchCachedEnabled
                : LegacyWebSearchCachedEnabled,
            LegacyWebSearchCachedSource = layer.LegacyWebSearchCachedEnabled.HasValue
                ? source
                : LegacyWebSearchCachedSource,
            LegacyWebSearchRequestEnabled = layer.LegacyWebSearchRequestEnabled.HasValue
                ? layer.LegacyWebSearchRequestEnabled
                : LegacyWebSearchRequestEnabled,
            LegacyWebSearchRequestSource = layer.LegacyWebSearchRequestEnabled.HasValue
                ? source
                : LegacyWebSearchRequestSource,
        };

        public CopilotCodexWebSearchModeResolution Resolve()
        {
            if (HasCanonicalModeOverride)
            {
                return new(
                    CanonicalMode,
                    HasOverride: true,
                    CopilotCodexWebSearchConfigKey.WebSearch,
                    CanonicalModeSource);
            }

            if (LegacyWebSearchCachedEnabled is true)
            {
                return new(
                    CopilotCodexWebSearchMode.Cached,
                    HasOverride: true,
                    CopilotCodexWebSearchConfigKey.FeaturesWebSearchCached,
                    LegacyWebSearchCachedSource);
            }

            if (LegacyWebSearchRequestEnabled.HasValue)
            {
                return LegacyWebSearchRequestEnabled.Value
                    ? new(
                        CopilotCodexWebSearchMode.Live,
                        HasOverride: true,
                        CopilotCodexWebSearchConfigKey.FeaturesWebSearchRequest,
                        LegacyWebSearchRequestSource)
                    : default;
            }

            return LegacyWebSearchEnabled is true
                ? new(
                    CopilotCodexWebSearchMode.Live,
                    HasOverride: true,
                    CopilotCodexWebSearchConfigKey.FeaturesWebSearch,
                    LegacyWebSearchSource)
                : default;
        }
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

        public static string GetConfigKey(CopilotCodexWebSearchConfigKey key) => key switch
        {
            CopilotCodexWebSearchConfigKey.WebSearch => "web_search",
            CopilotCodexWebSearchConfigKey.FeaturesWebSearch => "features.web_search",
            CopilotCodexWebSearchConfigKey.FeaturesWebSearchCached => "features.web_search_cached",
            CopilotCodexWebSearchConfigKey.FeaturesWebSearchRequest => "features.web_search_request",
            _ => "web_search",
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

        public IReadOnlyList<string> AppliedHookFilePaths { get; init; } = Array.Empty<string>();

        internal IReadOnlyList<CopilotCodexCommandHookDefinition> ConfiguredCommandHooks { get; init; } =
            Array.Empty<CopilotCodexCommandHookDefinition>();

        internal IReadOnlyList<CopilotCodexConfiguredHookIssue> ConfiguredHookIssues { get; init; } =
            Array.Empty<CopilotCodexConfiguredHookIssue>();

        internal IReadOnlyList<CopilotCodexCustomSubagentDefinition> CustomSubagents { get; init; } =
            Array.Empty<CopilotCodexCustomSubagentDefinition>();

        internal IReadOnlyList<CopilotCodexCustomSubagentDiscoveryIssue> CustomSubagentDiscoveryIssues { get; init; } =
            Array.Empty<CopilotCodexCustomSubagentDiscoveryIssue>();

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

        public CopilotCodexWebSearchConfigKey WebSearchModeConfigKey { get; init; } =
            CopilotCodexWebSearchConfigKey.None;

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
            || ConfiguredCommandHooks.Count > 0
            || ConfiguredHookIssues.Count > 0;

        public string ConfigSourceLabel => ConfigSources switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml",
            CopilotProjectInstructionConfigSources.CodexHome | CopilotProjectInstructionConfigSources.TrustedProject =>
                "Codex Home + 受信项目 .codex/config.toml",
            _ => UsesCodexConfig ? "Codex config.toml" : "ColorVision 默认",
        };

        public bool AllowsProjectCodexConfig => ProjectTrustLevel == CopilotCodexProjectTrustLevel.Trusted;

        public string DeveloperInstructionsSourceLabel => DeveloperInstructionsSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml",
            _ => string.Empty,
        };

        public string PersonalitySourceLabel => PersonalitySource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml personality",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml personality",
            _ => string.Empty,
        };

        public string PersonalityEnabledSourceLabel => PersonalityEnabledSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome =>
                "Codex Home config.toml features.personality",
            CopilotProjectInstructionConfigSources.TrustedProject =>
                "受信项目 .codex/config.toml features.personality",
            _ => string.Empty,
        };

        public string WebSearchModeSourceLabel
        {
            get
            {
                var configKey = CopilotCodexWebSearchModeSelection.GetConfigKey(WebSearchModeConfigKey);
                if (configKey.Length == 0)
                    return string.Empty;
                return WebSearchModeSource switch
                {
                    CopilotProjectInstructionConfigSources.CodexHome => $"Codex Home config.toml {configKey}",
                    CopilotProjectInstructionConfigSources.TrustedProject => $"受信项目 .codex/config.toml {configKey}",
                    _ => string.Empty,
                };
            }
        }

        public string SandboxModeSourceLabel => SandboxModeSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml sandbox_mode",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml sandbox_mode",
            _ => string.Empty,
        };

        public string ApprovalPolicySourceLabel => ApprovalPolicySource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml approval_policy",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml approval_policy",
            _ => string.Empty,
        };

        public string ShellEnvironmentPolicySourceLabel => ShellEnvironmentPolicySources switch
        {
            CopilotProjectInstructionConfigSources.CodexHome =>
                "Codex Home config.toml shell_environment_policy",
            CopilotProjectInstructionConfigSources.TrustedProject =>
                "受信项目 .codex/config.toml shell_environment_policy",
            CopilotProjectInstructionConfigSources.CodexHome
                | CopilotProjectInstructionConfigSources.TrustedProject =>
                "Codex Home + 受信项目 .codex/config.toml shell_environment_policy",
            _ => string.Empty,
        };

        public string ApprovalsReviewerSourceLabel => ApprovalsReviewerSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml approvals_reviewer",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml approvals_reviewer",
            _ => string.Empty,
        };

        public string GuardianApprovalEnabledSourceLabel => GuardianApprovalEnabledSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml features.guardian_approval",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml features.guardian_approval",
            _ => string.Empty,
        };

        public string AutoReviewPolicySourceLabel => AutoReviewPolicySource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml auto_review.policy",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml auto_review.policy",
            _ => string.Empty,
        };

        public string ModelSourceLabel => ModelSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml model",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml model",
            _ => string.Empty,
        };

        public string ReviewModelSourceLabel => ReviewModelSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml review_model",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml review_model",
            _ => string.Empty,
        };

        public string PreventIdleSleepSourceLabel => PreventIdleSleepSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml features.prevent_idle_sleep",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml features.prevent_idle_sleep",
            _ => string.Empty,
        };

        public string ShellToolEnabledSourceLabel => ShellToolEnabledSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml features.shell_tool",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml features.shell_tool",
            _ => string.Empty,
        };

        public string HooksEnabledSourceLabel => HooksEnabledSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml features.hooks",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml features.hooks",
            _ => string.Empty,
        };

        public string PluginsEnabledSourceLabel => PluginsEnabledSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml features.plugins",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml features.plugins",
            _ => string.Empty,
        };

        public string ErrorOnToolCollisionsSourceLabel => ErrorOnToolCollisionsSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome =>
                "Codex Home config.toml features.tool_registry.error_on_tool_collisions",
            CopilotProjectInstructionConfigSources.TrustedProject =>
                "受信项目 .codex/config.toml features.tool_registry.error_on_tool_collisions",
            _ => string.Empty,
        };

        public string MentionsV2EnabledSourceLabel => MentionsV2EnabledSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml features.mentions_v2",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml features.mentions_v2",
            _ => string.Empty,
        };

        public string SkillMcpDependencyInstallEnabledSourceLabel => SkillMcpDependencyInstallEnabledSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml features.skill_mcp_dependency_install",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml features.skill_mcp_dependency_install",
            _ => string.Empty,
        };

        public string GoalsEnabledSourceLabel => GoalsEnabledSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml features.goals",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml features.goals",
            _ => string.Empty,
        };

        public string DefaultModeRequestUserInputEnabledSourceLabel => DefaultModeRequestUserInputEnabledSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml features.default_mode_request_user_input",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml features.default_mode_request_user_input",
            _ => string.Empty,
        };

        public string ExperimentalRequestUserInputEnabledSourceLabel => ExperimentalRequestUserInputEnabledSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml tools.experimental_request_user_input.enabled",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml tools.experimental_request_user_input.enabled",
            _ => string.Empty,
        };

        public string UpdatePlanEnabledSourceLabel => UpdatePlanEnabledSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml tools.update_plan.enabled",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml tools.update_plan.enabled",
            _ => string.Empty,
        };

        public string IncludePermissionsInstructionsSourceLabel => IncludePermissionsInstructionsSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml include_permissions_instructions",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml include_permissions_instructions",
            _ => string.Empty,
        };

        public string IncludeCollaborationModeInstructionsSourceLabel => IncludeCollaborationModeInstructionsSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml include_collaboration_mode_instructions",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml include_collaboration_mode_instructions",
            _ => string.Empty,
        };

        public string IncludeEnvironmentContextSourceLabel => IncludeEnvironmentContextSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml include_environment_context",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml include_environment_context",
            _ => string.Empty,
        };

        public string IncludeSkillInstructionsSourceLabel => IncludeSkillInstructionsSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml skills.include_instructions",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml skills.include_instructions",
            _ => string.Empty,
        };

        public string MultiAgentEnabledSourceLabel => MultiAgentEnabledSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome =>
                "Codex Home config.toml features.multi_agent",
            CopilotProjectInstructionConfigSources.TrustedProject =>
                "受信项目 .codex/config.toml features.multi_agent",
            _ => string.Empty,
        };

        public bool EffectiveAgentsEnabled => ConfiguredMultiAgentEnabled
            && ConfiguredAgentsEnabled;

        public string AgentsEnabledSourceLabel => AgentsEnabledSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml agents.enabled",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml agents.enabled",
            _ => string.Empty,
        };

        public string InterruptMessageSourceLabel => InterruptMessageSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml agents.interrupt_message",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml agents.interrupt_message",
            _ => string.Empty,
        };

        public string MaximumConcurrentSubagentRunsSourceLabel => MaximumConcurrentSubagentRunsSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml agents concurrency",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml agents concurrency",
            _ => string.Empty,
        };

        public string DefaultSubagentModelSourceLabel => DefaultSubagentModelSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml agents.default_subagent_model",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml agents.default_subagent_model",
            _ => string.Empty,
        };

        public string DefaultSubagentReasoningEffortSourceLabel => DefaultSubagentReasoningEffortSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml agents.default_subagent_reasoning_effort",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml agents.default_subagent_reasoning_effort",
            _ => string.Empty,
        };

        public string ModelContextWindowSourceLabel => ModelContextWindowSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml model_context_window",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml model_context_window",
            _ => string.Empty,
        };

        public string ToolOutputTokenLimitSourceLabel => ToolOutputTokenLimitSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml tool_output_token_limit",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml tool_output_token_limit",
            _ => string.Empty,
        };

        public string ModelReasoningEffortSourceLabel => ModelReasoningEffortSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml model_reasoning_effort",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml model_reasoning_effort",
            _ => string.Empty,
        };

        public string PlanModeReasoningEffortSourceLabel => PlanModeReasoningEffortSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml plan_mode_reasoning_effort",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml plan_mode_reasoning_effort",
            _ => string.Empty,
        };

        public string ModelReasoningSummarySourceLabel => ModelReasoningSummarySource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml model_reasoning_summary",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml model_reasoning_summary",
            _ => string.Empty,
        };

        public string ModelSupportsReasoningSummariesSourceLabel => ModelSupportsReasoningSummariesSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml model_supports_reasoning_summaries",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml model_supports_reasoning_summaries",
            _ => string.Empty,
        };

        public string HideAgentReasoningSourceLabel => HideAgentReasoningSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml hide_agent_reasoning",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml hide_agent_reasoning",
            _ => string.Empty,
        };

        public string ServiceTierSourceLabel => ServiceTierSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml service_tier",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml service_tier",
            _ => string.Empty,
        };

        public string FastModeEnabledSourceLabel => FastModeEnabledSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome =>
                "Codex Home config.toml features.fast_mode",
            CopilotProjectInstructionConfigSources.TrustedProject =>
                "受信项目 .codex/config.toml features.fast_mode",
            _ => string.Empty,
        };

        public string ModelVerbositySourceLabel => ModelVerbositySource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml model_verbosity",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml model_verbosity",
            _ => string.Empty,
        };

        public string ModelAutoCompactTokenLimitSourceLabel => ModelAutoCompactTokenLimitSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml model_auto_compact_token_limit",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml model_auto_compact_token_limit",
            _ => string.Empty,
        };

        public string ModelAutoCompactTokenLimitScopeSourceLabel => ModelAutoCompactTokenLimitScopeSource switch
        {
            CopilotProjectInstructionConfigSources.CodexHome => "Codex Home config.toml model_auto_compact_token_limit_scope",
            CopilotProjectInstructionConfigSources.TrustedProject => "受信项目 .codex/config.toml model_auto_compact_token_limit_scope",
            _ => string.Empty,
        };

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
    }

    internal sealed record CopilotCodexHomeConfigSnapshot(
        string Source,
        CopilotProjectInstructionDiscoveryOptions Options);

    internal static partial class CopilotProjectInstructionDiscoveryConfig
    {
        internal const int DefaultMaximumBytes = 32 * 1024;
        internal const int MinimumMaximumBytes = 0;
        internal const int MaximumMaximumBytes = 64 * 1024;
        internal static IReadOnlyList<string> DefaultProjectRootMarkers { get; } =
            Array.AsReadOnly([".git"]);

        private const int MaximumConfigBytes = 256 * 1024;
        private const int MaximumFallbackFileNames = 16;
        private const int MaximumFallbackFileNameCharacters = 128;
        internal const int MaximumProjectRootMarkers = 16;
        private const int MaximumProjectRootMarkerCharacters = 128;
        private const int MaximumPersonalityCharacters = 32;
        private const int MaximumLogicalValueLines = 64;
        internal const int MaximumDeveloperInstructionCharacters = 64 * 1024;
        internal const int MaximumAutoReviewPolicyCharacters = 64 * 1024;
        private const int MaximumConfiguredTextLines = 512;
        internal const int MaximumModelInstructionCharacters = 64 * 1024;
        private const int MaximumModelInstructionBytes = 256 * 1024;
        internal const int MaximumCompactPromptCharacters = 32 * 1024;
        private const int MaximumCompactPromptBytes = 128 * 1024;
        private const int MaximumConfigReferencedPathCharacters = 2_048;
        private const string ConfigFileName = "config.toml";
        private const string MaximumBytesKey = "project_doc_max_bytes";
        private const string FallbackFileNamesKey = "project_doc_fallback_filenames";
        private const string ProjectRootMarkersKey = "project_root_markers";
        private const string DeveloperInstructionsKey = "developer_instructions";
        private const string PersonalityKey = "personality";
        private const string PersonalityEnabledKey = "features.personality";
        private const string PersonalityEnabledFeatureKey = "personality";
        private const string WebSearchKey = "web_search";
        private const string LegacyWebSearchKey = "features.web_search";
        private const string LegacyWebSearchFeatureKey = "web_search";
        private const string LegacyWebSearchCachedKey = "features.web_search_cached";
        private const string LegacyWebSearchCachedFeatureKey = "web_search_cached";
        private const string LegacyWebSearchRequestKey = "features.web_search_request";
        private const string LegacyWebSearchRequestFeatureKey = "web_search_request";
        private const string SandboxModeKey = "sandbox_mode";
        private const string ApprovalPolicyKey = "approval_policy";
        private const string ApprovalsReviewerKey = "approvals_reviewer";
        private const string GuardianApprovalEnabledKey = "features.guardian_approval";
        private const string GuardianApprovalEnabledFeatureKey = "guardian_approval";
        private const string AutoReviewPolicyKey = "auto_review.policy";
        private const string AutoReviewPolicyTableKey = "policy";
        private const string ModelKey = "model";
        private const string ReviewModelKey = "review_model";
        private const string PreventIdleSleepKey = "features.prevent_idle_sleep";
        private const string PreventIdleSleepFeatureKey = "prevent_idle_sleep";
        private const string ShellToolEnabledKey = "features.shell_tool";
        private const string ShellToolEnabledFeatureKey = "shell_tool";
        private const string HooksEnabledKey = "features.hooks";
        private const string HooksEnabledFeatureKey = "hooks";
        private const string PluginsEnabledKey = "features.plugins";
        private const string PluginsEnabledFeatureKey = "plugins";
        private const string ErrorOnToolCollisionsKey = "features.tool_registry.error_on_tool_collisions";
        private const string ErrorOnToolCollisionsTableKey = "error_on_tool_collisions";
        private const string MentionsV2EnabledKey = "features.mentions_v2";
        private const string MentionsV2EnabledFeatureKey = "mentions_v2";
        private const string SkillMcpDependencyInstallEnabledKey = "features.skill_mcp_dependency_install";
        private const string SkillMcpDependencyInstallEnabledFeatureKey = "skill_mcp_dependency_install";
        private const string GoalsEnabledKey = "features.goals";
        private const string GoalsEnabledFeatureKey = "goals";
        private const string DefaultModeRequestUserInputEnabledKey = "features.default_mode_request_user_input";
        private const string DefaultModeRequestUserInputEnabledFeatureKey = "default_mode_request_user_input";
        private const string ExperimentalRequestUserInputEnabledKey = "tools.experimental_request_user_input.enabled";
        private const string UpdatePlanEnabledKey = "tools.update_plan.enabled";
        private const string ToolsEnabledTableKey = "enabled";
        private const string IncludePermissionsInstructionsKey = "include_permissions_instructions";
        private const string IncludeCollaborationModeInstructionsKey = "include_collaboration_mode_instructions";
        private const string IncludeEnvironmentContextKey = "include_environment_context";
        private const string SkillsIncludeInstructionsKey = "skills.include_instructions";
        private const string SkillsIncludeInstructionsTableKey = "include_instructions";
        private const string MultiAgentEnabledKey = "features.multi_agent";
        private const string MultiAgentEnabledFeatureKey = "multi_agent";
        private const string AgentsEnabledKey = "agents.enabled";
        private const string AgentsEnabledTableKey = "enabled";
        private const string AgentsInterruptMessageKey = "agents.interrupt_message";
        private const string AgentsInterruptMessageTableKey = "interrupt_message";
        private const string AgentsMaximumConcurrentThreadsKey = "agents.max_concurrent_threads_per_session";
        private const string AgentsMaximumConcurrentThreadsTableKey = "max_concurrent_threads_per_session";
        private const string AgentsMaximumThreadsKey = "agents.max_threads";
        private const string AgentsMaximumThreadsTableKey = "max_threads";
        private const string AgentsDefaultSubagentModelKey = "agents.default_subagent_model";
        private const string AgentsDefaultSubagentModelTableKey = "default_subagent_model";
        private const string AgentsDefaultSubagentReasoningEffortKey = "agents.default_subagent_reasoning_effort";
        private const string AgentsDefaultSubagentReasoningEffortTableKey = "default_subagent_reasoning_effort";
        private const string ModelContextWindowKey = "model_context_window";
        private const string ToolOutputTokenLimitKey = "tool_output_token_limit";
        private const string ModelReasoningEffortKey = "model_reasoning_effort";
        private const string PlanModeReasoningEffortKey = "plan_mode_reasoning_effort";
        private const string ModelReasoningSummaryKey = "model_reasoning_summary";
        private const string ModelSupportsReasoningSummariesKey = "model_supports_reasoning_summaries";
        private const string HideAgentReasoningKey = "hide_agent_reasoning";
        private const string FastModeEnabledKey = "features.fast_mode";
        private const string FastModeEnabledFeatureKey = "fast_mode";
        private const string ServiceTierKey = "service_tier";
        private const string ModelVerbosityKey = "model_verbosity";
        private const string ModelAutoCompactTokenLimitKey = "model_auto_compact_token_limit";
        private const string ModelAutoCompactTokenLimitScopeKey = "model_auto_compact_token_limit_scope";
        private const string ModelInstructionsKey = "instructions";
        private const string ModelInstructionsFileKey = "model_instructions_file";
        private const string CompactPromptKey = "compact_prompt";
        private const string ExperimentalCompactPromptFileKey = "experimental_compact_prompt_file";
        private const string ProjectsTablePrefix = "projects.";
        private const string TrustLevelKey = "trust_level";

        public static CopilotProjectInstructionDiscoveryOptions Load(string? globalInstructionRootPath)
            => LoadCodexHome(globalInstructionRootPath).Options;

        public static CopilotProjectInstructionDiscoveryOptions Load(
            string? globalInstructionRootPath,
            string? trustedProjectRootPath)
            => LoadTrustedProjectLayer(LoadCodexHome(globalInstructionRootPath), trustedProjectRootPath);

        internal static CopilotCodexHomeConfigSnapshot LoadCodexHome(string? globalInstructionRootPath)
        {
            var options = CreateDefault();
            var normalizedRoot = CopilotAgentProjectInstructions.NormalizeGlobalInstructionRootPath(globalInstructionRootPath);
            var globalSource = string.Empty;
            var globalConfigPath = Path.Combine(normalizedRoot, ConfigFileName);
            if (normalizedRoot.Length > 0
                && TryReadConfigSource(normalizedRoot, globalConfigPath, out globalSource)
                && TryParseInstructionLayer(globalSource, out var globalLayer))
            {
                globalLayer = ResolveCompactPromptFile(
                    globalLayer,
                    globalConfigPath,
                    normalizedRoot,
                    allowOutsideRoot: true);
                globalLayer = ResolveModelInstructionsFile(
                    globalLayer,
                    globalConfigPath,
                    normalizedRoot,
                    allowOutsideRoot: true);
                options = ApplyLayer(
                    options,
                    globalLayer,
                    CopilotProjectInstructionConfigSources.CodexHome,
                    includeProjectRootMarkers: true);
            }

            var customSubagents = DiscoverCodexHomeCustomSubagents(
                normalizedRoot,
                globalSource,
                globalConfigPath,
                out var customSubagentDiscoveryIssues);
            var configuredHooks = normalizedRoot.Length == 0
                ? CopilotCodexConfiguredHookDiscoveryResult.Empty
                : DiscoverConfiguredHookFile(
                    normalizedRoot,
                    Path.Combine(normalizedRoot, HooksFileName),
                    CopilotProjectInstructionConfigSources.CodexHome,
                    startingOrder: 0);
            options = options with
            {
                CustomSubagents = customSubagents,
                CustomSubagentDiscoveryIssues = customSubagentDiscoveryIssues,
                ConfiguredCommandHooks = configuredHooks.CommandHooks,
                ConfiguredHookIssues = configuredHooks.Issues,
                AppliedHookFilePaths = configuredHooks.SourceFilePaths,
            };

            return new CopilotCodexHomeConfigSnapshot(globalSource, options);
        }

        internal static CopilotProjectInstructionDiscoveryOptions LoadTrustedProjectLayer(
            CopilotCodexHomeConfigSnapshot codexHome,
            string? trustedProjectRootPath)
            => LoadTrustedProjectLayers(codexHome, trustedProjectRootPath, trustedProjectRootPath);

        internal static CopilotProjectInstructionDiscoveryOptions LoadTrustedProjectLayers(
            CopilotCodexHomeConfigSnapshot codexHome,
            string? trustedProjectRootPath,
            string? workingDirectoryPath)
        {
            ArgumentNullException.ThrowIfNull(codexHome);
            var options = codexHome.Options;
            var normalizedProjectRoot = NormalizeTrustedProjectRootPath(trustedProjectRootPath);
            if (normalizedProjectRoot.Length == 0)
                return options;

            var projectTrustLevel = ResolveProjectTrustLevel(codexHome.Source, normalizedProjectRoot);
            options = options with { ProjectTrustLevel = projectTrustLevel };
            if (!options.AllowsProjectCodexConfig)
                return options;

            var configDirectories = EnumerateProjectConfigDirectories(
                normalizedProjectRoot,
                workingDirectoryPath);
            var appliedConfigFilePaths = new List<string>();
            var appliedHookFilePaths = options.AppliedHookFilePaths.ToList();
            var configuredCommandHooks = options.ConfiguredCommandHooks
                .Select(definition => definition.CreateSnapshot())
                .ToList();
            var configuredHookIssues = options.ConfiguredHookIssues.ToList();
            foreach (var directoryPath in configDirectories)
            {
                var configPath = Path.Combine(directoryPath, ".codex", ConfigFileName);
                var hasProjectConfig = TryReadConfigSource(
                    normalizedProjectRoot,
                    configPath,
                    out var projectSource);
                ProjectInstructionConfigLayer? projectLayer = null;
                if (hasProjectConfig
                    && TryParseInstructionLayer(projectSource, out var parsedProjectLayer))
                {
                    projectLayer = parsedProjectLayer;
                }
                var hasInstructionAssignments = projectLayer != null;
                var hasCustomSubagentDeclarations = hasProjectConfig
                    && HasValidCustomSubagentDeclarations(projectSource);
                var hasApplicableOverrides = false;

                if (hasInstructionAssignments && projectLayer != null)
                {
                    projectLayer = ResolveCompactPromptFile(
                        projectLayer,
                        configPath,
                        normalizedProjectRoot,
                        allowOutsideRoot: false);
                    projectLayer = ResolveModelInstructionsFile(
                        projectLayer,
                        configPath,
                        normalizedProjectRoot,
                        allowOutsideRoot: false);
                    hasApplicableOverrides = HasApplicableOverrides(
                        projectLayer,
                        includeProjectRootMarkers: false);
                    if (hasApplicableOverrides)
                    {
                        options = ApplyLayer(
                            options,
                            projectLayer,
                            CopilotProjectInstructionConfigSources.TrustedProject,
                        includeProjectRootMarkers: false);
                    }
                }
                if (hasApplicableOverrides || hasCustomSubagentDeclarations)
                    appliedConfigFilePaths.Add(Path.GetFullPath(configPath));

                var hookDiscovery = DiscoverConfiguredHookFile(
                    normalizedProjectRoot,
                    Path.Combine(directoryPath, ".codex", HooksFileName),
                    CopilotProjectInstructionConfigSources.TrustedProject,
                    configuredCommandHooks.Count);
                var remainingHookSlots = Math.Max(
                    0,
                    MaximumConfiguredHookHandlers - configuredCommandHooks.Count);
                configuredCommandHooks.AddRange(hookDiscovery.CommandHooks.Take(remainingHookSlots));
                configuredHookIssues.AddRange(hookDiscovery.Issues);
                if (hookDiscovery.CommandHooks.Count > remainingHookSlots)
                {
                    configuredHookIssues.Add(new CopilotCodexConfiguredHookIssue(
                        hookDiscovery.SourceFilePaths.FirstOrDefault() ?? directoryPath,
                        $"Configured command hooks are limited to {MaximumConfiguredHookHandlers} handlers across active layers."));
                }
                appliedHookFilePaths.AddRange(hookDiscovery.SourceFilePaths);
            }

            var customSubagents = ApplyTrustedProjectCustomSubagents(
                options.CustomSubagents,
                options.CustomSubagentDiscoveryIssues,
                normalizedProjectRoot,
                configDirectories,
                out var customSubagentDiscoveryIssues);
            options = options with
            {
                CustomSubagents = customSubagents,
                CustomSubagentDiscoveryIssues = customSubagentDiscoveryIssues,
                ConfiguredCommandHooks = configuredCommandHooks.ToArray(),
                ConfiguredHookIssues = configuredHookIssues.ToArray(),
                AppliedHookFilePaths = appliedHookFilePaths
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
            };

            return options with
            {
                AppliedProjectConfigFilePaths = appliedConfigFilePaths.ToArray(),
            };
        }

        public static CopilotProjectInstructionDiscoveryOptions CreateDefault() =>
            new(
                DefaultMaximumBytes,
                Array.Empty<string>(),
                HasMaximumBytesOverride: false,
                HasFallbackFileNamesOverride: false);

        private static CopilotProjectInstructionDiscoveryOptions ApplyLayer(
            CopilotProjectInstructionDiscoveryOptions current,
            ProjectInstructionConfigLayer layer,
            CopilotProjectInstructionConfigSources source,
            bool includeProjectRootMarkers)
        {
            var hasProjectRootMarkersOverride = includeProjectRootMarkers
                && layer.HasProjectRootMarkersOverride;
            if (!HasApplicableOverrides(layer, includeProjectRootMarkers))
                return current;

            var webSearchConfigState = current.WebSearchConfigState.Apply(
                layer.WebSearchConfigState,
                source);
            var webSearchResolution = webSearchConfigState.Resolve();

            return current with
            {
                MaximumBytes = layer.HasMaximumBytesOverride ? layer.MaximumBytes : current.MaximumBytes,
                FallbackFileNames = layer.HasFallbackFileNamesOverride ? layer.FallbackFileNames : current.FallbackFileNames,
                HasMaximumBytesOverride = current.HasMaximumBytesOverride || layer.HasMaximumBytesOverride,
                HasFallbackFileNamesOverride = current.HasFallbackFileNamesOverride || layer.HasFallbackFileNamesOverride,
                ConfigSources = current.ConfigSources | source,
                ProjectRootMarkers = hasProjectRootMarkersOverride
                    ? layer.ProjectRootMarkers
                    : current.ProjectRootMarkers,
                HasProjectRootMarkersOverride = current.HasProjectRootMarkersOverride
                    || hasProjectRootMarkersOverride,
                DeveloperInstructions = layer.HasDeveloperInstructionsOverride
                    ? layer.DeveloperInstructions
                    : current.DeveloperInstructions,
                HasDeveloperInstructionsOverride = current.HasDeveloperInstructionsOverride
                    || layer.HasDeveloperInstructionsOverride,
                DeveloperInstructionsSource = layer.HasDeveloperInstructionsOverride
                    ? source
                    : current.DeveloperInstructionsSource,
                ConfiguredPersonalityEnabled = layer.HasPersonalityEnabledOverride
                    ? layer.PersonalityEnabled
                    : current.ConfiguredPersonalityEnabled,
                HasPersonalityEnabledOverride = current.HasPersonalityEnabledOverride
                    || layer.HasPersonalityEnabledOverride,
                PersonalityEnabledSource = layer.HasPersonalityEnabledOverride
                    ? source
                    : current.PersonalityEnabledSource,
                ConfiguredPersonality = layer.HasPersonalityOverride
                    ? layer.Personality
                    : current.ConfiguredPersonality,
                HasPersonalityOverride = current.HasPersonalityOverride
                    || layer.HasPersonalityOverride,
                PersonalitySource = layer.HasPersonalityOverride
                    ? source
                    : current.PersonalitySource,
                ConfiguredWebSearchMode = webSearchResolution.Mode,
                HasWebSearchModeOverride = webSearchResolution.HasOverride,
                WebSearchModeSource = webSearchResolution.Source,
                WebSearchModeConfigKey = webSearchResolution.ConfigKey,
                WebSearchConfigState = webSearchConfigState,
                ConfiguredSandboxMode = layer.HasSandboxModeOverride
                    ? layer.SandboxMode
                    : current.ConfiguredSandboxMode,
                HasSandboxModeOverride = current.HasSandboxModeOverride
                    || layer.HasSandboxModeOverride,
                SandboxModeSource = layer.HasSandboxModeOverride
                    ? source
                    : current.SandboxModeSource,
                ConfiguredApprovalPolicy = layer.HasApprovalPolicyOverride
                    ? layer.ApprovalPolicy
                    : current.ConfiguredApprovalPolicy,
                HasApprovalPolicyOverride = current.HasApprovalPolicyOverride
                    || layer.HasApprovalPolicyOverride,
                ApprovalPolicySource = layer.HasApprovalPolicyOverride
                    ? source
                    : current.ApprovalPolicySource,
                ConfiguredApprovalsReviewer = layer.HasApprovalsReviewerOverride
                    ? layer.ApprovalsReviewer
                    : current.ConfiguredApprovalsReviewer,
                HasApprovalsReviewerOverride = current.HasApprovalsReviewerOverride
                    || layer.HasApprovalsReviewerOverride,
                ApprovalsReviewerSource = layer.HasApprovalsReviewerOverride
                    ? source
                    : current.ApprovalsReviewerSource,
                ConfiguredGuardianApprovalEnabled = layer.HasGuardianApprovalEnabledOverride
                    ? layer.GuardianApprovalEnabled
                    : current.ConfiguredGuardianApprovalEnabled,
                HasGuardianApprovalEnabledOverride = current.HasGuardianApprovalEnabledOverride
                    || layer.HasGuardianApprovalEnabledOverride,
                GuardianApprovalEnabledSource = layer.HasGuardianApprovalEnabledOverride
                    ? source
                    : current.GuardianApprovalEnabledSource,
                ConfiguredAutoReviewPolicy = layer.HasAutoReviewPolicyOverride
                    ? layer.AutoReviewPolicy
                    : current.ConfiguredAutoReviewPolicy,
                HasAutoReviewPolicyOverride = current.HasAutoReviewPolicyOverride
                    || layer.HasAutoReviewPolicyOverride,
                AutoReviewPolicySource = layer.HasAutoReviewPolicyOverride
                    ? source
                    : current.AutoReviewPolicySource,
                ConfiguredModel = layer.HasModelOverride
                    ? layer.Model
                    : current.ConfiguredModel,
                HasModelOverride = current.HasModelOverride
                    || layer.HasModelOverride,
                ModelSource = layer.HasModelOverride
                    ? source
                    : current.ModelSource,
                ConfiguredReviewModel = layer.HasReviewModelOverride
                    ? layer.ReviewModel
                    : current.ConfiguredReviewModel,
                HasReviewModelOverride = current.HasReviewModelOverride
                    || layer.HasReviewModelOverride,
                ReviewModelSource = layer.HasReviewModelOverride
                    ? source
                    : current.ReviewModelSource,
                ConfiguredPreventIdleSleep = layer.HasPreventIdleSleepOverride
                    ? layer.PreventIdleSleep
                    : current.ConfiguredPreventIdleSleep,
                HasPreventIdleSleepOverride = current.HasPreventIdleSleepOverride
                    || layer.HasPreventIdleSleepOverride,
                PreventIdleSleepSource = layer.HasPreventIdleSleepOverride
                    ? source
                    : current.PreventIdleSleepSource,
                ConfiguredShellToolEnabled = layer.HasShellToolEnabledOverride
                    ? layer.ShellToolEnabled
                    : current.ConfiguredShellToolEnabled,
                HasShellToolEnabledOverride = current.HasShellToolEnabledOverride
                    || layer.HasShellToolEnabledOverride,
                ShellToolEnabledSource = layer.HasShellToolEnabledOverride
                    ? source
                    : current.ShellToolEnabledSource,
                ConfiguredHooksEnabled = layer.HasHooksEnabledOverride
                    ? layer.HooksEnabled
                    : current.ConfiguredHooksEnabled,
                HasHooksEnabledOverride = current.HasHooksEnabledOverride
                    || layer.HasHooksEnabledOverride,
                HooksEnabledSource = layer.HasHooksEnabledOverride
                    ? source
                    : current.HooksEnabledSource,
                ConfiguredPluginsEnabled = layer.HasPluginsEnabledOverride
                    ? layer.PluginsEnabled
                    : current.ConfiguredPluginsEnabled,
                HasPluginsEnabledOverride = current.HasPluginsEnabledOverride
                    || layer.HasPluginsEnabledOverride,
                PluginsEnabledSource = layer.HasPluginsEnabledOverride
                    ? source
                    : current.PluginsEnabledSource,
                ConfiguredErrorOnToolCollisions = layer.HasErrorOnToolCollisionsOverride
                    ? layer.ErrorOnToolCollisions
                    : current.ConfiguredErrorOnToolCollisions,
                HasErrorOnToolCollisionsOverride = current.HasErrorOnToolCollisionsOverride
                    || layer.HasErrorOnToolCollisionsOverride,
                ErrorOnToolCollisionsSource = layer.HasErrorOnToolCollisionsOverride
                    ? source
                    : current.ErrorOnToolCollisionsSource,
                ConfiguredMentionsV2Enabled = layer.HasMentionsV2EnabledOverride
                    ? layer.MentionsV2Enabled
                    : current.ConfiguredMentionsV2Enabled,
                HasMentionsV2EnabledOverride = current.HasMentionsV2EnabledOverride
                    || layer.HasMentionsV2EnabledOverride,
                MentionsV2EnabledSource = layer.HasMentionsV2EnabledOverride
                    ? source
                    : current.MentionsV2EnabledSource,
                ConfiguredSkillMcpDependencyInstallEnabled = layer.HasSkillMcpDependencyInstallEnabledOverride
                    ? layer.SkillMcpDependencyInstallEnabled
                    : current.ConfiguredSkillMcpDependencyInstallEnabled,
                HasSkillMcpDependencyInstallEnabledOverride = current.HasSkillMcpDependencyInstallEnabledOverride
                    || layer.HasSkillMcpDependencyInstallEnabledOverride,
                SkillMcpDependencyInstallEnabledSource = layer.HasSkillMcpDependencyInstallEnabledOverride
                    ? source
                    : current.SkillMcpDependencyInstallEnabledSource,
                ConfiguredShellEnvironmentPolicy = layer.ShellEnvironmentPolicyLayer.HasAssignment
                    && current.ShellEnvironmentPolicyError.Length == 0
                        ? CopilotCodexShellEnvironmentPolicyMerge.Apply(
                            current.ConfiguredShellEnvironmentPolicy,
                            layer.ShellEnvironmentPolicyLayer)
                        : current.ConfiguredShellEnvironmentPolicy,
                HasShellEnvironmentPolicyOverride = current.HasShellEnvironmentPolicyOverride
                    || layer.ShellEnvironmentPolicyLayer.HasAssignment,
                ShellEnvironmentPolicySources = layer.ShellEnvironmentPolicyLayer.HasAssignment
                    ? current.ShellEnvironmentPolicySources | source
                    : current.ShellEnvironmentPolicySources,
                ShellEnvironmentPolicyError = layer.ShellEnvironmentPolicyLayer.HasAssignment
                    && !layer.ShellEnvironmentPolicyLayer.IsValid
                        ? layer.ShellEnvironmentPolicyLayer.ErrorMessage
                        : current.ShellEnvironmentPolicyError,
                ConfiguredGoalsEnabled = layer.HasGoalsEnabledOverride
                    ? layer.GoalsEnabled
                    : current.ConfiguredGoalsEnabled,
                HasGoalsEnabledOverride = current.HasGoalsEnabledOverride
                    || layer.HasGoalsEnabledOverride,
                GoalsEnabledSource = layer.HasGoalsEnabledOverride
                    ? source
                    : current.GoalsEnabledSource,
                ConfiguredDefaultModeRequestUserInputEnabled = layer.HasDefaultModeRequestUserInputEnabledOverride
                    ? layer.DefaultModeRequestUserInputEnabled
                    : current.ConfiguredDefaultModeRequestUserInputEnabled,
                HasDefaultModeRequestUserInputEnabledOverride = current.HasDefaultModeRequestUserInputEnabledOverride
                    || layer.HasDefaultModeRequestUserInputEnabledOverride,
                DefaultModeRequestUserInputEnabledSource = layer.HasDefaultModeRequestUserInputEnabledOverride
                    ? source
                    : current.DefaultModeRequestUserInputEnabledSource,
                ConfiguredExperimentalRequestUserInputEnabled = layer.HasExperimentalRequestUserInputEnabledOverride
                    ? layer.ExperimentalRequestUserInputEnabled
                    : current.ConfiguredExperimentalRequestUserInputEnabled,
                HasExperimentalRequestUserInputEnabledOverride = current.HasExperimentalRequestUserInputEnabledOverride
                    || layer.HasExperimentalRequestUserInputEnabledOverride,
                ExperimentalRequestUserInputEnabledSource = layer.HasExperimentalRequestUserInputEnabledOverride
                    ? source
                    : current.ExperimentalRequestUserInputEnabledSource,
                ConfiguredUpdatePlanEnabled = layer.HasUpdatePlanEnabledOverride
                    ? layer.UpdatePlanEnabled
                    : current.ConfiguredUpdatePlanEnabled,
                HasUpdatePlanEnabledOverride = current.HasUpdatePlanEnabledOverride
                    || layer.HasUpdatePlanEnabledOverride,
                UpdatePlanEnabledSource = layer.HasUpdatePlanEnabledOverride
                    ? source
                    : current.UpdatePlanEnabledSource,
                ConfiguredIncludePermissionsInstructions = layer.HasIncludePermissionsInstructionsOverride
                    ? layer.IncludePermissionsInstructions
                    : current.ConfiguredIncludePermissionsInstructions,
                HasIncludePermissionsInstructionsOverride = current.HasIncludePermissionsInstructionsOverride
                    || layer.HasIncludePermissionsInstructionsOverride,
                IncludePermissionsInstructionsSource = layer.HasIncludePermissionsInstructionsOverride
                    ? source
                    : current.IncludePermissionsInstructionsSource,
                ConfiguredIncludeCollaborationModeInstructions = layer.HasIncludeCollaborationModeInstructionsOverride
                    ? layer.IncludeCollaborationModeInstructions
                    : current.ConfiguredIncludeCollaborationModeInstructions,
                HasIncludeCollaborationModeInstructionsOverride = current.HasIncludeCollaborationModeInstructionsOverride
                    || layer.HasIncludeCollaborationModeInstructionsOverride,
                IncludeCollaborationModeInstructionsSource = layer.HasIncludeCollaborationModeInstructionsOverride
                    ? source
                    : current.IncludeCollaborationModeInstructionsSource,
                ConfiguredIncludeEnvironmentContext = layer.HasIncludeEnvironmentContextOverride
                    ? layer.IncludeEnvironmentContext
                    : current.ConfiguredIncludeEnvironmentContext,
                HasIncludeEnvironmentContextOverride = current.HasIncludeEnvironmentContextOverride
                    || layer.HasIncludeEnvironmentContextOverride,
                IncludeEnvironmentContextSource = layer.HasIncludeEnvironmentContextOverride
                    ? source
                    : current.IncludeEnvironmentContextSource,
                ConfiguredIncludeSkillInstructions = layer.HasIncludeSkillInstructionsOverride
                    ? layer.IncludeSkillInstructions
                    : current.ConfiguredIncludeSkillInstructions,
                HasIncludeSkillInstructionsOverride = current.HasIncludeSkillInstructionsOverride
                    || layer.HasIncludeSkillInstructionsOverride,
                IncludeSkillInstructionsSource = layer.HasIncludeSkillInstructionsOverride
                    ? source
                    : current.IncludeSkillInstructionsSource,
                ConfiguredMultiAgentEnabled = layer.HasMultiAgentEnabledOverride
                    ? layer.MultiAgentEnabled
                    : current.ConfiguredMultiAgentEnabled,
                HasMultiAgentEnabledOverride = current.HasMultiAgentEnabledOverride
                    || layer.HasMultiAgentEnabledOverride,
                MultiAgentEnabledSource = layer.HasMultiAgentEnabledOverride
                    ? source
                    : current.MultiAgentEnabledSource,
                ConfiguredAgentsEnabled = layer.HasAgentsEnabledOverride
                    ? layer.AgentsEnabled
                    : current.ConfiguredAgentsEnabled,
                HasAgentsEnabledOverride = current.HasAgentsEnabledOverride
                    || layer.HasAgentsEnabledOverride,
                AgentsEnabledSource = layer.HasAgentsEnabledOverride
                    ? source
                    : current.AgentsEnabledSource,
                ConfiguredInterruptMessageEnabled = layer.HasInterruptMessageOverride
                    ? layer.InterruptMessageEnabled
                    : current.ConfiguredInterruptMessageEnabled,
                HasInterruptMessageOverride = current.HasInterruptMessageOverride
                    || layer.HasInterruptMessageOverride,
                InterruptMessageSource = layer.HasInterruptMessageOverride
                    ? source
                    : current.InterruptMessageSource,
                ConfiguredMaximumConcurrentSubagentRuns = layer.HasMaximumConcurrentSubagentRunsOverride
                    ? layer.MaximumConcurrentSubagentRuns
                    : current.ConfiguredMaximumConcurrentSubagentRuns,
                HasMaximumConcurrentSubagentRunsOverride = current.HasMaximumConcurrentSubagentRunsOverride
                    || layer.HasMaximumConcurrentSubagentRunsOverride,
                MaximumConcurrentSubagentRunsSource = layer.HasMaximumConcurrentSubagentRunsOverride
                    ? source
                    : current.MaximumConcurrentSubagentRunsSource,
                ConfiguredDefaultSubagentModel = layer.HasDefaultSubagentModelOverride
                    ? layer.DefaultSubagentModel
                    : current.ConfiguredDefaultSubagentModel,
                HasDefaultSubagentModelOverride = current.HasDefaultSubagentModelOverride
                    || layer.HasDefaultSubagentModelOverride,
                DefaultSubagentModelSource = layer.HasDefaultSubagentModelOverride
                    ? source
                    : current.DefaultSubagentModelSource,
                ConfiguredDefaultSubagentReasoningEffort = layer.HasDefaultSubagentReasoningEffortOverride
                    ? layer.DefaultSubagentReasoningEffort
                    : current.ConfiguredDefaultSubagentReasoningEffort,
                HasDefaultSubagentReasoningEffortOverride = current.HasDefaultSubagentReasoningEffortOverride
                    || layer.HasDefaultSubagentReasoningEffortOverride,
                DefaultSubagentReasoningEffortSource = layer.HasDefaultSubagentReasoningEffortOverride
                    ? source
                    : current.DefaultSubagentReasoningEffortSource,
                ConfiguredModelContextWindowTokens = layer.HasModelContextWindowOverride
                    ? layer.ModelContextWindowTokens
                    : current.ConfiguredModelContextWindowTokens,
                HasModelContextWindowOverride = current.HasModelContextWindowOverride
                    || layer.HasModelContextWindowOverride,
                ModelContextWindowSource = layer.HasModelContextWindowOverride
                    ? source
                    : current.ModelContextWindowSource,
                ConfiguredToolOutputTokenLimit = layer.HasToolOutputTokenLimitOverride
                    ? layer.ToolOutputTokenLimit
                    : current.ConfiguredToolOutputTokenLimit,
                HasToolOutputTokenLimitOverride = current.HasToolOutputTokenLimitOverride
                    || layer.HasToolOutputTokenLimitOverride,
                ToolOutputTokenLimitSource = layer.HasToolOutputTokenLimitOverride
                    ? source
                    : current.ToolOutputTokenLimitSource,
                ConfiguredModelReasoningEffort = layer.HasModelReasoningEffortOverride
                    ? layer.ModelReasoningEffort
                    : current.ConfiguredModelReasoningEffort,
                HasModelReasoningEffortOverride = current.HasModelReasoningEffortOverride
                    || layer.HasModelReasoningEffortOverride,
                ModelReasoningEffortSource = layer.HasModelReasoningEffortOverride
                    ? source
                    : current.ModelReasoningEffortSource,
                ConfiguredPlanModeReasoningEffort = layer.HasPlanModeReasoningEffortOverride
                    ? layer.PlanModeReasoningEffort
                    : current.ConfiguredPlanModeReasoningEffort,
                HasPlanModeReasoningEffortOverride = current.HasPlanModeReasoningEffortOverride
                    || layer.HasPlanModeReasoningEffortOverride,
                PlanModeReasoningEffortSource = layer.HasPlanModeReasoningEffortOverride
                    ? source
                    : current.PlanModeReasoningEffortSource,
                ConfiguredModelReasoningSummary = layer.HasModelReasoningSummaryOverride
                    ? layer.ModelReasoningSummary
                    : current.ConfiguredModelReasoningSummary,
                HasModelReasoningSummaryOverride = current.HasModelReasoningSummaryOverride
                    || layer.HasModelReasoningSummaryOverride,
                ModelReasoningSummarySource = layer.HasModelReasoningSummaryOverride
                    ? source
                    : current.ModelReasoningSummarySource,
                ConfiguredModelSupportsReasoningSummaries = layer.HasModelSupportsReasoningSummariesOverride
                    ? layer.ModelSupportsReasoningSummaries
                    : current.ConfiguredModelSupportsReasoningSummaries,
                HasModelSupportsReasoningSummariesOverride = current.HasModelSupportsReasoningSummariesOverride
                    || layer.HasModelSupportsReasoningSummariesOverride,
                ModelSupportsReasoningSummariesSource = layer.HasModelSupportsReasoningSummariesOverride
                    ? source
                    : current.ModelSupportsReasoningSummariesSource,
                ConfiguredHideAgentReasoning = layer.HasHideAgentReasoningOverride
                    ? layer.HideAgentReasoning
                    : current.ConfiguredHideAgentReasoning,
                HasHideAgentReasoningOverride = current.HasHideAgentReasoningOverride
                    || layer.HasHideAgentReasoningOverride,
                HideAgentReasoningSource = layer.HasHideAgentReasoningOverride
                    ? source
                    : current.HideAgentReasoningSource,
                ConfiguredFastModeEnabled = layer.HasFastModeEnabledOverride
                    ? layer.FastModeEnabled
                    : current.ConfiguredFastModeEnabled,
                HasFastModeEnabledOverride = current.HasFastModeEnabledOverride
                    || layer.HasFastModeEnabledOverride,
                FastModeEnabledSource = layer.HasFastModeEnabledOverride
                    ? source
                    : current.FastModeEnabledSource,
                ConfiguredServiceTier = layer.HasServiceTierOverride
                    ? layer.ServiceTier
                    : current.ConfiguredServiceTier,
                HasServiceTierOverride = current.HasServiceTierOverride
                    || layer.HasServiceTierOverride,
                ServiceTierSource = layer.HasServiceTierOverride
                    ? source
                    : current.ServiceTierSource,
                ConfiguredModelVerbosity = layer.HasModelVerbosityOverride
                    ? layer.ModelVerbosity
                    : current.ConfiguredModelVerbosity,
                HasModelVerbosityOverride = current.HasModelVerbosityOverride
                    || layer.HasModelVerbosityOverride,
                ModelVerbositySource = layer.HasModelVerbosityOverride
                    ? source
                    : current.ModelVerbositySource,
                ConfiguredModelAutoCompactTokenLimit = layer.HasModelAutoCompactTokenLimitOverride
                    ? layer.ModelAutoCompactTokenLimit
                    : current.ConfiguredModelAutoCompactTokenLimit,
                HasModelAutoCompactTokenLimitOverride = current.HasModelAutoCompactTokenLimitOverride
                    || layer.HasModelAutoCompactTokenLimitOverride,
                ModelAutoCompactTokenLimitSource = layer.HasModelAutoCompactTokenLimitOverride
                    ? source
                    : current.ModelAutoCompactTokenLimitSource,
                ConfiguredModelAutoCompactTokenLimitScope = layer.HasModelAutoCompactTokenLimitScopeOverride
                    ? layer.ModelAutoCompactTokenLimitScope
                    : current.ConfiguredModelAutoCompactTokenLimitScope,
                HasModelAutoCompactTokenLimitScopeOverride = current.HasModelAutoCompactTokenLimitScopeOverride
                    || layer.HasModelAutoCompactTokenLimitScopeOverride,
                ModelAutoCompactTokenLimitScopeSource = layer.HasModelAutoCompactTokenLimitScopeOverride
                    ? source
                    : current.ModelAutoCompactTokenLimitScopeSource,
                ConfiguredModelInstructions = layer.HasModelInstructionsInlineOverride
                    ? layer.ModelInstructions
                    : current.ConfiguredModelInstructions,
                HasModelInstructionsInlineOverride = current.HasModelInstructionsInlineOverride
                    || layer.HasModelInstructionsInlineOverride,
                ModelInstructionsInlineSource = layer.HasModelInstructionsInlineOverride
                    ? source
                    : current.ModelInstructionsInlineSource,
                ConfiguredModelInstructionsFileContent = layer.HasModelInstructionsFileOverride
                    ? layer.ModelInstructionsFileContent
                    : current.ConfiguredModelInstructionsFileContent,
                HasModelInstructionsFileOverride = current.HasModelInstructionsFileOverride
                    || layer.HasModelInstructionsFileOverride,
                ModelInstructionsFileSource = layer.HasModelInstructionsFileOverride
                    ? source
                    : current.ModelInstructionsFileSource,
                ConfiguredModelInstructionsSourceFilePath = layer.HasModelInstructionsFileOverride
                    ? layer.ModelInstructionsSourceFilePath
                    : current.ConfiguredModelInstructionsSourceFilePath,
                ConfiguredCompactPrompt = layer.HasCompactPromptOverride
                    ? layer.CompactPrompt
                    : current.ConfiguredCompactPrompt,
                HasCompactPromptInlineOverride = current.HasCompactPromptInlineOverride
                    || layer.HasCompactPromptOverride,
                CompactPromptInlineSource = layer.HasCompactPromptOverride
                    ? source
                    : current.CompactPromptInlineSource,
                ConfiguredCompactPromptFileContent = layer.HasCompactPromptFileOverride
                    ? layer.CompactPromptFileContent
                    : current.ConfiguredCompactPromptFileContent,
                HasCompactPromptFileOverride = current.HasCompactPromptFileOverride
                    || layer.HasCompactPromptFileOverride,
                CompactPromptFileSource = layer.HasCompactPromptFileOverride
                    ? source
                    : current.CompactPromptFileSource,
                ConfiguredCompactPromptSourceFilePath = layer.HasCompactPromptFileOverride
                    ? layer.CompactPromptSourceFilePath
                    : current.ConfiguredCompactPromptSourceFilePath,
            };
        }

        private static bool HasApplicableOverrides(
            ProjectInstructionConfigLayer layer,
            bool includeProjectRootMarkers)
        {
            return layer.HasMaximumBytesOverride
                || layer.HasFallbackFileNamesOverride
                || layer.HasDeveloperInstructionsOverride
                || layer.HasPersonalityEnabledOverride
                || layer.HasPersonalityOverride
                || layer.WebSearchConfigState.HasAnyAssignment
                || layer.HasSandboxModeOverride
                || layer.HasApprovalPolicyOverride
                || layer.HasApprovalsReviewerOverride
                || layer.HasGuardianApprovalEnabledOverride
                || layer.HasAutoReviewPolicyOverride
                || layer.HasModelOverride
                || layer.HasReviewModelOverride
                || layer.HasPreventIdleSleepOverride
                || layer.HasShellToolEnabledOverride
                || layer.HasHooksEnabledOverride
                || layer.HasPluginsEnabledOverride
                || layer.HasErrorOnToolCollisionsOverride
                || layer.HasMentionsV2EnabledOverride
                || layer.HasSkillMcpDependencyInstallEnabledOverride
                || layer.ShellEnvironmentPolicyLayer.HasAssignment
                || layer.HasGoalsEnabledOverride
                || layer.HasDefaultModeRequestUserInputEnabledOverride
                || layer.HasExperimentalRequestUserInputEnabledOverride
                || layer.HasUpdatePlanEnabledOverride
                || layer.HasIncludePermissionsInstructionsOverride
                || layer.HasIncludeCollaborationModeInstructionsOverride
                || layer.HasIncludeEnvironmentContextOverride
                || layer.HasIncludeSkillInstructionsOverride
                || layer.HasMultiAgentEnabledOverride
                || layer.HasAgentsEnabledOverride
                || layer.HasInterruptMessageOverride
                || layer.HasMaximumConcurrentSubagentRunsOverride
                || layer.HasDefaultSubagentModelOverride
                || layer.HasDefaultSubagentReasoningEffortOverride
                || layer.HasModelContextWindowOverride
                || layer.HasToolOutputTokenLimitOverride
                || layer.HasModelReasoningEffortOverride
                || layer.HasPlanModeReasoningEffortOverride
                || layer.HasModelReasoningSummaryOverride
                || layer.HasModelSupportsReasoningSummariesOverride
                || layer.HasHideAgentReasoningOverride
                || layer.HasFastModeEnabledOverride
                || layer.HasServiceTierOverride
                || layer.HasModelVerbosityOverride
                || layer.HasModelAutoCompactTokenLimitOverride
                || layer.HasModelAutoCompactTokenLimitScopeOverride
                || layer.HasModelInstructionsInlineOverride
                || layer.HasModelInstructionsFileOverride
                || layer.HasCompactPromptOverride
                || layer.HasCompactPromptFileOverride
                || (includeProjectRootMarkers && layer.HasProjectRootMarkersOverride);
        }

        private static IReadOnlyList<string> EnumerateProjectConfigDirectories(
            string normalizedProjectRoot,
            string? workingDirectoryPath)
        {
            var normalizedWorkingDirectory = NormalizeProjectWorkingDirectoryPath(
                workingDirectoryPath,
                normalizedProjectRoot);
            var directories = new List<string>();
            try
            {
                var current = new DirectoryInfo(normalizedWorkingDirectory);
                while (current != null)
                {
                    var currentPath = Path.TrimEndingDirectorySeparator(current.FullName);
                    if (!CopilotWorkspaceSearchSupport.IsPathWithinRoots(currentPath, [normalizedProjectRoot]))
                        return [normalizedProjectRoot];

                    directories.Add(currentPath);
                    if (string.Equals(currentPath, normalizedProjectRoot, StringComparison.OrdinalIgnoreCase))
                        break;
                    current = current.Parent;
                }
            }
            catch
            {
                return [normalizedProjectRoot];
            }

            if (directories.Count == 0
                || !string.Equals(directories[^1], normalizedProjectRoot, StringComparison.OrdinalIgnoreCase))
            {
                return [normalizedProjectRoot];
            }

            directories.Reverse();
            return directories;
        }

        private static string NormalizeProjectWorkingDirectoryPath(
            string? path,
            string normalizedProjectRoot)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length > 2_048)
                return normalizedProjectRoot;

            try
            {
                var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));
                return fullPath.Length <= 2_048
                    && Directory.Exists(fullPath)
                    && !CopilotWorkspaceSearchSupport.HasReparsePointInPath(fullPath)
                    && CopilotWorkspaceSearchSupport.IsPathWithinRoots(fullPath, [normalizedProjectRoot])
                        ? fullPath
                        : normalizedProjectRoot;
            }
            catch
            {
                return normalizedProjectRoot;
            }
        }

        private static bool TryReadConfigSource(
            string allowedRootPath,
            string configPath,
            out string source)
        {
            source = string.Empty;
            try
            {
                configPath = Path.GetFullPath(configPath);
                var file = new FileInfo(configPath);
                if (!file.Exists
                    || file.Length <= 0
                    || file.Length > MaximumConfigBytes
                    || (file.Attributes & FileAttributes.ReparsePoint) != 0
                    || CopilotWorkspaceSearchSupport.HasReparsePointInPath(configPath)
                    || !CopilotWorkspaceSearchSupport.IsPathWithinRoots(configPath, [allowedRootPath]))
                {
                    return false;
                }

                using (var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                {
                    if (stream.Length <= 0 || stream.Length > MaximumConfigBytes)
                        return false;
                    var buffer = new char[MaximumConfigBytes + 1];
                    var count = reader.ReadBlock(buffer, 0, buffer.Length);
                    if (count > MaximumConfigBytes || !reader.EndOfStream)
                        return false;
                    source = new string(buffer, 0, count);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static ProjectInstructionConfigLayer ResolveCompactPromptFile(
            ProjectInstructionConfigLayer layer,
            string configPath,
            string allowedRootPath,
            bool allowOutsideRoot)
        {
            if (!layer.HasCompactPromptFileOverride)
                return layer;

            if (!TryReadCompactPromptFile(
                configPath,
                layer.ConfiguredCompactPromptFilePath,
                allowedRootPath,
                allowOutsideRoot,
                out var promptFilePath,
                out var compactPrompt))
            {
                return layer;
            }

            return layer with
            {
                CompactPromptFileContent = compactPrompt,
                CompactPromptSourceFilePath = promptFilePath,
            };
        }

        private static ProjectInstructionConfigLayer ResolveModelInstructionsFile(
            ProjectInstructionConfigLayer layer,
            string configPath,
            string allowedRootPath,
            bool allowOutsideRoot)
        {
            if (!layer.HasModelInstructionsFileOverride)
                return layer;

            if (!TryReadConfigReferencedTextFile(
                configPath,
                layer.ConfiguredModelInstructionsFilePath,
                allowedRootPath,
                allowOutsideRoot,
                MaximumModelInstructionBytes,
                MaximumModelInstructionCharacters,
                out var instructionsFilePath,
                out var modelInstructions))
            {
                return layer;
            }

            return layer with
            {
                ModelInstructionsFileContent = modelInstructions,
                ModelInstructionsSourceFilePath = instructionsFilePath,
            };
        }

        private static bool TryReadCompactPromptFile(
            string configPath,
            string configuredPath,
            string allowedRootPath,
            bool allowOutsideRoot,
            out string promptFilePath,
            out string compactPrompt) =>
            TryReadConfigReferencedTextFile(
                configPath,
                configuredPath,
                allowedRootPath,
                allowOutsideRoot,
                MaximumCompactPromptBytes,
                MaximumCompactPromptCharacters,
                out promptFilePath,
                out compactPrompt);

        private static bool TryReadConfigReferencedTextFile(
            string configPath,
            string configuredPath,
            string allowedRootPath,
            bool allowOutsideRoot,
            int maximumBytes,
            int maximumCharacters,
            out string sourceFilePath,
            out string content)
        {
            sourceFilePath = string.Empty;
            content = string.Empty;
            var normalizedConfiguredPath = (configuredPath ?? string.Empty).Trim();
            if (normalizedConfiguredPath.Length == 0
                || normalizedConfiguredPath.Length > MaximumConfigReferencedPathCharacters
                || normalizedConfiguredPath.StartsWith("\\\\", StringComparison.Ordinal)
                || normalizedConfiguredPath.StartsWith("//", StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                var configDirectory = Path.GetDirectoryName(Path.GetFullPath(configPath));
                if (string.IsNullOrWhiteSpace(configDirectory))
                    return false;
                var candidatePath = Path.IsPathFullyQualified(normalizedConfiguredPath)
                    ? Path.GetFullPath(normalizedConfiguredPath)
                    : Path.GetFullPath(normalizedConfiguredPath, configDirectory);
                if (!allowOutsideRoot
                    && !CopilotWorkspaceSearchSupport.IsPathWithinRoots(candidatePath, [allowedRootPath]))
                {
                    return false;
                }

                var file = new FileInfo(candidatePath);
                if (!file.Exists
                    || file.Length < 0
                    || file.Length > maximumBytes
                    || (file.Attributes & FileAttributes.ReparsePoint) != 0
                    || CopilotWorkspaceSearchSupport.HasReparsePointInPath(candidatePath))
                {
                    return false;
                }

                using var stream = new FileStream(
                    candidatePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                if (stream.Length < 0 || stream.Length > maximumBytes)
                    return false;
                using var reader = new StreamReader(
                    stream,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true);
                var buffer = new char[maximumCharacters + 1];
                var count = reader.ReadBlock(buffer, 0, buffer.Length);
                if (count > maximumCharacters || !reader.EndOfStream)
                    return false;
                var normalizedContent = new string(buffer, 0, count).Trim();
                if (normalizedContent.Contains('\0'))
                    return false;

                sourceFilePath = candidatePath;
                content = normalizedContent;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseInstructionLayer(
            string source,
            out ProjectInstructionConfigLayer layer)
        {
            var shellEnvironmentPolicyLayer = ParseShellEnvironmentPolicyLayer(source);
            var maximumBytes = DefaultMaximumBytes;
            var fallbackFileNames = Array.Empty<string>();
            var projectRootMarkers = Array.Empty<string>();
            var developerInstructions = string.Empty;
            var personalityEnabled = true;
            var personality = CopilotResponsePersonality.None;
            var webSearchMode = CopilotCodexWebSearchMode.Unspecified;
            var sandboxMode = CopilotCodexSandboxMode.Unspecified;
            var approvalPolicy = CopilotCodexApprovalPolicy.Unspecified;
            var approvalsReviewer = CopilotCodexApprovalsReviewer.Unspecified;
            var guardianApprovalEnabled = true;
            var autoReviewPolicy = string.Empty;
            var model = string.Empty;
            var reviewModel = string.Empty;
            var preventIdleSleep = false;
            var shellToolEnabled = true;
            var hooksEnabled = true;
            var pluginsEnabled = true;
            var errorOnToolCollisions = false;
            var mentionsV2Enabled = true;
            var skillMcpDependencyInstallEnabled = true;
            var goalsEnabled = true;
            var defaultModeRequestUserInputEnabled = false;
            var experimentalRequestUserInputEnabled = true;
            var updatePlanEnabled = true;
            var includePermissionsInstructions = true;
            var includeCollaborationModeInstructions = true;
            var includeEnvironmentContext = true;
            var includeSkillInstructions = true;
            var multiAgentEnabled = true;
            var agentsEnabled = true;
            var interruptMessageEnabled = true;
            var maximumConcurrentSubagentRuns = CopilotSubagentCoordinator.DefaultMaximumConcurrentRuns;
            var defaultSubagentModel = string.Empty;
            var defaultSubagentReasoningEffort = CopilotCodexReasoningEffort.Unspecified;
            var modelContextWindowTokens = 0;
            var toolOutputTokenLimit = 0;
            var modelReasoningEffort = CopilotCodexReasoningEffort.Unspecified;
            var planModeReasoningEffort = CopilotCodexReasoningEffort.Unspecified;
            var modelReasoningSummary = CopilotCodexReasoningSummary.Unspecified;
            var modelSupportsReasoningSummaries = false;
            var hideAgentReasoning = false;
            var fastModeEnabled = true;
            var serviceTier = string.Empty;
            var modelVerbosity = CopilotCodexModelVerbosity.Unspecified;
            var modelAutoCompactTokenLimit = 0;
            var modelAutoCompactTokenLimitScope = CopilotModelAutoCompactTokenLimitScope.Unspecified;
            var modelInstructions = string.Empty;
            var modelInstructionsFilePath = string.Empty;
            var compactPrompt = string.Empty;
            var compactPromptFilePath = string.Empty;
            var hasMaximumBytesOverride = false;
            var hasFallbackFileNamesOverride = false;
            var hasProjectRootMarkersOverride = false;
            var hasDeveloperInstructionsOverride = false;
            var hasPersonalityEnabledOverride = false;
            var hasPersonalityOverride = false;
            var hasCanonicalWebSearchModeOverride = false;
            var hasCanonicalWebSearchModeAssignment = false;
            var hasLegacyWebSearchAssignment = false;
            var legacyWebSearchEnabled = false;
            var hasLegacyWebSearchCachedAssignment = false;
            var legacyWebSearchCachedEnabled = false;
            var hasLegacyWebSearchRequestAssignment = false;
            var legacyWebSearchRequestEnabled = false;
            var hasSandboxModeOverride = false;
            var hasApprovalPolicyOverride = false;
            var hasApprovalsReviewerOverride = false;
            var hasGuardianApprovalEnabledOverride = false;
            var hasAutoReviewPolicyOverride = false;
            var hasModelOverride = false;
            var hasReviewModelOverride = false;
            var hasPreventIdleSleepOverride = false;
            var hasShellToolEnabledOverride = false;
            var hasHooksEnabledOverride = false;
            var hasPluginsEnabledOverride = false;
            var hasErrorOnToolCollisionsOverride = false;
            var hasMentionsV2EnabledOverride = false;
            var hasSkillMcpDependencyInstallEnabledOverride = false;
            var hasGoalsEnabledOverride = false;
            var hasDefaultModeRequestUserInputEnabledOverride = false;
            var hasExperimentalRequestUserInputEnabledOverride = false;
            var hasUpdatePlanEnabledOverride = false;
            var hasIncludePermissionsInstructionsOverride = false;
            var hasIncludeCollaborationModeInstructionsOverride = false;
            var hasIncludeEnvironmentContextOverride = false;
            var hasIncludeSkillInstructionsOverride = false;
            var hasMultiAgentEnabledOverride = false;
            var hasAgentsEnabledOverride = false;
            var hasInterruptMessageOverride = false;
            var hasMaximumConcurrentSubagentRunsOverride = false;
            var hasCanonicalMaximumConcurrentSubagentRunsOverride = false;
            var hasDefaultSubagentModelOverride = false;
            var hasDefaultSubagentReasoningEffortOverride = false;
            var hasModelContextWindowOverride = false;
            var hasToolOutputTokenLimitOverride = false;
            var hasModelReasoningEffortOverride = false;
            var hasPlanModeReasoningEffortOverride = false;
            var hasModelReasoningSummaryOverride = false;
            var hasModelSupportsReasoningSummariesOverride = false;
            var hasHideAgentReasoningOverride = false;
            var hasFastModeEnabledOverride = false;
            var hasServiceTierOverride = false;
            var hasModelVerbosityOverride = false;
            var hasModelAutoCompactTokenLimitOverride = false;
            var hasModelAutoCompactTokenLimitScopeOverride = false;
            var hasModelInstructionsInlineOverride = false;
            var hasModelInstructionsFileOverride = false;
            var hasCompactPromptOverride = false;
            var hasCompactPromptFileOverride = false;
            foreach (var assignment in EnumerateSupportedAssignments(source))
            {
                if (string.Equals(assignment.Key, MaximumBytesKey, StringComparison.Ordinal))
                {
                    if (!TryParseMaximumBytes(assignment.Value, out var configuredMaximumBytes))
                        continue;
                    maximumBytes = configuredMaximumBytes;
                    hasMaximumBytesOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, FallbackFileNamesKey, StringComparison.Ordinal))
                {
                    if (!TryParseFallbackFileNames(assignment.Value, out var configuredFallbackFileNames))
                        continue;
                    fallbackFileNames = configuredFallbackFileNames;
                    hasFallbackFileNamesOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, DeveloperInstructionsKey, StringComparison.Ordinal))
                {
                    if (!TryParseDeveloperInstructions(assignment.Value, out var configuredDeveloperInstructions))
                        continue;
                    developerInstructions = configuredDeveloperInstructions;
                    hasDeveloperInstructionsOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, PersonalityEnabledKey, StringComparison.Ordinal))
                {
                    if (!TryParseTomlBoolean(
                        assignment.Value,
                        out personalityEnabled))
                    {
                        continue;
                    }
                    hasPersonalityEnabledOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, PersonalityKey, StringComparison.Ordinal))
                {
                    if (!TryParseConfiguredText(
                        assignment.Value,
                        MaximumPersonalityCharacters,
                        out var configuredPersonality)
                        || !CopilotResponsePersonalitySelection.TryParse(
                            configuredPersonality,
                            out personality))
                    {
                        continue;
                    }
                    hasPersonalityOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, WebSearchKey, StringComparison.Ordinal))
                {
                    hasCanonicalWebSearchModeAssignment = true;
                    if (!TryParseConfiguredText(
                        assignment.Value,
                        MaximumPersonalityCharacters,
                        out var configuredWebSearchMode)
                        || !CopilotCodexWebSearchModeSelection.TryParse(
                            configuredWebSearchMode,
                            out webSearchMode))
                    {
                        continue;
                    }
                    hasCanonicalWebSearchModeOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, LegacyWebSearchKey, StringComparison.Ordinal))
                {
                    if (TryParseTomlBoolean(assignment.Value, out var enabled))
                    {
                        hasLegacyWebSearchAssignment = true;
                        legacyWebSearchEnabled = enabled;
                    }
                    continue;
                }

                if (string.Equals(assignment.Key, LegacyWebSearchCachedKey, StringComparison.Ordinal))
                {
                    if (TryParseTomlBoolean(assignment.Value, out var enabled))
                    {
                        hasLegacyWebSearchCachedAssignment = true;
                        legacyWebSearchCachedEnabled = enabled;
                    }
                    continue;
                }

                if (string.Equals(assignment.Key, LegacyWebSearchRequestKey, StringComparison.Ordinal))
                {
                    if (TryParseTomlBoolean(assignment.Value, out var enabled))
                    {
                        hasLegacyWebSearchRequestAssignment = true;
                        legacyWebSearchRequestEnabled = enabled;
                    }
                    continue;
                }

                if (string.Equals(assignment.Key, SandboxModeKey, StringComparison.Ordinal))
                {
                    if (!TryParseConfiguredText(
                        assignment.Value,
                        MaximumPersonalityCharacters,
                        out var configuredSandboxMode)
                        || !CopilotCodexSandboxModeSelection.TryParse(
                            configuredSandboxMode,
                            out sandboxMode))
                    {
                        continue;
                    }
                    hasSandboxModeOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, ApprovalPolicyKey, StringComparison.Ordinal))
                {
                    if (!TryParseApprovalPolicy(assignment.Value, out approvalPolicy))
                        continue;
                    hasApprovalPolicyOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, ApprovalsReviewerKey, StringComparison.Ordinal))
                {
                    if (!TryParseConfiguredText(
                            assignment.Value,
                            MaximumPersonalityCharacters,
                            out var configuredApprovalsReviewer)
                        || !CopilotCodexApprovalsReviewerSelection.TryParse(
                            configuredApprovalsReviewer,
                            out approvalsReviewer))
                    {
                        continue;
                    }
                    hasApprovalsReviewerOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, GuardianApprovalEnabledKey, StringComparison.Ordinal))
                {
                    if (!TryParseTomlBoolean(
                        assignment.Value,
                        out guardianApprovalEnabled))
                    {
                        continue;
                    }
                    hasGuardianApprovalEnabledOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, AutoReviewPolicyKey, StringComparison.Ordinal))
                {
                    if (!TryParseConfiguredText(
                            assignment.Value,
                            MaximumAutoReviewPolicyCharacters,
                            out autoReviewPolicy)
                        || string.IsNullOrWhiteSpace(autoReviewPolicy))
                    {
                        autoReviewPolicy = string.Empty;
                        continue;
                    }
                    hasAutoReviewPolicyOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, ReviewModelKey, StringComparison.Ordinal))
                {
                    if (!TryParseConfiguredText(
                        assignment.Value,
                        CopilotReviewModelSelection.MaximumModelCharacters,
                        out var configuredReviewModel)
                        || !CopilotReviewModelSelection.TryNormalize(
                            configuredReviewModel,
                            out reviewModel))
                    {
                        continue;
                    }
                    hasReviewModelOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, ModelKey, StringComparison.Ordinal))
                {
                    if (!TryParseConfiguredText(
                        assignment.Value,
                        CopilotConfiguredModelSelection.MaximumModelCharacters,
                        out var configuredModel)
                        || !CopilotConfiguredModelSelection.TryNormalize(
                            configuredModel,
                            out model))
                    {
                        continue;
                    }
                    hasModelOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, PreventIdleSleepKey, StringComparison.Ordinal))
                {
                    if (!TryParseTomlBoolean(
                        assignment.Value,
                        out preventIdleSleep))
                    {
                        continue;
                    }
                    hasPreventIdleSleepOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, ShellToolEnabledKey, StringComparison.Ordinal))
                {
                    if (!TryParseTomlBoolean(
                        assignment.Value,
                        out shellToolEnabled))
                    {
                        continue;
                    }
                    hasShellToolEnabledOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, HooksEnabledKey, StringComparison.Ordinal))
                {
                    if (!TryParseTomlBoolean(
                        assignment.Value,
                        out hooksEnabled))
                    {
                        continue;
                    }
                    hasHooksEnabledOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, PluginsEnabledKey, StringComparison.Ordinal))
                {
                    if (!TryParseTomlBoolean(
                        assignment.Value,
                        out pluginsEnabled))
                    {
                        continue;
                    }
                    hasPluginsEnabledOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, ErrorOnToolCollisionsKey, StringComparison.Ordinal))
                {
                    if (!TryParseTomlBoolean(
                        assignment.Value,
                        out errorOnToolCollisions))
                    {
                        continue;
                    }
                    hasErrorOnToolCollisionsOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, MentionsV2EnabledKey, StringComparison.Ordinal))
                {
                    if (!TryParseTomlBoolean(
                        assignment.Value,
                        out mentionsV2Enabled))
                    {
                        continue;
                    }
                    hasMentionsV2EnabledOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, SkillMcpDependencyInstallEnabledKey, StringComparison.Ordinal))
                {
                    if (!TryParseTomlBoolean(
                        assignment.Value,
                        out skillMcpDependencyInstallEnabled))
                    {
                        continue;
                    }
                    hasSkillMcpDependencyInstallEnabledOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, GoalsEnabledKey, StringComparison.Ordinal))
                {
                    if (!TryParseTomlBoolean(
                        assignment.Value,
                        out goalsEnabled))
                    {
                        continue;
                    }
                    hasGoalsEnabledOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, DefaultModeRequestUserInputEnabledKey, StringComparison.Ordinal))
                {
                    if (!TryParseTomlBoolean(
                        assignment.Value,
                        out defaultModeRequestUserInputEnabled))
                    {
                        continue;
                    }
                    hasDefaultModeRequestUserInputEnabledOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, ExperimentalRequestUserInputEnabledKey, StringComparison.Ordinal))
                {
                    if (!TryParseTomlBoolean(
                        assignment.Value,
                        out experimentalRequestUserInputEnabled))
                    {
                        continue;
                    }
                    hasExperimentalRequestUserInputEnabledOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, UpdatePlanEnabledKey, StringComparison.Ordinal))
                {
                    if (!TryParseTomlBoolean(
                        assignment.Value,
                        out updatePlanEnabled))
                    {
                        continue;
                    }
                    hasUpdatePlanEnabledOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, IncludePermissionsInstructionsKey, StringComparison.Ordinal))
                {
                    if (!TryParseTomlBoolean(
                        assignment.Value,
                        out includePermissionsInstructions))
                    {
                        continue;
                    }
                    hasIncludePermissionsInstructionsOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, IncludeCollaborationModeInstructionsKey, StringComparison.Ordinal))
                {
                    if (!TryParseTomlBoolean(
                        assignment.Value,
                        out includeCollaborationModeInstructions))
                    {
                        continue;
                    }
                    hasIncludeCollaborationModeInstructionsOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, IncludeEnvironmentContextKey, StringComparison.Ordinal))
                {
                    if (!TryParseTomlBoolean(
                        assignment.Value,
                        out includeEnvironmentContext))
                    {
                        continue;
                    }
                    hasIncludeEnvironmentContextOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, SkillsIncludeInstructionsKey, StringComparison.Ordinal))
                {
                    if (!TryParseTomlBoolean(
                        assignment.Value,
                        out includeSkillInstructions))
                    {
                        continue;
                    }
                    hasIncludeSkillInstructionsOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, MultiAgentEnabledKey, StringComparison.Ordinal))
                {
                    if (!TryParseTomlBoolean(
                        assignment.Value,
                        out multiAgentEnabled))
                    {
                        continue;
                    }
                    hasMultiAgentEnabledOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, AgentsEnabledKey, StringComparison.Ordinal))
                {
                    if (!TryParseTomlBoolean(
                        assignment.Value,
                        out agentsEnabled))
                    {
                        continue;
                    }
                    hasAgentsEnabledOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, AgentsInterruptMessageKey, StringComparison.Ordinal))
                {
                    if (!TryParseTomlBoolean(
                        assignment.Value,
                        out interruptMessageEnabled))
                    {
                        continue;
                    }
                    hasInterruptMessageOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, AgentsMaximumConcurrentThreadsKey, StringComparison.Ordinal)
                    || string.Equals(assignment.Key, AgentsMaximumThreadsKey, StringComparison.Ordinal))
                {
                    var isCanonical = string.Equals(
                        assignment.Key,
                        AgentsMaximumConcurrentThreadsKey,
                        StringComparison.Ordinal);
                    if (!isCanonical && hasCanonicalMaximumConcurrentSubagentRunsOverride)
                        continue;
                    if (!TryParsePositiveInteger(assignment.Value, out var configuredMaximumConcurrentSubagentRuns))
                        continue;
                    maximumConcurrentSubagentRuns = configuredMaximumConcurrentSubagentRuns;
                    hasMaximumConcurrentSubagentRunsOverride = true;
                    hasCanonicalMaximumConcurrentSubagentRunsOverride |= isCanonical;
                    continue;
                }

                if (string.Equals(assignment.Key, AgentsDefaultSubagentModelKey, StringComparison.Ordinal))
                {
                    if (!TryParseConfiguredText(
                        assignment.Value,
                        CopilotConfiguredModelSelection.MaximumModelCharacters,
                        out var configuredDefaultSubagentModel)
                        || !CopilotConfiguredModelSelection.TryNormalize(
                            configuredDefaultSubagentModel,
                            out defaultSubagentModel))
                    {
                        continue;
                    }
                    hasDefaultSubagentModelOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, AgentsDefaultSubagentReasoningEffortKey, StringComparison.Ordinal))
                {
                    if (!TryParseConfiguredText(
                        assignment.Value,
                        maximumCharacters: 32,
                        out var configuredDefaultSubagentReasoningEffort)
                        || !CopilotCodexReasoningEffortSelection.TryParse(
                            configuredDefaultSubagentReasoningEffort,
                            out defaultSubagentReasoningEffort))
                    {
                        continue;
                    }
                    hasDefaultSubagentReasoningEffortOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, ModelContextWindowKey, StringComparison.Ordinal))
                {
                    if (!TryParseModelContextWindowTokens(
                        assignment.Value,
                        out modelContextWindowTokens))
                    {
                        continue;
                    }
                    hasModelContextWindowOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, ToolOutputTokenLimitKey, StringComparison.Ordinal))
                {
                    if (!TryParseToolOutputTokenLimit(
                        assignment.Value,
                        out toolOutputTokenLimit))
                    {
                        continue;
                    }
                    hasToolOutputTokenLimitOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, ModelReasoningEffortKey, StringComparison.Ordinal))
                {
                    if (!TryParseConfiguredText(
                        assignment.Value,
                        MaximumPersonalityCharacters,
                        out var configuredModelReasoningEffort)
                        || !CopilotCodexReasoningEffortSelection.TryParse(
                            configuredModelReasoningEffort,
                            out modelReasoningEffort))
                    {
                        continue;
                    }
                    hasModelReasoningEffortOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, PlanModeReasoningEffortKey, StringComparison.Ordinal))
                {
                    if (!TryParseConfiguredText(
                        assignment.Value,
                        MaximumPersonalityCharacters,
                        out var configuredPlanModeReasoningEffort)
                        || !CopilotCodexReasoningEffortSelection.TryParsePlanMode(
                            configuredPlanModeReasoningEffort,
                            out planModeReasoningEffort))
                    {
                        continue;
                    }
                    hasPlanModeReasoningEffortOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, ModelReasoningSummaryKey, StringComparison.Ordinal))
                {
                    if (!TryParseConfiguredText(
                        assignment.Value,
                        MaximumPersonalityCharacters,
                        out var configuredModelReasoningSummary)
                        || !CopilotCodexReasoningSummarySelection.TryParse(
                            configuredModelReasoningSummary,
                            out modelReasoningSummary))
                    {
                        continue;
                    }
                    hasModelReasoningSummaryOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, ModelSupportsReasoningSummariesKey, StringComparison.Ordinal))
                {
                    if (!TryParseTomlBoolean(
                        assignment.Value,
                        out modelSupportsReasoningSummaries))
                    {
                        continue;
                    }
                    hasModelSupportsReasoningSummariesOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, HideAgentReasoningKey, StringComparison.Ordinal))
                {
                    if (!TryParseTomlBoolean(
                        assignment.Value,
                        out hideAgentReasoning))
                    {
                        continue;
                    }
                    hasHideAgentReasoningOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, FastModeEnabledKey, StringComparison.Ordinal))
                {
                    if (!TryParseTomlBoolean(
                        assignment.Value,
                        out fastModeEnabled))
                    {
                        continue;
                    }
                    hasFastModeEnabledOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, ServiceTierKey, StringComparison.Ordinal))
                {
                    if (!TryParseConfiguredText(
                        assignment.Value,
                        CopilotCodexServiceTierSelection.MaximumCharacters,
                        out var configuredServiceTier)
                        || !CopilotCodexServiceTierSelection.TryNormalize(
                            configuredServiceTier,
                            out serviceTier))
                    {
                        continue;
                    }
                    hasServiceTierOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, ModelVerbosityKey, StringComparison.Ordinal))
                {
                    if (!TryParseConfiguredText(
                        assignment.Value,
                        MaximumPersonalityCharacters,
                        out var configuredModelVerbosity)
                        || !CopilotCodexModelVerbositySelection.TryParse(
                            configuredModelVerbosity,
                            out modelVerbosity))
                    {
                        continue;
                    }
                    hasModelVerbosityOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, ModelAutoCompactTokenLimitKey, StringComparison.Ordinal))
                {
                    if (!TryParsePositiveTokenLimit(
                        assignment.Value,
                        out modelAutoCompactTokenLimit))
                    {
                        continue;
                    }
                    hasModelAutoCompactTokenLimitOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, ModelAutoCompactTokenLimitScopeKey, StringComparison.Ordinal))
                {
                    if (!TryParseConfiguredText(
                        assignment.Value,
                        MaximumPersonalityCharacters,
                        out var configuredModelAutoCompactTokenLimitScope)
                        || !CopilotModelAutoCompactTokenLimitScopeSelection.TryParse(
                            configuredModelAutoCompactTokenLimitScope,
                            out modelAutoCompactTokenLimitScope))
                    {
                        continue;
                    }
                    hasModelAutoCompactTokenLimitScopeOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, ModelInstructionsKey, StringComparison.Ordinal))
                {
                    if (!TryParseConfiguredText(
                        assignment.Value,
                        MaximumModelInstructionCharacters,
                        out var configuredModelInstructions))
                    {
                        continue;
                    }
                    modelInstructions = configuredModelInstructions;
                    hasModelInstructionsInlineOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, CompactPromptKey, StringComparison.Ordinal))
                {
                    if (!TryParseConfiguredText(
                        assignment.Value,
                        MaximumCompactPromptCharacters,
                        out var configuredCompactPrompt))
                    {
                        continue;
                    }
                    compactPrompt = configuredCompactPrompt;
                    hasCompactPromptOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, ModelInstructionsFileKey, StringComparison.Ordinal))
                {
                    if (!TryParseConfiguredText(
                        assignment.Value,
                        MaximumConfigReferencedPathCharacters,
                        out var configuredModelInstructionsFilePath)
                        || configuredModelInstructionsFilePath.IndexOfAny(['\r', '\n', '\0']) >= 0)
                    {
                        continue;
                    }
                    modelInstructionsFilePath = configuredModelInstructionsFilePath;
                    hasModelInstructionsFileOverride = true;
                    continue;
                }

                if (string.Equals(assignment.Key, ExperimentalCompactPromptFileKey, StringComparison.Ordinal))
                {
                    if (!TryParseConfiguredText(
                        assignment.Value,
                        MaximumConfigReferencedPathCharacters,
                        out var configuredCompactPromptFilePath)
                        || configuredCompactPromptFilePath.IndexOfAny(['\r', '\n', '\0']) >= 0)
                    {
                        continue;
                    }
                    compactPromptFilePath = configuredCompactPromptFilePath;
                    hasCompactPromptFileOverride = true;
                    continue;
                }

                if (!string.Equals(assignment.Key, ProjectRootMarkersKey, StringComparison.Ordinal)
                    || !TryParseProjectRootMarkers(assignment.Value, out var configuredProjectRootMarkers))
                {
                    continue;
                }

                projectRootMarkers = configuredProjectRootMarkers;
                hasProjectRootMarkersOverride = true;
            }

            var acceptLegacyWebSearchAssignments = !hasCanonicalWebSearchModeAssignment
                || hasCanonicalWebSearchModeOverride;
            var webSearchConfigState = new CopilotCodexWebSearchConfigState(
                hasCanonicalWebSearchModeOverride,
                webSearchMode,
                CopilotProjectInstructionConfigSources.None,
                acceptLegacyWebSearchAssignments && hasLegacyWebSearchAssignment
                    ? legacyWebSearchEnabled
                    : null,
                CopilotProjectInstructionConfigSources.None,
                acceptLegacyWebSearchAssignments && hasLegacyWebSearchCachedAssignment
                    ? legacyWebSearchCachedEnabled
                    : null,
                CopilotProjectInstructionConfigSources.None,
                acceptLegacyWebSearchAssignments && hasLegacyWebSearchRequestAssignment
                    ? legacyWebSearchRequestEnabled
                    : null,
                CopilotProjectInstructionConfigSources.None);

            layer = new ProjectInstructionConfigLayer(
                maximumBytes,
                fallbackFileNames,
                projectRootMarkers,
                developerInstructions,
                hasMaximumBytesOverride,
                hasFallbackFileNamesOverride,
                hasProjectRootMarkersOverride,
                hasDeveloperInstructionsOverride)
            {
                ModelInstructions = modelInstructions,
                CompactPrompt = compactPrompt,
                ConfiguredModelInstructionsFilePath = modelInstructionsFilePath,
                ConfiguredCompactPromptFilePath = compactPromptFilePath,
                HasModelInstructionsInlineOverride = hasModelInstructionsInlineOverride,
                HasModelInstructionsFileOverride = hasModelInstructionsFileOverride,
                PersonalityEnabled = personalityEnabled,
                HasPersonalityEnabledOverride = hasPersonalityEnabledOverride,
                Personality = personality,
                HasPersonalityOverride = hasPersonalityOverride,
                WebSearchConfigState = webSearchConfigState,
                SandboxMode = sandboxMode,
                HasSandboxModeOverride = hasSandboxModeOverride,
                ApprovalPolicy = approvalPolicy,
                HasApprovalPolicyOverride = hasApprovalPolicyOverride,
                ApprovalsReviewer = approvalsReviewer,
                HasApprovalsReviewerOverride = hasApprovalsReviewerOverride,
                GuardianApprovalEnabled = guardianApprovalEnabled,
                HasGuardianApprovalEnabledOverride = hasGuardianApprovalEnabledOverride,
                AutoReviewPolicy = autoReviewPolicy,
                HasAutoReviewPolicyOverride = hasAutoReviewPolicyOverride,
                Model = model,
                HasModelOverride = hasModelOverride,
                ReviewModel = reviewModel,
                HasReviewModelOverride = hasReviewModelOverride,
                PreventIdleSleep = preventIdleSleep,
                HasPreventIdleSleepOverride = hasPreventIdleSleepOverride,
                ShellToolEnabled = shellToolEnabled,
                HasShellToolEnabledOverride = hasShellToolEnabledOverride,
                HooksEnabled = hooksEnabled,
                HasHooksEnabledOverride = hasHooksEnabledOverride,
                PluginsEnabled = pluginsEnabled,
                HasPluginsEnabledOverride = hasPluginsEnabledOverride,
                ErrorOnToolCollisions = errorOnToolCollisions,
                HasErrorOnToolCollisionsOverride = hasErrorOnToolCollisionsOverride,
                MentionsV2Enabled = mentionsV2Enabled,
                HasMentionsV2EnabledOverride = hasMentionsV2EnabledOverride,
                SkillMcpDependencyInstallEnabled = skillMcpDependencyInstallEnabled,
                HasSkillMcpDependencyInstallEnabledOverride = hasSkillMcpDependencyInstallEnabledOverride,
                ShellEnvironmentPolicyLayer = shellEnvironmentPolicyLayer,
                GoalsEnabled = goalsEnabled,
                HasGoalsEnabledOverride = hasGoalsEnabledOverride,
                DefaultModeRequestUserInputEnabled = defaultModeRequestUserInputEnabled,
                HasDefaultModeRequestUserInputEnabledOverride = hasDefaultModeRequestUserInputEnabledOverride,
                ExperimentalRequestUserInputEnabled = experimentalRequestUserInputEnabled,
                HasExperimentalRequestUserInputEnabledOverride = hasExperimentalRequestUserInputEnabledOverride,
                UpdatePlanEnabled = updatePlanEnabled,
                HasUpdatePlanEnabledOverride = hasUpdatePlanEnabledOverride,
                IncludePermissionsInstructions = includePermissionsInstructions,
                HasIncludePermissionsInstructionsOverride = hasIncludePermissionsInstructionsOverride,
                IncludeCollaborationModeInstructions = includeCollaborationModeInstructions,
                HasIncludeCollaborationModeInstructionsOverride = hasIncludeCollaborationModeInstructionsOverride,
                IncludeEnvironmentContext = includeEnvironmentContext,
                HasIncludeEnvironmentContextOverride = hasIncludeEnvironmentContextOverride,
                IncludeSkillInstructions = includeSkillInstructions,
                HasIncludeSkillInstructionsOverride = hasIncludeSkillInstructionsOverride,
                MultiAgentEnabled = multiAgentEnabled,
                HasMultiAgentEnabledOverride = hasMultiAgentEnabledOverride,
                AgentsEnabled = agentsEnabled,
                HasAgentsEnabledOverride = hasAgentsEnabledOverride,
                InterruptMessageEnabled = interruptMessageEnabled,
                HasInterruptMessageOverride = hasInterruptMessageOverride,
                MaximumConcurrentSubagentRuns = maximumConcurrentSubagentRuns,
                HasMaximumConcurrentSubagentRunsOverride = hasMaximumConcurrentSubagentRunsOverride,
                DefaultSubagentModel = defaultSubagentModel,
                HasDefaultSubagentModelOverride = hasDefaultSubagentModelOverride,
                DefaultSubagentReasoningEffort = defaultSubagentReasoningEffort,
                HasDefaultSubagentReasoningEffortOverride = hasDefaultSubagentReasoningEffortOverride,
                ModelContextWindowTokens = modelContextWindowTokens,
                HasModelContextWindowOverride = hasModelContextWindowOverride,
                ToolOutputTokenLimit = toolOutputTokenLimit,
                HasToolOutputTokenLimitOverride = hasToolOutputTokenLimitOverride,
                ModelReasoningEffort = modelReasoningEffort,
                HasModelReasoningEffortOverride = hasModelReasoningEffortOverride,
                PlanModeReasoningEffort = planModeReasoningEffort,
                HasPlanModeReasoningEffortOverride = hasPlanModeReasoningEffortOverride,
                ModelReasoningSummary = modelReasoningSummary,
                HasModelReasoningSummaryOverride = hasModelReasoningSummaryOverride,
                ModelSupportsReasoningSummaries = modelSupportsReasoningSummaries,
                HasModelSupportsReasoningSummariesOverride = hasModelSupportsReasoningSummariesOverride,
                HideAgentReasoning = hideAgentReasoning,
                HasHideAgentReasoningOverride = hasHideAgentReasoningOverride,
                FastModeEnabled = fastModeEnabled,
                HasFastModeEnabledOverride = hasFastModeEnabledOverride,
                ServiceTier = serviceTier,
                HasServiceTierOverride = hasServiceTierOverride,
                ModelVerbosity = modelVerbosity,
                HasModelVerbosityOverride = hasModelVerbosityOverride,
                ModelAutoCompactTokenLimit = modelAutoCompactTokenLimit,
                HasModelAutoCompactTokenLimitOverride = hasModelAutoCompactTokenLimitOverride,
                ModelAutoCompactTokenLimitScope = modelAutoCompactTokenLimitScope,
                HasModelAutoCompactTokenLimitScopeOverride = hasModelAutoCompactTokenLimitScopeOverride,
                HasCompactPromptOverride = hasCompactPromptOverride,
                HasCompactPromptFileOverride = hasCompactPromptFileOverride,
            };
            return hasMaximumBytesOverride
                || hasFallbackFileNamesOverride
                || hasProjectRootMarkersOverride
                || hasDeveloperInstructionsOverride
                || hasPersonalityEnabledOverride
                || hasPersonalityOverride
                || webSearchConfigState.HasAnyAssignment
                || hasSandboxModeOverride
                || hasApprovalPolicyOverride
                || hasApprovalsReviewerOverride
                || hasGuardianApprovalEnabledOverride
                || hasAutoReviewPolicyOverride
                || hasModelOverride
                || hasReviewModelOverride
                || hasPreventIdleSleepOverride
                || hasShellToolEnabledOverride
                || hasHooksEnabledOverride
                || hasPluginsEnabledOverride
                || hasErrorOnToolCollisionsOverride
                || hasMentionsV2EnabledOverride
                || hasSkillMcpDependencyInstallEnabledOverride
                || shellEnvironmentPolicyLayer.HasAssignment
                || hasGoalsEnabledOverride
                || hasDefaultModeRequestUserInputEnabledOverride
                || hasExperimentalRequestUserInputEnabledOverride
                || hasUpdatePlanEnabledOverride
                || hasIncludePermissionsInstructionsOverride
                || hasIncludeCollaborationModeInstructionsOverride
                || hasIncludeEnvironmentContextOverride
                || hasIncludeSkillInstructionsOverride
                || hasMultiAgentEnabledOverride
                || hasAgentsEnabledOverride
                || hasInterruptMessageOverride
                || hasMaximumConcurrentSubagentRunsOverride
                || hasDefaultSubagentModelOverride
                || hasDefaultSubagentReasoningEffortOverride
                || hasModelContextWindowOverride
                || hasToolOutputTokenLimitOverride
                || hasModelReasoningEffortOverride
                || hasPlanModeReasoningEffortOverride
                || hasModelReasoningSummaryOverride
                || hasModelSupportsReasoningSummariesOverride
                || hasHideAgentReasoningOverride
                || hasFastModeEnabledOverride
                || hasServiceTierOverride
                || hasModelVerbosityOverride
                || hasModelAutoCompactTokenLimitOverride
                || hasModelAutoCompactTokenLimitScopeOverride
                || hasModelInstructionsInlineOverride
                || hasModelInstructionsFileOverride
                || hasCompactPromptOverride
                || hasCompactPromptFileOverride;
        }

        private static CopilotCodexProjectTrustLevel ResolveProjectTrustLevel(
            string source,
            string normalizedProjectRoot)
        {
            var currentTableMatches = false;
            var hasTrustLevel = false;
            var result = CopilotCodexProjectTrustLevel.Unspecified;
            foreach (var rawLine in NormalizeLines(source))
            {
                var line = StripComment(rawLine).Trim();
                if (line.Length == 0)
                    continue;
                if (line[0] == '[')
                {
                    currentTableMatches = TryParseProjectTableHeader(line, out var configuredProjectPath)
                        && string.Equals(configuredProjectPath, normalizedProjectRoot, StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                if (!currentTableMatches)
                    continue;

                var equalsIndex = line.IndexOf('=');
                if (equalsIndex <= 0
                    || !string.Equals(line[..equalsIndex].Trim(), TrustLevelKey, StringComparison.Ordinal))
                {
                    continue;
                }
                if (hasTrustLevel)
                    return CopilotCodexProjectTrustLevel.Invalid;
                hasTrustLevel = true;

                var value = line[(equalsIndex + 1)..].Trim();
                var index = 0;
                if (!TryReadTomlString(value, ref index, out var trustLevel))
                    return CopilotCodexProjectTrustLevel.Invalid;
                SkipWhitespace(value, ref index);
                if (index != value.Length)
                    return CopilotCodexProjectTrustLevel.Invalid;

                result = trustLevel switch
                {
                    "trusted" => CopilotCodexProjectTrustLevel.Trusted,
                    "untrusted" => CopilotCodexProjectTrustLevel.Untrusted,
                    _ => CopilotCodexProjectTrustLevel.Invalid,
                };
                if (result == CopilotCodexProjectTrustLevel.Invalid)
                    return result;
            }

            return result;
        }

        internal static bool TryParseProjectTableHeader(string line, out string normalizedProjectPath)
        {
            normalizedProjectPath = string.Empty;
            if (line.Length < 4
                || line[0] != '['
                || line[^1] != ']'
                || line[1] == '[')
            {
                return false;
            }

            var tableName = line[1..^1].Trim();
            if (!tableName.StartsWith(ProjectsTablePrefix, StringComparison.Ordinal))
                return false;

            var index = ProjectsTablePrefix.Length;
            if (!TryReadTomlString(tableName, ref index, out var configuredProjectPath))
                return false;
            SkipWhitespace(tableName, ref index);
            if (index != tableName.Length)
                return false;

            normalizedProjectPath = NormalizeConfiguredProjectPath(configuredProjectPath);
            return normalizedProjectPath.Length > 0;
        }

        private static string NormalizeConfiguredProjectPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length > 2_048)
                return string.Empty;

            try
            {
                var trimmed = path.Trim();
                if (!Path.IsPathFullyQualified(trimmed))
                    return string.Empty;
                return Path.TrimEndingDirectorySeparator(Path.GetFullPath(trimmed));
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string NormalizeTrustedProjectRootPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length > 2_048)
                return string.Empty;

            try
            {
                var trimmed = path.Trim();
                if (!Path.IsPathFullyQualified(trimmed))
                    return string.Empty;
                var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(trimmed));
                return fullPath.Length <= 2_048
                    && Directory.Exists(fullPath)
                    && !CopilotWorkspaceSearchSupport.HasReparsePointInPath(fullPath)
                        ? fullPath
                        : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static IEnumerable<TomlAssignment> EnumerateSupportedAssignments(string source)
        {
            var lines = NormalizeLines(source);
            var inRootTable = true;
            var inFeaturesTable = false;
            var inToolRegistryTable = false;
            var inAgentsTable = false;
            var inAutoReviewTable = false;
            var inExperimentalRequestUserInputTable = false;
            var inUpdatePlanTable = false;
            var inSkillsTable = false;
            for (var index = 0; index < lines.Length; index++)
            {
                var line = StripComment(lines[index]).Trim();
                if (line.Length == 0)
                    continue;
                if (line[0] == '[')
                {
                    inRootTable = false;
                    inFeaturesTable = IsExactTableHeader(line, "features");
                    inToolRegistryTable = IsExactTableHeader(line, "features.tool_registry");
                    inAgentsTable = IsExactTableHeader(line, "agents");
                    inAutoReviewTable = IsExactTableHeader(line, "auto_review");
                    inExperimentalRequestUserInputTable = IsExactTableHeader(
                        line,
                        "tools.experimental_request_user_input");
                    inUpdatePlanTable = IsExactTableHeader(line, "tools.update_plan");
                    inSkillsTable = IsExactTableHeader(line, "skills");
                    continue;
                }

                var equalsIndex = line.IndexOf('=');
                if (equalsIndex <= 0)
                    continue;
                var parsedKey = line[..equalsIndex].Trim();
                var key = inFeaturesTable
                    ? parsedKey switch
                    {
                        PreventIdleSleepFeatureKey => PreventIdleSleepKey,
                        ShellToolEnabledFeatureKey => ShellToolEnabledKey,
                        HooksEnabledFeatureKey => HooksEnabledKey,
                        PluginsEnabledFeatureKey => PluginsEnabledKey,
                        MentionsV2EnabledFeatureKey => MentionsV2EnabledKey,
                        SkillMcpDependencyInstallEnabledFeatureKey => SkillMcpDependencyInstallEnabledKey,
                        GuardianApprovalEnabledFeatureKey => GuardianApprovalEnabledKey,
                        PersonalityEnabledFeatureKey => PersonalityEnabledKey,
                        FastModeEnabledFeatureKey => FastModeEnabledKey,
                        MultiAgentEnabledFeatureKey => MultiAgentEnabledKey,
                        GoalsEnabledFeatureKey => GoalsEnabledKey,
                        DefaultModeRequestUserInputEnabledFeatureKey => DefaultModeRequestUserInputEnabledKey,
                        LegacyWebSearchFeatureKey => LegacyWebSearchKey,
                        LegacyWebSearchCachedFeatureKey => LegacyWebSearchCachedKey,
                        LegacyWebSearchRequestFeatureKey => LegacyWebSearchRequestKey,
                        _ => string.Empty,
                    }
                        : inToolRegistryTable
                            && string.Equals(parsedKey, ErrorOnToolCollisionsTableKey, StringComparison.Ordinal)
                                ? ErrorOnToolCollisionsKey
                        : inAgentsTable
                            ? parsedKey switch
                            {
                                AgentsEnabledTableKey => AgentsEnabledKey,
                                AgentsInterruptMessageTableKey => AgentsInterruptMessageKey,
                                AgentsMaximumConcurrentThreadsTableKey => AgentsMaximumConcurrentThreadsKey,
                                AgentsMaximumThreadsTableKey => AgentsMaximumThreadsKey,
                                AgentsDefaultSubagentModelTableKey => AgentsDefaultSubagentModelKey,
                                AgentsDefaultSubagentReasoningEffortTableKey => AgentsDefaultSubagentReasoningEffortKey,
                                _ => string.Empty,
                            }
                        : inAutoReviewTable
                            && string.Equals(parsedKey, AutoReviewPolicyTableKey, StringComparison.Ordinal)
                                ? AutoReviewPolicyKey
                        : inExperimentalRequestUserInputTable
                            && string.Equals(parsedKey, ToolsEnabledTableKey, StringComparison.Ordinal)
                                ? ExperimentalRequestUserInputEnabledKey
                        : inUpdatePlanTable
                            && string.Equals(parsedKey, ToolsEnabledTableKey, StringComparison.Ordinal)
                                ? UpdatePlanEnabledKey
                        : inSkillsTable
                            && string.Equals(parsedKey, SkillsIncludeInstructionsTableKey, StringComparison.Ordinal)
                                ? SkillsIncludeInstructionsKey
                        : inRootTable
                            ? parsedKey
                            : string.Empty;
                if (!string.Equals(key, MaximumBytesKey, StringComparison.Ordinal)
                    && !string.Equals(key, FallbackFileNamesKey, StringComparison.Ordinal)
                    && !string.Equals(key, ProjectRootMarkersKey, StringComparison.Ordinal)
                    && !string.Equals(key, DeveloperInstructionsKey, StringComparison.Ordinal)
                    && !string.Equals(key, PersonalityEnabledKey, StringComparison.Ordinal)
                    && !string.Equals(key, PersonalityKey, StringComparison.Ordinal)
                    && !string.Equals(key, WebSearchKey, StringComparison.Ordinal)
                    && !string.Equals(key, LegacyWebSearchKey, StringComparison.Ordinal)
                    && !string.Equals(key, LegacyWebSearchCachedKey, StringComparison.Ordinal)
                    && !string.Equals(key, LegacyWebSearchRequestKey, StringComparison.Ordinal)
                    && !string.Equals(key, SandboxModeKey, StringComparison.Ordinal)
                    && !string.Equals(key, ApprovalPolicyKey, StringComparison.Ordinal)
                    && !string.Equals(key, ApprovalsReviewerKey, StringComparison.Ordinal)
                    && !string.Equals(key, GuardianApprovalEnabledKey, StringComparison.Ordinal)
                    && !string.Equals(key, AutoReviewPolicyKey, StringComparison.Ordinal)
                    && !string.Equals(key, ModelKey, StringComparison.Ordinal)
                    && !string.Equals(key, ReviewModelKey, StringComparison.Ordinal)
                    && !string.Equals(key, PreventIdleSleepKey, StringComparison.Ordinal)
                    && !string.Equals(key, ShellToolEnabledKey, StringComparison.Ordinal)
                    && !string.Equals(key, HooksEnabledKey, StringComparison.Ordinal)
                    && !string.Equals(key, PluginsEnabledKey, StringComparison.Ordinal)
                    && !string.Equals(key, ErrorOnToolCollisionsKey, StringComparison.Ordinal)
                    && !string.Equals(key, MentionsV2EnabledKey, StringComparison.Ordinal)
                    && !string.Equals(key, SkillMcpDependencyInstallEnabledKey, StringComparison.Ordinal)
                    && !string.Equals(key, GoalsEnabledKey, StringComparison.Ordinal)
                    && !string.Equals(key, DefaultModeRequestUserInputEnabledKey, StringComparison.Ordinal)
                    && !string.Equals(key, ExperimentalRequestUserInputEnabledKey, StringComparison.Ordinal)
                    && !string.Equals(key, UpdatePlanEnabledKey, StringComparison.Ordinal)
                    && !string.Equals(key, IncludePermissionsInstructionsKey, StringComparison.Ordinal)
                    && !string.Equals(key, IncludeCollaborationModeInstructionsKey, StringComparison.Ordinal)
                    && !string.Equals(key, IncludeEnvironmentContextKey, StringComparison.Ordinal)
                    && !string.Equals(key, SkillsIncludeInstructionsKey, StringComparison.Ordinal)
                    && !string.Equals(key, MultiAgentEnabledKey, StringComparison.Ordinal)
                    && !string.Equals(key, AgentsEnabledKey, StringComparison.Ordinal)
                    && !string.Equals(key, AgentsInterruptMessageKey, StringComparison.Ordinal)
                    && !string.Equals(key, AgentsMaximumConcurrentThreadsKey, StringComparison.Ordinal)
                    && !string.Equals(key, AgentsMaximumThreadsKey, StringComparison.Ordinal)
                    && !string.Equals(key, AgentsDefaultSubagentModelKey, StringComparison.Ordinal)
                    && !string.Equals(key, AgentsDefaultSubagentReasoningEffortKey, StringComparison.Ordinal)
                    && !string.Equals(key, ModelContextWindowKey, StringComparison.Ordinal)
                    && !string.Equals(key, ToolOutputTokenLimitKey, StringComparison.Ordinal)
                    && !string.Equals(key, ModelReasoningEffortKey, StringComparison.Ordinal)
                    && !string.Equals(key, PlanModeReasoningEffortKey, StringComparison.Ordinal)
                    && !string.Equals(key, ModelReasoningSummaryKey, StringComparison.Ordinal)
                    && !string.Equals(key, ModelSupportsReasoningSummariesKey, StringComparison.Ordinal)
                    && !string.Equals(key, HideAgentReasoningKey, StringComparison.Ordinal)
                    && !string.Equals(key, FastModeEnabledKey, StringComparison.Ordinal)
                    && !string.Equals(key, ServiceTierKey, StringComparison.Ordinal)
                    && !string.Equals(key, ModelVerbosityKey, StringComparison.Ordinal)
                    && !string.Equals(key, ModelAutoCompactTokenLimitKey, StringComparison.Ordinal)
                    && !string.Equals(key, ModelAutoCompactTokenLimitScopeKey, StringComparison.Ordinal)
                    && !string.Equals(key, ModelInstructionsKey, StringComparison.Ordinal)
                    && !string.Equals(key, ModelInstructionsFileKey, StringComparison.Ordinal)
                    && !string.Equals(key, CompactPromptKey, StringComparison.Ordinal)
                    && !string.Equals(key, ExperimentalCompactPromptFileKey, StringComparison.Ordinal))
                {
                    continue;
                }

                var value = line[(equalsIndex + 1)..].Trim();
                if ((string.Equals(key, FallbackFileNamesKey, StringComparison.Ordinal)
                        || string.Equals(key, ProjectRootMarkersKey, StringComparison.Ordinal))
                    && value.StartsWith('[')
                    && !HasClosedArray(value))
                {
                    var builder = new StringBuilder(value);
                    for (var logicalLine = 1;
                        logicalLine < MaximumLogicalValueLines && index + 1 < lines.Length;
                        logicalLine++)
                    {
                        index++;
                        var continuation = StripComment(lines[index]).Trim();
                        if (continuation.Length > 0)
                            builder.Append(' ').Append(continuation);
                        if (HasClosedArray(builder.ToString()))
                            break;
                    }
                    value = builder.ToString();
                }
                else if (string.Equals(key, ApprovalPolicyKey, StringComparison.Ordinal)
                    && value.StartsWith('{')
                    && !HasClosedInlineTable(value))
                {
                    var builder = new StringBuilder(value);
                    for (var logicalLine = 1;
                        logicalLine < MaximumLogicalValueLines && index + 1 < lines.Length;
                        logicalLine++)
                    {
                        index++;
                        var continuation = StripComment(lines[index]).Trim();
                        if (continuation.Length > 0)
                            builder.Append(' ').Append(continuation);
                        if (HasClosedInlineTable(builder.ToString()))
                            break;
                    }
                    value = builder.ToString();
                }
                else if ((string.Equals(key, DeveloperInstructionsKey, StringComparison.Ordinal)
                        || string.Equals(key, ModelInstructionsKey, StringComparison.Ordinal)
                        || string.Equals(key, CompactPromptKey, StringComparison.Ordinal)
                        || string.Equals(key, AutoReviewPolicyKey, StringComparison.Ordinal))
                    && TryGetMultilineStringDelimiter(value, out var delimiter)
                    && !HasClosedMultilineString(value, delimiter))
                {
                    var builder = new StringBuilder(value);
                    for (var logicalLine = 1;
                        logicalLine < MaximumConfiguredTextLines && index + 1 < lines.Length;
                        logicalLine++)
                    {
                        index++;
                        builder.Append('\n').Append(lines[index]);
                        if (HasClosedMultilineString(builder.ToString(), delimiter))
                            break;
                    }
                    value = builder.ToString();
                }

                yield return new TomlAssignment(key, value);
            }
        }

        private static bool IsExactTableHeader(string line, string tableName)
        {
            if (string.IsNullOrWhiteSpace(line)
                || string.IsNullOrWhiteSpace(tableName)
                || line.Length < 3
                || line[0] != '['
                || line[1] == '['
                || line[^1] != ']')
            {
                return false;
            }

            return string.Equals(
                line[1..^1].Trim(),
                tableName,
                StringComparison.Ordinal);
        }

        private static string[] NormalizeLines(string source) =>
            (source ?? string.Empty)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n');

        internal static string StripComment(string line)
        {
            var quote = '\0';
            var escaped = false;
            for (var index = 0; index < line.Length; index++)
            {
                var current = line[index];
                if (quote == '"')
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }
                    if (current == '\\')
                    {
                        escaped = true;
                        continue;
                    }
                    if (current == quote)
                        quote = '\0';
                    continue;
                }
                if (quote == '\'')
                {
                    if (current == quote)
                        quote = '\0';
                    continue;
                }
                if (current is '"' or '\'')
                {
                    quote = current;
                    continue;
                }
                if (current == '#')
                    return line[..index];
            }
            return line;
        }

        private static bool HasClosedArray(string value)
        {
            var quote = '\0';
            var escaped = false;
            var depth = 0;
            foreach (var current in value)
            {
                if (quote == '"')
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }
                    if (current == '\\')
                    {
                        escaped = true;
                        continue;
                    }
                    if (current == quote)
                        quote = '\0';
                    continue;
                }
                if (quote == '\'')
                {
                    if (current == quote)
                        quote = '\0';
                    continue;
                }
                if (current is '"' or '\'')
                {
                    quote = current;
                    continue;
                }
                if (current == '[')
                    depth++;
                else if (current == ']' && depth > 0 && --depth == 0)
                    return true;
            }
            return false;
        }

        private static bool HasClosedInlineTable(string value)
        {
            var quote = '\0';
            var escaped = false;
            var depth = 0;
            foreach (var current in value)
            {
                if (quote == '"')
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }
                    if (current == '\\')
                    {
                        escaped = true;
                        continue;
                    }
                    if (current == quote)
                        quote = '\0';
                    continue;
                }
                if (quote == '\'')
                {
                    if (current == quote)
                        quote = '\0';
                    continue;
                }
                if (current is '"' or '\'')
                {
                    quote = current;
                    continue;
                }
                if (current == '{')
                    depth++;
                else if (current == '}' && depth > 0 && --depth == 0)
                    return true;
            }
            return false;
        }

        private static bool TryParseMaximumBytes(string value, out int maximumBytes)
        {
            maximumBytes = DefaultMaximumBytes;
            var normalized = (value ?? string.Empty).Replace("_", string.Empty, StringComparison.Ordinal).Trim();
            if (!int.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                || parsed < MinimumMaximumBytes
                || parsed > MaximumMaximumBytes)
            {
                return false;
            }
            maximumBytes = parsed;
            return true;
        }

        private static bool TryParseModelContextWindowTokens(
            string value,
            out int contextWindowTokens)
        {
            contextWindowTokens = 0;
            var normalized = (value ?? string.Empty)
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Trim();
            return int.TryParse(
                    normalized,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out contextWindowTokens)
                && contextWindowTokens >= CopilotAgentTokenBudget.MinimumContextWindowTokens
                && contextWindowTokens <= CopilotAgentTokenBudget.MaximumContextWindowTokens;
        }

        private static bool TryParsePositiveTokenLimit(
            string value,
            out int tokenLimit)
        {
            tokenLimit = 0;
            var normalized = (value ?? string.Empty)
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Trim();
            return int.TryParse(
                    normalized,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out tokenLimit)
                && tokenLimit > 0;
        }

        private static bool TryParsePositiveInteger(string value, out int result)
        {
            result = 0;
            var normalized = (value ?? string.Empty)
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Trim();
            return int.TryParse(
                    normalized,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out result)
                && result > 0;
        }

        private static bool TryParseToolOutputTokenLimit(
            string value,
            out int tokenLimit)
        {
            tokenLimit = 0;
            var normalized = (value ?? string.Empty)
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Trim();
            return int.TryParse(
                    normalized,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out tokenLimit)
                && tokenLimit >= CopilotFrameworkToolResultFormatter.MinimumConfiguredTokenLimit;
        }

        private static bool TryParseTomlBoolean(string value, out bool result)
        {
            switch (value.Trim())
            {
                case "true":
                    result = true;
                    return true;
                case "false":
                    result = false;
                    return true;
                default:
                    result = false;
                    return false;
            }
        }

        private static bool TryParseApprovalPolicy(
            string value,
            out CopilotCodexApprovalPolicy policy)
        {
            policy = CopilotCodexApprovalPolicy.Unspecified;
            if (TryParseConfiguredText(
                    value,
                    MaximumPersonalityCharacters,
                    out var scalar))
            {
                return CopilotCodexApprovalPolicySelection.TryParseScalar(scalar, out policy);
            }

            var index = 0;
            SkipWhitespace(value, ref index);
            if (!TryConsume(value, ref index, '{')
                || !TryReadBareTomlKey(value, ref index, out var outerKey)
                || !string.Equals(outerKey, "granular", StringComparison.Ordinal))
            {
                return false;
            }
            SkipWhitespace(value, ref index);
            if (!TryConsume(value, ref index, '='))
                return false;
            SkipWhitespace(value, ref index);
            if (!TryConsume(value, ref index, '{'))
                return false;

            var sandboxApproval = false;
            var rules = false;
            var mcpElicitations = false;
            var requestPermissions = false;
            var skillApproval = false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (true)
            {
                SkipWhitespace(value, ref index);
                if (TryConsume(value, ref index, '}'))
                    break;
                if (!TryReadBareTomlKey(value, ref index, out var key)
                    || !seen.Add(key))
                {
                    return false;
                }
                SkipWhitespace(value, ref index);
                if (!TryConsume(value, ref index, '='))
                    return false;
                SkipWhitespace(value, ref index);
                if (!TryReadTomlBoolean(value, ref index, out var enabled))
                    return false;

                switch (key)
                {
                    case "sandbox_approval":
                        sandboxApproval = enabled;
                        break;
                    case "rules":
                        rules = enabled;
                        break;
                    case "mcp_elicitations":
                        mcpElicitations = enabled;
                        break;
                    case "request_permissions":
                        requestPermissions = enabled;
                        break;
                    case "skill_approval":
                        skillApproval = enabled;
                        break;
                    default:
                        return false;
                }

                SkipWhitespace(value, ref index);
                if (TryConsume(value, ref index, ','))
                    continue;
                if (index >= value.Length || value[index] != '}')
                    return false;
            }

            if (!seen.Contains("sandbox_approval")
                || !seen.Contains("rules")
                || !seen.Contains("mcp_elicitations"))
            {
                return false;
            }
            SkipWhitespace(value, ref index);
            if (!TryConsume(value, ref index, '}'))
                return false;
            SkipWhitespace(value, ref index);
            if (index != value.Length)
                return false;

            policy = CopilotCodexApprovalPolicy.CreateGranular(
                sandboxApproval,
                rules,
                mcpElicitations,
                requestPermissions,
                skillApproval);
            return true;
        }

        private static bool TryReadBareTomlKey(
            string value,
            ref int index,
            out string key)
        {
            SkipWhitespace(value, ref index);
            var start = index;
            while (index < value.Length
                && (char.IsAsciiLetterOrDigit(value[index])
                    || value[index] is '_' or '-'))
            {
                index++;
            }
            key = value[start..index];
            return key.Length > 0;
        }

        private static bool TryReadTomlBoolean(
            string value,
            ref int index,
            out bool result)
        {
            if (value.AsSpan(index).StartsWith("true", StringComparison.Ordinal))
            {
                index += 4;
                result = true;
                return true;
            }
            if (value.AsSpan(index).StartsWith("false", StringComparison.Ordinal))
            {
                index += 5;
                result = false;
                return true;
            }
            result = false;
            return false;
        }

        private static bool TryParseFallbackFileNames(string value, out string[] fallbackFileNames)
        {
            fallbackFileNames = Array.Empty<string>();
            var index = 0;
            SkipWhitespace(value, ref index);
            if (!TryConsume(value, ref index, '['))
                return false;

            var results = new List<string>();
            var expectValue = true;
            while (index < value.Length)
            {
                SkipWhitespace(value, ref index);
                if (TryConsume(value, ref index, ']'))
                {
                    SkipWhitespace(value, ref index);
                    if (index != value.Length)
                        return false;
                    fallbackFileNames = results.ToArray();
                    return true;
                }
                if (!expectValue || !TryReadTomlString(value, ref index, out var candidate))
                    return false;

                var normalized = NormalizeFallbackFileName(candidate);
                if (normalized.Length > 0
                    && !results.Contains(normalized, StringComparer.OrdinalIgnoreCase)
                    && results.Count < MaximumFallbackFileNames)
                {
                    results.Add(normalized);
                }

                SkipWhitespace(value, ref index);
                if (TryConsume(value, ref index, ','))
                {
                    expectValue = true;
                    continue;
                }
                expectValue = false;
            }
            return false;
        }

        private static bool TryParseProjectRootMarkers(string value, out string[] projectRootMarkers)
        {
            projectRootMarkers = Array.Empty<string>();
            var index = 0;
            SkipWhitespace(value, ref index);
            if (!TryConsume(value, ref index, '['))
                return false;

            var results = new List<string>();
            var expectValue = true;
            while (index < value.Length)
            {
                SkipWhitespace(value, ref index);
                if (TryConsume(value, ref index, ']'))
                {
                    SkipWhitespace(value, ref index);
                    if (index != value.Length)
                        return false;
                    projectRootMarkers = results.ToArray();
                    return true;
                }
                if (!expectValue
                    || !TryReadTomlString(value, ref index, out var candidate)
                    || results.Count >= MaximumProjectRootMarkers)
                {
                    return false;
                }

                var normalized = NormalizeProjectRootMarker(candidate);
                if (normalized.Length == 0)
                    return false;
                if (!results.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    results.Add(normalized);

                SkipWhitespace(value, ref index);
                if (TryConsume(value, ref index, ','))
                {
                    expectValue = true;
                    continue;
                }
                expectValue = false;
            }
            return false;
        }

        private static bool TryParseDeveloperInstructions(
            string value,
            out string developerInstructions)
            => TryParseConfiguredText(
                value,
                MaximumDeveloperInstructionCharacters,
                out developerInstructions);

        private static bool TryParseConfiguredText(
            string value,
            int maximumCharacters,
            out string configuredText)
        {
            configuredText = string.Empty;
            var index = 0;
            SkipWhitespace(value, ref index);
            string parsed;
            if (TryGetMultilineStringDelimiter(value[index..], out var delimiter))
            {
                if (!TryReadTomlMultilineString(value, ref index, delimiter, out parsed))
                    return false;
            }
            else if (!TryReadTomlString(value, ref index, out parsed))
            {
                return false;
            }

            var trailing = StripComment(value[index..]).Trim();
            if (trailing.Length > 0)
                return false;

            parsed = parsed.Trim();
            if (parsed.Length > maximumCharacters || parsed.IndexOf('\0') >= 0)
                return false;
            configuredText = parsed;
            return true;
        }

        private static bool TryGetMultilineStringDelimiter(string value, out string delimiter)
        {
            delimiter = string.Empty;
            var normalized = value.AsSpan().TrimStart();
            if (normalized.StartsWith("\"\"\"", StringComparison.Ordinal))
            {
                delimiter = "\"\"\"";
                return true;
            }
            if (normalized.StartsWith("'''", StringComparison.Ordinal))
            {
                delimiter = "'''";
                return true;
            }
            return false;
        }

        private static bool HasClosedMultilineString(string value, string delimiter)
        {
            var start = value.IndexOf(delimiter, StringComparison.Ordinal);
            return start >= 0
                && FindClosingMultilineString(value, delimiter, start + delimiter.Length) >= 0;
        }

        private static bool TryReadTomlMultilineString(
            string value,
            ref int index,
            string delimiter,
            out string result)
        {
            result = string.Empty;
            if (!value.AsSpan(index).StartsWith(delimiter, StringComparison.Ordinal))
                return false;

            index += delimiter.Length;
            var closingIndex = FindClosingMultilineString(value, delimiter, index);
            if (closingIndex < 0)
                return false;

            var content = value[index..closingIndex];
            if (content.StartsWith('\n'))
                content = content[1..];
            if (delimiter[0] == '\'')
            {
                result = content;
            }
            else if (!TryDecodeTomlBasicMultilineString(content, out result))
            {
                return false;
            }

            index = closingIndex + delimiter.Length;
            return true;
        }

        private static int FindClosingMultilineString(
            string value,
            string delimiter,
            int startIndex)
        {
            for (var index = startIndex; index <= value.Length - delimiter.Length; index++)
            {
                if (!value.AsSpan(index).StartsWith(delimiter, StringComparison.Ordinal))
                    continue;
                if (delimiter[0] == '\'' || CountPrecedingBackslashes(value, index) % 2 == 0)
                    return index;
            }
            return -1;
        }

        private static int CountPrecedingBackslashes(string value, int index)
        {
            var count = 0;
            while (index > 0 && value[--index] == '\\')
                count++;
            return count;
        }

        private static bool TryDecodeTomlBasicMultilineString(string value, out string result)
        {
            var builder = new StringBuilder(value.Length);
            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];
                if (current != '\\')
                {
                    if (current == '\0')
                    {
                        result = string.Empty;
                        return false;
                    }
                    builder.Append(current);
                    continue;
                }

                if (++index >= value.Length)
                {
                    result = string.Empty;
                    return false;
                }
                var escaped = value[index];
                if (escaped == '\n')
                {
                    while (index + 1 < value.Length && char.IsWhiteSpace(value[index + 1]))
                        index++;
                    continue;
                }

                switch (escaped)
                {
                    case 'b': builder.Append('\b'); break;
                    case 't': builder.Append('\t'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'r': builder.Append('\r'); break;
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case 'u':
                        index++;
                        if (!TryReadUnicodeEscape(value, ref index, 4, builder))
                        {
                            result = string.Empty;
                            return false;
                        }
                        index--;
                        break;
                    case 'U':
                        index++;
                        if (!TryReadUnicodeEscape(value, ref index, 8, builder))
                        {
                            result = string.Empty;
                            return false;
                        }
                        index--;
                        break;
                    default:
                        result = string.Empty;
                        return false;
                }
            }
            result = builder.ToString();
            return true;
        }

        private static bool TryReadTomlString(string value, ref int index, out string result)
        {
            result = string.Empty;
            if (index >= value.Length || value[index] is not ('"' or '\''))
                return false;

            var quote = value[index++];
            var builder = new StringBuilder();
            while (index < value.Length)
            {
                var current = value[index++];
                if (current == quote)
                {
                    result = builder.ToString();
                    return true;
                }
                if (quote == '\'' || current != '\\')
                {
                    builder.Append(current);
                    continue;
                }
                if (index >= value.Length)
                    return false;

                var escaped = value[index++];
                switch (escaped)
                {
                    case 'b': builder.Append('\b'); break;
                    case 't': builder.Append('\t'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'r': builder.Append('\r'); break;
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case 'u':
                        if (!TryReadUnicodeEscape(value, ref index, 4, builder))
                            return false;
                        break;
                    case 'U':
                        if (!TryReadUnicodeEscape(value, ref index, 8, builder))
                            return false;
                        break;
                    default:
                        return false;
                }
            }
            return false;
        }

        private static bool TryReadUnicodeEscape(string value, ref int index, int digits, StringBuilder builder)
        {
            if (index + digits > value.Length
                || !int.TryParse(value.AsSpan(index, digits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var codePoint)
                || !Rune.IsValid(codePoint))
            {
                return false;
            }
            builder.Append(new Rune(codePoint).ToString());
            index += digits;
            return true;
        }

        private static string NormalizeFallbackFileName(string? value)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (normalized.Length == 0
                || normalized.Length > MaximumFallbackFileNameCharacters
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

        private static void SkipWhitespace(string value, ref int index)
        {
            while (index < value.Length && char.IsWhiteSpace(value[index]))
                index++;
        }

        private static bool TryConsume(string value, ref int index, char expected)
        {
            if (index >= value.Length || value[index] != expected)
                return false;
            index++;
            return true;
        }

        private sealed record TomlAssignment(string Key, string Value);

        private sealed record ProjectInstructionConfigLayer(
            int MaximumBytes,
            IReadOnlyList<string> FallbackFileNames,
            IReadOnlyList<string> ProjectRootMarkers,
            string DeveloperInstructions,
            bool HasMaximumBytesOverride,
            bool HasFallbackFileNamesOverride,
            bool HasProjectRootMarkersOverride,
            bool HasDeveloperInstructionsOverride)
        {
            public string ModelInstructions { get; init; } = string.Empty;

            public bool HasModelInstructionsInlineOverride { get; init; }

            public string ConfiguredModelInstructionsFilePath { get; init; } = string.Empty;

            public string ModelInstructionsFileContent { get; init; } = string.Empty;

            public string ModelInstructionsSourceFilePath { get; init; } = string.Empty;

            public bool HasModelInstructionsFileOverride { get; init; }

            public bool PersonalityEnabled { get; init; } = true;

            public bool HasPersonalityEnabledOverride { get; init; }

            public CopilotResponsePersonality Personality { get; init; } =
                CopilotResponsePersonality.None;

            public bool HasPersonalityOverride { get; init; }

            public CopilotCodexWebSearchConfigState WebSearchConfigState { get; init; }

            public CopilotCodexSandboxMode SandboxMode { get; init; } =
                CopilotCodexSandboxMode.Unspecified;

            public bool HasSandboxModeOverride { get; init; }

            public CopilotCodexApprovalPolicy ApprovalPolicy { get; init; } =
                CopilotCodexApprovalPolicy.Unspecified;

            public bool HasApprovalPolicyOverride { get; init; }

            public CopilotCodexApprovalsReviewer ApprovalsReviewer { get; init; } =
                CopilotCodexApprovalsReviewer.Unspecified;

            public bool HasApprovalsReviewerOverride { get; init; }

            public bool GuardianApprovalEnabled { get; init; } = true;

            public bool HasGuardianApprovalEnabledOverride { get; init; }

            public string AutoReviewPolicy { get; init; } = string.Empty;

            public bool HasAutoReviewPolicyOverride { get; init; }

            public string Model { get; init; } = string.Empty;

            public bool HasModelOverride { get; init; }

            public string ReviewModel { get; init; } = string.Empty;

            public bool HasReviewModelOverride { get; init; }

            public bool PreventIdleSleep { get; init; }

            public bool HasPreventIdleSleepOverride { get; init; }

            public bool ShellToolEnabled { get; init; } = true;

            public bool HasShellToolEnabledOverride { get; init; }

            public bool HooksEnabled { get; init; } = true;

            public bool HasHooksEnabledOverride { get; init; }

            public bool PluginsEnabled { get; init; } = true;

            public bool HasPluginsEnabledOverride { get; init; }

            public bool ErrorOnToolCollisions { get; init; }

            public bool HasErrorOnToolCollisionsOverride { get; init; }

            public bool MentionsV2Enabled { get; init; } = true;

            public bool HasMentionsV2EnabledOverride { get; init; }

            public bool SkillMcpDependencyInstallEnabled { get; init; } = true;

            public bool HasSkillMcpDependencyInstallEnabledOverride { get; init; }

            public CopilotCodexShellEnvironmentPolicyLayer ShellEnvironmentPolicyLayer { get; init; } =
                CopilotCodexShellEnvironmentPolicyLayer.Empty;

            public bool GoalsEnabled { get; init; } = true;

            public bool HasGoalsEnabledOverride { get; init; }

            public bool DefaultModeRequestUserInputEnabled { get; init; }

            public bool HasDefaultModeRequestUserInputEnabledOverride { get; init; }

            public bool ExperimentalRequestUserInputEnabled { get; init; } = true;

            public bool HasExperimentalRequestUserInputEnabledOverride { get; init; }

            public bool UpdatePlanEnabled { get; init; } = true;

            public bool HasUpdatePlanEnabledOverride { get; init; }

            public bool IncludePermissionsInstructions { get; init; } = true;

            public bool HasIncludePermissionsInstructionsOverride { get; init; }

            public bool IncludeCollaborationModeInstructions { get; init; } = true;

            public bool HasIncludeCollaborationModeInstructionsOverride { get; init; }

            public bool IncludeEnvironmentContext { get; init; } = true;

            public bool HasIncludeEnvironmentContextOverride { get; init; }

            public bool IncludeSkillInstructions { get; init; } = true;

            public bool HasIncludeSkillInstructionsOverride { get; init; }

            public bool MultiAgentEnabled { get; init; } = true;

            public bool HasMultiAgentEnabledOverride { get; init; }

            public bool AgentsEnabled { get; init; } = true;

            public bool HasAgentsEnabledOverride { get; init; }

            public bool InterruptMessageEnabled { get; init; } = true;

            public bool HasInterruptMessageOverride { get; init; }

            public int MaximumConcurrentSubagentRuns { get; init; } =
                CopilotSubagentCoordinator.DefaultMaximumConcurrentRuns;

            public bool HasMaximumConcurrentSubagentRunsOverride { get; init; }

            public string DefaultSubagentModel { get; init; } = string.Empty;

            public bool HasDefaultSubagentModelOverride { get; init; }

            public CopilotCodexReasoningEffort DefaultSubagentReasoningEffort { get; init; } =
                CopilotCodexReasoningEffort.Unspecified;

            public bool HasDefaultSubagentReasoningEffortOverride { get; init; }

            public int ModelContextWindowTokens { get; init; }

            public bool HasModelContextWindowOverride { get; init; }

            public int ToolOutputTokenLimit { get; init; }

            public bool HasToolOutputTokenLimitOverride { get; init; }

            public CopilotCodexReasoningEffort ModelReasoningEffort { get; init; } =
                CopilotCodexReasoningEffort.Unspecified;

            public bool HasModelReasoningEffortOverride { get; init; }

            public CopilotCodexReasoningEffort PlanModeReasoningEffort { get; init; } =
                CopilotCodexReasoningEffort.Unspecified;

            public bool HasPlanModeReasoningEffortOverride { get; init; }

            public CopilotCodexReasoningSummary ModelReasoningSummary { get; init; } =
                CopilotCodexReasoningSummary.Unspecified;

            public bool HasModelReasoningSummaryOverride { get; init; }

            public bool ModelSupportsReasoningSummaries { get; init; }

            public bool HasModelSupportsReasoningSummariesOverride { get; init; }

            public bool HideAgentReasoning { get; init; }

            public bool HasHideAgentReasoningOverride { get; init; }

            public bool FastModeEnabled { get; init; } = true;

            public bool HasFastModeEnabledOverride { get; init; }

            public string ServiceTier { get; init; } = string.Empty;

            public bool HasServiceTierOverride { get; init; }

            public CopilotCodexModelVerbosity ModelVerbosity { get; init; } =
                CopilotCodexModelVerbosity.Unspecified;

            public bool HasModelVerbosityOverride { get; init; }

            public int ModelAutoCompactTokenLimit { get; init; }

            public bool HasModelAutoCompactTokenLimitOverride { get; init; }

            public CopilotModelAutoCompactTokenLimitScope ModelAutoCompactTokenLimitScope { get; init; } =
                CopilotModelAutoCompactTokenLimitScope.Unspecified;

            public bool HasModelAutoCompactTokenLimitScopeOverride { get; init; }

            public string CompactPrompt { get; init; } = string.Empty;

            public string ConfiguredCompactPromptFilePath { get; init; } = string.Empty;

            public string CompactPromptFileContent { get; init; } = string.Empty;

            public string CompactPromptSourceFilePath { get; init; } = string.Empty;

            public bool HasCompactPromptOverride { get; init; }

            public bool HasCompactPromptFileOverride { get; init; }
        }
    }
}
