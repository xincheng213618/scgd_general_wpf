using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal sealed class CopilotDoctorDiagnosticSnapshot
    {
        public string ProfileLabel { get; init; } = string.Empty;

        public bool ProfileConfigured { get; init; }

        public bool ProfileUsesInsecureHttp { get; init; }

        public string StatePersistenceNotice { get; init; } = string.Empty;

        public bool StatePersistenceBlocked { get; init; }

        public string StateRecoveryNotice { get; init; } = string.Empty;

        public bool TaskHostShutdown { get; init; }

        public int QueuedAgentRuns { get; init; }

        public int MaximumQueuedAgentRuns { get; init; }

        public bool McpListenerEnabled { get; init; }

        public bool McpListenerRunning { get; init; }

        public int RecentMcpFailureCount { get; init; }

        public int EnabledExternalMcpServers { get; init; }

        public IReadOnlyList<string> ConnectedExternalMcpServers { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> UnavailableExternalMcpServers { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> ChangedExternalMcpServers { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> UncheckedExternalMcpServers { get; init; } = Array.Empty<string>();

        public bool HookSurfaceValid { get; init; }

        public int EffectiveHookCount { get; init; }

        public int ExtensionSourceCount { get; init; }

        public int ExtensionIssueCount { get; init; }

        public int RecentHookFailureCount { get; init; }

        public int TrackedSkillCount { get; init; }

        public int ExplicitOnlySkillCount { get; init; }

        public int PendingApprovals { get; init; }
    }

    internal static class CopilotDoctorDiagnostics
    {
        private const int MaxVisibleNames = 5;

        public static string Format(CopilotDoctorDiagnosticSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var checks = BuildChecks(snapshot);
            var errorCount = checks.Count(check => check.Level == CopilotDoctorCheckLevel.Error);
            var warningCount = checks.Count(check => check.Level == CopilotDoctorCheckLevel.Warning);
            var okCount = checks.Count(check => check.Level == CopilotDoctorCheckLevel.Ok);
            var infoCount = checks.Count(check => check.Level == CopilotDoctorCheckLevel.Info);

            var builder = new StringBuilder();
            builder.AppendLine("/doctor · Copilot 健康检查");
            builder.AppendLine("本地只读预检：复用当前进程已有状态，不调用模型、工具或 MCP，不联网，也不自动修改配置。");
            builder.Append("结论：")
                .Append(errorCount > 0
                    ? "存在需要先处理的错误"
                    : warningCount > 0
                        ? "发现需要关注的项目"
                        : "未发现阻塞问题")
                .Append(" · 错误 ")
                .Append(FormatCount(errorCount))
                .Append(" · 警告 ")
                .Append(FormatCount(warningCount))
                .Append(" · 正常 ")
                .Append(FormatCount(okCount))
                .Append(" · 提示 ")
                .Append(FormatCount(infoCount))
                .AppendLine()
                .AppendLine();

            foreach (var check in checks)
            {
                builder.Append('[')
                    .Append(FormatLevel(check.Level))
                    .Append(']')
                    .Append(' ')
                    .Append(check.Name)
                    .Append('：')
                    .AppendLine(check.Detail);
                if (!string.IsNullOrWhiteSpace(check.Action))
                    builder.Append("       建议：").AppendLine(check.Action);
            }

            builder.AppendLine()
                .AppendLine("详情入口：/status 查看运行概览，/mcp 查看本机 MCP 审计，/hooks 查看 Hook 明细，/skills 查看 Skill 使用证据。")
                .Append("隐私边界：报告不显示 API Key、模型地址、MCP Endpoint、token 环境变量、工具参数、会话正文、内部 ID 或异常堆栈。");
            return builder.ToString().TrimEnd();
        }

        private static List<CopilotDoctorCheck> BuildChecks(CopilotDoctorDiagnosticSnapshot snapshot)
        {
            var checks = new List<CopilotDoctorCheck>
            {
                BuildProfileCheck(snapshot),
                BuildStatePersistenceCheck(snapshot),
            };
            if (!string.IsNullOrWhiteSpace(snapshot.StateRecoveryNotice) && !snapshot.StatePersistenceBlocked)
            {
                checks.Add(new CopilotDoctorCheck(
                    "会话恢复",
                    CopilotDoctorCheckLevel.Info,
                    FormatInline(snapshot.StateRecoveryNotice, "启动时使用了恢复数据。"),
                    "重要会话可先用 /export 保存可见 Markdown；确认内容后再继续工作。"));
            }

            checks.Add(BuildTaskHostCheck(snapshot));
            checks.Add(BuildMcpListenerCheck(snapshot));
            checks.Add(BuildExternalMcpCheck(snapshot));
            checks.Add(BuildHookCheck(snapshot));
            checks.Add(BuildSkillCheck(snapshot));
            checks.Add(BuildApprovalCheck(snapshot));
            return checks;
        }

        private static CopilotDoctorCheck BuildProfileCheck(CopilotDoctorDiagnosticSnapshot snapshot)
        {
            var profileLabel = FormatInline(snapshot.ProfileLabel, "当前 Profile");
            if (!snapshot.ProfileConfigured)
            {
                return new CopilotDoctorCheck(
                    "模型配置",
                    CopilotDoctorCheckLevel.Error,
                    $"{profileLabel} 尚未完成 API Key、Endpoint 与模型配置。",
                    "打开 Copilot 设置并补全当前 Profile；需要联网验证时再显式使用“Test Model”。");
            }

            if (snapshot.ProfileUsesInsecureHttp)
            {
                return new CopilotDoctorCheck(
                    "模型配置",
                    CopilotDoctorCheckLevel.Warning,
                    $"{profileLabel} 配置完整，但远程端点允许明文 HTTP；本次未主动联网验证。",
                    "优先改用 HTTPS；只有本机 loopback 模型服务才应使用 HTTP。");
            }

            return new CopilotDoctorCheck(
                "模型配置",
                CopilotDoctorCheckLevel.Ok,
                $"{profileLabel} 配置完整；本次未主动联网验证。");
        }

        private static CopilotDoctorCheck BuildStatePersistenceCheck(CopilotDoctorDiagnosticSnapshot snapshot)
        {
            if (snapshot.StatePersistenceBlocked)
            {
                return new CopilotDoctorCheck(
                    "会话保存",
                    CopilotDoctorCheckLevel.Error,
                    FormatInline(snapshot.StatePersistenceNotice, "会话状态写入已停止以保护现有记录。"),
                    "更新至兼容版本并重新打开应用；不要用旧版本覆盖现有会话记录。");
            }

            if (!string.IsNullOrWhiteSpace(snapshot.StatePersistenceNotice))
            {
                return new CopilotDoctorCheck(
                    "会话保存",
                    CopilotDoctorCheckLevel.Warning,
                    FormatInline(snapshot.StatePersistenceNotice, "最近一次会话保存失败。"),
                    "先用 /export 备份重要会话，再使用界面的“重试保存”；成功前不要关闭应用。");
            }

            return new CopilotDoctorCheck(
                "会话保存",
                CopilotDoctorCheckLevel.Ok,
                "当前没有持久化失败提示。");
        }

        private static CopilotDoctorCheck BuildTaskHostCheck(CopilotDoctorDiagnosticSnapshot snapshot)
        {
            var queued = Math.Max(0, snapshot.QueuedAgentRuns);
            var capacity = Math.Max(0, snapshot.MaximumQueuedAgentRuns);
            if (snapshot.TaskHostShutdown)
            {
                return new CopilotDoctorCheck(
                    "Agent 宿主",
                    CopilotDoctorCheckLevel.Error,
                    "任务宿主已经关闭，不能再接收 Agent 请求。",
                    "保存当前可见内容并重新打开应用。");
            }

            if (capacity > 0 && queued >= capacity)
            {
                return new CopilotDoctorCheck(
                    "Agent 宿主",
                    CopilotDoctorCheckLevel.Warning,
                    $"排队已满（{FormatCount(queued)}/{FormatCount(capacity)}）。",
                    "等待、完成或停止现有任务后再提交新的 Agent 请求；可用 /tasks 查看任务快照。");
            }

            return new CopilotDoctorCheck(
                "Agent 宿主",
                CopilotDoctorCheckLevel.Ok,
                $"任务宿主可调度 · 排队 {FormatCount(queued)}/{FormatCount(capacity)}。");
        }

        private static CopilotDoctorCheck BuildMcpListenerCheck(CopilotDoctorDiagnosticSnapshot snapshot)
        {
            if (!snapshot.McpListenerEnabled)
            {
                return new CopilotDoctorCheck(
                    "本机 MCP",
                    CopilotDoctorCheckLevel.Info,
                    "监听器未启用；这是可选能力。");
            }

            if (!snapshot.McpListenerRunning)
            {
                return new CopilotDoctorCheck(
                    "本机 MCP",
                    CopilotDoctorCheckLevel.Warning,
                    "监听器已启用但当前未运行。",
                    "打开 Copilot 设置检查监听器配置，或重新启动应用；使用 /mcp 查看脱敏状态。");
            }

            if (snapshot.RecentMcpFailureCount > 0)
            {
                return new CopilotDoctorCheck(
                    "本机 MCP",
                    CopilotDoctorCheckLevel.Warning,
                    $"监听器正在运行，但最近 15 分钟有 {FormatCount(snapshot.RecentMcpFailureCount)} 次失败记录。",
                    "使用 /mcp 查看稳定错误摘要与最近审计，再决定是否重试。");
            }

            return new CopilotDoctorCheck(
                "本机 MCP",
                CopilotDoctorCheckLevel.Ok,
                "监听器正在运行，最近 15 分钟没有失败记录。");
        }

        private static CopilotDoctorCheck BuildExternalMcpCheck(CopilotDoctorDiagnosticSnapshot snapshot)
        {
            var enabled = Math.Max(0, snapshot.EnabledExternalMcpServers);
            if (enabled == 0)
            {
                return new CopilotDoctorCheck(
                    "外部 MCP",
                    CopilotDoctorCheckLevel.Info,
                    "未启用外部服务；这是可选能力。");
            }

            var unavailable = NormalizeNames(snapshot.UnavailableExternalMcpServers);
            var changed = NormalizeNames(snapshot.ChangedExternalMcpServers);
            var uncheckedServers = NormalizeNames(snapshot.UncheckedExternalMcpServers);
            var connected = NormalizeNames(snapshot.ConnectedExternalMcpServers);
            if (unavailable.Length > 0 || changed.Length > 0)
            {
                var details = new List<string>
                {
                    $"已连接 {FormatCount(connected.Length)}/{FormatCount(enabled)}",
                };
                if (unavailable.Length > 0)
                    details.Add($"不可用 {FormatNames(unavailable)}");
                if (changed.Length > 0)
                    details.Add($"工具列表已变化 {FormatNames(changed)}");
                if (uncheckedServers.Length > 0)
                    details.Add($"未检查 {FormatNames(uncheckedServers)}");
                return new CopilotDoctorCheck(
                    "外部 MCP",
                    CopilotDoctorCheckLevel.Warning,
                    string.Join(" · ", details) + "。",
                    "打开 Copilot 设置并显式执行“Refresh Discovery”；报告不会自动连接远端服务。");
            }

            if (uncheckedServers.Length > 0)
            {
                return new CopilotDoctorCheck(
                    "外部 MCP",
                    CopilotDoctorCheckLevel.Info,
                    $"已连接 {FormatCount(connected.Length)}/{FormatCount(enabled)} · 尚未检查 {FormatNames(uncheckedServers)}。",
                    "需要验证时在 Copilot 设置中显式执行“Refresh Discovery”。");
            }

            return new CopilotDoctorCheck(
                "外部 MCP",
                CopilotDoctorCheckLevel.Ok,
                $"{FormatCount(connected.Length)}/{FormatCount(enabled)} 个已启用服务有可用健康快照。");
        }

        private static CopilotDoctorCheck BuildHookCheck(CopilotDoctorDiagnosticSnapshot snapshot)
        {
            if (!snapshot.HookSurfaceValid)
            {
                return new CopilotDoctorCheck(
                    "Hook 与扩展",
                    CopilotDoctorCheckLevel.Error,
                    "当前 Hook 运行时快照结构无效。",
                    "使用 /hooks 保存脱敏证据后重新打开应用；若仍复现，请更新或检查扩展模块。");
            }

            var extensionIssues = Math.Max(0, snapshot.ExtensionIssueCount);
            var recentFailures = Math.Max(0, snapshot.RecentHookFailureCount);
            if (extensionIssues > 0 || recentFailures > 0)
            {
                return new CopilotDoctorCheck(
                    "Hook 与扩展",
                    CopilotDoctorCheckLevel.Warning,
                    $"生效 Hook {FormatCount(snapshot.EffectiveHookCount)} 个 · 扩展激活问题 {FormatCount(extensionIssues)} 个 · 最近失败或超时 {FormatCount(recentFailures)} 次。",
                    "使用 /hooks 查看模块来源、稳定失败码与最近运行状态。");
            }

            return new CopilotDoctorCheck(
                "Hook 与扩展",
                CopilotDoctorCheckLevel.Ok,
                $"生效 Hook {FormatCount(snapshot.EffectiveHookCount)} 个 · 扩展来源 {FormatCount(snapshot.ExtensionSourceCount)} 个 · 没有激活问题或最近失败。");
        }

        private static CopilotDoctorCheck BuildSkillCheck(CopilotDoctorDiagnosticSnapshot snapshot)
        {
            var tracked = Math.Max(0, snapshot.TrackedSkillCount);
            var explicitOnly = Math.Max(0, snapshot.ExplicitOnlySkillCount);
            if (explicitOnly > 0)
            {
                return new CopilotDoctorCheck(
                    "Agent Skill",
                    CopilotDoctorCheckLevel.Info,
                    $"本地使用证据跟踪 {FormatCount(tracked)} 项 · {FormatCount(explicitOnly)} 项因持续未加载而仅限显式调用。",
                    "使用 /skills 查看证据；点名并实际加载对应 Skill 后可恢复隐式匹配。");
            }

            return new CopilotDoctorCheck(
                "Agent Skill",
                CopilotDoctorCheckLevel.Ok,
                tracked == 0
                    ? "本地使用证据可读取，尚无已跟踪 Skill。"
                    : $"本地使用证据可读取 · 跟踪 {FormatCount(tracked)} 项 · 没有低使用率降级。");
        }

        private static CopilotDoctorCheck BuildApprovalCheck(CopilotDoctorDiagnosticSnapshot snapshot)
        {
            var pending = Math.Max(0, snapshot.PendingApprovals);
            return pending == 0
                ? new CopilotDoctorCheck(
                    "待确认操作",
                    CopilotDoctorCheckLevel.Ok,
                    "当前没有等待用户确认的 MCP 操作。")
                : new CopilotDoctorCheck(
                    "待确认操作",
                    CopilotDoctorCheckLevel.Info,
                    $"当前有 {FormatCount(pending)} 个操作等待用户确认。",
                    "打开待确认列表逐项审阅；诊断不会代替用户批准或拒绝。");
        }

        private static string[] NormalizeNames(IReadOnlyList<string>? names)
        {
            return (names ?? Array.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => FormatInline(name, "unnamed", 80))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string FormatNames(string[] names)
        {
            var visible = names.Take(MaxVisibleNames).ToArray();
            var text = string.Join("、", visible);
            return names.Length > MaxVisibleNames
                ? $"{text} 等 {FormatCount(names.Length)} 个"
                : text;
        }

        private static string FormatInline(string? value, string fallback, int maximumLength = 160)
        {
            var normalized = string.Join(
                " ",
                (value ?? string.Empty)
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (normalized.Length == 0)
                return fallback;
            return normalized.Length <= maximumLength
                ? normalized
                : normalized[..maximumLength] + "…";
        }

        private static string FormatCount(int value) => Math.Max(0, value).ToString(CultureInfo.InvariantCulture);

        private static string FormatLevel(CopilotDoctorCheckLevel level)
        {
            return level switch
            {
                CopilotDoctorCheckLevel.Ok => "OK",
                CopilotDoctorCheckLevel.Info => "INFO",
                CopilotDoctorCheckLevel.Warning => "WARN",
                CopilotDoctorCheckLevel.Error => "ERROR",
                _ => "INFO",
            };
        }

        private enum CopilotDoctorCheckLevel
        {
            Ok,
            Info,
            Warning,
            Error,
        }

        private sealed record CopilotDoctorCheck(
            string Name,
            CopilotDoctorCheckLevel Level,
            string Detail,
            string Action = "");
    }
}
