using System;
using System.Globalization;
using System.Text;

namespace ColorVision.Copilot
{
    public sealed class CopilotStatusDiagnosticSnapshot
    {
        public string ApplicationVersion { get; init; } = string.Empty;

        public string ProfileLabel { get; init; } = string.Empty;

        public string ProfileDetails { get; init; } = string.Empty;

        public bool ProfileConfigured { get; init; }

        public int ProviderFirstContentTimeoutSeconds { get; init; } =
            CopilotProfileConfig.DefaultFirstContentTimeoutSeconds;

        public int ProviderStreamingInactivityTimeoutSeconds { get; init; } =
            CopilotProfileConfig.DefaultStreamingInactivityTimeoutSeconds;

        public int ProviderMaximumAttempts { get; init; } =
            CopilotProviderRetryChatClient.DefaultMaximumAttempts;

        public int ActiveProviderRetryCount { get; init; }

        public int ActiveProviderRetryNextAttempt { get; init; }

        public int ActiveProviderRetryMaximumAttempts { get; init; }

        public long ActiveProviderRetryDelayMilliseconds { get; init; }

        public string ActiveProviderRetryFailureKind { get; init; } = string.Empty;

        public string ActiveProviderRetryRequestId { get; init; } = string.Empty;

        public string ReasoningLabel { get; init; } = string.Empty;

        public CopilotAgentMode Mode { get; init; }

        public string AgentState { get; init; } = string.Empty;

        public int QueuedAgentRuns { get; init; }

        public int MaximumQueuedAgentRuns { get; init; }

        public bool HasConversation { get; init; }

        public string ConversationTitle { get; init; } = string.Empty;

        public string ConversationId { get; init; } = string.Empty;

        public int ConversationVisibleTurns { get; init; }

        public int ConversationMessageCount { get; init; }

        public CopilotHostedRunState? ConversationRunState { get; init; }

        public int ConversationQueuedFollowUps { get; init; }

        public bool ConversationHasCheckpoint { get; init; }

        public bool ConversationHasRecoverableAgentTasks { get; init; }

        public bool ConversationIsBranch { get; init; }

        public string ConversationParentId { get; init; } = string.Empty;

        public string ConversationRootId { get; init; } = string.Empty;

        public string WorkspacePath { get; init; } = string.Empty;

        public string ActiveDocumentPath { get; init; } = string.Empty;

        public CopilotShellKind PreferredShell { get; init; }

        public int ContextWindowTokens { get; init; }

        public int RequestTokenBudget { get; init; }

        public int MaximumToolCalls { get; init; }

        public int MaximumAgentPasses { get; init; }

        public int TimeoutSeconds { get; init; }

        public int RegisteredCapabilities { get; init; }

        public int ApprovalCapabilities { get; init; }

        public int TrackedSkills { get; init; }

        public int ExplicitOnlySkills { get; init; }

        public bool McpListenerEnabled { get; init; }

        public bool McpListenerRunning { get; init; }

        public int EnabledExternalMcpServers { get; init; }

        public int PendingApprovals { get; init; }
    }

    public static class CopilotStatusDiagnostics
    {
        internal static string FormatApplicationVersion(Version? version)
        {
            return version?.ToString() ?? "unknown";
        }

        public static string Format(CopilotStatusDiagnosticSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            var builder = new StringBuilder();
            builder.AppendLine("ColorVision Copilot 状态");
            builder.AppendLine("本地快照：不会调用模型、工具或 MCP，也不会加入模型历史。");
            builder.AppendLine();
            builder.Append("版本：").AppendLine(ValueOrFallback(snapshot.ApplicationVersion));
            builder.Append("模型：").Append(ValueOrFallback(snapshot.ProfileLabel, "未选择"));
            if (!string.IsNullOrWhiteSpace(snapshot.ProfileDetails))
                builder.Append(" · ").Append(snapshot.ProfileDetails.Trim());
            builder.AppendLine();
            builder.Append("连接：").AppendLine(snapshot.ProfileConfigured ? "已配置" : "未完成配置");
            builder.Append("供应商保护：首个可显示内容 ")
                .Append(FormatCount(snapshot.ProviderFirstContentTimeoutSeconds))
                .Append(" 秒 / 流更新停滞 ")
                .Append(FormatCount(snapshot.ProviderStreamingInactivityTimeoutSeconds))
                .Append(" 秒 / 最多 ")
                .Append(FormatCount(Math.Max(1, snapshot.ProviderMaximumAttempts)))
                .AppendLine(" 次请求");
            AppendActiveProviderRetry(builder, snapshot);
            builder.Append("模式：").Append(snapshot.Mode).Append(" · 推理 ").AppendLine(ValueOrFallback(snapshot.ReasoningLabel, "默认"));
            builder.Append("Agent：").Append(ValueOrFallback(snapshot.AgentState, "Idle"))
                .Append(" · 队列 ").Append(FormatCount(snapshot.QueuedAgentRuns)).Append('/').AppendLine(FormatCount(snapshot.MaximumQueuedAgentRuns));
            builder.AppendLine();
            AppendConversation(builder, snapshot);
            builder.AppendLine();
            builder.Append("工作区：").AppendLine(ValueOrFallback(snapshot.WorkspacePath, "未打开解决方案"));
            builder.Append("活动文档：").AppendLine(ValueOrFallback(snapshot.ActiveDocumentPath, "无"));
            builder.Append("Shell：").AppendLine(FormatShell(snapshot.PreferredShell));
            builder.Append("Agent 预算：上下文 ").Append(FormatCount(snapshot.ContextWindowTokens))
                .Append(" Token / 累计请求 ").Append(FormatCount(snapshot.RequestTokenBudget))
                .Append(" Token / 工具 ").Append(FormatCount(snapshot.MaximumToolCalls))
                .Append(" / pass ").Append(FormatCount(snapshot.MaximumAgentPasses))
                .Append(" / 超时 ").Append(FormatCount(snapshot.TimeoutSeconds)).AppendLine(" 秒");
            builder.Append("能力：").Append(FormatCount(snapshot.RegisteredCapabilities))
                .Append(" 个已注册，其中 ").Append(FormatCount(snapshot.ApprovalCapabilities)).AppendLine(" 个可能或始终需要审批");
            builder.Append("Skills：").Append(FormatCount(snapshot.TrackedSkills))
                .Append(" 个已跟踪，").Append(FormatCount(snapshot.ExplicitOnlySkills)).AppendLine(" 个低使用率仅显式调用");
            builder.Append("MCP：内置监听器 ").Append(FormatMcpListener(snapshot))
                .Append(" · 外部启用 ").Append(FormatCount(snapshot.EnabledExternalMcpServers))
                .Append(" · 待审批 ").AppendLine(FormatCount(snapshot.PendingApprovals));
            return builder.ToString().TrimEnd();
        }

        private static void AppendConversation(
            StringBuilder builder,
            CopilotStatusDiagnosticSnapshot snapshot)
        {
            if (!snapshot.HasConversation)
            {
                builder.AppendLine("会话：未选择");
                return;
            }

            builder.Append("会话：")
                .AppendLine(FormatInline(
                    snapshot.ConversationTitle,
                    CopilotUiText.NewConversationTitle,
                    CopilotConversationRecord.MaximumTitleCharacters));
            builder.Append("会话 ID：")
                .AppendLine(FormatIdentifier(snapshot.ConversationId));
            builder.Append("可见历史：")
                .Append(FormatCount(snapshot.ConversationVisibleTurns))
                .Append(" 轮请求 · ")
                .Append(FormatCount(snapshot.ConversationMessageCount))
                .AppendLine(" 条消息");
            builder.Append("恢复：")
                .AppendLine(FormatConversationRecovery(snapshot));
            if (!snapshot.ConversationIsBranch)
                return;

            builder.Append("分支：父会话 ")
                .Append(FormatIdentifier(snapshot.ConversationParentId));
            var parentId = FormatIdentifier(snapshot.ConversationParentId);
            var rootId = FormatIdentifier(snapshot.ConversationRootId);
            if (!string.Equals(parentId, rootId, StringComparison.Ordinal))
                builder.Append(" · 根会话 ").Append(rootId);
            builder.AppendLine();
        }

