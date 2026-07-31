using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal enum CopilotSubagentDiagnosticAction
    {
        Overview,
        Roles,
        Runs,
        Invalid,
    }

    internal readonly record struct CopilotSubagentDiagnosticRequest(
        CopilotSubagentDiagnosticAction Action,
        int Limit);

    internal sealed record CopilotSubagentRunDiagnostic(
        string RunId,
        string ResumeFromRunId,
        string RoleId,
        CopilotToolExecutionState State,
        CopilotAgentStopReason StopReason,
        DateTimeOffset StartedAtUtc,
        long DurationMs,
        long QueueDurationMs,
        int RequestTokenBudget,
        long ConsumedTokens,
        int ProviderCalls,
        int ToolCalls,
        int RegisteredToolCount,
        int AvailableToolCount);

    internal static class CopilotSubagentDiagnostics
    {
        internal const int DefaultDisplayedRuns = 8;
        internal const int MaximumDisplayedRuns = 20;
        internal const string Usage = "用法：/agents [roles|runs [N]]"
            + "\nN 可取 1–20；/subagents 为同义命令。";

        public static CopilotSubagentDiagnosticRequest ParseCommand(string? arguments)
        {
            var tokens = (arguments ?? string.Empty).Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length == 0)
            {
                return new CopilotSubagentDiagnosticRequest(
                    CopilotSubagentDiagnosticAction.Overview,
                    DefaultDisplayedRuns);
            }
            if (tokens.Length == 1
                && string.Equals(tokens[0], "roles", StringComparison.OrdinalIgnoreCase))
            {
                return new CopilotSubagentDiagnosticRequest(
                    CopilotSubagentDiagnosticAction.Roles,
                    0);
            }
            if (string.Equals(tokens[0], "runs", StringComparison.OrdinalIgnoreCase)
                && (tokens.Length == 1
                    || tokens.Length == 2
                    && int.TryParse(
                        tokens[1],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var limit)
                    && limit is >= 1 and <= MaximumDisplayedRuns))
            {
                return new CopilotSubagentDiagnosticRequest(
                    CopilotSubagentDiagnosticAction.Runs,
                    tokens.Length == 1
                        ? DefaultDisplayedRuns
                        : int.Parse(tokens[1], CultureInfo.InvariantCulture));
            }

            return new CopilotSubagentDiagnosticRequest(
                CopilotSubagentDiagnosticAction.Invalid,
                0);
        }

        public static IReadOnlyList<CopilotSubagentRunDiagnostic> CaptureRuns(
            CopilotConversationRecord? conversation,
            CopilotSubagentRoleCatalog? catalog = null)
        {
            if (conversation == null)
                return Array.Empty<CopilotSubagentRunDiagnostic>();

            catalog ??= CopilotSubagentRoleCatalog.Default;
            var runs = new List<CopilotSubagentRunDiagnostic>();
            for (var messageIndex = conversation.Messages.Count - 1; messageIndex >= 0; messageIndex--)
            {
                var message = conversation.Messages[messageIndex];
                if (message?.IsUser != false || message.AgentTraceEntries == null)
                    continue;

                for (var traceIndex = message.AgentTraceEntries.Count - 1; traceIndex >= 0; traceIndex--)
                {
                    var trace = message.AgentTraceEntries[traceIndex];
                    if (trace == null || !TryResolveRoleId(trace, catalog, out var roleId))
                        continue;

                    runs.Add(new CopilotSubagentRunDiagnostic(
                        trace.DelegatedRunId,
                        trace.DelegatedResumeFromRunId,
                        roleId,
                        trace.State,
                        trace.DelegatedStopReason,
                        trace.StartedAtUtc,
                        Math.Max(0, trace.DurationMs),
                        Math.Max(0, trace.DelegatedQueueDurationMs),
                        Math.Max(0, trace.DelegatedRequestTokenBudget),
                        Math.Max(0, trace.DelegatedConsumedTokens),
                        Math.Max(0, trace.DelegatedProviderCalls),
                        Math.Max(0, trace.DelegatedToolCalls),
                        Math.Max(0, trace.DelegatedRegisteredToolCount),
                        Math.Clamp(
                            trace.DelegatedAvailableToolCount,
                            0,
                            Math.Max(0, trace.DelegatedRegisteredToolCount))));
                }
            }
            return runs;
        }

        public static string Format(
            CopilotConversationRecord? conversation,
            string? arguments,
            CopilotSubagentRoleCatalog? catalog = null)
        {
            var request = ParseCommand(arguments);
            if (request.Action == CopilotSubagentDiagnosticAction.Invalid)
                return Usage;

            catalog ??= CopilotSubagentRoleCatalog.Default;
            var title = string.IsNullOrWhiteSpace(conversation?.Title)
                ? CopilotUiText.NewConversationTitle
                : conversation.Title.Trim();
            var builder = new StringBuilder()
                .Append("子代理 · ")
                .AppendLine(title)
                .Append("运行模型：请求级只读委派 · 并发硬上限 ")
                .Append(CopilotSubagentCoordinator.MaximumConcurrentRuns.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" · 单次硬上限 ")
                .Append(FormatTokens(CopilotSubagentCoordinator.MaximumRunTokenBudget))
                .Append(" tokens · 请求合计硬上限 ")
                .Append(FormatTokens(CopilotSubagentCoordinator.MaximumTotalTokenBudget))
                .AppendLine(" tokens")
                .Append("角色目录：")
                .Append(catalog.Roles.Count.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" 个 · revision ")
                .AppendLine(catalog.Revision.ToString(CultureInfo.InvariantCulture))
                .AppendLine("实际子运行 Token 预算还会根据父任务预算下调。");

            if (request.Action is CopilotSubagentDiagnosticAction.Overview
                or CopilotSubagentDiagnosticAction.Roles)
            {
                AppendRoles(builder, catalog);
            }

            if (request.Action is CopilotSubagentDiagnosticAction.Overview
                or CopilotSubagentDiagnosticAction.Runs)
            {
                AppendRuns(builder, CaptureRuns(conversation, catalog), request.Limit);
            }

            builder.AppendLine()
                .Append("边界：子代理由父 Agent 按请求创建并回传结果；同一父请求内，可用完成结果给出的 run_id 续跑同角色且具有有效 checkpoint 的子代理。")
                .Append("它仍不是可切换、跨请求或应用重启后可恢复的独立会话。")
                .Append("列表仅来自当前会话保存的限长运行元数据，不显示任务提示、回答正文、隐藏推理或凭据。");
            return builder.ToString();
        }

        private static bool TryResolveRoleId(
            CopilotAgentTraceEntry trace,
            CopilotSubagentRoleCatalog catalog,
            out string roleId)
        {
            roleId = (trace.DelegatedRoleId ?? string.Empty).Trim();
            if (roleId.Length > 0)
                return true;
            if (!string.IsNullOrWhiteSpace(trace.ToolName)
                && catalog.TryGetByToolName(trace.ToolName, out var role))
            {
                roleId = role!.Id;
                return true;
            }
            if (!string.IsNullOrWhiteSpace(trace.DelegatedRunId)
                && trace.ToolName.StartsWith("Delegate", StringComparison.Ordinal))
            {
                roleId = "unknown";
                return true;
            }
            return false;
        }

        private static void AppendRoles(
            StringBuilder builder,
            CopilotSubagentRoleCatalog catalog)
        {
            builder.AppendLine()
                .AppendLine("角色");
            foreach (var role in catalog.Roles.OrderBy(role => role.Id, StringComparer.OrdinalIgnoreCase))
            {
                builder.Append("- ")
                    .Append(role.Id)
                    .Append(" · ")
                    .Append(role.DisplayName)
                    .Append(" · ")
                    .Append(FormatContextScope(role.ContextScope))
                    .Append(" · 子模式 ")
                    .AppendLine(role.ChildMode.ToString())
                    .Append("  来源：")
                    .Append(role.SourceName)
                    .Append(" [")
                    .Append(role.SourceId)
                    .Append("] v")
                    .Append(role.SourceVersion)
                    .Append(" · tool ")
                    .AppendLine(role.ToolName)
                    .Append("  工具：")
                    .AppendLine(FormatCapabilities(role.ReadCapabilities))
                    .Append("  边界：最多 ")
                    .Append(role.MaximumToolCalls.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" 次工具调用 · ")
                    .Append(role.MaximumAgentPasses.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" 个 Agent pass · ")
                    .Append(FormatDuration((long)role.MaximumDuration.TotalMilliseconds))
                    .Append(" · 回答 ")
                    .Append(role.MaximumAnswerCharacters.ToString("N0", CultureInfo.CurrentCulture))
                    .AppendLine(" 字符")
                    .Append("  父模式：")
                    .AppendLine(string.Join(", ", role.ParentModes));
            }
        }

        private static void AppendRuns(
            StringBuilder builder,
            IReadOnlyList<CopilotSubagentRunDiagnostic> runs,
            int limit)
        {
            builder.AppendLine()
                .AppendLine("最近运行");
            if (runs.Count == 0)
            {
                builder.AppendLine("当前会话没有可见的子代理运行轨迹。");
                return;
            }

            var visibleRuns = runs.Take(limit).ToArray();
            builder.Append("显示 ")
                .Append(visibleRuns.Length.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" / ")
                .Append(runs.Count.ToString("N0", CultureInfo.CurrentCulture))
                .AppendLine(" 次（新到旧）");
            for (var index = 0; index < visibleRuns.Length; index++)
            {
                var run = visibleRuns[index];
                builder.Append('#')
                    .Append((index + 1).ToString(CultureInfo.InvariantCulture))
                    .Append(" · ")
                    .Append(run.RoleId)
                    .Append(" · ")
                    .Append(string.IsNullOrWhiteSpace(run.RunId) ? "ID 待回传" : run.RunId)
                    .Append(" · state=")
                    .Append(run.State);
                if (!string.IsNullOrWhiteSpace(run.ResumeFromRunId))
                    builder.Append(" · resumed_from=").Append(run.ResumeFromRunId);
                if (run.StopReason != CopilotAgentStopReason.None)
                    builder.Append(" · stop=").Append(run.StopReason);
                builder.AppendLine();

                builder.Append("  ");
                var hasDetail = false;
                if (run.StartedAtUtc != default)
                {
                    builder.Append(run.StartedAtUtc.ToLocalTime().ToString("MM-dd HH:mm:ss", CultureInfo.CurrentCulture));
                    hasDetail = true;
                }
                if (run.DurationMs > 0)
                {
                    AppendSeparator(builder, ref hasDetail);
                    builder.Append("耗时 ").Append(FormatDuration(run.DurationMs));
                }
                if (run.QueueDurationMs > 0)
                {
                    AppendSeparator(builder, ref hasDetail);
                    builder.Append("排队 ").Append(FormatDuration(run.QueueDurationMs));
                }
                if (run.RequestTokenBudget > 0 || run.ConsumedTokens > 0)
                {
                    AppendSeparator(builder, ref hasDetail);
                    builder.Append("tokens ")
                        .Append(FormatTokens(run.ConsumedTokens))
                        .Append('/')
                        .Append(FormatTokens(run.RequestTokenBudget));
                }
                if (run.ProviderCalls > 0 || run.ToolCalls > 0)
                {
                    AppendSeparator(builder, ref hasDetail);
                    builder.Append("模型 ")
                        .Append(run.ProviderCalls.ToString("N0", CultureInfo.CurrentCulture))
                        .Append(" · 工具 ")
                        .Append(run.ToolCalls.ToString("N0", CultureInfo.CurrentCulture));
                }
                if (run.RegisteredToolCount > 0)
                {
                    AppendSeparator(builder, ref hasDetail);
                    builder.Append("工具面 ")
                        .Append(run.AvailableToolCount.ToString("N0", CultureInfo.CurrentCulture))
                        .Append('/')
                        .Append(run.RegisteredToolCount.ToString("N0", CultureInfo.CurrentCulture));
                }
                if (!hasDetail)
                    builder.Append("等待子运行回传用量");
                builder.AppendLine();
            }

            if (visibleRuns.Length < runs.Count)
            {
                builder.Append("另有 ")
                    .Append((runs.Count - visibleRuns.Length).ToString("N0", CultureInfo.CurrentCulture))
                    .AppendLine(" 次较早运行未显示。");
            }
        }

        private static void AppendSeparator(StringBuilder builder, ref bool hasValue)
        {
            if (hasValue)
                builder.Append(" · ");
            hasValue = true;
        }

        private static string FormatContextScope(CopilotSubagentContextScope scope) =>
            scope switch
            {
                CopilotSubagentContextScope.WorkspaceReadOnly => "工作区只读",
                CopilotSubagentContextScope.PublicWeb => "公共网页只读",
                _ => scope.ToString(),
            };

        private static string FormatCapabilities(CopilotSubagentReadCapabilities capabilities)
        {
            var values = new List<string>();
            if (capabilities.HasFlag(CopilotSubagentReadCapabilities.SearchFiles))
                values.Add("SearchFiles");
            if (capabilities.HasFlag(CopilotSubagentReadCapabilities.GrepText))
                values.Add("GrepText");
            if (capabilities.HasFlag(CopilotSubagentReadCapabilities.ReadLocalFile))
                values.Add("ReadLocalFile");
            if (capabilities.HasFlag(CopilotSubagentReadCapabilities.ListDirectory))
                values.Add("ListDirectory");
            if (capabilities.HasFlag(CopilotSubagentReadCapabilities.WebSearch))
                values.Add("WebSearch");
            if (capabilities.HasFlag(CopilotSubagentReadCapabilities.FetchUrl))
                values.Add("FetchUrl");
            return values.Count == 0 ? "无" : string.Join(", ", values);
        }

        private static string FormatDuration(long milliseconds)
        {
            var normalized = Math.Max(0, milliseconds);
            if (normalized < 1000)
                return normalized.ToString("N0", CultureInfo.CurrentCulture) + "ms";
            if (normalized < 60_000)
                return (normalized / 1000d).ToString("0.#", CultureInfo.CurrentCulture) + "s";

            var duration = TimeSpan.FromMilliseconds(Math.Min(
                normalized,
                TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerMillisecond));
            return duration.TotalHours < 1
                ? $"{(int)duration.TotalMinutes:N0}m {duration.Seconds:N0}s"
                : $"{(int)duration.TotalHours:N0}h {duration.Minutes:N0}m";
        }

        private static string FormatTokens(long value) =>
            Math.Max(0, value).ToString("N0", CultureInfo.CurrentCulture);
    }
}
