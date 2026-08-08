using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal static partial class CopilotSubagentDiagnostics
    {
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
            int limit,
            string heading,
            string emptyMessage,
            string orderingDescription,
            string hiddenRunDescription)
        {
            builder.AppendLine()
                .AppendLine(heading);
            if (runs.Count == 0)
            {
                builder.AppendLine(emptyMessage);
                return;
            }

            var visibleRuns = runs.Take(limit).ToArray();
            builder.Append("显示 ")
                .Append(visibleRuns.Length.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" / ")
                .Append(runs.Count.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" 次（")
                .Append(orderingDescription)
                .AppendLine("）");
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
                if (run.State == CopilotToolExecutionState.Running
                    && !string.IsNullOrWhiteSpace(run.Activity))
                {
                    builder.Append(" · activity=").Append(run.Activity);
                }
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
                    .Append(" 次")
                    .Append(hiddenRunDescription)
                    .AppendLine("运行未显示。");
            }
        }

        private static void AppendRunDetails(
            StringBuilder builder,
            IReadOnlyList<CopilotSubagentRunDiagnostic> runs,
            string runId)
        {
            builder.AppendLine()
                .AppendLine("运行详情");
            var run = runs.FirstOrDefault(candidate =>
                string.Equals(candidate.RunId, runId, StringComparison.Ordinal));
            if (run == null)
            {
                builder.Append("当前会话没有可查看的子代理运行 ")
                    .Append(runId)
                    .AppendLine("；请先用 /agents runs 查找有效 run_id。");
                return;
            }

            builder.Append(run.RoleId)
                .Append(" · ")
                .Append(run.RunId)
                .Append(" · state=")
                .Append(run.State);
            if (run.Closed)
                builder.Append(" · closed=true");
            if (!string.IsNullOrWhiteSpace(run.ResumeFromRunId))
                builder.Append(" · resumed_from=").Append(run.ResumeFromRunId);
            if (run.StopReason != CopilotAgentStopReason.None)
                builder.Append(" · stop=").Append(run.StopReason);
            builder.AppendLine();

            var hasTiming = false;
            if (run.StartedAtUtc != default)
            {
                builder.Append("开始 ")
                    .Append(run.StartedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture));
                hasTiming = true;
            }
            if (run.CompletedAtUtc.HasValue)
            {
                AppendSeparator(builder, ref hasTiming);
                builder.Append("完成 ")
                    .Append(run.CompletedAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture));
            }
            if (run.DurationMs > 0)
            {
                AppendSeparator(builder, ref hasTiming);
                builder.Append("耗时 ").Append(FormatDuration(run.DurationMs));
            }
            if (run.QueueDurationMs > 0)
            {
                AppendSeparator(builder, ref hasTiming);
                builder.Append("排队 ").Append(FormatDuration(run.QueueDurationMs));
            }
            if (hasTiming)
                builder.AppendLine();

            if (!string.IsNullOrWhiteSpace(run.Model)
                || !string.IsNullOrWhiteSpace(run.ReasoningEffort)
                || !string.IsNullOrWhiteSpace(run.AgentName))
            {
                builder.Append("配置：agent ")
                    .Append(string.IsNullOrWhiteSpace(run.AgentName) ? "none" : run.AgentName)
                    .Append(" · model ")
                    .Append(string.IsNullOrWhiteSpace(run.Model) ? "unknown" : run.Model)
                    .Append(" · reasoning ")
                    .Append(string.IsNullOrWhiteSpace(run.ReasoningEffort) ? "unknown" : run.ReasoningEffort)
                    .AppendLine();
            }

            builder.Append("用量：tokens ")
                .Append(FormatTokens(run.ConsumedTokens))
                .Append('/')
                .Append(FormatTokens(run.RequestTokenBudget))
                .Append(" · 模型 ")
                .Append(run.ProviderCalls.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" · 工具 ")
                .Append(run.ToolCalls.ToString("N0", CultureInfo.CurrentCulture))
                .AppendLine();
            builder.Append("工具面：")
                .Append(run.AvailableToolCount.ToString("N0", CultureInfo.CurrentCulture))
                .Append('/')
                .Append(run.RegisteredToolCount.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" · 定义 ")
                .Append(run.AvailableToolDefinitionCharacters.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" 字符 · harness ")
                .Append(run.HarnessInstructionCharacters.ToString("N0", CultureInfo.CurrentCulture))
                .AppendLine(" 字符");
            builder.Append("运行中指令：已送达 ")
                .Append(run.DeliveredSteeringCount.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" · 未送达 ")
                .Append(run.UndeliveredSteeringCount.ToString("N0", CultureInfo.CurrentCulture))
                .AppendLine();
            if (run.FailureKind != CopilotToolFailureKind.None)
            {
                builder.Append("失败：")
                    .Append(run.FailureKind);
                if (!string.IsNullOrWhiteSpace(run.FailureCode))
                    builder.Append(" · code=").Append(run.FailureCode);
                builder.Append(" · retry=")
                    .AppendLine(run.RetryEligible ? "yes" : "no");
            }

            builder.Append("结果：");
            if (string.IsNullOrWhiteSpace(run.AnswerText))
            {
                builder.AppendLine(run.State is CopilotToolExecutionState.Pending or CopilotToolExecutionState.Running
                    ? "子代理仍在运行，尚未回传可展示回答。"
                    : "该运行没有保存可展示回答。");
                return;
            }

            builder.AppendLine()
                .AppendLine(run.AnswerText)
                .Append("结果证明：")
                .Append(run.AnswerHasSuccessfulEvidence ? "已取得成功工具证据" : "未取得成功工具证据")
                .Append(" · 输出截断：")
                .AppendLine(run.AnswerWasTruncated ? "是" : "否");
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