        private static string FormatConversationRecovery(
            CopilotStatusDiagnosticSnapshot snapshot)
        {
            var runState = snapshot.ConversationRunState;
            var taskState = runState switch
            {
                CopilotHostedRunState.Queued => "Agent 已排队",
                CopilotHostedRunState.Running => "Agent 运行中",
                CopilotHostedRunState.PauseRequested => "Agent 正在暂停",
                CopilotHostedRunState.CancelRequested => "Agent 正在取消",
                _ when snapshot.ConversationHasCheckpoint
                    && snapshot.ConversationHasRecoverableAgentTasks =>
                    "有可安全继续的 Agent 任务",
                _ when snapshot.ConversationHasCheckpoint =>
                    "已保存 Agent checkpoint",
                _ => "无待恢复 Agent 任务",
            };
            if (snapshot.ConversationQueuedFollowUps <= 0)
                return taskState;

            return taskState
                + " · "
                + FormatCount(snapshot.ConversationQueuedFollowUps)
                + " 条排队后续";
        }

        private static void AppendActiveProviderRetry(
            StringBuilder builder,
            CopilotStatusDiagnosticSnapshot snapshot)
        {
            if (snapshot.ActiveProviderRetryCount <= 0)
                return;

            builder.Append("当前运行重试：")
                .Append(FormatCount(snapshot.ActiveProviderRetryCount))
                .Append(" 次");
            if (snapshot.ActiveProviderRetryNextAttempt > 0)
            {
                builder.Append(" · 最近 ")
                    .Append(FormatCount(snapshot.ActiveProviderRetryNextAttempt))
                    .Append('/')
                    .Append(FormatCount(Math.Max(
                        snapshot.ActiveProviderRetryNextAttempt,
                        snapshot.ActiveProviderRetryMaximumAttempts)));
            }
            if (!string.IsNullOrWhiteSpace(snapshot.ActiveProviderRetryFailureKind))
                builder.Append(" · ").Append(snapshot.ActiveProviderRetryFailureKind.Trim());
            var requestId = CopilotProviderRequestId.Normalize(
                snapshot.ActiveProviderRetryRequestId);
            if (requestId.Length > 0)
                builder.Append(" · 请求 ").Append(requestId);
            if (snapshot.ActiveProviderRetryDelayMilliseconds > 0)
            {
                builder.Append(" · 计划等待 ")
                    .Append(FormatDuration(snapshot.ActiveProviderRetryDelayMilliseconds));
            }
            builder.AppendLine();
        }

        private static string FormatShell(CopilotShellKind shell)
        {
            return shell switch
            {
                CopilotShellKind.CommandPrompt => "CMD",
                CopilotShellKind.PowerShell => "PowerShell",
                _ => "自动（PowerShell）",
            };
        }

        private static string FormatMcpListener(CopilotStatusDiagnosticSnapshot snapshot)
        {
            if (!snapshot.McpListenerEnabled)
                return "已禁用";
            return snapshot.McpListenerRunning ? "运行中" : "未运行";
        }

        private static string ValueOrFallback(string? value, string fallback = "unknown")
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string FormatIdentifier(string? value)
        {
            var normalized = string.Join(
                " ",
                (value ?? string.Empty).Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            return normalized.Length is > 0 and <= 128 ? normalized : "unknown";
        }

        private static string FormatInline(
            string? value,
            string fallback,
            int maximumCharacters)
        {
            var normalized = string.Join(
                " ",
                (value ?? string.Empty).Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            if (normalized.Length == 0)
                return fallback;
            if (normalized.Length <= maximumCharacters)
                return normalized;

            var retainedLength = Math.Max(1, maximumCharacters - 1);
            if (char.IsHighSurrogate(normalized[retainedLength - 1])
                && char.IsLowSurrogate(normalized[retainedLength]))
            {
                retainedLength--;
            }
            return normalized[..retainedLength].TrimEnd() + "…";
        }

        private static string FormatCount(long value)
        {
            return Math.Max(0, value).ToString("N0", CultureInfo.InvariantCulture);
        }

        private static string FormatDuration(long milliseconds)
        {
            var normalized = Math.Max(0, milliseconds);
            return normalized < 1000
                ? normalized.ToString(CultureInfo.InvariantCulture) + " 毫秒"
                : (normalized / 1000d).ToString("0.#", CultureInfo.InvariantCulture) + " 秒";
        }
    }
}
