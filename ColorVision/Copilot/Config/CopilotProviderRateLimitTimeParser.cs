using System;
using System.Globalization;

namespace ColorVision.Copilot
{
    internal static class CopilotProviderRateLimitTimeParser
    {
        private const long MinimumPlausibleUnixTimestampSeconds = 1_000_000_000;
        private const long MinimumPlausibleUnixTimestampMilliseconds = 1_000_000_000_000;

        public static bool TryResolveRetryAfterDeadline(
            string? value,
            DateTimeOffset capturedAtUtc,
            out DateTimeOffset deadlineUtc)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (normalized.Length == 0 || capturedAtUtc == default)
            {
                deadlineUtc = default;
                return false;
            }

            if (double.TryParse(
                    normalized,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var seconds)
                && double.IsFinite(seconds)
                && seconds >= 0)
            {
                deadlineUtc = AddSecondsSafely(capturedAtUtc, seconds);
                return true;
            }

            return TryParseAbsoluteDeadline(normalized, out deadlineUtc);
        }

        public static bool TryResolveResetDeadline(
            string? value,
            DateTimeOffset capturedAtUtc,
            out DateTimeOffset deadlineUtc)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (normalized.Length == 0 || capturedAtUtc == default)
            {
                deadlineUtc = default;
                return false;
            }

            if (long.TryParse(
                    normalized,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var unixSeconds)
                && unixSeconds >= MinimumPlausibleUnixTimestampSeconds)
            {
                try
                {
                    deadlineUtc = unixSeconds >= MinimumPlausibleUnixTimestampMilliseconds
                        ? DateTimeOffset.FromUnixTimeMilliseconds(unixSeconds)
                        : DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
                    return true;
                }
                catch (ArgumentOutOfRangeException)
                {
                    deadlineUtc = default;
                    return false;
                }
            }
            if (TryParseDuration(normalized, out var duration))
            {
                deadlineUtc = AddDurationSafely(capturedAtUtc, duration);
                return true;
            }
            if (!TryParseAbsoluteDeadline(normalized, out deadlineUtc))
            {
                deadlineUtc = default;
                return false;
            }
            return true;
        }

        private static bool TryParseAbsoluteDeadline(
            string value,
            out DateTimeOffset deadlineUtc)
        {
            if (DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces
                        | DateTimeStyles.AssumeUniversal
                        | DateTimeStyles.AdjustToUniversal,
                    out var parsed))
            {
                deadlineUtc = parsed.ToUniversalTime();
                return true;
            }

            deadlineUtc = default;
            return false;
        }

        private static bool TryParseDuration(string value, out TimeSpan duration)
        {
            if (value.Contains(':')
                && TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out duration)
                && duration >= TimeSpan.Zero)
            {
                return true;
            }

            decimal totalMilliseconds = 0;
            var index = 0;
            var hasComponent = false;
            var isSaturated = false;
            while (index < value.Length)
            {
                while (index < value.Length && char.IsWhiteSpace(value[index]))
                    index++;
                if (index >= value.Length)
                    break;

                var numberStart = index;
                var hasDecimalPoint = false;
                while (index < value.Length)
                {
                    if (char.IsDigit(value[index]))
                    {
                        index++;
                        continue;
                    }
                    if (value[index] == '.' && !hasDecimalPoint)
                    {
                        hasDecimalPoint = true;
                        index++;
                        continue;
                    }
                    break;
                }
                if (numberStart == index
                    || !decimal.TryParse(
                        value[numberStart..index],
                        NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture,
                        out var amount)
                    || amount < 0)
                {
                    duration = default;
                    return false;
                }

                decimal unitMilliseconds;
                if (index + 1 < value.Length
                    && char.ToLowerInvariant(value[index]) == 'm'
                    && char.ToLowerInvariant(value[index + 1]) == 's')
                {
                    unitMilliseconds = 1;
                    index += 2;
                }
                else if (index < value.Length)
                {
                    unitMilliseconds = char.ToLowerInvariant(value[index]) switch
                    {
                        's' => 1_000,
                        'm' => 60_000,
                        'h' => 3_600_000,
                        'd' => 86_400_000,
                        _ => 0,
                    };
                    if (unitMilliseconds == 0)
                    {
                        duration = default;
                        return false;
                    }
                    index++;
                }
                else
                {
                    unitMilliseconds = 1_000;
                }

                var maximumMilliseconds = (decimal)TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerMillisecond;
                if (amount > maximumMilliseconds / unitMilliseconds)
                {
                    isSaturated = true;
                }
                else if (!isSaturated)
                {
                    totalMilliseconds += amount * unitMilliseconds;
                    if (totalMilliseconds >= maximumMilliseconds)
                        isSaturated = true;
                }
                hasComponent = true;
            }

            if (!hasComponent)
            {
                duration = default;
                return false;
            }

            duration = isSaturated
                ? TimeSpan.MaxValue
                : TimeSpan.FromTicks(
                    decimal.ToInt64(decimal.Truncate(
                        totalMilliseconds * TimeSpan.TicksPerMillisecond)));
            return true;
        }

        private static DateTimeOffset AddDurationSafely(
            DateTimeOffset capturedAtUtc,
            TimeSpan duration)
        {
            var normalizedStart = capturedAtUtc.ToUniversalTime();
            if (duration <= TimeSpan.Zero)
                return normalizedStart;
            var maximumDuration = DateTimeOffset.MaxValue - normalizedStart;
            return normalizedStart + (duration >= maximumDuration ? maximumDuration : duration);
        }

        private static DateTimeOffset AddSecondsSafely(
            DateTimeOffset capturedAtUtc,
            double seconds)
        {
            var normalizedStart = capturedAtUtc.ToUniversalTime();
            var maximumDuration = DateTimeOffset.MaxValue - normalizedStart;
            if (seconds >= maximumDuration.TotalSeconds)
                return DateTimeOffset.MaxValue;
            return normalizedStart.AddMilliseconds(seconds * 1000);
        }
    }
}
