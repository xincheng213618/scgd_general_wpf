using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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

        public IReadOnlyList<CopilotMcpExternalServerDiagnosticSnapshot> ExternalServers { get; init; } =
            Array.Empty<CopilotMcpExternalServerDiagnosticSnapshot>();
    }

    internal sealed class CopilotMcpExternalServerDiagnosticSnapshot
    {
        public string Name { get; init; } = string.Empty;

        public string Endpoint { get; init; } = string.Empty;

        public bool Enabled { get; init; }

        public bool CredentialReferenceConfigured { get; init; }

        public CopilotMcpClientAccessPolicy AccessPolicy { get; init; }

        public int ReadOnlyToolRules { get; init; }

        public int ApprovalToolRules { get; init; }

        public int ConnectionTimeoutSeconds { get; init; }

        public int ToolTimeoutSeconds { get; init; }

        public CopilotMcpClientHealthSnapshot? Health { get; init; }
    }

    internal static class CopilotMcpDiagnostics
    {
        private const int MaximumDisplayedExternalServers = 8;
        private const int MaximumInlineTextCharacters = 240;
        private static readonly Regex EndpointRegex = new(
            "https?://[^\\s]+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex InlineWhitespaceRegex = new(
            "\\s+",
            RegexOptions.Compiled);

        public static CopilotMcpExternalServerDiagnosticSnapshot CaptureExternalServer(
            CopilotMcpClientServerConfig server)
        {
            ArgumentNullException.ThrowIfNull(server);
            var rules = server.ToolRules
                .Where(rule => rule != null && !string.IsNullOrWhiteSpace(rule.ToolName))
                .ToArray();
            CopilotMcpClientHealthRegistry.TryGetSnapshot(server, out var health);
            return new CopilotMcpExternalServerDiagnosticSnapshot
            {
                Name = server.Name,
                Endpoint = server.Endpoint,
                Enabled = server.Enabled,
                CredentialReferenceConfigured = !string.IsNullOrWhiteSpace(server.BearerTokenEnvironmentVariable),
                AccessPolicy = server.AccessPolicy,
                ReadOnlyToolRules = rules.Count(rule =>
                    rule.AccessPolicy == CopilotMcpClientAccessPolicy.ReadOnly),
                ApprovalToolRules = rules.Count(rule =>
                    rule.AccessPolicy == CopilotMcpClientAccessPolicy.RequireApproval),
                ConnectionTimeoutSeconds = server.ConnectionTimeoutSeconds,
                ToolTimeoutSeconds = server.ToolTimeoutSeconds,
                Health = health,
            };
        }

        public static string Format(CopilotMcpDiagnosticSnapshot snapshot, bool verbose = false)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var entries = snapshot.RecentEntries ?? Array.Empty<CopilotMcpAuditEntry>();
            var externalServers = (snapshot.ExternalServers
                    ?? Array.Empty<CopilotMcpExternalServerDiagnosticSnapshot>())
                .Where(server => server?.Enabled == true)
                .ToArray();
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

            AppendExternalSummary(builder, externalServers);
            if (verbose)
                AppendExternalDetails(builder, externalServers);
            builder.Append(FormatStatusMessage(snapshot.StatusMessage));
            return builder.ToString();
        }

        private static void AppendExternalSummary(
            StringBuilder builder,
            CopilotMcpExternalServerDiagnosticSnapshot[] servers)
        {
            var connected = servers.Count(server =>
                server.Health?.State == CopilotMcpClientHealthState.Connected
                && server.Health.CacheInvalidated == false);
            var changed = servers.Count(server => server.Health?.CacheInvalidated == true);
            var unavailable = servers.Count(server =>
                server.Health?.State == CopilotMcpClientHealthState.Unavailable
                && server.Health.CacheInvalidated == false);
            var uncheckedCount = servers.Length - connected - changed - unavailable;
            builder.Append("外部服务：")
                .Append(servers.Length.ToString("N0", CultureInfo.InvariantCulture));
            if (servers.Length > 0)
            {
                builder.Append("；已连接 ")
                    .Append(connected.ToString("N0", CultureInfo.InvariantCulture))
                    .Append("；不可用 ")
                    .Append(unavailable.ToString("N0", CultureInfo.InvariantCulture))
                    .Append("；工具列表变化 ")
                    .Append(changed.ToString("N0", CultureInfo.InvariantCulture))
                    .Append("；未检查 ")
                    .Append(uncheckedCount.ToString("N0", CultureInfo.InvariantCulture));
            }
            builder.AppendLine();
        }

        private static void AppendExternalDetails(
            StringBuilder builder,
            CopilotMcpExternalServerDiagnosticSnapshot[] servers)
        {
            builder.AppendLine();
            builder.AppendLine("外部服务详情（仅使用本地配置与最近健康快照，不主动联网）：");
            if (servers.Length == 0)
            {
                builder.AppendLine("  - 未配置已启用的外部 MCP 服务");
                return;
            }

            foreach (var server in servers.Take(MaximumDisplayedExternalServers))
            {
                builder.Append("  - ")
                    .Append(FormatInlineText(server.Name, "unnamed"))
                    .Append(" · ")
                    .Append(FormatTransport(server.Endpoint))
                    .Append(" · ")
                    .Append(FormatAccessPolicy(server))
                    .Append(" · 凭据引用")
                    .Append(server.CredentialReferenceConfigured ? "已配置" : "未配置")
                    .Append(" · 超时 ")
                    .Append(Math.Max(1, server.ConnectionTimeoutSeconds).ToString(
                        CultureInfo.InvariantCulture))
                    .Append('/')
                    .Append(Math.Max(1, server.ToolTimeoutSeconds).ToString(
                        CultureInfo.InvariantCulture))
                    .AppendLine(" 秒");
                builder.Append("    健康：")
                    .AppendLine(FormatHealth(server.Health));
            }

            if (servers.Length > MaximumDisplayedExternalServers)
            {
                builder.Append("  - 其余 ")
                    .Append((servers.Length - MaximumDisplayedExternalServers).ToString(
                        "N0",
                        CultureInfo.InvariantCulture))
                    .AppendLine(" 个服务仅计入汇总");
            }
        }

        private static string FormatTransport(string? endpoint)
        {
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
                return "传输未知";

            var scheme = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                ? "HTTPS"
                : string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                    ? "HTTP"
                    : uri.Scheme.ToUpperInvariant();
            return scheme + (uri.IsLoopback ? " loopback" : " remote");
        }

        private static string FormatAccessPolicy(CopilotMcpExternalServerDiagnosticSnapshot server)
        {
            var ruleCount = Math.Max(0, server.ReadOnlyToolRules)
                + Math.Max(0, server.ApprovalToolRules);
            if (ruleCount == 0)
            {
                return "默认"
                    + (server.AccessPolicy == CopilotMcpClientAccessPolicy.ReadOnly
                        ? "只读"
                        : "每次审批");
            }

            return "白名单 "
                + ruleCount.ToString("N0", CultureInfo.InvariantCulture)
                + "（只读 "
                + Math.Max(0, server.ReadOnlyToolRules).ToString("N0", CultureInfo.InvariantCulture)
                + "，每次审批 "
                + Math.Max(0, server.ApprovalToolRules).ToString("N0", CultureInfo.InvariantCulture)
                + "）";
        }

        private static string FormatHealth(CopilotMcpClientHealthSnapshot? health)
        {
            if (health == null || health.State == CopilotMcpClientHealthState.Unknown)
                return "未检查；请在设置页显式执行 Refresh Discovery";

            var checkedAt = health.CheckedAtUtc == default
                ? string.Empty
                : " · 检查 " + health.CheckedAtUtc.ToLocalTime().ToString(
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture);
            if (health.CacheInvalidated)
            {
                return "工具列表已变化 · 缓存已失效"
                    + FormatCapabilityRevision(health)
                    + checkedAt;
            }
            if (health.State == CopilotMcpClientHealthState.Unavailable)
            {
                var message = FormatInlineText(health.Message, "连接不可用");
                return "不可用 · " + message + checkedAt;
            }

            return "已连接 · 工具 "
                + Math.Max(0, health.ExposedToolCount).ToString("N0", CultureInfo.InvariantCulture)
                + '/'
                + Math.Max(0, health.DiscoveredToolCount).ToString("N0", CultureInfo.InvariantCulture)
                + "（过滤 "
                + Math.Max(0, health.FilteredToolCount).ToString("N0", CultureInfo.InvariantCulture)
                + "） · "
                + (health.UsedCachedDiscovery ? "缓存发现" : "实时发现")
                + FormatCapabilityRevision(health)
                + (health.ToolListChangeNotificationsEnabled ? " · 列表通知已启用" : string.Empty)
                + checkedAt;
        }

        private static string FormatCapabilityRevision(CopilotMcpClientHealthSnapshot health)
        {
            var result = " · capability revision "
                + Math.Max(0, health.CapabilityRevision).ToString(
                    "N0",
                    CultureInfo.InvariantCulture);
            return health.CapabilitiesChanged ? result + "（已变化）" : result;
        }

        private static string FormatInlineText(string? value, string fallback)
        {
            var sanitized = CopilotMcpAuditLogger.RedactText(value ?? string.Empty);
            sanitized = EndpointRegex.Replace(sanitized, "<endpoint>");
            sanitized = InlineWhitespaceRegex.Replace(sanitized, " ").Trim();
            if (sanitized.Length > MaximumInlineTextCharacters)
                sanitized = sanitized[..MaximumInlineTextCharacters] + "...";
            return sanitized.Length == 0 ? fallback : sanitized;
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
