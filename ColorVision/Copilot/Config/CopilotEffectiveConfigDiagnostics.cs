using ColorVision.Copilot.Mcp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal enum CopilotConfigFileProbeState
    {
        Loaded,
        FileMissing,
        SectionMissing,
        InvalidJson,
        TooLarge,
        Unreadable,
        Unavailable,
    }

    internal sealed record CopilotConfigFileProbe(
        string FilePath,
        CopilotConfigFileProbeState State,
        int? SchemaVersion,
        bool HasAgentDefaults,
        bool HasMcpSettings,
        IReadOnlySet<string> PersistedProfileIds);

    internal sealed class CopilotEffectiveConfigDiagnosticContext
    {
        public CopilotConfig Config { get; init; } = null!;

        public CopilotChatState State { get; init; } = null!;

        public CopilotConversationRecord? Conversation { get; init; }

        public CopilotProfileConfig? SelectedProfile { get; init; }

        public CopilotAgentMode ComposerMode { get; init; } = CopilotAgentMode.Auto;

        public string ConfigFilePath { get; init; } = string.Empty;

        public string StateFilePath { get; init; } = string.Empty;

        public CopilotChatStateLoadStatus StateLoadStatus { get; init; } =
            new(CopilotChatStateLoadSource.NotAttempted);

        public CopilotHostedRunState? ConversationRunState { get; init; }

        public bool McpListenerRunning { get; init; }

        public CopilotProjectInstructionDiscoveryOptions CodexConfigOptions { get; init; } =
            CopilotProjectInstructionDiscoveryConfig.CreateDefault();
    }

    internal static class CopilotEffectiveConfigDiagnostics
    {
        private const long MaximumConfigProbeBytes = 16L * 1024 * 1024;
        private const int MaximumDisplayTextCharacters = 160;

        public static CopilotConfigFileProbe ProbeConfigFile(string? filePath)
        {
            var normalizedPath = NormalizePath(filePath);
            if (normalizedPath.Length == 0)
                return CreateProbe(normalizedPath, CopilotConfigFileProbeState.Unavailable);
            if (!File.Exists(normalizedPath))
                return CreateProbe(normalizedPath, CopilotConfigFileProbeState.FileMissing);

            try
            {
                var fileInfo = new FileInfo(normalizedPath);
                if (fileInfo.Length > MaximumConfigProbeBytes)
                    return CreateProbe(normalizedPath, CopilotConfigFileProbeState.TooLarge);

                using var stream = new FileStream(
                    normalizedPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var textReader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
                using var jsonReader = new JsonTextReader(textReader)
                {
                    DateParseHandling = DateParseHandling.None,
                };
                var document = JObject.Load(jsonReader);
                if (document.GetValue(nameof(CopilotConfig), StringComparison.OrdinalIgnoreCase) is not JObject section)
                    return CreateProbe(normalizedPath, CopilotConfigFileProbeState.SectionMissing);

                var schemaToken = section.GetValue(
                    nameof(CopilotConfig.SchemaVersion),
                    StringComparison.OrdinalIgnoreCase);
                int? schemaVersion = schemaToken?.Type == JTokenType.Integer
                    ? schemaToken.Value<int>()
                    : null;
                var profileIds = section
                    .GetValue(nameof(CopilotConfig.Profiles), StringComparison.OrdinalIgnoreCase) is JArray profiles
                    ? profiles
                        .OfType<JObject>()
                        .Select(profile => profile
                            .GetValue(nameof(CopilotProfileConfig.Id), StringComparison.OrdinalIgnoreCase)
                            ?.Value<string>()
                            ?.Trim() ?? string.Empty)
                        .Where(id => id.Length > 0)
                        .ToHashSet(StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal);
                var hasAgentDefaults = section
                    .GetValue(nameof(CopilotConfig.AgentDefaults), StringComparison.OrdinalIgnoreCase) is JObject;
                var hasMcpSettings = section.Properties().Any(property =>
                    string.Equals(property.Name, nameof(CopilotConfig.McpEnabled), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(property.Name, nameof(CopilotConfig.McpPort), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(property.Name, nameof(CopilotConfig.ExternalMcpServers), StringComparison.OrdinalIgnoreCase));
                return new CopilotConfigFileProbe(
                    normalizedPath,
                    CopilotConfigFileProbeState.Loaded,
                    schemaVersion,
                    hasAgentDefaults,
                    hasMcpSettings,
                    profileIds);
            }
            catch (JsonException)
            {
                return CreateProbe(normalizedPath, CopilotConfigFileProbeState.InvalidJson);
            }
            catch (IOException)
            {
                return CreateProbe(normalizedPath, CopilotConfigFileProbeState.Unreadable);
            }
            catch (UnauthorizedAccessException)
            {
                return CreateProbe(normalizedPath, CopilotConfigFileProbeState.Unreadable);
            }
        }

        public static string Format(CopilotEffectiveConfigDiagnosticContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(context.Config);
            ArgumentNullException.ThrowIfNull(context.State);

            var config = context.Config;
            var state = context.State;
            var conversation = context.Conversation;
            var profile = context.SelectedProfile;
            var defaults = config.AgentDefaults ?? new CopilotAgentDefaultsConfig();
            var configProbe = ProbeConfigFile(context.ConfigFilePath);
            var title = string.IsNullOrWhiteSpace(conversation?.Title)
                ? CopilotUiText.NewConversationTitle
                : NormalizeDisplayText(conversation.Title);
            var builder = new StringBuilder()
                .Append("有效配置 · ")
                .AppendLine(title)
                .AppendLine()
                .AppendLine("生效来源（基础 → 当前任务）")
                .Append("1. 内置默认 · CopilotConfig schema ")
                .Append(CopilotConfig.CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture))
                .Append(" · ChatState schema ")
                .AppendLine(CopilotChatState.CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture))
                .Append("2. 应用配置 · ")
                .AppendLine(FormatConfigProbe(configProbe, config.SchemaVersion))
                .Append("   ")
                .AppendLine(FormatPath(configProbe.FilePath))
                .Append("3. 会话状态 · ")
                .Append(FormatStateLoadStatus(context.StateLoadStatus))
                .Append(" · runtime schema ")
                .AppendLine(state.SchemaVersion.ToString(CultureInfo.InvariantCulture))
                .Append("   ")
                .AppendLine(FormatPath(context.StateFilePath))
                .Append("4. 临时运行 · ")
                .Append(context.ConversationRunState?.ToString() ?? "Idle")
                .Append(" · 权限 ")
                .AppendLine(FormatAccessMode(conversation));

            AppendProfile(
                builder,
                profile,
                configProbe,
                state,
                conversation,
                context.ComposerMode,
                context.CodexConfigOptions);
            AppendAgent(
                builder,
                defaults,
                configProbe,
                context.ComposerMode,
                context.CodexConfigOptions);
            AppendConversation(builder, state, conversation, context.CodexConfigOptions);
            AppendIntegrations(builder, config, configProbe, context.McpListenerRunning);

            builder.AppendLine()
                .AppendLine("快照边界")
                .AppendLine(context.ConversationRunState.HasValue
                    ? "当前运行已在请求启动时固定模型 Profile、Agent 预算、Skill 覆盖和外部 MCP 列表；设置修改只影响后续请求。临时权限仍受当前任务与到期时间约束。"
                    : "下一次请求会从上述当前值创建独立请求快照；临时权限仍按会话、工作区、任务与到期时间单独约束。")
                .AppendLine("来源说明：文件状态按执行命令时重新探测；应用未保留每个键的启动期来源，因此文件在启动后被修改、删除或损坏时只报告“当前文件来源未证实”。")
                .Append("脱敏：报告仅使用主配置的节、schema、属性存在性与 Profile ID 元数据；不会输出 API Key、MCP Bearer、系统提示正文、后端同步地址、外部 MCP 地址或其他配置值；模型端点仅显示 origin。");
            return builder.ToString();
        }

        private static void AppendProfile(
            StringBuilder builder,
            CopilotProfileConfig? profile,
            CopilotConfigFileProbe configProbe,
            CopilotChatState state,
            CopilotConversationRecord? conversation,
            CopilotAgentMode composerMode,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions)
        {
            builder.AppendLine()
                .AppendLine("模型 Profile");
            if (profile == null)
            {
                builder.AppendLine("- 当前没有可用 Profile。");
                AppendConfiguredModel(builder, null, composerMode, codexConfigOptions);
                AppendReviewModel(builder, null, composerMode, codexConfigOptions);
                AppendConfiguredModelInstructions(builder, codexConfigOptions, profileOverrideWins: false);
                return;
            }

            var selectionSource = string.Equals(conversation?.ProfileId, profile.Id, StringComparison.Ordinal)
                ? "会话 ProfileId"
                : string.Equals(state.ActiveProfileId, profile.Id, StringComparison.Ordinal)
                    ? "ChatState ActiveProfileId"
                    : "应用首选 Profile";
            var definitionSource = profile.IsBackendSynced
                ? "后端同步快照"
                : configProbe.PersistedProfileIds.Contains(profile.Id)
                    ? "应用配置 CopilotConfig.Profiles"
                    : configProbe.State == CopilotConfigFileProbeState.Loaded
                        ? "运行时默认或迁移"
                        : "已加载运行时 Profile（当前文件来源未证实）";
            var promptSource = profile.HasSystemPromptOverride
                ? "Profile 覆盖"
                : codexConfigOptions.HasEffectiveModelInstructions
                    ? codexConfigOptions.ModelInstructionsSourceLabel
                    : "内置默认";
            var effectiveBasePromptCharacters = profile.HasSystemPromptOverride
                ? profile.EffectiveSystemPrompt.Length
                : codexConfigOptions.HasEffectiveModelInstructions
                    ? CopilotConfiguredModelInstructions.Compose(codexConfigOptions.ModelInstructions).Length
                    : CopilotProfileConfig.DefaultSystemPrompt.Length;
            var credential = profile.CredentialNeedsReentry
                ? "需重新输入"
                : string.IsNullOrWhiteSpace(profile.ApiKey)
                    ? "缺失"
                    : "已配置";

            builder.Append("- 选择：")
                .Append(NormalizeDisplayText(profile.DisplayLabel))
                .Append(" · ")
                .Append(NormalizeDisplayText(profile.Model))
                .Append(" · 来源 ")
                .AppendLine(selectionSource)
                .Append("- 定义：")
                .Append(definitionSource)
                .Append(" · ")
                .Append(profile.VendorLabel)
                .Append(" · ")
                .AppendLine(profile.ProviderLabel)
                .Append("- 端点：")
                .Append(FormatEndpointOrigin(profile.BaseUrl))
                .Append(" · 凭据 ")
                .AppendLine(credential)
                .Append("- 推理：")
                .Append(profile.ReasoningLabel)
                .Append(" · 系统提示 ")
                .Append(promptSource)
                .Append('（')
                .Append(effectiveBasePromptCharacters.ToString("N0", CultureInfo.CurrentCulture))
                .AppendLine(" 字符）")
                .Append("- Provider 停顿：首内容 ")
                .Append(profile.FirstContentTimeoutSeconds.ToString("N0", CultureInfo.CurrentCulture))
                .Append("s · 流式静默 ")
                .Append(profile.StreamingInactivityTimeoutSeconds.ToString("N0", CultureInfo.CurrentCulture))
                .AppendLine("s");
            AppendConfiguredModel(builder, profile, composerMode, codexConfigOptions);
            AppendReviewModel(builder, profile, composerMode, codexConfigOptions);
            AppendConfiguredModelInstructions(
                builder,
                codexConfigOptions,
                profile.HasSystemPromptOverride);
        }

        private static void AppendConfiguredModel(
            StringBuilder builder,
            CopilotProfileConfig? profile,
            CopilotAgentMode composerMode,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions)
        {
            builder.Append("- Codex model：");
            if (!codexConfigOptions.HasModelOverride)
            {
                builder.AppendLine("未配置 · 使用当前 Profile 模型");
                return;
            }

            builder.Append(codexConfigOptions.ConfiguredModel)
                .Append(" · 来源 ")
                .Append(codexConfigOptions.ModelSourceLabel.Length == 0
                    ? "Codex config.toml"
                    : codexConfigOptions.ModelSourceLabel)
                .Append(" · 提交快照 · 沿用 Profile Provider/端点/凭据");
            if (profile != null)
            {
                builder.Append(" · 当前有效模型 ")
                    .Append(CopilotReviewModelSelection.ResolveEffectiveModel(
                        composerMode,
                        codexConfigOptions.ConfiguredReviewModel,
                        profile.Model,
                        codexConfigOptions.ConfiguredModel));
            }
            builder.AppendLine();
        }

        private static void AppendReviewModel(
            StringBuilder builder,
            CopilotProfileConfig? profile,
            CopilotAgentMode composerMode,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions)
        {
            builder.Append("- Codex review_model：");
            if (!codexConfigOptions.HasReviewModelOverride)
            {
                builder.AppendLine("未配置 · Review 沿用 Codex model 或当前 Profile 模型");
                return;
            }

            builder.Append(codexConfigOptions.ConfiguredReviewModel)
                .Append(" · 来源 ")
                .Append(codexConfigOptions.ReviewModelSourceLabel.Length == 0
                    ? "Codex config.toml"
                    : codexConfigOptions.ReviewModelSourceLabel)
                .Append(composerMode == CopilotAgentMode.Review
                    ? " · 当前 Review 模式生效"
                    : " · 仅 Review 模式生效，当前模式不替换")
                .Append(" · 沿用 Profile Provider/端点/凭据");
            if (profile != null)
            {
                builder.Append(" · 当前有效模型 ")
                    .Append(CopilotReviewModelSelection.ResolveEffectiveModel(
                        composerMode,
                        codexConfigOptions.ConfiguredReviewModel,
                        profile.Model,
                        codexConfigOptions.ConfiguredModel));
            }
            builder.AppendLine();
        }

        private static void AppendConfiguredModelInstructions(
            StringBuilder builder,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions,
            bool profileOverrideWins)
        {
            builder.Append("- Codex ")
                .Append(codexConfigOptions.ModelInstructionsUsesFile
                    ? "model_instructions_file"
                    : "instructions")
                .Append('：');
            if (!codexConfigOptions.HasModelInstructionsOverride)
            {
                builder.AppendLine("未配置 · 使用 Profile/内置主体");
            }
            else
            {
                builder.Append(codexConfigOptions.ModelInstructions.Length.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" 字符 · 来源 ")
                    .Append(codexConfigOptions.ModelInstructionsSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.ModelInstructionsSourceLabel)
                    .AppendLine(profileOverrideWins
                        ? " · Profile 显式覆盖优先"
                        : codexConfigOptions.HasEffectiveModelInstructions
                            ? " · 宿主安全规则强制保留"
                            : codexConfigOptions.ModelInstructionsUsesFile
                                ? " · 文件为空或未安全加载"
                                : " · 内联值为空或无效");
                if (codexConfigOptions.ModelInstructionsSourceFilePath.Length > 0)
                {
                    builder.Append("  文件：")
                        .AppendLine(FormatPath(codexConfigOptions.ModelInstructionsSourceFilePath));
                }
            }
        }

        private static void AppendAgent(
            StringBuilder builder,
            CopilotAgentDefaultsConfig defaults,
            CopilotConfigFileProbe configProbe,
            CopilotAgentMode composerMode,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions)
        {
            var source = configProbe.HasAgentDefaults
                ? "应用配置 CopilotConfig.AgentDefaults"
                : configProbe.State == CopilotConfigFileProbeState.Loaded
                    ? "内置默认或迁移"
                    : "已加载运行时值（当前文件来源未证实）";
            builder.AppendLine()
                .AppendLine("Agent")
                .Append("- 当前输入模式：")
                .Append(composerMode)
                .AppendLine(" · 来源 会话草稿")
                .Append("- 预算：context ")
                .Append(FormatNumber(defaults.ContextWindowTokens))
                .Append(" · request ")
                .Append(FormatNumber(defaults.RequestTokenBudget))
                .Append(" tokens · tools ")
                .Append(FormatNumber(defaults.MaxToolCalls))
                .Append(" · passes ")
                .Append(FormatNumber(defaults.MaxAgentPasses))
                .Append(" · timeout ")
                .Append(FormatDuration(TimeSpan.FromSeconds(defaults.TimeoutSeconds)))
                .Append(" · 来源 ")
                .AppendLine(source)
                .Append("- Codex model_context_window：");
            if (!codexConfigOptions.HasModelContextWindowOverride)
            {
                builder.Append("未配置 · 有效 ")
                    .Append(FormatNumber(defaults.ContextWindowTokens))
                    .AppendLine(" tokens · 使用应用 AgentDefaults");
            }
            else
            {
                builder.Append(FormatNumber(codexConfigOptions.ConfiguredModelContextWindowTokens))
                    .Append(" tokens · 来源 ")
                    .Append(codexConfigOptions.ModelContextWindowSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.ModelContextWindowSourceLabel)
                    .AppendLine(" · 请求快照覆盖应用默认值");
            }
            builder.Append("- Codex tool_output_token_limit：");
            if (!codexConfigOptions.HasToolOutputTokenLimitOverride)
            {
                builder.Append("未配置 · 使用 ColorVision ")
                    .Append(FormatNumber(CopilotFrameworkToolResultFormatter.MaxSerializedCharacters))
                    .AppendLine(" 序列化字符上限");
            }
            else
            {
                builder.Append(FormatNumber(codexConfigOptions.ConfiguredToolOutputTokenLimit))
                    .Append(" tokens · 来源 ")
                    .Append(codexConfigOptions.ToolOutputTokenLimitSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.ToolOutputTokenLimitSourceLabel)
                    .AppendLine(" · 仅约束模型历史中的单次工具结果；本地审计与证据保持完整");
            }
            builder.Append("- Codex model_reasoning_effort：")
                .Append(CopilotCodexReasoningEffortSelection.GetConfigToken(
                    codexConfigOptions.ConfiguredModelReasoningEffort));
            if (codexConfigOptions.HasModelReasoningEffortOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.ModelReasoningEffortSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.ModelReasoningEffortSourceLabel)
                    .Append(" · 请求快照");
            }
            builder.AppendLine(" · 仅 Agent 官方 OpenAI Responses 生效");
            builder.Append("- Codex plan_mode_reasoning_effort：")
                .Append(CopilotCodexReasoningEffortSelection.GetConfigToken(
                    codexConfigOptions.ConfiguredPlanModeReasoningEffort));
            if (codexConfigOptions.HasPlanModeReasoningEffortOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.PlanModeReasoningEffortSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.PlanModeReasoningEffortSourceLabel)
                    .Append(" · 请求快照");
            }
            builder.AppendLine(" · 仅覆盖 Plan 模式的 Agent 官方 OpenAI Responses 推理强度");
            builder.Append("- Codex model_reasoning_summary：")
                .Append(CopilotCodexReasoningSummarySelection.GetConfigToken(
                    codexConfigOptions.ConfiguredModelReasoningSummary));
            if (codexConfigOptions.HasModelReasoningSummaryOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.ModelReasoningSummarySourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.ModelReasoningSummarySourceLabel)
                    .Append(" · 请求快照");
            }
            builder.AppendLine(" · 仅 Agent 官方 OpenAI Responses 生效");
            builder.Append("- Codex model_supports_reasoning_summaries：")
                .Append(CopilotCodexReasoningSummarySupportSelection.GetConfigToken(
                    codexConfigOptions.HasModelSupportsReasoningSummariesOverride
                        ? codexConfigOptions.ConfiguredModelSupportsReasoningSummaries
                        : null));
            if (codexConfigOptions.HasModelSupportsReasoningSummariesOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.ModelSupportsReasoningSummariesSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.ModelSupportsReasoningSummariesSourceLabel)
                    .Append(" · 请求快照")
                    .Append(codexConfigOptions.ConfiguredModelSupportsReasoningSummaries
                        ? " · 启用 reasoning metadata；摘要未配置时使用 auto，显式 none 仍关闭摘要"
                        : " · 阻断 reasoning metadata；覆盖 effort/summary");
            }
            builder.AppendLine(" · 仅 Agent 官方 OpenAI Responses 生效");
            builder.Append("- Codex hide_agent_reasoning：");
            if (!codexConfigOptions.HasHideAgentReasoningOverride)
            {
                builder.AppendLine("未配置 · 显示 Chat/Agent reasoning 输出");
            }
            else
            {
                builder.Append(codexConfigOptions.ConfiguredHideAgentReasoning ? "true" : "false")
                    .Append(" · 来源 ")
                    .Append(codexConfigOptions.HideAgentReasoningSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.HideAgentReasoningSourceLabel)
                    .AppendLine(" · 提交快照 · 仅改变 Chat/Agent 用户可见输出；请求、Token 计量与运行事件保持完整");
            }
            AppendPreventIdleSleep(builder, codexConfigOptions);
            AppendShellToolEnabled(builder, codexConfigOptions);
            AppendHooksEnabled(builder, codexConfigOptions);
            AppendShellEnvironmentPolicy(builder, codexConfigOptions);
            AppendGoalsEnabled(builder, codexConfigOptions);
            AppendDefaultModeRequestUserInputEnabled(builder, codexConfigOptions);
            AppendExperimentalRequestUserInputEnabled(builder, codexConfigOptions);
            AppendUpdatePlanEnabled(builder, codexConfigOptions);
            AppendIncludePermissionsInstructions(builder, codexConfigOptions);
            AppendIncludeCollaborationModeInstructions(builder, codexConfigOptions);
            AppendIncludeEnvironmentContext(builder, codexConfigOptions);
            AppendIncludeSkillInstructions(builder, codexConfigOptions);
            AppendAgentsEnabled(builder, codexConfigOptions);
            builder.Append("- Codex features.fast_mode：")
                .Append(codexConfigOptions.ConfiguredFastModeEnabled ? "true" : "false");
            if (codexConfigOptions.HasFastModeEnabledOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.FastModeEnabledSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.FastModeEnabledSourceLabel);
            }
            else
            {
                builder.Append(" · Codex 稳定功能默认值");
            }
            builder.AppendLine(codexConfigOptions.ConfiguredFastModeEnabled
                ? " · 请求快照 · 允许 service_tier"
                : " · 请求快照 · 总闸门关闭，不发送任何 service_tier");
            builder.Append("- Codex service_tier：");
            if (!codexConfigOptions.HasServiceTierOverride)
            {
                builder.AppendLine("未配置 · 使用模型/Provider 默认");
            }
            else
            {
                builder.Append(codexConfigOptions.ConfiguredServiceTier)
                    .Append(codexConfigOptions.ConfiguredFastModeEnabled
                        ? " → 请求 " + CopilotCodexServiceTierSelection.GetRequestToken(
                            codexConfigOptions.ConfiguredServiceTier)
                        : " → 不发送（features.fast_mode=false）")
                    .Append(" · 来源 ")
                    .Append(codexConfigOptions.ServiceTierSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.ServiceTierSourceLabel)
                    .AppendLine(" · 请求快照 · 仅 Agent 官方 OpenAI Responses 生效");
            }
            builder.Append("- Codex model_verbosity：")
                .Append(CopilotCodexModelVerbositySelection.GetConfigToken(
                    codexConfigOptions.ConfiguredModelVerbosity));
            if (codexConfigOptions.HasModelVerbosityOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.ModelVerbositySourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.ModelVerbositySourceLabel)
                    .Append(" · 请求快照");
            }
            builder.AppendLine(" · 仅 Agent 官方 OpenAI Responses 生效");
            builder.Append("- Codex model_auto_compact_token_limit：");
            if (!codexConfigOptions.HasModelAutoCompactTokenLimitOverride)
            {
                builder.Append("未配置 · 使用应用 ")
                    .Append(defaults.AutoCompactThresholdPercent.ToString(CultureInfo.CurrentCulture))
                    .AppendLine("% 阈值");
            }
            else
            {
                builder.Append(FormatNumber(codexConfigOptions.ConfiguredModelAutoCompactTokenLimit))
                    .Append(" tokens @ ")
                    .Append(CopilotModelAutoCompactTokenLimitScopeSelection.GetConfigToken(
                        codexConfigOptions.EffectiveModelAutoCompactTokenLimitScope))
                    .Append(" · 来源 ")
                    .Append(codexConfigOptions.ModelAutoCompactTokenLimitSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.ModelAutoCompactTokenLimitSourceLabel)
                    .AppendLine(" · 请求快照覆盖应用百分比阈值");
            }
            if (codexConfigOptions.HasModelAutoCompactTokenLimitScopeOverride)
            {
                builder.Append("- Codex model_auto_compact_token_limit_scope：")
                    .Append(CopilotModelAutoCompactTokenLimitScopeSelection.GetConfigToken(
                        codexConfigOptions.EffectiveModelAutoCompactTokenLimitScope))
                    .Append(" · 来源 ")
                    .Append(codexConfigOptions.ModelAutoCompactTokenLimitScopeSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.ModelAutoCompactTokenLimitScopeSourceLabel)
                    .AppendLine(codexConfigOptions.HasModelAutoCompactTokenLimitOverride
                        ? " · 请求快照"
                        : " · 尚未配置 token limit，当前不影响自动压缩");
            }
            builder
                .Append("- Shell：")
                .Append(defaults.PreferredShell)
                .Append(" · 自动压缩 ")
                .Append(defaults.AutoCompactConversationHistory ? "开启" : "关闭")
                .Append(" @ ")
                .Append(defaults.AutoCompactThresholdPercent.ToString(CultureInfo.CurrentCulture))
                .Append("% · 自定义聚焦 ")
                .Append(defaults.AutoCompactInstructions.Length.ToString("N0", CultureInfo.CurrentCulture))
                .AppendLine(" 字符");
            builder.Append("- Codex compact_prompt：");
            if (!codexConfigOptions.HasCompactPromptOverride)
            {
                builder.AppendLine("内置主体");
            }
            else
            {
                builder.Append(codexConfigOptions.CompactPrompt.Length.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" 字符 · 来源 ")
                    .AppendLine(codexConfigOptions.CompactPromptSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.CompactPromptSourceLabel);
            }
            builder.Append("- Codex web_search：")
                .Append(CopilotCodexWebSearchModeSelection.GetConfigToken(
                    codexConfigOptions.ConfiguredWebSearchMode))
                .Append(" · ")
                .Append(CopilotCodexWebSearchModeSelection.GetEffectiveLabel(
                    codexConfigOptions.ConfiguredWebSearchMode));
            if (codexConfigOptions.HasWebSearchModeOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.WebSearchModeSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.WebSearchModeSourceLabel);
            }
            builder.AppendLine();
            AppendSandboxMode(builder, codexConfigOptions);
            AppendApprovalPolicy(builder, codexConfigOptions);
            AppendApprovalsReviewer(builder, codexConfigOptions);
            AppendAutoReviewPolicy(builder, codexConfigOptions);
            builder
                .Append("- Skill 手动覆盖：")
                .Append(defaults.SkillOverrides?.Count.ToString("N0", CultureInfo.CurrentCulture) ?? "0")
                .AppendLine(" 项");
        }

        private static void AppendSandboxMode(
            StringBuilder builder,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions)
        {
            builder.Append("- Codex sandbox_mode：")
                .Append(CopilotCodexSandboxModeSelection.GetConfigToken(
                    codexConfigOptions.ConfiguredSandboxMode))
                .Append(" · ")
                .Append(CopilotCodexSandboxModeSelection.GetEffectiveLabel(
                    codexConfigOptions.ConfiguredSandboxMode));
            if (codexConfigOptions.HasSandboxModeOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.SandboxModeSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.SandboxModeSourceLabel)
                    .Append(" · 提交快照");
            }
            builder.AppendLine();
        }

        private static void AppendApprovalPolicy(
            StringBuilder builder,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions)
        {
            builder.Append("- Codex approval_policy：")
                .Append(CopilotCodexApprovalPolicySelection.GetConfigToken(
                    codexConfigOptions.ConfiguredApprovalPolicy))
                .Append(" · ")
                .Append(CopilotCodexApprovalPolicySelection.GetEffectiveLabel(
                    codexConfigOptions.ConfiguredApprovalPolicy));
            if (codexConfigOptions.HasApprovalPolicyOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.ApprovalPolicySourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.ApprovalPolicySourceLabel)
                    .Append(" · 提交快照");
            }
            builder.AppendLine();
        }

        private static void AppendApprovalsReviewer(
            StringBuilder builder,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions)
        {
            builder.Append("- Codex approvals_reviewer：")
                .Append(CopilotCodexApprovalsReviewerSelection.GetConfigToken(
                    codexConfigOptions.ConfiguredApprovalsReviewer))
                .Append(" · ")
                .Append(CopilotCodexApprovalsReviewerSelection.GetEffectiveLabel(
                    codexConfigOptions.ConfiguredApprovalsReviewer));
            if (codexConfigOptions.HasApprovalsReviewerOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.ApprovalsReviewerSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.ApprovalsReviewerSourceLabel)
                    .Append(" · 提交快照");
            }
            builder.AppendLine();
        }

        private static void AppendAutoReviewPolicy(
            StringBuilder builder,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions)
        {
            if (!codexConfigOptions.HasAutoReviewPolicyOverride)
                return;

            builder.Append("- Codex auto_review.policy：")
                .Append(codexConfigOptions.ConfiguredAutoReviewPolicy.Length.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" 字符 · 仅注入独立 reviewer，不作为主 Agent 授权 · 来源 ")
                .Append(codexConfigOptions.AutoReviewPolicySourceLabel.Length == 0
                    ? "Codex config.toml auto_review.policy"
                    : codexConfigOptions.AutoReviewPolicySourceLabel)
                .AppendLine(" · 提交快照");
        }

        private static void AppendPreventIdleSleep(
            StringBuilder builder,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions)
        {
            builder.Append("- Codex features.prevent_idle_sleep：");
            if (!codexConfigOptions.HasPreventIdleSleepOverride)
            {
                builder.AppendLine("未配置 · 默认关闭");
                return;
            }

            builder.Append(codexConfigOptions.ConfiguredPreventIdleSleep ? "true" : "false")
                .Append(" · 来源 ")
                .Append(codexConfigOptions.PreventIdleSleepSourceLabel.Length == 0
                    ? "Codex config.toml"
                    : codexConfigOptions.PreventIdleSleepSourceLabel)
                .Append(" · 提交快照");
            if (!codexConfigOptions.ConfiguredPreventIdleSleep)
            {
                builder.AppendLine(" · 不阻止系统空闲休眠");
                return;
            }

            var runtime = CopilotActiveTurnSleepPrevention.CaptureRuntimeSnapshot();
            if (runtime.IsActive)
            {
                builder.Append(" · Windows Power Request 活动 ")
                    .Append(runtime.ActiveLeaseCount.ToString(CultureInfo.CurrentCulture))
                    .AppendLine(" 个");
            }
            else if (runtime.LastFailure.Length > 0)
            {
                builder.Append(" · 最近一次系统请求失败：")
                    .Append(runtime.LastFailure);
                if (runtime.LastErrorCode.HasValue)
                {
                    builder.Append("（Win32 ")
                        .Append(runtime.LastErrorCode.Value.ToString(CultureInfo.InvariantCulture))
                        .Append('）');
                }
                builder.AppendLine();
            }
            else
            {
                builder.AppendLine(" · 当前无活动轮次；排队等待不占用系统请求");
            }
        }

        private static void AppendShellToolEnabled(
            StringBuilder builder,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions)
        {
            builder.Append("- Codex features.shell_tool：")
                .Append(codexConfigOptions.ConfiguredShellToolEnabled ? "true" : "false");
            if (codexConfigOptions.HasShellToolEnabledOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.ShellToolEnabledSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.ShellToolEnabledSourceLabel)
                    .Append(" · 提交快照");
            }
            else
            {
                builder.Append(" · 官方默认");
            }
            builder.AppendLine(codexConfigOptions.ConfiguredShellToolEnabled
                ? " · 按请求意图暴露命令启动工具"
                : " · 命令启动工具已从目录移除，旧计划、恢复状态与注入调用也会拒绝；已有后台命令仍可观察或停止");
        }

        private static void AppendHooksEnabled(
            StringBuilder builder,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions)
        {
            builder.Append("- Codex features.hooks：")
                .Append(codexConfigOptions.ConfiguredHooksEnabled ? "true" : "false");
            if (codexConfigOptions.HasHooksEnabledOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.HooksEnabledSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.HooksEnabledSourceLabel)
                    .Append(" · 提交快照");
            }
            else
            {
                builder.Append(" · 官方默认");
            }
            builder.AppendLine(codexConfigOptions.ConfiguredHooksEnabled
                ? " · ColorVision 模块扩展授权与生命周期 Hook 可运行；内置写入安全策略始终保留"
                : " · 模块扩展授权与生命周期 Hook 已省略；内置写入安全策略仍保留，checkpoint 按有效 Hook 面校验");
        }

        private static void AppendIncludePermissionsInstructions(
            StringBuilder builder,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions)
        {
            builder.Append("- Codex include_permissions_instructions：")
                .Append(codexConfigOptions.ConfiguredIncludePermissionsInstructions ? "true" : "false");
            if (codexConfigOptions.HasIncludePermissionsInstructionsOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.IncludePermissionsInstructionsSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.IncludePermissionsInstructionsSourceLabel)
                    .Append(" · 提交快照");
            }
            else
            {
                builder.Append(" · 官方默认");
            }
            builder.AppendLine(codexConfigOptions.ConfiguredIncludePermissionsInstructions
                ? " · 注入模型可见的完整权限说明"
                : " · 仅省略模型可见权限说明；沙箱、审批、工具过滤与执行策略保持强制");
        }

        private static void AppendShellEnvironmentPolicy(
            StringBuilder builder,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions)
        {
            builder.Append("- Codex shell_environment_policy：")
                .Append(codexConfigOptions.ConfiguredShellEnvironmentPolicy.BuildRedactedSummary());
            if (codexConfigOptions.HasShellEnvironmentPolicyOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.ShellEnvironmentPolicySourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.ShellEnvironmentPolicySourceLabel)
                    .Append(" · 提交快照");
            }
            else
            {
                builder.Append(" · 官方默认");
            }
            builder.AppendLine(codexConfigOptions.ShellEnvironmentPolicyError.Length == 0
                ? " · 前台、后台与固定 Git 子进程共享；set 仅报告数量"
                : " · " + codexConfigOptions.ShellEnvironmentPolicyError);
        }

        private static void AppendIncludeEnvironmentContext(
            StringBuilder builder,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions)
        {
            builder.Append("- Codex include_environment_context：")
                .Append(codexConfigOptions.ConfiguredIncludeEnvironmentContext ? "true" : "false");
            if (codexConfigOptions.HasIncludeEnvironmentContextOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.IncludeEnvironmentContextSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.IncludeEnvironmentContextSourceLabel)
                    .Append(" · 提交快照");
            }
            else
            {
                builder.Append(" · 官方默认");
            }
            builder.AppendLine(codexConfigOptions.ConfiguredIncludeEnvironmentContext
                ? " · 向模型注入请求开始时的 runtime_environment 数据块"
                : " · 省略模型可见 runtime_environment；工具侧路径、沙箱与审批边界保持不变");
        }

        private static void AppendIncludeCollaborationModeInstructions(
            StringBuilder builder,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions)
        {
            builder.Append("- Codex include_collaboration_mode_instructions：")
                .Append(codexConfigOptions.ConfiguredIncludeCollaborationModeInstructions ? "true" : "false");
            if (codexConfigOptions.HasIncludeCollaborationModeInstructionsOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.IncludeCollaborationModeInstructionsSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.IncludeCollaborationModeInstructionsSourceLabel)
                    .Append(" · 提交快照");
            }
            else
            {
                builder.Append(" · 官方默认");
            }
            builder.AppendLine(codexConfigOptions.ConfiguredIncludeCollaborationModeInstructions
                ? " · 注入模型可见的当前协作模式说明"
                : " · 仅省略模型可见模式说明；当前模式、工具过滤、任务清单与完成循环保持不变");
        }

        private static void AppendGoalsEnabled(
            StringBuilder builder,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions)
        {
            builder.Append("- Codex features.goals：")
                .Append(codexConfigOptions.ConfiguredGoalsEnabled ? "true" : "false");
            if (codexConfigOptions.HasGoalsEnabledOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.GoalsEnabledSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.GoalsEnabledSourceLabel)
                    .Append(" · 提交快照");
            }
            else
            {
                builder.Append(" · 官方默认");
            }
            builder.AppendLine(codexConfigOptions.ConfiguredGoalsEnabled
                ? " · 活动目标会绑定到 Agent 请求，并执行完成评估与自动续作"
                : " · 不绑定、计数、评估或自动续作；已有目标记录保留，/goal 仍可查看、暂停或清除");
        }

        private static void AppendIncludeSkillInstructions(
            StringBuilder builder,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions)
        {
            builder.Append("- Codex skills.include_instructions：")
                .Append(codexConfigOptions.ConfiguredIncludeSkillInstructions ? "true" : "false");
            if (codexConfigOptions.HasIncludeSkillInstructionsOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.IncludeSkillInstructionsSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.IncludeSkillInstructionsSourceLabel)
                    .Append(" · 提交快照");
            }
            else
            {
                builder.Append(" · 官方默认");
            }
            builder.AppendLine(codexConfigOptions.ConfiguredIncludeSkillInstructions
                ? " · 允许按请求相关性自动注入 Skill 元数据"
                : " · 省略自动 Skill 说明；显式 $name 或 /name 调用仍可加载匹配 Skill");
        }

        private static void AppendDefaultModeRequestUserInputEnabled(
            StringBuilder builder,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions)
        {
            builder.Append("- Codex features.default_mode_request_user_input：")
                .Append(codexConfigOptions.ConfiguredDefaultModeRequestUserInputEnabled ? "true" : "false");
            if (codexConfigOptions.HasDefaultModeRequestUserInputEnabledOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.DefaultModeRequestUserInputEnabledSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.DefaultModeRequestUserInputEnabledSourceLabel)
                    .Append(" · 提交快照");
            }
            else
            {
                builder.Append(" · 官方默认");
            }
            builder.AppendLine(codexConfigOptions.ConfiguredDefaultModeRequestUserInputEnabled
                ? " · Default 模式允许 AskUserQuestion；仍受 tools.experimental_request_user_input.enabled 总开关约束"
                : " · Default 模式不暴露 AskUserQuestion；Plan 模式仍由 tools.experimental_request_user_input.enabled 控制");
        }

        private static void AppendExperimentalRequestUserInputEnabled(
            StringBuilder builder,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions)
        {
            AppendToolEnabled(
                builder,
                "tools.experimental_request_user_input.enabled",
                codexConfigOptions.ConfiguredExperimentalRequestUserInputEnabled,
                codexConfigOptions.HasExperimentalRequestUserInputEnabledOverride,
                codexConfigOptions.ExperimentalRequestUserInputEnabledSourceLabel,
                "结构化澄清工具 AskUserQuestion 已注册",
                "结构化澄清工具 AskUserQuestion 已移除；这不授予或替代审批");
        }

        private static void AppendUpdatePlanEnabled(
            StringBuilder builder,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions)
        {
            AppendToolEnabled(
                builder,
                "tools.update_plan.enabled",
                codexConfigOptions.ConfiguredUpdatePlanEnabled,
                codexConfigOptions.HasUpdatePlanEnabledOverride,
                codexConfigOptions.UpdatePlanEnabledSourceLabel,
                "复杂请求可启用任务清单与 plan/execute 完成循环",
                "任务清单与 plan/execute 完成循环已移除");
        }

        private static void AppendToolEnabled(
            StringBuilder builder,
            string key,
            bool enabled,
            bool hasOverride,
            string sourceLabel,
            string enabledDescription,
            string disabledDescription)
        {
            builder.Append("- Codex ")
                .Append(key)
                .Append('：')
                .Append(enabled ? "true" : "false");
            if (hasOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(sourceLabel.Length == 0 ? "Codex config.toml" : sourceLabel)
                    .Append(" · 提交快照");
            }
            else
            {
                builder.Append(" · 官方默认");
            }
            builder.Append(" · ")
                .AppendLine(enabled ? enabledDescription : disabledDescription);
        }

        private static void AppendAgentsEnabled(
            StringBuilder builder,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions)
        {
            builder.Append("- Codex features.multi_agent：")
                .Append(codexConfigOptions.ConfiguredMultiAgentEnabled ? "true" : "false");
            if (codexConfigOptions.HasMultiAgentEnabledOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.MultiAgentEnabledSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.MultiAgentEnabledSourceLabel)
                    .Append(" · 提交快照");
            }
            else
            {
                builder.Append(" · Codex 稳定功能默认值");
            }
            builder.AppendLine();
            builder.Append("- Codex agents.enabled：")
                .Append(codexConfigOptions.ConfiguredAgentsEnabled ? "true" : "false");
            if (codexConfigOptions.HasAgentsEnabledOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.AgentsEnabledSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.AgentsEnabledSourceLabel)
                    .Append(" · 提交快照");
            }
            else
            {
                builder.Append(" · 官方默认");
            }
            builder.AppendLine();
            builder.Append("- 子代理工具（有效）：")
                .Append(codexConfigOptions.EffectiveAgentsEnabled ? "开启" : "关闭")
                .AppendLine(codexConfigOptions.EffectiveAgentsEnabled
                    ? " · features.multi_agent 与 agents.enabled 均允许"
                    : " · 任一门槛关闭都会隐藏工具并拒绝旧计划、恢复状态或注入调用");
            builder.Append("- Codex agents.interrupt_message：")
                .Append(codexConfigOptions.ConfiguredInterruptMessageEnabled ? "true" : "false");
            if (codexConfigOptions.HasInterruptMessageOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.InterruptMessageSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.InterruptMessageSourceLabel)
                    .Append(" · 提交快照");
            }
            else
            {
                builder.Append(" · 官方默认");
            }
            builder.AppendLine(codexConfigOptions.ConfiguredInterruptMessageEnabled
                ? " · 中断后记录模型可见取消结果"
                : " · 中断后模型工具输出为空；UI、事件与审计仍保留");
            builder.Append("- Codex agents.max_concurrent_threads_per_session：")
                .Append(FormatNumber(codexConfigOptions.ConfiguredMaximumConcurrentSubagentRuns));
            if (codexConfigOptions.HasMaximumConcurrentSubagentRunsOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.MaximumConcurrentSubagentRunsSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.MaximumConcurrentSubagentRunsSourceLabel)
                    .Append(" · 提交快照");
            }
            else
            {
                builder.Append(" · ColorVision 默认");
            }
            builder.AppendLine(" · 限制单个父请求的并行子代理槽位，不扩大请求级 Token 总预算");
            builder.Append("- Codex agents.default_subagent_model：");
            if (codexConfigOptions.HasDefaultSubagentModelOverride)
            {
                builder.Append(codexConfigOptions.ConfiguredDefaultSubagentModel)
                    .Append(" · 来源 ")
                    .Append(codexConfigOptions.DefaultSubagentModelSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.DefaultSubagentModelSourceLabel)
                    .AppendLine(" · 提交快照 · 子代理沿用父 Profile Provider/端点/凭据");
            }
            else
            {
                builder.AppendLine("未配置 · 子代理沿用父 Profile 模型");
            }
            builder.Append("- Codex agents.default_subagent_reasoning_effort：")
                .Append(CopilotCodexReasoningEffortSelection.GetConfigToken(
                    codexConfigOptions.ConfiguredDefaultSubagentReasoningEffort));
            if (codexConfigOptions.HasDefaultSubagentReasoningEffortOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.DefaultSubagentReasoningEffortSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.DefaultSubagentReasoningEffortSourceLabel)
                    .AppendLine(" · 提交快照 · 子代理官方 OpenAI Responses 生效");
            }
            else
            {
                builder.AppendLine(" · 未配置 · 子代理继承父请求推理强度");
            }
            var customSubagentDiagnostics = CopilotCodexCustomSubagentDiagnostics.Format(
                codexConfigOptions.CustomSubagents);
            if (customSubagentDiagnostics.Length > 0)
                builder.AppendLine(customSubagentDiagnostics);
            var customSubagentDiscoveryIssues = CopilotCodexCustomSubagentDiagnostics.FormatDiscoveryIssues(
                codexConfigOptions.CustomSubagentDiscoveryIssues);
            if (customSubagentDiscoveryIssues.Length > 0)
                builder.AppendLine(customSubagentDiscoveryIssues);
        }

        private static void AppendConversation(
            StringBuilder builder,
            CopilotChatState state,
            CopilotConversationRecord? conversation,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions)
        {
            var personality = CopilotResponsePersonalitySelection.Resolve(
                conversation,
                codexConfigOptions);
            var followUp = CopilotFollowUpPreference.Normalize(state.DefaultFollowUpBehavior);
            var followUpSource = followUp == CopilotFollowUpBehavior.Steer
                ? "ChatState 默认"
                : "ChatState 保存值";

            builder.AppendLine()
                .AppendLine("会话与本机偏好")
                .Append("- Codex features.personality：")
                .Append(codexConfigOptions.ConfiguredPersonalityEnabled ? "true" : "false");
            if (codexConfigOptions.HasPersonalityEnabledOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(codexConfigOptions.PersonalityEnabledSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.PersonalityEnabledSourceLabel);
            }
            else
            {
                builder.Append(" · Codex 稳定功能默认值");
            }
            builder.AppendLine(codexConfigOptions.ConfiguredPersonalityEnabled
                    ? " · 允许 personality 指令"
                    : " · 总闸门关闭，不注入任何 personality 指令")
                .Append("- 回答风格：")
                .Append(CopilotResponsePersonalitySelection.GetDisplayName(personality.Personality))
                .Append(" · 来源 ")
                .AppendLine(personality.SourceLabel);
            if (codexConfigOptions.ConfiguredPersonalityEnabled
                && codexConfigOptions.HasPersonalityOverride
                && string.Equals(personality.SourceLabel, "会话覆盖", StringComparison.Ordinal))
            {
                builder.Append("- Codex personality 默认：")
                    .Append(CopilotResponsePersonalitySelection.GetDisplayName(
                        codexConfigOptions.ConfiguredPersonality))
                    .Append(" · 来源 ")
                    .Append(codexConfigOptions.PersonalitySourceLabel.Length == 0
                        ? "Codex config.toml"
                        : codexConfigOptions.PersonalitySourceLabel)
                    .AppendLine(" · 会话覆盖优先");
            }
            builder
                .Append("- 权限：")
                .AppendLine(FormatAccessMode(conversation))
                .Append("- 附加只读目录：")
                .Append(CopilotAdditionalDirectoryCommand.NormalizeStoredPaths(
                    conversation?.AdditionalReadRootPaths).Length)
                .AppendLine(" 个 · 来源 会话状态")
                .Append("- 运行中 Enter：")
                .Append(followUp == CopilotFollowUpBehavior.Queue ? "排队" : "调整当前任务")
                .Append(" · 来源 ")
                .AppendLine(followUpSource)
                .Append("- 输入与显示：多行 ")
                .Append(state.UseMultilineComposer ? "开启" : "关闭")
                .Append(" · 时间戳 ")
                .Append(state.ShowMessageTimestamps ? "显示" : "隐藏")
                .Append(" · 紧凑布局 ")
                .Append(state.UseCompactMessageLayout ? "开启" : "关闭")
                .Append(" · 历史提示 ")
                .Append(state.EnablePromptHistoryCompletions ? "开启" : "关闭")
                .AppendLine(" · 来源 ChatState");
        }

        private static void AppendIntegrations(
            StringBuilder builder,
            CopilotConfig config,
            CopilotConfigFileProbe configProbe,
            bool mcpListenerRunning)
        {
            var enabledExternalServers = config.ExternalMcpServers?
                .Count(server => server?.Enabled == true) ?? 0;
            var source = configProbe.HasMcpSettings
                ? "应用配置 CopilotConfig"
                : configProbe.State == CopilotConfigFileProbeState.Loaded
                    ? "内置默认或迁移"
                    : "已加载运行时值（当前文件来源未证实）";
            builder.AppendLine()
                .AppendLine("集成")
                .Append("- 本机 MCP：")
                .Append(config.McpEnabled ? "启用" : "禁用")
                .Append(" · listener ")
                .Append(mcpListenerRunning ? "运行中" : "未运行")
                .Append(" · port ")
                .Append(config.McpPort.ToString(CultureInfo.InvariantCulture))
                .Append(" · Bearer ")
                .Append(string.IsNullOrWhiteSpace(config.McpBearerToken) ? "缺失" : "已配置")
                .Append(" · 来源 ")
                .AppendLine(source)
                .Append("- 外部 MCP：")
                .Append(enabledExternalServers.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" / ")
                .Append(config.ExternalMcpServers?.Count.ToString("N0", CultureInfo.CurrentCulture) ?? "0")
                .AppendLine(" 个已启用");
        }

        private static string FormatConfigProbe(
            CopilotConfigFileProbe probe,
            int runtimeSchemaVersion)
        {
            return probe.State switch
            {
                CopilotConfigFileProbeState.Loaded =>
                    $"已加载 CopilotConfig · file schema {probe.SchemaVersion?.ToString(CultureInfo.InvariantCulture) ?? "未声明"} → runtime {runtimeSchemaVersion.ToString(CultureInfo.InvariantCulture)}",
                CopilotConfigFileProbeState.FileMissing => "当前文件不存在 · 继续使用已加载运行时值",
                CopilotConfigFileProbeState.SectionMissing => "当前无 CopilotConfig 节 · 继续使用已加载运行时值",
                CopilotConfigFileProbeState.InvalidJson => "JSON 无法解析 · 运行时使用已加载值或默认值",
                CopilotConfigFileProbeState.TooLarge => "超过 16 MiB · 为安全起见未解析来源元数据",
                CopilotConfigFileProbeState.Unreadable => "当前不可读取 · 运行时使用已加载值",
                _ => "路径不可用 · 运行时使用已加载值",
            };
        }

        private static string FormatStateLoadStatus(CopilotChatStateLoadStatus status)
        {
            var label = status.Source switch
            {
                CopilotChatStateLoadSource.Primary => "主状态文件",
                CopilotChatStateLoadSource.Temporary => "临时快照恢复",
                CopilotChatStateLoadSource.Backup => "备份恢复",
                CopilotChatStateLoadSource.RecoverySnapshot => "历史恢复快照",
                CopilotChatStateLoadSource.Fresh => "新建内存状态",
                CopilotChatStateLoadSource.FutureVersion => "更高版本阻止写入",
                CopilotChatStateLoadSource.Unrecoverable => "状态不可恢复",
                _ => "尚未探测",
            };
            return status.SchemaVersion.HasValue
                ? $"{label} · file schema {status.SchemaVersion.Value.ToString(CultureInfo.InvariantCulture)}"
                : label;
        }

        private static string FormatAccessMode(CopilotConversationRecord? conversation)
        {
            if (conversation?.AccessMode != CopilotAgentAccessMode.FullAccess)
                return "按需确认 · 内置安全默认";

            var scope = conversation.IsFullAccessPreparedForNextTask
                ? "自动复核 · 下一任务"
                : "自动复核 · 当前任务";
            return conversation.FullAccessExpiresAtUtc.HasValue
                ? $"{scope} · 到期 {conversation.FullAccessExpiresAtUtc.Value.ToLocalTime():MM-dd HH:mm:ss}"
                : scope;
        }

        private static string FormatEndpointOrigin(string? baseUrl)
        {
            if (!Uri.TryCreate((baseUrl ?? string.Empty).Trim(), UriKind.Absolute, out var uri)
                || string.IsNullOrWhiteSpace(uri.Host))
            {
                return "无有效 origin";
            }

            try
            {
                var host = uri.HostNameType == UriHostNameType.IPv6
                    ? "[" + uri.Host + "]"
                    : uri.IdnHost;
                return uri.Scheme + "://" + host + (uri.IsDefaultPort ? string.Empty : ":" + uri.Port.ToString(CultureInfo.InvariantCulture));
            }
            catch (UriFormatException)
            {
                return "无有效 origin";
            }
        }

        private static string NormalizeDisplayText(string? value)
        {
            var text = CopilotMcpAuditLogger.RedactText(value ?? string.Empty)
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
            if (text.Length > MaximumDisplayTextCharacters)
                text = text[..MaximumDisplayTextCharacters].TrimEnd() + "…";
            if (text.Length > 0 && char.IsHighSurrogate(text[^1]))
                text = text[..^1].TrimEnd() + "…";
            return text.Length == 0 ? "未设置" : text;
        }

        private static string FormatPath(string? path)
        {
            var normalized = NormalizePath(path);
            return normalized.Length == 0 ? "路径不可用" : normalized;
        }

        private static string NormalizePath(string? path)
        {
            var normalized = (path ?? string.Empty).Trim();
            if (normalized.Length == 0)
                return string.Empty;
            try
            {
                return Path.GetFullPath(normalized);
            }
            catch (Exception ex) when (ex is ArgumentException
                or NotSupportedException
                or PathTooLongException)
            {
                return string.Empty;
            }
        }

        private static CopilotConfigFileProbe CreateProbe(
            string filePath,
            CopilotConfigFileProbeState state) =>
            new(
                filePath,
                state,
                null,
                false,
                false,
                new HashSet<string>(StringComparer.Ordinal));

        private static string FormatNumber(long value) =>
            Math.Max(0, value).ToString("N0", CultureInfo.CurrentCulture);

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
                return $"{(int)duration.TotalHours:N0}h {duration.Minutes:N0}m";
            if (duration.TotalMinutes >= 1)
                return $"{(int)duration.TotalMinutes:N0}m {duration.Seconds:N0}s";
            return $"{Math.Max(0, (int)duration.TotalSeconds):N0}s";
        }
    }
}
