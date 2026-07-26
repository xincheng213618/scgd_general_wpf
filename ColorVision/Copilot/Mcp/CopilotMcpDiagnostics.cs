using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot.Mcp
{
    internal sealed class CopilotMcpDiagnosticSnapshot
    {
        public string Endpoint { get; init; } = string.Empty;

        public bool Enabled { get; init; }

        public bool Running { get; init; }

        public int PendingActions { get; init; }

        public IReadOnlyList<CopilotMcpAuditEntry> RecentEntries { get; init; } = Array.Empty<CopilotMcpAuditEntry>();

        public CopilotMcpAuditEntry? LastError { get; init; }

        public string StatusMessage { get; init; } = string.Empty;
    }

    internal static class CopilotMcpDiagnostics
    {
        public static string Format(CopilotMcpDiagnosticSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var entries = snapshot.RecentEntries ?? Array.Empty<CopilotMcpAuditEntry>();
            var failureCount = entries.Count(entry => entry != null && !entry.Success);
            var builder = new StringBuilder();
            builder.Append("端点：").AppendLine(snapshot.Endpoint?.Trim() ?? string.Empty);
            builder.Append("服务：").AppendLine(FormatServiceState(snapshot.Enabled, snapshot.Running));
            builder.Append("待处理操作：").AppendLine(Math.Max(0, snapshot.PendingActions).ToString());
            builder.Append("近期调用：")
                .Append(entries.Count)
                .Append("；失败：")
                .AppendLine(failureCount.ToString());

            var lastEntry = entries.LastOrDefault(entry => entry != null);
            if (lastEntry != null)
                builder.Append("最后调用：").AppendLine(FormatAuditEntry(lastEntry));

            if (snapshot.LastError != null && !IsSameEntry(lastEntry, snapshot.LastError))
                builder.Append("最后错误：").AppendLine(FormatAuditEntry(snapshot.LastError));

            builder.Append(FormatStatusMessage(snapshot.StatusMessage));
            return builder.ToString();
        }

        private static string FormatServiceState(bool enabled, bool running)
        {
            if (!enabled)
                return "已禁用";
            return running ? "运行中" : "已停止";
        }

        private static string FormatAuditEntry(CopilotMcpAuditEntry entry)
        {
            var result = entry.Success ? "成功" : "失败";
            var toolName = CopilotMcpAuditLogger.RedactText(entry.ToolName);
            var message = string.IsNullOrWhiteSpace(entry.ErrorMessage)
                ? string.Empty
                : " - " + CopilotMcpAuditLogger.RedactText(entry.ErrorMessage);
            var caller = string.IsNullOrWhiteSpace(entry.CallerSource)
                ? string.Empty
                : $" 调用方={CopilotMcpAuditLogger.RedactText(entry.CallerSource)}";

            return $"{entry.TimestampUtc.ToLocalTime():HH:mm:ss} {toolName} {result} {entry.DurationMs}ms{caller}{message}";
        }

        private static string FormatStatusMessage(string? value)
        {
            var status = CopilotMcpAuditLogger.RedactText(value).Trim();
            if (status.Length == 0)
                return "暂无 MCP 状态信息。";

            return status switch
            {
                "ColorVision MCP server is disabled." => "ColorVision MCP 服务已禁用。",
                "ColorVision MCP server is stopped." => "ColorVision MCP 服务已停止。",
                "ColorVision MCP server token is missing." => "ColorVision MCP 服务缺少访问令牌。",
                "Restarting ColorVision MCP server." => "正在重启 ColorVision MCP 服务。",
                _ => TranslateKnownStatusPrefix(status),
            };
        }

        private static string TranslateKnownStatusPrefix(string status)
        {
            const string runningPrefix = "ColorVision MCP server is running at ";
            const string portUnavailablePrefix = "ColorVision MCP server port unavailable at ";
            const string failedPrefix = "ColorVision MCP server failed to start: ";
            if (status.StartsWith(runningPrefix, StringComparison.Ordinal))
                return "ColorVision MCP 服务运行于 " + status[runningPrefix.Length..];
            if (status.StartsWith(portUnavailablePrefix, StringComparison.Ordinal))
                return "ColorVision MCP 服务端口不可用：" + status[portUnavailablePrefix.Length..];
            if (status.StartsWith(failedPrefix, StringComparison.Ordinal))
                return "ColorVision MCP 服务启动失败：" + status[failedPrefix.Length..];
            return status;
        }

        private static bool IsSameEntry(CopilotMcpAuditEntry? left, CopilotMcpAuditEntry right)
        {
            return left != null
                && left.TimestampUtc == right.TimestampUtc
                && string.Equals(left.ToolName, right.ToolName, StringComparison.Ordinal)
                && left.Success == right.Success
                && left.DurationMs == right.DurationMs
                && string.Equals(left.ErrorMessage, right.ErrorMessage, StringComparison.Ordinal)
                && string.Equals(left.CallerSource, right.CallerSource, StringComparison.Ordinal);
        }
    }
}
