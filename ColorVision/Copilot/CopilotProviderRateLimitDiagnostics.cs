using System;
using System.Globalization;
using System.Text;

namespace ColorVision.Copilot
{
    internal static class CopilotProviderRateLimitDiagnostics
    {
        private const int MaximumDisplayValueLength = 128;

        public static string Format(CopilotProviderRateLimitSnapshot? snapshot)
        {
            snapshot ??= CopilotProviderRateLimitSnapshot.Empty;
            if (snapshot.CapturedAtUtc == default)
                return "供应商限额：尚未收到可识别的限额响应头";

            var builder = new StringBuilder("供应商限额：");
            var hasValue = false;
            AppendBucket(
                builder,
                "请求",
                snapshot.RequestRemaining,
                snapshot.RequestLimit,
                snapshot.RequestReset,
                ref hasValue);
            AppendBucket(
                builder,
                "Token",
                snapshot.TokenRemaining,
                snapshot.TokenLimit,
                snapshot.TokenReset,
                ref hasValue);
            AppendBucket(
                builder,
                "项目 Token",
                snapshot.ProjectTokenRemaining,
                snapshot.ProjectTokenLimit,
                snapshot.ProjectTokenReset,
                ref hasValue);
            AppendDetail(builder, "Retry-After ", snapshot.RetryAfter, ref hasValue);
            AppendDetail(
                builder,
                "请求 ",
                CopilotProviderRequestId.Normalize(snapshot.RequestId),
                ref hasValue);
            if (!hasValue)
                builder.Append("已捕获响应头");
            return builder.Append(" · 快照 ")
                .Append(snapshot.CapturedAtUtc
                    .ToUniversalTime()
                    .ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture))
                .ToString();
        }

        private static void AppendBucket(
            StringBuilder builder,
            string label,
            long? remaining,
            long? limit,
            string? reset,
            ref bool hasValue)
        {
            if (!remaining.HasValue
                && !limit.HasValue
                && string.IsNullOrWhiteSpace(reset))
            {
                return;
            }

            AppendSeparator(builder, hasValue);
            builder.Append(label);
            if (remaining.HasValue || limit.HasValue)
            {
                builder.Append("：剩余 ")
                    .Append(remaining.HasValue ? FormatCount(remaining.Value) : "unknown")
                    .Append('/')
                    .Append(limit.HasValue ? FormatCount(limit.Value) : "unknown");
            }
            var normalizedReset = NormalizeDisplayValue(reset);
            if (normalizedReset.Length > 0)
                builder.Append("（重置 ").Append(normalizedReset).Append('）');
            hasValue = true;
        }

        private static void AppendDetail(
            StringBuilder builder,
            string label,
            string? value,
            ref bool hasValue)
        {
            var normalized = NormalizeDisplayValue(value);
            if (normalized.Length == 0)
                return;
            AppendSeparator(builder, hasValue);
            builder.Append(label).Append(normalized);
            hasValue = true;
        }

        private static void AppendSeparator(StringBuilder builder, bool hasValue)
        {
            if (hasValue)
                builder.Append(" · ");
        }

        private static string NormalizeDisplayValue(string? value)
        {
            var normalized = string.Join(
                " ",
                (value ?? string.Empty).Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            if (normalized.Length <= MaximumDisplayValueLength)
                return normalized;

            var retainedLength = MaximumDisplayValueLength - 1;
            if (char.IsHighSurrogate(normalized[retainedLength - 1])
                && char.IsLowSurrogate(normalized[retainedLength]))
            {
                retainedLength--;
            }
            return normalized[..retainedLength].TrimEnd() + "…";
        }

        private static string FormatCount(long value)
        {
            return Math.Max(0, value).ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}
