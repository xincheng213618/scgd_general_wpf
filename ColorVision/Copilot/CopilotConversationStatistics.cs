using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal enum CopilotConversationStatisticsWindow
    {
        SevenDays,
        ThirtyDays,
        All,
    }

    internal sealed record CopilotConversationDailyStatistics(
        DateOnly Date,
        int ActiveConversations,
        int UserTurns,
        int TerminalResponses,
        int InterruptedResponses,
        int TrackedResponses,
        int UnreportedResponses,
        int ActiveResponses,
        CopilotTokenUsage Usage);

    internal sealed record CopilotConversationStatisticsSnapshot(
        CopilotConversationStatisticsWindow Window,
        DateOnly StartDate,
        DateOnly EndDate,
        int StoredConversations,
        int ActiveConversations,
        int UserTurns,
        int TerminalResponses,
        int InterruptedResponses,
        int TrackedResponses,
        int UnreportedResponses,
        int ActiveResponses,
        int ActiveDays,
        int CurrentStreakDays,
        int LongestStreakDays,
        CopilotTokenUsage Usage,
        IReadOnlyList<CopilotConversationDailyStatistics> DailyActivity);

    internal static class CopilotConversationStatistics
    {
        public static string Format(
            IEnumerable<CopilotConversationRecord>? conversations,
            DateTimeOffset now,
            string? arguments)
        {
            if (!TryParseWindow(arguments, out var window))
                return "/stats 参数无效。可用 /stats、/stats 7、/stats 30 或 /stats all。";

            return Format(Capture(conversations, now, window));
        }

        public static CopilotConversationStatisticsSnapshot Capture(
            IEnumerable<CopilotConversationRecord>? conversations,
            DateTimeOffset now,
            CopilotConversationStatisticsWindow window)
        {
            var source = (conversations ?? Array.Empty<CopilotConversationRecord>())
                .Where(conversation => conversation != null)
                .ToArray();
            var today = DateOnly.FromDateTime(now.DateTime);
            var requestedStartDate = window switch
            {
                CopilotConversationStatisticsWindow.SevenDays => today.AddDays(-6),
                CopilotConversationStatisticsWindow.ThirtyDays => today.AddDays(-29),
                _ => (DateOnly?)null,
            };
            var daily = new SortedDictionary<DateOnly, DailyAccumulator>();
            var allUserActivityDates = new HashSet<DateOnly>();
            var activeConversationIndices = new HashSet<int>();

            for (var conversationIndex = 0; conversationIndex < source.Length; conversationIndex++)
            {
                var conversation = source[conversationIndex];
                var messages = conversation.Messages ?? [];
                var ownedStartIndex = ResolveOwnedMessageStartIndex(source, conversation, messages);
                for (var messageIndex = ownedStartIndex; messageIndex < messages.Count; messageIndex++)
                {
                    var message = messages[messageIndex];
                    var date = ResolveLocalDate(message?.CreatedAt ?? default);
                    if (message == null || !date.HasValue || date.Value > today)
                        continue;

                    if (message.IsUser)
                        allUserActivityDates.Add(date.Value);
                    if (requestedStartDate.HasValue && date.Value < requestedStartDate.Value)
                        continue;

                    if (!daily.TryGetValue(date.Value, out var accumulator))
                    {
                        accumulator = new DailyAccumulator(date.Value);
                        daily.Add(date.Value, accumulator);
                    }

                    activeConversationIndices.Add(conversationIndex);
                    accumulator.Add(conversationIndex, message);
                }
            }

            var dailyActivity = daily.Values
                .Select(accumulator => accumulator.ToSnapshot())
                .ToArray();
            var startDate = requestedStartDate
                ?? dailyActivity.FirstOrDefault()?.Date
                ?? today;
            var usage = dailyActivity.Aggregate(
                CopilotTokenUsage.Empty,
                (total, day) => total.Add(day.Usage));
            var streaks = CalculateStreaks(allUserActivityDates, today);
            return new CopilotConversationStatisticsSnapshot(
                window,
                startDate,
                today,
                source.Length,
                activeConversationIndices.Count,
                SaturatingSum(dailyActivity.Select(day => day.UserTurns)),
                SaturatingSum(dailyActivity.Select(day => day.TerminalResponses)),
                SaturatingSum(dailyActivity.Select(day => day.InterruptedResponses)),
                SaturatingSum(dailyActivity.Select(day => day.TrackedResponses)),
                SaturatingSum(dailyActivity.Select(day => day.UnreportedResponses)),
                SaturatingSum(dailyActivity.Select(day => day.ActiveResponses)),
                dailyActivity.Count(day => day.UserTurns > 0),
                streaks.Current,
                streaks.Longest,
                usage,
                dailyActivity);
        }

        public static string Format(CopilotConversationStatisticsSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var builder = new StringBuilder()
                .AppendLine("/stats · 本地会话统计")
                .Append("范围：")
                .Append(FormatWindow(snapshot))
                .Append(" · ")
                .Append(snapshot.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Append(" 至 ")
                .AppendLine(snapshot.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Append("会话：保存 ")
                .Append(FormatCount(snapshot.StoredConversations))
                .Append(" 个 · 本窗口活跃 ")
                .Append(FormatCount(snapshot.ActiveConversations))
                .AppendLine(" 个")
                .Append("交互：提问 ")
                .Append(FormatCount(snapshot.UserTurns))
                .Append(" · 已结束回答 ")
                .Append(FormatCount(snapshot.TerminalResponses))
                .Append(" · 标记中断 ")
                .Append(FormatCount(snapshot.InterruptedResponses))
                .Append(" · 进行中 ")
                .Append(FormatCount(snapshot.ActiveResponses))
                .AppendLine()
                .Append("Provider Token：已记录轮次 ")
                .Append(FormatCount(snapshot.TrackedResponses))
                .Append('/')
                .Append(FormatCount(snapshot.TerminalResponses))
                .Append(" · 输入 ")
                .Append(FormatTokens(snapshot.Usage.InputTokens))
                .Append(" · 输出 ")
                .Append(FormatTokens(snapshot.Usage.OutputTokens))
                .Append(" · 总计 ")
                .AppendLine(FormatTokens(snapshot.Usage.EffectiveTotalTokens));
            if (snapshot.Usage.CachedInputTokens.HasValue)
            {
                builder.Append("缓存输入：")
                    .Append(FormatTokens(snapshot.Usage.EffectiveCachedInputTokens))
                    .Append('（')
                    .Append(snapshot.Usage.CachedInputPercentage.ToString("0.#", CultureInfo.CurrentCulture))
                    .AppendLine("%）");
            }
            if (snapshot.UnreportedResponses > 0)
            {
                builder.Append("未纳入：")
                    .Append(FormatCount(snapshot.UnreportedResponses))
                    .AppendLine(" 条旧回答、失败回答或未返回 Token 元数据的回答。");
            }

            builder.Append("活跃：本窗口 ")
                .Append(FormatCount(snapshot.ActiveDays))
                .Append(" 天 · 全历史当前连续 ")
                .Append(FormatCount(snapshot.CurrentStreakDays))
                .Append(" 天 · 最长连续 ")
                .Append(FormatCount(snapshot.LongestStreakDays))
                .AppendLine(" 天");
            AppendDailyActivity(builder, snapshot);
            builder.Append("边界：只汇总本机已保存消息中由 Provider 返回的 Token；会话分支复制的历史前缀不会重复计数。"
                + "统计不代表账户账单、信用额度、套餐余额、费用、速率限制或未返回用量的失败调用。");
            return builder.ToString().TrimEnd();
        }

        private static bool TryParseWindow(
            string? arguments,
            out CopilotConversationStatisticsWindow window)
        {
            var normalized = (arguments ?? string.Empty).Trim();
            if (normalized.Length == 0 || string.Equals(normalized, "7", StringComparison.OrdinalIgnoreCase))
            {
                window = CopilotConversationStatisticsWindow.SevenDays;
                return true;
            }
            if (string.Equals(normalized, "30", StringComparison.OrdinalIgnoreCase))
            {
                window = CopilotConversationStatisticsWindow.ThirtyDays;
                return true;
            }
            if (string.Equals(normalized, "all", StringComparison.OrdinalIgnoreCase))
            {
                window = CopilotConversationStatisticsWindow.All;
                return true;
            }

            window = default;
            return false;
        }

        private static int ResolveOwnedMessageStartIndex(
            IReadOnlyList<CopilotConversationRecord> conversations,
            CopilotConversationRecord conversation,
            IReadOnlyList<CopilotChatMessage> messages)
        {
            var origin = conversation.BranchOrigin;
            if (origin?.IsStructurallyValid(conversation.Id) != true)
                return 0;

            var parent = conversations.FirstOrDefault(candidate => string.Equals(
                candidate.Id,
                origin.ParentConversationId,
                StringComparison.Ordinal));
            if (parent != null)
            {
                var throughIndex = parent.Messages
                    .Select((message, index) => (message, index))
                    .FirstOrDefault(item => string.Equals(
                        item.message?.Id,
                        origin.ThroughMessageId,
                        StringComparison.Ordinal))
                    .index;
                if (throughIndex >= 0
                    && throughIndex < parent.Messages.Count
                    && string.Equals(parent.Messages[throughIndex]?.Id, origin.ThroughMessageId, StringComparison.Ordinal))
                {
                    return Math.Min(messages.Count, throughIndex + 1);
                }
            }

            var forkedAtLocal = origin.ForkedAtUtc.ToLocalTime().DateTime;
            var inheritedCount = 0;
            while (inheritedCount < messages.Count)
            {
                var createdAt = messages[inheritedCount]?.CreatedAt ?? default;
                if (createdAt == default || createdAt > forkedAtLocal)
                    break;
                inheritedCount++;
            }
            return inheritedCount;
        }

        private static DateOnly? ResolveLocalDate(DateTime timestamp)
        {
            if (timestamp == default)
                return null;

            var local = timestamp.Kind == DateTimeKind.Utc
                ? timestamp.ToLocalTime()
                : timestamp;
            return DateOnly.FromDateTime(local);
        }

        private static (int Current, int Longest) CalculateStreaks(
            IReadOnlySet<DateOnly> activityDates,
            DateOnly today)
        {
            var current = 0;
            while (activityDates.Contains(today.AddDays(-current)))
                current++;

            var longest = 0;
            var run = 0;
            DateOnly? previous = null;
            foreach (var date in activityDates.Where(date => date <= today).OrderBy(date => date))
            {
                run = previous.HasValue && date.DayNumber == previous.Value.DayNumber + 1
                    ? run + 1
                    : 1;
                longest = Math.Max(longest, run);
                previous = date;
            }
            return (current, longest);
        }

        private static void AppendDailyActivity(
            StringBuilder builder,
            CopilotConversationStatisticsSnapshot snapshot)
        {
            var byDate = snapshot.DailyActivity.ToDictionary(day => day.Date);
            CopilotConversationDailyStatistics[] visibleDays;
            string heading;
            if (snapshot.Window == CopilotConversationStatisticsWindow.All)
            {
                visibleDays = snapshot.DailyActivity.TakeLast(7).ToArray();
                heading = "最近活跃日";
            }
            else
            {
                var visibleStart = snapshot.EndDate.AddDays(-6);
                visibleDays = Enumerable.Range(0, 7)
                    .Select(offset =>
                    {
                        var date = visibleStart.AddDays(offset);
                        return byDate.TryGetValue(date, out var day)
                            ? day
                            : new CopilotConversationDailyStatistics(
                                date,
                                0,
                                0,
                                0,
                                0,
                                0,
                                0,
                                0,
                                CopilotTokenUsage.Empty);
                    })
                    .ToArray();
                heading = "最近 7 日";
            }

            if (visibleDays.Length == 0)
            {
                builder.AppendLine("活动明细：本窗口尚无已保存消息。");
                return;
            }

            builder.Append(heading).AppendLine("（本机时间）：");
            foreach (var day in visibleDays)
            {
                builder.Append("  ")
                    .Append(day.Date.ToString("MM-dd", CultureInfo.InvariantCulture))
                    .Append(" · 会话 ")
                    .Append(FormatCount(day.ActiveConversations))
                    .Append(" · 提问 ")
                    .Append(FormatCount(day.UserTurns))
                    .Append(" · 回答 ")
                    .Append(FormatCount(day.TerminalResponses))
                    .Append(" · 中断 ")
                    .Append(FormatCount(day.InterruptedResponses))
                    .Append(" · Token ")
                    .AppendLine(FormatTokens(day.Usage.EffectiveTotalTokens));
            }
        }

        private static string FormatWindow(CopilotConversationStatisticsSnapshot snapshot)
        {
            return snapshot.Window switch
            {
                CopilotConversationStatisticsWindow.SevenDays => "最近 7 天",
                CopilotConversationStatisticsWindow.ThirtyDays => "最近 30 天",
                _ => "全部本地历史",
            };
        }

        private static int SaturatingSum(IEnumerable<int> values)
        {
            var total = values.Aggregate(0L, (sum, value) => Math.Min(int.MaxValue, sum + Math.Max(0, value)));
            return (int)total;
        }

        private static string FormatCount(int value) =>
            Math.Max(0, value).ToString("N0", CultureInfo.CurrentCulture);

        private static string FormatTokens(int value) =>
            Math.Max(0, value).ToString("N0", CultureInfo.CurrentCulture);

        private sealed class DailyAccumulator
        {
            private readonly HashSet<int> _conversationIndices = new();

            public DailyAccumulator(DateOnly date)
            {
                Date = date;
            }

            public DateOnly Date { get; }

            public int UserTurns { get; private set; }

            public int TerminalResponses { get; private set; }

            public int InterruptedResponses { get; private set; }

            public int TrackedResponses { get; private set; }

            public int ActiveResponses { get; private set; }

            public CopilotTokenUsage Usage { get; private set; } = CopilotTokenUsage.Empty;

            public void Add(int conversationIndex, CopilotChatMessage message)
            {
                _conversationIndices.Add(conversationIndex);
                if (message.IsUser)
                {
                    UserTurns++;
                    return;
                }

                if (message.IsResponsePending
                    || message.IsThinkingInProgress
                    || message.IsExecutionInProgress)
                {
                    ActiveResponses++;
                    return;
                }

                TerminalResponses++;
                if (message.WasResponseInterrupted)
                    InterruptedResponses++;
                if (!message.ReportedUsage.HasAny)
                    return;

                TrackedResponses++;
                Usage = Usage.Add(message.ReportedUsage);
            }

            public CopilotConversationDailyStatistics ToSnapshot()
            {
                return new CopilotConversationDailyStatistics(
                    Date,
                    _conversationIndices.Count,
                    UserTurns,
                    TerminalResponses,
                    InterruptedResponses,
                    TrackedResponses,
                    Math.Max(0, TerminalResponses - TrackedResponses),
                    ActiveResponses,
                    Usage);
            }
        }
    }
}
