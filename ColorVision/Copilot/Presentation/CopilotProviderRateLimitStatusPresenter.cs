using System;
using System.Globalization;
using System.Linq;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotProviderRateLimitStatusPresentation(
        bool IsVisible,
        string Label,
        string ToolTip,
        bool IsUnderPressure)
    {
        public static CopilotProviderRateLimitStatusPresentation Empty { get; } =
            new(false, string.Empty, string.Empty, false);
    }

    internal static class CopilotProviderRateLimitStatusPresenter
    {
        public static CopilotProviderRateLimitStatusPresentation Create(
            CopilotProviderRateLimitSnapshot? snapshot)
        {
            return Create(snapshot, DateTimeOffset.UtcNow);
        }

        internal static CopilotProviderRateLimitStatusPresentation Create(
            CopilotProviderRateLimitSnapshot? snapshot,
            DateTimeOffset nowUtc)
        {
            snapshot ??= CopilotProviderRateLimitSnapshot.Empty;
            if (snapshot.CapturedAtUtc == default)
                return CopilotProviderRateLimitStatusPresentation.Empty;

            nowUtc = nowUtc.ToUniversalTime();
            var request = CreateBucket(
                "请求",
                snapshot.RequestRemaining,
                snapshot.RequestLimit,
                snapshot.RequestReset,
                snapshot.CapturedAtUtc,
                nowUtc);
            var token = CreateBucket(
                "Token",
                snapshot.TokenRemaining,
                snapshot.TokenLimit,
                snapshot.TokenReset,
                snapshot.CapturedAtUtc,
                nowUtc);
            var projectToken = CreateBucket(
                "项目",
                snapshot.ProjectTokenRemaining,
                snapshot.ProjectTokenLimit,
                snapshot.ProjectTokenReset,
                snapshot.CapturedAtUtc,
                nowUtc);
            var buckets = new[] { request, token, projectToken };
            var isThrottled = CopilotProviderRateLimitTimeParser.TryResolveRetryAfterDeadline(
                    snapshot.RetryAfter,
                    snapshot.CapturedAtUtc,
                    out var retryAfterDeadline)
                && retryAfterDeadline > nowUtc;
            var pressuredBucket = Array.Find(buckets, bucket => bucket.IsUnderPressure);
            var currentBucket = Array.Find(buckets, bucket => bucket.HasValue && bucket.IsCurrent);
            var label = isThrottled
                ? "限流重试"
                : pressuredBucket.HasValue
                    ? pressuredBucket.Label
                    : currentBucket.HasValue
                        ? currentBucket.Label
                        : buckets.Any(bucket => bucket.HasValue && !bucket.IsCurrent)
                            ? "限额待刷新"
                            : "供应商限额";

            var toolTip = CopilotProviderRateLimitDiagnostics.Format(snapshot)
                + Environment.NewLine
                + Environment.NewLine
                + "这是当前模型 Profile 最近一次可识别的供应商响应头快照，可能已经过期；"
                + "不代表账户套餐余额或可用金额。点击查看 /usage session。";
            if (!isThrottled && buckets.Any(bucket => bucket.HasValue && !bucket.IsCurrent))
            {
                toolTip += Environment.NewLine
                    + "响应头中的可解析重置时间已经到达；旧的剩余值不再作为当前压力告警，等待下一次供应商响应刷新。";
            }
            return new CopilotProviderRateLimitStatusPresentation(
                true,
                label,
                toolTip,
                isThrottled || pressuredBucket.HasValue);
        }

        private static RateLimitBucket CreateBucket(
            string label,
            long? remaining,
            long? limit,
            string? reset,
            DateTimeOffset capturedAtUtc,
            DateTimeOffset nowUtc)
        {
            var hasValue = remaining.HasValue || limit.HasValue || !string.IsNullOrWhiteSpace(reset);
            var isCurrent = !CopilotProviderRateLimitTimeParser.TryResolveResetDeadline(
                    reset,
                    capturedAtUtc,
                    out var resetDeadline)
                || resetDeadline > nowUtc;
            return new RateLimitBucket(
                hasValue,
                isCurrent,
                isCurrent && IsUnderPressure(remaining, limit),
                FormatBucket(label, remaining, limit));
        }

        private static bool IsUnderPressure(long? remaining, long? limit)
        {
            if (!remaining.HasValue)
                return false;
            if (remaining.Value <= 0)
                return true;
            return limit is > 0 && remaining.Value <= limit.Value / 10m;
        }

        private static string FormatBucket(string label, long? remaining, long? limit)
        {
            if (remaining.HasValue && limit.HasValue)
                return $"{label} {FormatCompactCount(remaining.Value)}/{FormatCompactCount(limit.Value)}";
            if (remaining.HasValue)
                return $"{label} {FormatCompactCount(remaining.Value)}";
            return label + "限额";
        }

        private static string FormatCompactCount(long value)
        {
            value = Math.Max(0, value);
            if (value < 1_000)
                return value.ToString(CultureInfo.InvariantCulture);
            if (value < 1_000_000)
                return FormatScaled(value, 1_000m, "K");
            if (value < 1_000_000_000)
                return FormatScaled(value, 1_000_000m, "M");
            if (value < 1_000_000_000_000)
                return FormatScaled(value, 1_000_000_000m, "B");
            return FormatScaled(value, 1_000_000_000_000m, "T");
        }

        private static string FormatScaled(long value, decimal divisor, string suffix)
        {
            return (value / divisor).ToString("0.#", CultureInfo.InvariantCulture) + suffix;
        }

        private readonly record struct RateLimitBucket(
            bool HasValue,
            bool IsCurrent,
            bool IsUnderPressure,
            string Label);
    }
}
