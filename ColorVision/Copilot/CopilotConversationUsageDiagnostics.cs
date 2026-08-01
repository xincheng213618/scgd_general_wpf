using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal sealed record CopilotConversationAgentUsageSnapshot(
        int Runs,
        int ActiveRuns,
        long ProviderCalls,
        long ToolCalls,
        int DelegatedRuns,
        int EstimatedUsageRuns,
        long ProviderRetries,
        long ProviderRateLimitRetries,
        long ProviderStallTerminations,
        long ContextRecoveries,
        long ProviderResponses,
        long ProviderFirstResponseLatencyTotalMs,
        long ProviderFirstResponseLatencyMaxMs,
        long ProviderCallDurationTotalMs,
        long ElapsedMs);

    internal sealed record CopilotConversationUsageSnapshot(
        CopilotTokenUsage TotalUsage,
        CopilotTokenUsage LastUsage,
        int TrackedResponses,
        int InterruptedResponses,
        int UnreportedResponses,
        int ActiveResponses,
        CopilotConversationAgentUsageSnapshot AgentUsage);

    internal static class CopilotConversationUsageDiagnostics
    {
        public static CopilotConversationUsageSnapshot Capture(CopilotConversationRecord? conversation)
        {
            if (conversation == null)
            {
                return new CopilotConversationUsageSnapshot(
                    CopilotTokenUsage.Empty,
                    CopilotTokenUsage.Empty,
                    0,
                    0,
                    0,
                    0,
                    EmptyAgentUsage);
            }

            var assistantMessages = conversation.Messages
                .Where(message => message != null && !message.IsUser)
                .ToArray();
            var activeResponses = assistantMessages.Count(message =>
                message.IsResponsePending
                || message.IsThinkingInProgress
                || message.IsExecutionInProgress);
            var completedResponses = assistantMessages
                .Where(message =>
                    !message.IsResponsePending
                    && !message.IsThinkingInProgress
                    && !message.IsExecutionInProgress)
                .ToArray();
            var trackedResponses = completedResponses
                .Where(message => message.ReportedUsage.HasAny)
                .ToArray();
            var interruptedResponses = completedResponses.Count(message => message.WasResponseInterrupted);
            var totalUsage = trackedResponses.Aggregate(
                CopilotTokenUsage.Empty,
                (total, message) => total.Add(message.ReportedUsage));
            var lastUsage = trackedResponses.LastOrDefault()?.ReportedUsage ?? CopilotTokenUsage.Empty;
            return new CopilotConversationUsageSnapshot(
                totalUsage,
                lastUsage,
                trackedResponses.Length,
                interruptedResponses,
                completedResponses.Length - trackedResponses.Length,
                activeResponses,
                CaptureAgentUsage(assistantMessages));
        }

        public static string Format(
            CopilotConversationRecord? conversation,
            CopilotProviderRateLimitSnapshot? providerRateLimits = null)
        {
            if (conversation == null)
            {
                return new StringBuilder("使用量")
                    .AppendLine()
                    .AppendLine("当前没有可统计的 Copilot 会话。")
                    .AppendLine(CopilotProviderRateLimitDiagnostics.Format(providerRateLimits))
                    .Append("范围：供应商限额仅为当前模型 Profile 最近一次可识别的响应头快照，可能随时间过期，不代表账户套餐余额。")
                    .ToString();
            }

            var snapshot = Capture(conversation);
            var title = string.IsNullOrWhiteSpace(conversation.Title)
                ? CopilotUiText.NewConversationTitle
                : conversation.Title.Trim();
            var builder = new StringBuilder()
                .Append("使用量 · ")
                .AppendLine(title);
            if (!snapshot.TotalUsage.HasAny)
            {
                builder.AppendLine("尚未收到可累计的 Provider Token 元数据。");
            }
            else
            {
                builder
                    .Append("已记录轮次：")
                    .Append(snapshot.TrackedResponses.ToString("N0", CultureInfo.CurrentCulture))
                    .AppendLine()
                    .Append("累计输入：")
                    .Append(FormatTokens(snapshot.TotalUsage.InputTokens))
                    .Append(" · 输出：")
                    .Append(FormatTokens(snapshot.TotalUsage.OutputTokens))
                    .Append(" · 总计：")
                    .AppendLine(FormatTokens(snapshot.TotalUsage.EffectiveTotalTokens));
                if (snapshot.TotalUsage.CachedInputTokens.HasValue)
                {
                    builder
                        .Append("累计缓存输入：")
                        .Append(FormatTokens(snapshot.TotalUsage.EffectiveCachedInputTokens))
                        .Append('（')
                        .Append(snapshot.TotalUsage.CachedInputPercentage.ToString("0.#", CultureInfo.CurrentCulture))
                        .AppendLine("%）");
                }
                builder
                    .Append("最近一轮：输入 ")
                    .Append(FormatTokens(snapshot.LastUsage.InputTokens))
                    .Append(" · 输出 ")
                    .Append(FormatTokens(snapshot.LastUsage.OutputTokens))
                    .Append(" · 总计 ")
                    .AppendLine(FormatTokens(snapshot.LastUsage.EffectiveTotalTokens));
            }

            if (snapshot.InterruptedResponses > 0)
            {
                builder
                    .Append("标记中断：")
                    .Append(snapshot.InterruptedResponses.ToString("N0", CultureInfo.CurrentCulture))
                    .AppendLine(" 条；若 Provider 返回了 Token 元数据，仍按实际用量计入。");
            }
            if (snapshot.UnreportedResponses > 0)
            {
                builder
                    .Append("未纳入：")
                    .Append(snapshot.UnreportedResponses.ToString("N0", CultureInfo.CurrentCulture))
                    .AppendLine(" 条旧回答、失败回答或未返回 Token 元数据的回答。");
            }
            if (snapshot.ActiveResponses > 0)
            {
                builder
                    .Append("进行中：")
                    .Append(snapshot.ActiveResponses.ToString("N0", CultureInfo.CurrentCulture))
                    .AppendLine(" 条回答将在结束并收到 Token 元数据后计入。");
            }
            AppendAgentUsage(builder, snapshot.AgentUsage);
            builder.AppendLine(CopilotProviderRateLimitDiagnostics.Format(providerRateLimits));

            builder.Append("范围：Token 仅统计当前会话消息中由 Provider 返回并由应用保存的元数据；Agent 指标来自本地保存的任务快照。这些会话统计不代表账户账单或费用。供应商限额仅为当前模型 Profile 最近一次可识别的响应头快照，可能随时间过期，不代表账户套餐余额。");
            return builder.ToString();
        }

        private static CopilotConversationAgentUsageSnapshot EmptyAgentUsage => new(
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0);

        private static CopilotConversationAgentUsageSnapshot CaptureAgentUsage(
            IReadOnlyList<CopilotChatMessage> assistantMessages)
        {
            var runs = 0;
            var activeRuns = 0;
            var providerCalls = 0L;
            var toolCalls = 0L;
            var estimatedUsageRuns = 0;
            var providerRetries = 0L;
            var providerRateLimitRetries = 0L;
            var providerStallTerminations = 0L;
            var contextRecoveries = 0L;
            var providerResponses = 0L;
            var providerFirstResponseLatencyTotalMs = 0L;
            var providerFirstResponseLatencyMaxMs = 0L;
            var providerCallDurationTotalMs = 0L;
            var elapsedMs = 0L;
            var delegatedRunIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var message in assistantMessages.Where(message => message?.HasAgentRunMetrics == true))
            {
                runs++;
                if (message.IsResponsePending
                    || message.IsThinkingInProgress
                    || message.IsExecutionInProgress)
                {
                    activeRuns++;
                }

                var budget = message.AgentRunBudget;
                var delegatedProviderCalls = 0L;
                var delegatedToolCalls = 0L;
                foreach (var trace in message.AgentTraceEntries ?? [])
                {
                    if (trace == null)
                        continue;
                    delegatedProviderCalls = AddClamped(delegatedProviderCalls, trace.DelegatedProviderCalls);
                    delegatedToolCalls = AddClamped(delegatedToolCalls, trace.DelegatedToolCalls);
                    if (!string.IsNullOrWhiteSpace(trace.DelegatedRunId))
                        delegatedRunIds.Add(trace.DelegatedRunId);
                }

                providerCalls = AddClamped(
                    providerCalls,
                    Math.Max(Math.Max(0, budget.ProviderCalls), delegatedProviderCalls));
                toolCalls = AddClamped(
                    toolCalls,
                    AddClamped(Math.Max(0, budget.ToolCalls), delegatedToolCalls));
                if (budget.UsedEstimatedUsage)
                    estimatedUsageRuns++;
                providerRetries = AddClamped(providerRetries, budget.ProviderRetryCount);
                providerRateLimitRetries = AddClamped(
                    providerRateLimitRetries,
                    budget.ProviderRateLimitRetryCount);
                providerStallTerminations = AddClamped(
                    providerStallTerminations,
                    AddClamped(
                        Math.Max(0, budget.ProviderFirstContentTimeoutCount),
                        Math.Max(0, budget.ProviderStreamInactivityTimeoutCount)));
                contextRecoveries = AddClamped(contextRecoveries, budget.ContextRecoveryCount);
                providerResponses = AddClamped(providerResponses, budget.ProviderResponseCount);
                providerFirstResponseLatencyTotalMs = AddClamped(
                    providerFirstResponseLatencyTotalMs,
                    budget.ProviderFirstResponseLatencyTotalMs);
                providerFirstResponseLatencyMaxMs = Math.Max(
                    providerFirstResponseLatencyMaxMs,
                    Math.Max(0, budget.ProviderFirstResponseLatencyMaxMs));
                providerCallDurationTotalMs = AddClamped(
                    providerCallDurationTotalMs,
                    budget.ProviderCallDurationTotalMs);
                elapsedMs = AddClamped(elapsedMs, budget.ElapsedMs);
            }

            return new CopilotConversationAgentUsageSnapshot(
                runs,
                activeRuns,
                providerCalls,
                toolCalls,
                delegatedRunIds.Count,
                estimatedUsageRuns,
                providerRetries,
                providerRateLimitRetries,
                providerStallTerminations,
                contextRecoveries,
                providerResponses,
                providerFirstResponseLatencyTotalMs,
                providerFirstResponseLatencyMaxMs,
                providerCallDurationTotalMs,
                elapsedMs);
        }

        private static void AppendAgentUsage(
            StringBuilder builder,
            CopilotConversationAgentUsageSnapshot usage)
        {
            if (usage.Runs <= 0)
                return;

            builder.Append("Agent 运行：")
                .Append(FormatCount(usage.Runs))
                .Append(" 轮");
            if (usage.ActiveRuns > 0)
            {
                builder.Append("（进行中 ")
                    .Append(FormatCount(usage.ActiveRuns))
                    .Append('）');
            }
            builder.Append(" · 模型调用 ")
                .Append(FormatCount(usage.ProviderCalls))
                .Append(" · 工具调用 ")
                .Append(FormatCount(usage.ToolCalls));
            if (usage.DelegatedRuns > 0)
            {
                builder.Append(" · 委派 ")
                    .Append(FormatCount(usage.DelegatedRuns));
            }
            builder.AppendLine();

            if (usage.ElapsedMs > 0
                || usage.ProviderResponses > 0
                || usage.ProviderCallDurationTotalMs > 0)
            {
                builder.Append("时延：");
                var hasValue = false;
                if (usage.ElapsedMs > 0)
                {
                    builder.Append("Agent 累计 ")
                        .Append(FormatDuration(usage.ElapsedMs));
                    hasValue = true;
                }
                if (usage.ProviderResponses > 0)
                {
                    if (hasValue)
                        builder.Append(" · ");
                    builder.Append("首响应平均 ")
                        .Append(FormatDuration(
                            usage.ProviderFirstResponseLatencyTotalMs
                            / usage.ProviderResponses))
                        .Append(" · 最慢 ")
                        .Append(FormatDuration(usage.ProviderFirstResponseLatencyMaxMs));
                    hasValue = true;
                }
                if (usage.ProviderCallDurationTotalMs > 0)
                {
                    if (hasValue)
                        builder.Append(" · ");
                    builder.Append("模型调用累计 ")
                        .Append(FormatDuration(usage.ProviderCallDurationTotalMs));
                }
                builder.AppendLine();
            }

            if (usage.ProviderRetries > 0
                || usage.ProviderStallTerminations > 0
                || usage.ContextRecoveries > 0
                || usage.EstimatedUsageRuns > 0)
            {
                builder.Append("恢复与估算：");
                var hasValue = false;
                if (usage.ProviderRetries > 0)
                {
                    builder.Append("Provider 重试 ")
                        .Append(FormatCount(usage.ProviderRetries));
                    if (usage.ProviderRateLimitRetries > 0)
                    {
                        builder.Append("（限流 ")
                            .Append(FormatCount(usage.ProviderRateLimitRetries))
                            .Append('）');
                    }
                    hasValue = true;
                }
                if (usage.ProviderStallTerminations > 0)
                {
                    if (hasValue)
                        builder.Append(" · ");
                    builder.Append("停顿中止 ")
                        .Append(FormatCount(usage.ProviderStallTerminations));
                    hasValue = true;
                }
                if (usage.ContextRecoveries > 0)
                {
                    if (hasValue)
                        builder.Append(" · ");
                    builder.Append("窗口恢复 ")
                        .Append(FormatCount(usage.ContextRecoveries));
                    hasValue = true;
                }
                if (usage.EstimatedUsageRuns > 0)
                {
                    if (hasValue)
                        builder.Append(" · ");
                    builder.Append("预算计数含估算 ")
                        .Append(FormatCount(usage.EstimatedUsageRuns))
                        .Append(" 轮");
                }
                builder.AppendLine();
            }
        }

        private static long AddClamped(long total, long value)
        {
            var normalizedValue = Math.Max(0, value);
            return long.MaxValue - total < normalizedValue
                ? long.MaxValue
                : total + normalizedValue;
        }

        private static string FormatCount(long value)
        {
            return Math.Max(0, value).ToString("N0", CultureInfo.CurrentCulture);
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
            if (duration.TotalHours < 1)
                return $"{(int)duration.TotalMinutes:N0}m {duration.Seconds:N0}s";
            return $"{(int)duration.TotalHours:N0}h {duration.Minutes:N0}m";
        }

        private static string FormatTokens(int value)
        {
            return Math.Max(0, value).ToString("N0", CultureInfo.CurrentCulture);
        }
    }
}
