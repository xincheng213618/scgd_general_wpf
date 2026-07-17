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

        public string ReasoningLabel { get; init; } = string.Empty;

        public CopilotAgentMode Mode { get; init; }

        public string AgentState { get; init; } = string.Empty;

        public int QueuedAgentRuns { get; init; }

        public int MaximumQueuedAgentRuns { get; init; }

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
            builder.Append("模式：").Append(snapshot.Mode).Append(" · 推理 ").AppendLine(ValueOrFallback(snapshot.ReasoningLabel, "默认"));
            builder.Append("Agent：").Append(ValueOrFallback(snapshot.AgentState, "Idle"))
                .Append(" · 队列 ").Append(FormatCount(snapshot.QueuedAgentRuns)).Append('/').AppendLine(FormatCount(snapshot.MaximumQueuedAgentRuns));
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

        private static string FormatCount(long value)
        {
            return Math.Max(0, value).ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}
