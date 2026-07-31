using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http;
using System.Text;

namespace ColorVision.Copilot
{
    internal sealed class CopilotProviderRateLimitSnapshot
    {
        public static CopilotProviderRateLimitSnapshot Empty { get; } = new();

        public DateTimeOffset CapturedAtUtc { get; init; }

        public long? RequestLimit { get; init; }

        public long? RequestRemaining { get; init; }

        public string RequestReset { get; init; } = string.Empty;

        public long? TokenLimit { get; init; }

        public long? TokenRemaining { get; init; }

        public string TokenReset { get; init; } = string.Empty;

        public long? ProjectTokenLimit { get; init; }

        public long? ProjectTokenRemaining { get; init; }

        public string ProjectTokenReset { get; init; } = string.Empty;

        public string RetryAfter { get; init; } = string.Empty;

        public string RequestId { get; init; } = string.Empty;
    }

    internal static class CopilotProviderRateLimitTracker
    {
        private const int MaximumHeaderValueLength = 128;
        private const int MaximumProfileIdLength = 128;
        private static readonly ConcurrentDictionary<string, CopilotProviderRateLimitSnapshot> Snapshots =
            new(StringComparer.Ordinal);
        private static readonly string[] RequestLimitHeaders =
        {
            "x-ratelimit-limit-requests",
            "anthropic-ratelimit-requests-limit",
        };
        private static readonly string[] RequestRemainingHeaders =
        {
            "x-ratelimit-remaining-requests",
            "anthropic-ratelimit-requests-remaining",
        };
        private static readonly string[] RequestResetHeaders =
        {
            "x-ratelimit-reset-requests",
            "anthropic-ratelimit-requests-reset",
        };
        private static readonly string[] TokenLimitHeaders =
        {
            "x-ratelimit-limit-tokens",
            "anthropic-ratelimit-tokens-limit",
        };
        private static readonly string[] TokenRemainingHeaders =
        {
            "x-ratelimit-remaining-tokens",
            "anthropic-ratelimit-tokens-remaining",
        };
        private static readonly string[] TokenResetHeaders =
        {
            "x-ratelimit-reset-tokens",
            "anthropic-ratelimit-tokens-reset",
        };

        public static void Capture(string? profileId, HttpResponseMessage response)
        {
            ArgumentNullException.ThrowIfNull(response);
            var key = NormalizeProfileId(profileId);
            if (key.Length == 0)
                return;

            var requestLimit = ReadNonNegativeInteger(response, RequestLimitHeaders);
            var requestRemaining = ReadNonNegativeInteger(response, RequestRemainingHeaders);
            var requestReset = ReadHeader(response, RequestResetHeaders);
            var tokenLimit = ReadNonNegativeInteger(response, TokenLimitHeaders);
            var tokenRemaining = ReadNonNegativeInteger(response, TokenRemainingHeaders);
            var tokenReset = ReadHeader(response, TokenResetHeaders);
            var projectTokenLimit = ReadNonNegativeInteger(response, "x-ratelimit-limit-project-tokens");
            var projectTokenRemaining = ReadNonNegativeInteger(response, "x-ratelimit-remaining-project-tokens");
            var projectTokenReset = ReadHeader(response, "x-ratelimit-reset-project-tokens");
            var retryAfter = ReadHeader(response, "Retry-After");
            if (!requestLimit.HasValue
                && !requestRemaining.HasValue
                && requestReset.Length == 0
                && !tokenLimit.HasValue
                && !tokenRemaining.HasValue
                && tokenReset.Length == 0
                && !projectTokenLimit.HasValue
                && !projectTokenRemaining.HasValue
                && projectTokenReset.Length == 0
                && retryAfter.Length == 0)
            {
                return;
            }

            Snapshots[key] = new CopilotProviderRateLimitSnapshot
            {
                CapturedAtUtc = DateTimeOffset.UtcNow,
                RequestLimit = requestLimit,
                RequestRemaining = requestRemaining,
                RequestReset = requestReset,
                TokenLimit = tokenLimit,
                TokenRemaining = tokenRemaining,
                TokenReset = tokenReset,
                ProjectTokenLimit = projectTokenLimit,
                ProjectTokenRemaining = projectTokenRemaining,
                ProjectTokenReset = projectTokenReset,
                RetryAfter = retryAfter,
                RequestId = CopilotProviderRequestId.Extract(response),
            };
        }

        public static CopilotProviderRateLimitSnapshot GetSnapshot(string? profileId)
        {
            var key = NormalizeProfileId(profileId);
            return key.Length > 0 && Snapshots.TryGetValue(key, out var snapshot)
                ? snapshot
                : CopilotProviderRateLimitSnapshot.Empty;
        }

        internal static void Clear(string? profileId)
        {
            var key = NormalizeProfileId(profileId);
            if (key.Length > 0)
                Snapshots.TryRemove(key, out _);
        }

        private static string NormalizeProfileId(string? profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return string.Empty;
            var normalized = profileId.Trim();
            return normalized.Length <= MaximumProfileIdLength
                ? normalized
                : string.Empty;
        }

        private static long? ReadNonNegativeInteger(
            HttpResponseMessage response,
            params string[] headerNames)
        {
            foreach (var headerName in headerNames)
            {
                if (!response.Headers.TryGetValues(headerName, out var values))
                    continue;
                foreach (var value in values)
                {
                    var normalized = NormalizeHeaderValue(value);
                    if (long.TryParse(
                        normalized,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var parsed)
                        && parsed >= 0)
                    {
                        return parsed;
                    }
                }
            }
            return null;
        }

        private static string ReadHeader(
            HttpResponseMessage response,
            params string[] headerNames)
        {
            foreach (var headerName in headerNames)
            {
                if (!response.Headers.TryGetValues(headerName, out var values))
                    continue;
                foreach (var value in values)
                {
                    var normalized = NormalizeHeaderValue(value);
                    if (normalized.Length > 0)
                        return normalized;
                }
            }
            return string.Empty;
        }

        private static string NormalizeHeaderValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var builder = new StringBuilder(Math.Min(value.Length, MaximumHeaderValueLength));
            var pendingWhitespace = false;
            foreach (var character in value.Trim())
            {
                if (char.IsWhiteSpace(character))
                {
                    pendingWhitespace = builder.Length > 0;
                    continue;
                }
                if (char.IsControl(character))
                    continue;
                if (pendingWhitespace && builder.Length < MaximumHeaderValueLength)
                    builder.Append(' ');
                pendingWhitespace = false;
                if (builder.Length < MaximumHeaderValueLength)
                    builder.Append(character);
                if (builder.Length == MaximumHeaderValueLength)
                    break;
            }
            return builder.ToString();
        }
    }
}
