using System;
using System.Globalization;

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
            snapshot ??= CopilotProviderRateLimitSnapshot.Empty;
            if (snapshot.CapturedAtUtc == default)
                return CopilotProviderRateLimitStatusPresentation.Empty;

            var requestUnderPressure = IsUnderPressure(snapshot.RequestRemaining, snapshot.RequestLimit);
            var tokenUnderPressure = IsUnderPressure(snapshot.TokenRemaining, snapshot.TokenLimit);
            var projectTokenUnderPressure = IsUnderPressure(
                snapshot.ProjectTokenRemaining,
                snapshot.ProjectTokenLimit);
            var isThrottled = !string.IsNullOrWhiteSpace(snapshot.RetryAfter);
            var label = isThrottled
                ? "限流重试"
                : requestUnderPressure
                    ? FormatBucket("请求", snapshot.RequestRemaining, snapshot.RequestLimit)
                    : tokenUnderPressure
                        ? FormatBucket("Token", snapshot.TokenRemaining, snapshot.TokenLimit)
                        : projectTokenUnderPressure
                            ? FormatBucket("项目", snapshot.ProjectTokenRemaining, snapshot.ProjectTokenLimit)
                            : HasBucket(snapshot.RequestRemaining, snapshot.RequestLimit, snapshot.RequestReset)
                                ? FormatBucket("请求", snapshot.RequestRemaining, snapshot.RequestLimit)
                                : HasBucket(snapshot.TokenRemaining, snapshot.TokenLimit, snapshot.TokenReset)
                                    ? FormatBucket("Token", snapshot.TokenRemaining, snapshot.TokenLimit)
                                    : HasBucket(
                                        snapshot.ProjectTokenRemaining,
                                        snapshot.ProjectTokenLimit,
                                        snapshot.ProjectTokenReset)
                                        ? FormatBucket(
                                            "项目",
                                            snapshot.ProjectTokenRemaining,
                                            snapshot.ProjectTokenLimit)
                                        : "供应商限额";

            var toolTip = CopilotProviderRateLimitDiagnostics.Format(snapshot)
                + Environment.NewLine
                + Environment.NewLine
                + "这是当前模型 Profile 最近一次可识别的供应商响应头快照，可能已经过期；"
                + "不代表账户套餐余额或可用金额。点击查看 /usage session。";
            return new CopilotProviderRateLimitStatusPresentation(
                true,
                label,
                toolTip,
                isThrottled || requestUnderPressure || tokenUnderPressure || projectTokenUnderPressure);
        }

        private static bool HasBucket(long? remaining, long? limit, string? reset)
        {
            return remaining.HasValue || limit.HasValue || !string.IsNullOrWhiteSpace(reset);
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
    }
}
