using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal enum CopilotProjectInstructionCommandAction
    {
        List,
        Open,
        Invalid,
    }

    internal sealed record CopilotProjectInstructionCommandRequest(
        CopilotProjectInstructionCommandAction Action,
        int Position);

    internal sealed record CopilotProjectInstructionSnapshot(
        string WorkspacePath,
        string ActiveDocumentPath,
        string GlobalInstructionRootPath,
        CopilotProjectInstructionDiscoveryOptions DiscoveryOptions,
        IReadOnlyList<CopilotProjectInstructionDocument> Documents);

    internal static class CopilotProjectInstructionDiagnostics
    {
        internal const string Usage =
            "用法：/memory [open N]。不带参数时预览基于当前目标、会被工作区型 Agent 请求加载的个人与项目指令；open N 在内置编辑器中打开第 N 个文件。";

        public static CopilotProjectInstructionCommandRequest ParseCommand(string? arguments)
        {
            var parts = (arguments ?? string.Empty).Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0
                || (parts.Length == 1
                    && string.Equals(parts[0], "list", StringComparison.OrdinalIgnoreCase)))
            {
                return new CopilotProjectInstructionCommandRequest(
                    CopilotProjectInstructionCommandAction.List,
                    0);
            }

            if (parts.Length == 2
                && string.Equals(parts[0], "open", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var position)
                && position > 0)
            {
                return new CopilotProjectInstructionCommandRequest(
                    CopilotProjectInstructionCommandAction.Open,
                    position);
            }

            return new CopilotProjectInstructionCommandRequest(
                CopilotProjectInstructionCommandAction.Invalid,
                0);
        }

        public static IReadOnlyList<CopilotProjectInstructionDocument> GetEffectiveDocuments(
            IEnumerable<CopilotProjectInstructionDocument>? documents)
        {
            return (documents ?? Array.Empty<CopilotProjectInstructionDocument>())
                .Where(document => document?.IsStructurallyValid() == true)
                .Take(CopilotAgentProjectInstructions.MaxDocuments)
                .ToArray();
        }

        public static CopilotProjectInstructionDocument? FindByPosition(
            IEnumerable<CopilotProjectInstructionDocument>? documents,
            int position)
        {
            if (position <= 0)
                return null;

            return GetEffectiveDocuments(documents).ElementAtOrDefault(position - 1);
        }

        public static string Format(
            CopilotProjectInstructionSnapshot? snapshot,
            bool hasActiveAgentRun)
        {
            var documents = GetEffectiveDocuments(snapshot?.Documents);
            var builder = new StringBuilder()
                .Append("Copilot 个人与项目指令 · ")
                .AppendLine(documents.Count.ToString("N0", CultureInfo.CurrentCulture));
            if (documents.Count == 0)
            {
                builder.AppendLine()
                    .AppendLine("当前 Codex Home、受信项目根和目标文件没有发现会被工作区型 Agent 请求加载的指令。")
                    .AppendLine("使用 /init 可在项目根创建 AGENTS.md；现有文件不会被覆盖。");
                AppendDiscoveryOptions(builder, snapshot?.DiscoveryOptions, snapshot?.WorkspacePath);
                builder
                    .Append("这里展示的是个人与工作区指令，不是自动生成的跨会话记忆；/memory 不会写入文件。");
                return builder.ToString();
            }

            builder.AppendLine()
                .AppendLine("以下是基于当前 Codex Home、活动文档与文件附件的注入预览：个人指令在前，项目指令由宽到窄，后列的局部规则只在自身作用域内覆盖前列规则。")
                .AppendLine("只有需要本地工作区证据的 Agent 请求才注入这些文件；下一条提示词中的显式本地路径也可能改变路径规则匹配。");
            AppendTarget(builder, snapshot);
            foreach (var document in documents.Select((value, index) => (Document: value, Position: index + 1)))
            {
                builder.Append('#')
                    .Append(document.Position.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" · ")
                    .Append(Path.GetFileName(document.Document.Path))
                    .Append(" · ")
                    .Append(GetSourceLabel(document.Document.Path, snapshot?.GlobalInstructionRootPath))
                    .Append(" · ")
                    .Append(document.Document.Content.Length.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" 字符");
                if (document.Document.IsTruncated)
                    builder.Append(" · 已截断");
                builder.AppendLine()
                    .Append("  ")
                    .AppendLine(FormatPath(document.Document.Path, snapshot?.WorkspacePath));
            }

            builder.AppendLine()
                .AppendLine("选择规则：Codex Home 先选首个非空 AGENTS.override.md/AGENTS.md；项目同目录优先 AGENTS.override.md、AGENTS.md，再尝试配置备用名与 CLAUDE.md 兼容回退；.claude/rules 为附加规则，CLAUDE.local.md 为私有局部覆盖。")
                .AppendLine("使用 /memory open N 打开文件。报告不包含指令正文，也不会自动修改任何指令。");
            AppendDiscoveryOptions(builder, snapshot?.DiscoveryOptions, snapshot?.WorkspacePath);
            if (hasActiveAgentRun)
                builder.AppendLine("当前运行中的任务已固定请求启动时的指令快照；现在编辑只影响后续请求。");
            builder.Append("这里展示的是个人与工作区指令，不是自动生成的跨会话记忆；Codex Home 不会因此成为通用文件或写入权限根。");
            return builder.ToString();
        }

        private static void AppendDiscoveryOptions(
            StringBuilder builder,
            CopilotProjectInstructionDiscoveryOptions? options,
            string? workspacePath)
        {
            var effective = options ?? CopilotProjectInstructionDiscoveryConfig.CreateDefault();
            builder.Append("发现预算：")
                .Append(effective.MaximumBytes.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" UTF-8 字节 · ")
                .AppendLine(effective.UsesCodexConfig
                    ? effective.ConfigSourceLabel + " 请求快照"
                    : "ColorVision 默认");
            if (effective.FallbackFileNames.Count > 0)
            {
                builder.Append("配置备用名：")
                    .AppendLine(string.Join("、", effective.FallbackFileNames));
            }
            if (effective.ProjectTrustLabel.Length > 0)
            {
                builder.Append("项目配置信任：")
                    .AppendLine(effective.ProjectTrustLabel);
            }
            if (effective.HasDeveloperInstructionsOverride)
            {
                builder.Append("Codex developer_instructions：")
                    .Append(effective.DeveloperInstructions.Length.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" 字符（")
                    .Append(effective.DeveloperInstructionsSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.DeveloperInstructionsSourceLabel)
                    .AppendLine(effective.DeveloperInstructions.Length == 0
                        ? " 请求快照；显式清空）"
                        : " 请求快照；独立开发者指令）");
            }
            if (effective.HasPersonalityOverride)
            {
                builder.Append("Codex personality：")
                    .Append(CopilotResponsePersonalitySelection.GetDisplayName(effective.ConfiguredPersonality))
                    .Append('（')
                    .Append(CopilotResponsePersonalitySelection.GetCommandToken(effective.ConfiguredPersonality))
                    .Append("） · 来源 ")
                    .Append(effective.PersonalitySourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.PersonalitySourceLabel)
                    .AppendLine(" 请求快照；会话显式选择优先");
            }
            if (effective.HasWebSearchModeOverride)
            {
                builder.Append("Codex web_search：")
                    .Append(CopilotCodexWebSearchModeSelection.GetConfigToken(
                        effective.ConfiguredWebSearchMode))
                    .Append(" · 来源 ")
                    .Append(effective.WebSearchModeSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.WebSearchModeSourceLabel)
                    .Append(" 请求快照；")
                    .AppendLine(CopilotCodexWebSearchModeSelection.GetEffectiveLabel(
                        effective.ConfiguredWebSearchMode));
            }
            builder.Append("Codex sandbox_mode：")
                .Append(CopilotCodexSandboxModeSelection.GetConfigToken(
                    effective.ConfiguredSandboxMode));
            if (effective.HasSandboxModeOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(effective.SandboxModeSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.SandboxModeSourceLabel)
                    .Append(" 请求快照；");
            }
            else
            {
                builder.Append(" · 未配置；");
            }
            builder.AppendLine(CopilotCodexSandboxModeSelection.GetEffectiveLabel(
                effective.ConfiguredSandboxMode));
            builder.Append("Codex approval_policy：")
                .Append(CopilotCodexApprovalPolicySelection.GetConfigToken(
                    effective.ConfiguredApprovalPolicy));
            if (effective.HasApprovalPolicyOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(effective.ApprovalPolicySourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.ApprovalPolicySourceLabel)
                    .Append(" 请求快照；");
            }
            else
            {
                builder.Append(" · 未配置；");
            }
            builder.AppendLine(CopilotCodexApprovalPolicySelection.GetEffectiveLabel(
                effective.ConfiguredApprovalPolicy));
            builder.Append("Codex approvals_reviewer：")
                .Append(CopilotCodexApprovalsReviewerSelection.GetConfigToken(
                    effective.ConfiguredApprovalsReviewer));
            if (effective.HasApprovalsReviewerOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(effective.ApprovalsReviewerSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.ApprovalsReviewerSourceLabel)
                    .Append(" 请求快照；");
            }
            else
            {
                builder.Append(" · 未配置；");
            }
            builder.AppendLine(CopilotCodexApprovalsReviewerSelection.GetEffectiveLabel(
                effective.ConfiguredApprovalsReviewer));
            if (effective.HasAutoReviewPolicyOverride)
            {
                builder.Append("Codex auto_review.policy：")
                    .Append(effective.ConfiguredAutoReviewPolicy.Length.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" 字符 · 来源 ")
                    .Append(effective.AutoReviewPolicySourceLabel.Length == 0
                        ? "Codex config.toml auto_review.policy"
                        : effective.AutoReviewPolicySourceLabel)
                    .AppendLine(" 请求快照；仅注入独立 reviewer，不作为主 Agent 授权");
            }
            if (effective.HasModelOverride)
            {
                builder.Append("Codex model：")
                    .Append(effective.ConfiguredModel)
                    .Append(" · 来源 ")
                    .Append(effective.ModelSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.ModelSourceLabel)
                    .AppendLine(" 请求快照；替换模型名，沿用所选 Profile 的 Provider、端点与凭据；Review 模式的 review_model 优先");
            }
            if (effective.HasReviewModelOverride)
            {
                builder.Append("Codex review_model：")
                    .Append(effective.ConfiguredReviewModel)
                    .Append(" · 来源 ")
                    .Append(effective.ReviewModelSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.ReviewModelSourceLabel)
                    .AppendLine(" 请求快照；仅 Review 模式替换模型名，沿用所选 Profile 的 Provider、端点与凭据");
            }
            if (effective.HasPreventIdleSleepOverride)
            {
                builder.Append("Codex features.prevent_idle_sleep：")
                    .Append(effective.ConfiguredPreventIdleSleep ? "true" : "false")
                    .Append(" · 来源 ")
                    .Append(effective.PreventIdleSleepSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.PreventIdleSleepSourceLabel)
                    .AppendLine(effective.ConfiguredPreventIdleSleep
                        ? " 提交快照；仅活动轮次持有 Windows Power Request，排队等待不占用"
                        : " 提交快照；不阻止系统空闲休眠");
            }
            builder.Append("Codex features.shell_tool：")
                .Append(effective.ConfiguredShellToolEnabled ? "true" : "false");
            if (effective.HasShellToolEnabledOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(effective.ShellToolEnabledSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.ShellToolEnabledSourceLabel)
                    .Append(" 提交快照；");
            }
            else
            {
                builder.Append(" · 官方默认；");
            }
            builder.AppendLine(effective.ConfiguredShellToolEnabled
                ? "按请求意图暴露命令启动工具"
                : "隐藏命令启动工具并拒绝旧计划、恢复状态或注入调用；已有后台命令仍可观察或停止");
            builder.Append("Codex shell_environment_policy：")
                .Append(effective.ConfiguredShellEnvironmentPolicy.BuildRedactedSummary());
            if (effective.HasShellEnvironmentPolicyOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(effective.ShellEnvironmentPolicySourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.ShellEnvironmentPolicySourceLabel)
                    .Append(" 提交快照；");
            }
            else
            {
                builder.Append(" · 官方默认；");
            }
            builder.AppendLine(effective.ShellEnvironmentPolicyError.Length == 0
                ? "应用于前台、后台与固定 Git 子进程；set 仅显示数量，不显示名称或值"
                : effective.ShellEnvironmentPolicyError);
            builder.Append("Codex features.goals：")
                .Append(effective.ConfiguredGoalsEnabled ? "true" : "false");
            if (effective.HasGoalsEnabledOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(effective.GoalsEnabledSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.GoalsEnabledSourceLabel)
                    .Append(" 提交快照；");
            }
            else
            {
                builder.Append(" · 官方默认；");
            }
            builder.AppendLine(effective.ConfiguredGoalsEnabled
                ? "活动目标会绑定到 Agent 请求，并执行完成评估与自动续作"
                : "不绑定、计数、评估或自动续作；已有目标记录保留，/goal 仍可查看、暂停或清除");
            builder.Append("Codex features.default_mode_request_user_input：")
                .Append(effective.ConfiguredDefaultModeRequestUserInputEnabled ? "true" : "false");
            if (effective.HasDefaultModeRequestUserInputEnabledOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(effective.DefaultModeRequestUserInputEnabledSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.DefaultModeRequestUserInputEnabledSourceLabel)
                    .Append(" 提交快照；");
            }
            else
            {
                builder.Append(" · 官方默认；");
            }
            builder.AppendLine(effective.ConfiguredDefaultModeRequestUserInputEnabled
                ? "允许 Default 模式暴露 AskUserQuestion；仍受 tools.experimental_request_user_input.enabled 总开关约束"
                : "Default 模式不暴露 AskUserQuestion；Plan 模式仍由 tools.experimental_request_user_input.enabled 控制");
            AppendToolEnabled(
                builder,
                "tools.experimental_request_user_input.enabled",
                effective.ConfiguredExperimentalRequestUserInputEnabled,
                effective.HasExperimentalRequestUserInputEnabledOverride,
                effective.ExperimentalRequestUserInputEnabledSourceLabel,
                "结构化澄清工具 AskUserQuestion 已注册",
                "结构化澄清工具 AskUserQuestion 已移除；这不授予或替代审批");
            AppendToolEnabled(
                builder,
                "tools.update_plan.enabled",
                effective.ConfiguredUpdatePlanEnabled,
                effective.HasUpdatePlanEnabledOverride,
                effective.UpdatePlanEnabledSourceLabel,
                "复杂请求可启用任务清单与 plan/execute 完成循环",
                "任务清单与 plan/execute 完成循环已移除");
            builder.Append("Codex include_permissions_instructions：")
                .Append(effective.ConfiguredIncludePermissionsInstructions ? "true" : "false");
            if (effective.HasIncludePermissionsInstructionsOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(effective.IncludePermissionsInstructionsSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.IncludePermissionsInstructionsSourceLabel)
                    .Append(" 提交快照；");
            }
            else
            {
                builder.Append(" · 官方默认；");
            }
            builder.AppendLine(effective.ConfiguredIncludePermissionsInstructions
                ? "注入模型可见的完整权限说明"
                : "仅省略模型可见权限说明；沙箱、审批、工具过滤与执行策略保持强制");
            builder.Append("Codex include_collaboration_mode_instructions：")
                .Append(effective.ConfiguredIncludeCollaborationModeInstructions ? "true" : "false");
            if (effective.HasIncludeCollaborationModeInstructionsOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(effective.IncludeCollaborationModeInstructionsSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.IncludeCollaborationModeInstructionsSourceLabel)
                    .Append(" 提交快照；");
            }
            else
            {
                builder.Append(" · 官方默认；");
            }
            builder.AppendLine(effective.ConfiguredIncludeCollaborationModeInstructions
                ? "注入模型可见的当前协作模式说明"
                : "仅省略模型可见模式说明；当前模式、工具过滤、任务清单与完成循环保持不变");
            builder.Append("Codex include_environment_context：")
                .Append(effective.ConfiguredIncludeEnvironmentContext ? "true" : "false");
            if (effective.HasIncludeEnvironmentContextOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(effective.IncludeEnvironmentContextSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.IncludeEnvironmentContextSourceLabel)
                    .Append(" 提交快照；");
            }
            else
            {
                builder.Append(" · 官方默认；");
            }
            builder.AppendLine(effective.ConfiguredIncludeEnvironmentContext
                ? "向模型注入请求开始时的 runtime_environment 数据块"
                : "省略模型可见 runtime_environment；工具侧路径、沙箱与审批边界保持不变");
            builder.Append("Codex skills.include_instructions：")
                .Append(effective.ConfiguredIncludeSkillInstructions ? "true" : "false");
            if (effective.HasIncludeSkillInstructionsOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(effective.IncludeSkillInstructionsSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.IncludeSkillInstructionsSourceLabel)
                    .Append(" 提交快照；");
            }
            else
            {
                builder.Append(" · 官方默认；");
            }
            builder.AppendLine(effective.ConfiguredIncludeSkillInstructions
                ? "允许按请求相关性自动注入 Skill 元数据"
                : "省略自动 Skill 说明；显式 $name 或 /name 调用仍可加载匹配 Skill");
            builder.Append("Codex agents.enabled：")
                .Append(effective.ConfiguredAgentsEnabled ? "true" : "false");
            if (effective.HasAgentsEnabledOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(effective.AgentsEnabledSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.AgentsEnabledSourceLabel)
                    .Append(" 提交快照；");
            }
            else
            {
                builder.Append(" · 官方默认；");
            }
            builder.AppendLine(effective.ConfiguredAgentsEnabled
                ? "允许按请求意图暴露子代理工具"
                : "隐藏子代理工具并拒绝旧计划、恢复状态或注入调用");
            builder.Append("Codex agents.interrupt_message：")
                .Append(effective.ConfiguredInterruptMessageEnabled ? "true" : "false");
            if (effective.HasInterruptMessageOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(effective.InterruptMessageSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.InterruptMessageSourceLabel)
                    .Append(" 提交快照");
            }
            else
            {
                builder.Append(" · 官方默认");
            }
            builder.AppendLine(effective.ConfiguredInterruptMessageEnabled
                ? "；用户中断子代理后记录模型可见的取消工具结果"
                : "；用户中断子代理后仅保留 UI、事件与审计记录，模型工具输出为空");
            builder.Append("Codex agents.max_concurrent_threads_per_session：")
                .Append(effective.ConfiguredMaximumConcurrentSubagentRuns.ToString("N0", CultureInfo.CurrentCulture));
            if (effective.HasMaximumConcurrentSubagentRunsOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(effective.MaximumConcurrentSubagentRunsSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.MaximumConcurrentSubagentRunsSourceLabel)
                    .Append(" 提交快照");
            }
            else
            {
                builder.Append(" · ColorVision 默认");
            }
            builder.AppendLine("；限制单个父请求的并行子代理槽位，不扩大请求级 Token 总预算");
            builder.Append("Codex agents.default_subagent_model：");
            if (effective.HasDefaultSubagentModelOverride)
            {
                builder.Append(effective.ConfiguredDefaultSubagentModel)
                    .Append(" · 来源 ")
                    .Append(effective.DefaultSubagentModelSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.DefaultSubagentModelSourceLabel)
                    .AppendLine(" 提交快照；子代理替换模型名并沿用父 Profile 的 Provider、端点与凭据");
            }
            else
            {
                builder.AppendLine("未配置；子代理沿用父 Profile 模型");
            }
            builder.Append("Codex agents.default_subagent_reasoning_effort：")
                .Append(CopilotCodexReasoningEffortSelection.GetConfigToken(
                    effective.ConfiguredDefaultSubagentReasoningEffort));
            if (effective.HasDefaultSubagentReasoningEffortOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(effective.DefaultSubagentReasoningEffortSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.DefaultSubagentReasoningEffortSourceLabel)
                    .AppendLine(" 提交快照；覆盖子代理推理强度，仅官方 OpenAI Responses 生效");
            }
            else
            {
                builder.AppendLine(" · 未配置；子代理继承父请求推理强度");
            }
            var customSubagentDiagnostics = CopilotCodexCustomSubagentDiagnostics.Format(
                effective.CustomSubagents);
            if (customSubagentDiagnostics.Length > 0)
                builder.AppendLine(customSubagentDiagnostics);
            var customSubagentDiscoveryIssues = CopilotCodexCustomSubagentDiagnostics.FormatDiscoveryIssues(
                effective.CustomSubagentDiscoveryIssues);
            if (customSubagentDiscoveryIssues.Length > 0)
                builder.AppendLine(customSubagentDiscoveryIssues);
            if (effective.HasModelContextWindowOverride)
            {
                builder.Append("Codex model_context_window：")
                    .Append(effective.ConfiguredModelContextWindowTokens.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" Token · 来源 ")
                    .Append(effective.ModelContextWindowSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.ModelContextWindowSourceLabel)
                    .AppendLine(" 请求快照；覆盖应用默认上下文窗口");
            }
            if (effective.HasToolOutputTokenLimitOverride)
            {
                builder.Append("Codex tool_output_token_limit：")
                    .Append(effective.ConfiguredToolOutputTokenLimit.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" Token · 来源 ")
                    .Append(effective.ToolOutputTokenLimitSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.ToolOutputTokenLimitSourceLabel)
                    .AppendLine(" 请求快照；仅约束写入模型历史的单次工具结果，完整本地审计与证据不裁剪");
            }
            if (effective.HasModelReasoningEffortOverride)
            {
                builder.Append("Codex model_reasoning_effort：")
                    .Append(CopilotCodexReasoningEffortSelection.GetConfigToken(
                        effective.ConfiguredModelReasoningEffort))
                    .Append(" · 来源 ")
                    .Append(effective.ModelReasoningEffortSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.ModelReasoningEffortSourceLabel)
                    .AppendLine(" 请求快照；仅 Agent 官方 OpenAI Responses 生效");
            }
            if (effective.HasPlanModeReasoningEffortOverride)
            {
                builder.Append("Codex plan_mode_reasoning_effort：")
                    .Append(CopilotCodexReasoningEffortSelection.GetConfigToken(
                        effective.ConfiguredPlanModeReasoningEffort))
                    .Append(" · 来源 ")
                    .Append(effective.PlanModeReasoningEffortSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.PlanModeReasoningEffortSourceLabel)
                    .AppendLine(" 请求快照；仅覆盖 Plan 模式的 Agent 官方 OpenAI Responses 推理强度");
            }
            if (effective.HasModelReasoningSummaryOverride)
            {
                builder.Append("Codex model_reasoning_summary：")
                    .Append(CopilotCodexReasoningSummarySelection.GetConfigToken(
                        effective.ConfiguredModelReasoningSummary))
                    .Append(" · 来源 ")
                    .Append(effective.ModelReasoningSummarySourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.ModelReasoningSummarySourceLabel)
                    .AppendLine(" 请求快照；none 不请求摘要；仅 Agent 官方 OpenAI Responses 生效");
            }
            if (effective.HasModelSupportsReasoningSummariesOverride)
            {
                builder.Append("Codex model_supports_reasoning_summaries：")
                    .Append(CopilotCodexReasoningSummarySupportSelection.GetConfigToken(
                        effective.ConfiguredModelSupportsReasoningSummaries))
                    .Append(" · 来源 ")
                    .Append(effective.ModelSupportsReasoningSummariesSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.ModelSupportsReasoningSummariesSourceLabel)
                    .Append(effective.ConfiguredModelSupportsReasoningSummaries
                        ? " 请求快照；启用 reasoning metadata，摘要未配置时使用 auto；显式 none 仍关闭摘要"
                        : " 请求快照；阻断 reasoning metadata，覆盖 model_reasoning_effort/model_reasoning_summary")
                    .AppendLine("；仅 Agent 官方 OpenAI Responses 生效");
            }
            if (effective.HasHideAgentReasoningOverride)
            {
                builder.Append("Codex hide_agent_reasoning：")
                    .Append(effective.ConfiguredHideAgentReasoning ? "true" : "false")
                    .Append(" · 来源 ")
                    .Append(effective.HideAgentReasoningSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.HideAgentReasoningSourceLabel)
                    .AppendLine(" 提交快照；同时作用于 Chat/Agent，仅隐藏用户可见 reasoning，不改变请求、Token 计量或运行事件");
            }
            if (effective.HasServiceTierOverride)
            {
                builder.Append("Codex service_tier：")
                    .Append(effective.ConfiguredServiceTier)
                    .Append(" → 请求 ")
                    .Append(CopilotCodexServiceTierSelection.GetRequestToken(
                        effective.ConfiguredServiceTier))
                    .Append(" · 来源 ")
                    .Append(effective.ServiceTierSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.ServiceTierSourceLabel)
                    .AppendLine(" 请求快照；仅 Agent 官方 OpenAI Responses 生效");
            }
            if (effective.HasModelVerbosityOverride)
            {
                builder.Append("Codex model_verbosity：")
                    .Append(CopilotCodexModelVerbositySelection.GetConfigToken(
                        effective.ConfiguredModelVerbosity))
                    .Append(" · 来源 ")
                    .Append(effective.ModelVerbositySourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.ModelVerbositySourceLabel)
                    .AppendLine(" 请求快照；仅 Agent 官方 OpenAI Responses 生效");
            }
            if (effective.HasModelAutoCompactTokenLimitOverride)
            {
                builder.Append("Codex model_auto_compact_token_limit：")
                    .Append(effective.ConfiguredModelAutoCompactTokenLimit.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" Token · 来源 ")
                    .Append(effective.ModelAutoCompactTokenLimitSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.ModelAutoCompactTokenLimitSourceLabel)
                    .AppendLine(" 请求快照；覆盖应用百分比阈值");
            }
            if (effective.HasModelAutoCompactTokenLimitScopeOverride)
            {
                builder.Append("Codex model_auto_compact_token_limit_scope：")
                    .Append(CopilotModelAutoCompactTokenLimitScopeSelection.GetConfigToken(
                        effective.EffectiveModelAutoCompactTokenLimitScope))
                    .Append(" · 来源 ")
                    .Append(effective.ModelAutoCompactTokenLimitScopeSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.ModelAutoCompactTokenLimitScopeSourceLabel)
                    .AppendLine(" 请求快照");
            }
            if (effective.HasModelInstructionsOverride)
            {
                builder.Append("Codex ")
                    .Append(effective.ModelInstructionsUsesFile
                        ? "model_instructions_file"
                        : "instructions")
                    .Append('：')
                    .Append(effective.ModelInstructions.Length.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" 字符（")
                    .Append(effective.ModelInstructionsSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.ModelInstructionsSourceLabel)
                    .AppendLine(effective.HasEffectiveModelInstructions
                        ? " 请求快照；替换会话内置主体，宿主安全规则强制保留）"
                        : effective.ModelInstructionsUsesFile
                            ? " 请求快照；文件为空或未安全加载，使用 Profile/内置主体）"
                            : " 请求快照；内联值为空或无效，使用 Profile/内置主体）");
                if (effective.ModelInstructionsSourceFilePath.Length > 0)
                {
                    builder.Append("模型指令文件：")
                        .AppendLine(FormatPath(effective.ModelInstructionsSourceFilePath, workspacePath));
                }
            }
            if (effective.HasCompactPromptOverride)
            {
                builder.Append("Codex compact_prompt：")
                    .Append(effective.CompactPrompt.Length.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" 字符（")
                    .Append(effective.CompactPromptSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.CompactPromptSourceLabel)
                    .AppendLine(effective.CompactPrompt.Length == 0
                        ? " 请求快照；未产生非空覆盖，使用内置主体）"
                        : " 请求快照；终态完整性后缀由宿主强制保留）");
                if (effective.CompactPromptSourceFilePath.Length > 0)
                {
                    builder.Append("压缩提示文件：")
                        .AppendLine(FormatPath(effective.CompactPromptSourceFilePath, workspacePath));
                }
            }
            builder.Append("项目根标记：");
            if (effective.ProjectRootMarkers.Count == 0)
            {
                builder.AppendLine(effective.HasProjectRootMarkersOverride
                    ? "[]（Codex Home 请求快照；不向上搜索）"
                    : "[]（默认；不向上搜索）");
            }
            else
            {
                builder.Append(string.Join("、", effective.ProjectRootMarkers))
                    .AppendLine(effective.HasProjectRootMarkersOverride
                        ? "（Codex Home 请求快照）"
                        : "（默认）");
            }
            if (effective.AppliedProjectConfigFilePaths.Count > 0)
            {
                builder.Append("项目配置层：")
                    .Append(effective.AppliedProjectConfigFilePaths.Count.ToString("N0", CultureInfo.CurrentCulture))
                    .AppendLine(" 个（项目根 → 工作目录，后者优先）");
                foreach (var configPath in effective.AppliedProjectConfigFilePaths)
                    builder.Append("  - ").AppendLine(FormatPath(configPath, workspacePath));
            }
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
            builder.Append("Codex ")
                .Append(key)
                .Append('：')
                .Append(enabled ? "true" : "false");
            if (hasOverride)
            {
                builder.Append(" · 来源 ")
                    .Append(sourceLabel.Length == 0 ? "Codex config.toml" : sourceLabel)
                    .Append(" 提交快照；");
            }
            else
            {
                builder.Append(" · 官方默认；");
            }
            builder.AppendLine(enabled ? enabledDescription : disabledDescription);
        }

        private static void AppendTarget(
            StringBuilder builder,
            CopilotProjectInstructionSnapshot? snapshot)
        {
            var workspacePath = (snapshot?.WorkspacePath ?? string.Empty).Trim();
            if (workspacePath.Length > 0)
                builder.Append("项目根：").AppendLine(workspacePath);

            var activeDocumentPath = (snapshot?.ActiveDocumentPath ?? string.Empty).Trim();
            if (activeDocumentPath.Length > 0)
            {
                builder.Append("活动目标：")
                    .AppendLine(FormatPath(activeDocumentPath, workspacePath));
            }
        }

        private static string GetSourceLabel(string? path, string? globalInstructionRootPath)
        {
            var normalized = (path ?? string.Empty).Replace('/', '\\');
            if (!string.IsNullOrWhiteSpace(globalInstructionRootPath)
                && CopilotWorkspaceSearchSupport.IsPathWithinRoots(path, [globalInstructionRootPath]))
            {
                return "Codex 全局指令";
            }
            var fileName = Path.GetFileName(normalized);
            if (string.Equals(fileName, "AGENTS.override.md", StringComparison.OrdinalIgnoreCase))
                return "共享覆盖";
            if (string.Equals(fileName, "AGENTS.md", StringComparison.OrdinalIgnoreCase))
                return "共享指令";
            if (string.Equals(fileName, "CLAUDE.local.md", StringComparison.OrdinalIgnoreCase))
                return "私有局部覆盖";
            if (normalized.Contains(@"\.claude\rules\", StringComparison.OrdinalIgnoreCase))
                return "Claude 路径规则";
            if (string.Equals(fileName, "CLAUDE.md", StringComparison.OrdinalIgnoreCase))
                return "Claude 兼容指令";
            return "项目指令";
        }

        private static string FormatPath(string? path, string? workspacePath)
        {
            var normalizedPath = (path ?? string.Empty).Trim();
            if (normalizedPath.Length == 0)
                return "（路径不可用）";

            try
            {
                var fullPath = Path.GetFullPath(normalizedPath);
                var normalizedWorkspace = string.IsNullOrWhiteSpace(workspacePath)
                    ? string.Empty
                    : Path.GetFullPath(workspacePath);
                if (normalizedWorkspace.Length > 0
                    && CopilotWorkspaceSearchSupport.IsPathWithinRoots(fullPath, [normalizedWorkspace]))
                {
                    return Path.GetRelativePath(normalizedWorkspace, fullPath);
                }
                return fullPath;
            }
            catch
            {
                return normalizedPath;
            }
        }
    }
}
