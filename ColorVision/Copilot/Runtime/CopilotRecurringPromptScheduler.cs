using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ColorVision.Copilot
{
    internal enum CopilotLoopCommandAction
    {
        Usage,
        List,
        Create,
        Cancel,
        Invalid,
    }

    internal sealed record CopilotLoopCommandRequest(
        CopilotLoopCommandAction Action,
        TimeSpan Interval,
        string Prompt,
        string JobId,
        bool IntervalWasClamped,
        string ErrorMessage);

    internal static partial class CopilotLoopCommand
    {
        internal const int MaximumPromptLength = 32_000;
        internal static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(1);
        internal static readonly TimeSpan MaximumInterval = TimeSpan.FromDays(7);
        internal const string Usage = "用法：/loop <间隔> <请求> | /loop list | /loop cancel <任务 ID>"
            + "\n间隔支持 60s、30m、2h、1d、every hour、每 30 分钟；最短 60 秒。"
            + "\n循环任务立即首发、7 天后自动过期，仅在当前应用会话内有效。";

        [GeneratedRegex(
            @"^(?:every\s+|每\s*)?(?<value>\d+)?\s*(?<unit>seconds?|secs?|s|minutes?|mins?|m|hours?|hrs?|h|days?|d|秒|分钟|分|小时|天)\s+(?<prompt>.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex LeadingIntervalRegex();

        [GeneratedRegex(
            @"^(?<prompt>.+?)\s+(?:every\s+|每\s*)(?<value>\d+)?\s*(?<unit>seconds?|secs?|s|minutes?|mins?|m|hours?|hrs?|h|days?|d|秒|分钟|分|小时|天)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex TrailingIntervalRegex();

        public static CopilotLoopCommandRequest Parse(string? arguments)
        {
            var normalized = NormalizeWhitespace(arguments);
            if (normalized.Length == 0)
                return CreateResult(CopilotLoopCommandAction.Usage);
            if (string.Equals(normalized, "list", StringComparison.OrdinalIgnoreCase))
                return CreateResult(CopilotLoopCommandAction.List);

            if (normalized.StartsWith("cancel", StringComparison.OrdinalIgnoreCase))
            {
                var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2
                    && string.Equals(parts[0], "cancel", StringComparison.OrdinalIgnoreCase)
                    && IsJobId(parts[1]))
                {
                    return new CopilotLoopCommandRequest(
                        CopilotLoopCommandAction.Cancel,
                        TimeSpan.Zero,
                        string.Empty,
                        parts[1].ToLowerInvariant(),
                        IntervalWasClamped: false,
                        ErrorMessage: string.Empty);
                }

                return Invalid("取消循环任务需要一个有效任务 ID，例如 /loop cancel loop:1a2b3c4d。");
            }

            var match = LeadingIntervalRegex().Match(normalized);
            if (!match.Success)
                match = TrailingIntervalRegex().Match(normalized);
            if (!match.Success)
                return Invalid("没有识别出运行间隔。请使用 60s、30m、2h、1d 或 every hour 等格式。");

            var prompt = NormalizeWhitespace(match.Groups["prompt"].Value);
            if (prompt.Length == 0)
                return Invalid("循环任务缺少要执行的请求。");
            if (prompt.Length > MaximumPromptLength)
                return Invalid($"循环请求超过 {MaximumPromptLength:N0} 个字符，请缩短后重试。");
            if (!TryParseInterval(
                    match.Groups["value"].Value,
                    match.Groups["unit"].Value,
                    out var interval,
                    out var intervalError))
            {
                return Invalid(intervalError);
            }

            var clamped = interval < MinimumInterval;
            if (clamped)
                interval = MinimumInterval;
            if (interval > MaximumInterval)
                return Invalid("循环间隔不能超过 7 天；任务本身会在创建 7 天后自动过期。");

            return new CopilotLoopCommandRequest(
                CopilotLoopCommandAction.Create,
                interval,
                prompt,
                string.Empty,
                clamped,
                string.Empty);
        }

        public static string FormatInterval(TimeSpan interval)
        {
            var totalSeconds = Math.Max(0, (long)interval.TotalSeconds);
            if (totalSeconds % 86_400 == 0)
                return $"{totalSeconds / 86_400:N0} 天";
            if (totalSeconds % 3_600 == 0)
                return $"{totalSeconds / 3_600:N0} 小时";
            if (totalSeconds % 60 == 0)
                return $"{totalSeconds / 60:N0} 分钟";
            return $"{totalSeconds:N0} 秒";
        }

        private static bool TryParseInterval(
            string valueText,
            string unitText,
            out TimeSpan interval,
            out string errorMessage)
        {
            var value = 1L;
            if (!string.IsNullOrWhiteSpace(valueText)
                && (!long.TryParse(valueText, NumberStyles.None, CultureInfo.InvariantCulture, out value)
                    || value <= 0))
            {
                interval = TimeSpan.Zero;
                errorMessage = "循环间隔必须是大于 0 的整数。";
                return false;
            }

            long unitSeconds;
            switch (unitText.Trim().ToLowerInvariant())
            {
                case "s":
                case "sec":
                case "secs":
                case "second":
                case "seconds":
                case "秒":
                    unitSeconds = 1;
                    break;
                case "m":
                case "min":
                case "mins":
                case "minute":
                case "minutes":
                case "分":
                case "分钟":
                    unitSeconds = 60;
                    break;
                case "h":
                case "hr":
                case "hrs":
                case "hour":
                case "hours":
                case "小时":
                    unitSeconds = 3_600;
                    break;
                case "d":
                case "day":
                case "days":
                case "天":
                    unitSeconds = 86_400;
                    break;
                default:
                    interval = TimeSpan.Zero;
                    errorMessage = "循环间隔单位只支持秒、分钟、小时或天。";
                    return false;
            }

            if (value > (long)MaximumInterval.TotalSeconds / unitSeconds)
            {
                interval = MaximumInterval + TimeSpan.FromSeconds(1);
                errorMessage = string.Empty;
                return true;
            }

            interval = TimeSpan.FromSeconds(value * unitSeconds);
            errorMessage = string.Empty;
            return true;
        }

        private static bool IsJobId(string? value)
        {
            return value?.Length == 13
                && value.StartsWith("loop:", StringComparison.OrdinalIgnoreCase)
                && value[5..].All(Uri.IsHexDigit);
        }

        private static string NormalizeWhitespace(string? value)
        {
            return string.Join(
                " ",
                (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        private static CopilotLoopCommandRequest Invalid(string message)
        {
            return new CopilotLoopCommandRequest(
                CopilotLoopCommandAction.Invalid,
                TimeSpan.Zero,
                string.Empty,
                string.Empty,
                IntervalWasClamped: false,
                message);
        }

        private static CopilotLoopCommandRequest CreateResult(CopilotLoopCommandAction action)
        {
            return new CopilotLoopCommandRequest(
                action,
                TimeSpan.Zero,
                string.Empty,
                string.Empty,
                IntervalWasClamped: false,
                ErrorMessage: string.Empty);
        }
    }

    internal sealed record CopilotRecurringPromptJobSnapshot(
        string Id,
        string ConversationId,
        string ConversationTitle,
        string ProfileId,
        string WorkspacePath,
        string Prompt,
        TimeSpan Interval,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset ExpiresAtUtc,
        DateTimeOffset NextRunAtUtc,
        int FireCount,
        string LastStatus);

    internal sealed record CopilotRecurringPromptDispatch(
        CopilotRecurringPromptJobSnapshot Job);

    internal sealed class CopilotRecurringPromptScheduler
    {
        internal const int MaximumJobs = 16;
        internal static readonly TimeSpan JobLifetime = TimeSpan.FromDays(7);
        internal static readonly TimeSpan DeferredRetryDelay = TimeSpan.FromSeconds(5);

        private readonly object _gate = new();
        private readonly List<JobState> _jobs = new();

        public bool HasJobs
        {
            get
            {
                lock (_gate)
                    return _jobs.Count > 0;
            }
        }

        public bool TryCreate(
            string conversationId,
            string conversationTitle,
            string profileId,
            string workspacePath,
            string prompt,
            TimeSpan interval,
            DateTimeOffset now,
            out CopilotRecurringPromptJobSnapshot? job,
            out string errorMessage)
        {
            lock (_gate)
            {
                var normalizedConversationId = (conversationId ?? string.Empty).Trim();
                var normalizedProfileId = (profileId ?? string.Empty).Trim();
                var normalizedPrompt = (prompt ?? string.Empty).Trim();
                if (normalizedConversationId.Length == 0
                    || normalizedProfileId.Length == 0
                    || normalizedPrompt.Length == 0
                    || normalizedPrompt.Length > CopilotLoopCommand.MaximumPromptLength
                    || interval < CopilotLoopCommand.MinimumInterval
                    || interval > CopilotLoopCommand.MaximumInterval)
                {
                    job = null;
                    errorMessage = "循环任务参数无效；请重新输入 /loop 查看支持的间隔和请求格式。";
                    return false;
                }

                RemoveExpiredNoLock(now);
                if (_jobs.Count >= MaximumJobs)
                {
                    job = null;
                    errorMessage = $"循环任务已达到上限 {MaximumJobs:N0}；请先取消一个任务。";
                    return false;
                }

                var state = new JobState
                {
                    Id = CreateJobIdNoLock(),
                    ConversationId = normalizedConversationId,
                    ConversationTitle = NormalizeLabel(conversationTitle, CopilotUiText.NewConversationTitle, 120),
                    ProfileId = normalizedProfileId,
                    WorkspacePath = (workspacePath ?? string.Empty).Trim(),
                    Prompt = normalizedPrompt,
                    Interval = interval,
                    CreatedAtUtc = now,
                    ExpiresAtUtc = now.Add(JobLifetime),
                    NextRunAtUtc = now,
                };
                _jobs.Add(state);
                job = Snapshot(state);
                errorMessage = string.Empty;
                return true;
            }
        }

        public IReadOnlyList<CopilotRecurringPromptJobSnapshot> GetJobs(DateTimeOffset now)
        {
            lock (_gate)
            {
                RemoveExpiredNoLock(now);
                return _jobs
                    .OrderBy(job => job.NextRunAtUtc)
                    .ThenBy(job => job.CreatedAtUtc)
                    .Select(Snapshot)
                    .ToArray();
            }
        }

        public bool TryClaimDue(
            DateTimeOffset now,
            out CopilotRecurringPromptDispatch? dispatch)
        {
            lock (_gate)
            {
                RemoveExpiredNoLock(now);
                var state = _jobs
                    .Where(job => !job.IsDispatching && job.NextRunAtUtc <= now)
                    .OrderBy(job => job.NextRunAtUtc)
                    .ThenBy(job => job.CreatedAtUtc)
                    .FirstOrDefault();
                if (state == null)
                {
                    dispatch = null;
                    return false;
                }

                state.IsDispatching = true;
                dispatch = new CopilotRecurringPromptDispatch(Snapshot(state));
                return true;
            }
        }

        public bool CompleteDispatch(
            string jobId,
            bool scheduled,
            string status,
            DateTimeOffset now,
            bool terminal = false)
        {
            lock (_gate)
            {
                var state = FindNoLock(jobId);
                if (state == null)
                    return false;
                if (terminal)
                {
                    _jobs.Remove(state);
                    return true;
                }

                state.IsDispatching = false;
                state.LastStatus = NormalizeLabel(status, scheduled ? "已排入 Agent 宿主" : "等待重试", 160);
                if (scheduled)
                {
                    state.FireCount = state.FireCount == int.MaxValue ? int.MaxValue : state.FireCount + 1;
                    state.NextRunAtUtc = now.Add(state.Interval);
                }
                else
                {
                    var retryAt = now.Add(DeferredRetryDelay);
                    state.NextRunAtUtc = retryAt < state.ExpiresAtUtc ? retryAt : state.ExpiresAtUtc;
                }
                return true;
            }
        }

        public bool Cancel(string? jobId, out CopilotRecurringPromptJobSnapshot? cancelled)
        {
            lock (_gate)
            {
                var state = FindNoLock(jobId);
                if (state == null)
                {
                    cancelled = null;
                    return false;
                }

                cancelled = Snapshot(state);
                _jobs.Remove(state);
                return true;
            }
        }

        public int CancelConversation(string? conversationId)
        {
            lock (_gate)
            {
                return _jobs.RemoveAll(job =>
                    string.Equals(job.ConversationId, conversationId, StringComparison.Ordinal));
            }
        }

        public void Clear()
        {
            lock (_gate)
                _jobs.Clear();
        }

        private JobState? FindNoLock(string? jobId)
        {
            return _jobs.FirstOrDefault(job =>
                string.Equals(job.Id, jobId, StringComparison.OrdinalIgnoreCase));
        }

        private string CreateJobIdNoLock()
        {
            while (true)
            {
                var id = "loop:" + Guid.NewGuid().ToString("N")[..8];
                if (_jobs.All(job => !string.Equals(job.Id, id, StringComparison.OrdinalIgnoreCase)))
                    return id;
            }
        }

        private void RemoveExpiredNoLock(DateTimeOffset now)
        {
            _jobs.RemoveAll(job => job.ExpiresAtUtc <= now);
        }

        private static CopilotRecurringPromptJobSnapshot Snapshot(JobState state)
        {
            return new CopilotRecurringPromptJobSnapshot(
                state.Id,
                state.ConversationId,
                state.ConversationTitle,
                state.ProfileId,
                state.WorkspacePath,
                state.Prompt,
                state.Interval,
                state.CreatedAtUtc,
                state.ExpiresAtUtc,
                state.NextRunAtUtc,
                state.FireCount,
                state.LastStatus);
        }

        private static string NormalizeLabel(string? value, string fallback, int maximumLength)
        {
            var normalized = string.Join(
                " ",
                (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (normalized.Length == 0)
                return fallback;
            if (normalized.Length <= maximumLength)
                return normalized;

            var retainedLength = maximumLength;
            if (char.IsHighSurrogate(normalized[retainedLength - 1])
                && char.IsLowSurrogate(normalized[retainedLength]))
            {
                retainedLength--;
            }
            return normalized[..retainedLength].TrimEnd() + "...";
        }

        private sealed class JobState
        {
            public string Id { get; init; } = string.Empty;
            public string ConversationId { get; init; } = string.Empty;
            public string ConversationTitle { get; init; } = string.Empty;
            public string ProfileId { get; init; } = string.Empty;
            public string WorkspacePath { get; init; } = string.Empty;
            public string Prompt { get; init; } = string.Empty;
            public TimeSpan Interval { get; init; }
            public DateTimeOffset CreatedAtUtc { get; init; }
            public DateTimeOffset ExpiresAtUtc { get; init; }
            public DateTimeOffset NextRunAtUtc { get; set; }
            public int FireCount { get; set; }
            public string LastStatus { get; set; } = string.Empty;
            public bool IsDispatching { get; set; }
        }
    }

    internal static class CopilotRecurringPromptDiagnostics
    {
        public static string Format(
            IReadOnlyList<CopilotRecurringPromptJobSnapshot>? jobs,
            DateTimeOffset now)
        {
            var items = jobs ?? Array.Empty<CopilotRecurringPromptJobSnapshot>();
            if (items.Count == 0)
            {
                return "Copilot 循环任务"
                    + Environment.NewLine
                    + "当前应用会话没有活动循环任务。"
                    + Environment.NewLine
                    + CopilotLoopCommand.Usage;
            }

            var builder = new StringBuilder()
                .Append("Copilot 循环任务 · ")
                .AppendLine(items.Count.ToString("N0", CultureInfo.CurrentCulture))
                .AppendLine("仅当前应用会话有效；每次触发仍遵循现有工具审批策略。");
            foreach (var job in items)
            {
                builder
                    .Append(job.Id)
                    .Append(" · ")
                    .Append(job.ConversationTitle)
                    .Append(" · 每 ")
                    .Append(CopilotLoopCommand.FormatInterval(job.Interval))
                    .Append(" · 下次 ")
                    .Append(FormatRelative(job.NextRunAtUtc, now))
                    .Append(" · 已触发 ")
                    .Append(job.FireCount.ToString("N0", CultureInfo.CurrentCulture))
                    .AppendLine(" 次");
                builder
                    .Append("  ")
                    .AppendLine(Preview(job.Prompt, 160));
                if (!string.IsNullOrWhiteSpace(job.LastStatus))
                    builder.Append("  最近：").AppendLine(job.LastStatus);
            }
            builder.Append("取消：/loop cancel <任务 ID>；任务创建 7 天后自动过期。");
            return builder.ToString();
        }

        private static string FormatRelative(DateTimeOffset value, DateTimeOffset now)
        {
            var remaining = value - now;
            if (remaining <= TimeSpan.Zero)
                return "等待调度";
            if (remaining < TimeSpan.FromMinutes(1))
                return $"{Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds)):N0} 秒后";
            if (remaining < TimeSpan.FromHours(1))
                return $"{Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes)):N0} 分钟后";
            if (remaining < TimeSpan.FromDays(1))
                return $"{Math.Max(1, (int)Math.Ceiling(remaining.TotalHours)):N0} 小时后";
            return $"{Math.Max(1, (int)Math.Ceiling(remaining.TotalDays)):N0} 天后";
        }

        private static string Preview(string? value, int maximumLength)
        {
            var normalized = string.Join(
                " ",
                (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return normalized.Length <= maximumLength
                ? normalized
                : normalized[..(maximumLength - 3)].TrimEnd() + "...";
        }
    }
}
