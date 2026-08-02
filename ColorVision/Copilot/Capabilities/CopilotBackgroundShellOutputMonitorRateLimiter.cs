using System;

namespace ColorVision.Copilot
{
    internal sealed class CopilotBackgroundShellOutputMonitorRateLimiter
    {
        internal const int Capacity = 10;
        internal static readonly TimeSpan RefillInterval =
            TimeSpan.FromSeconds(2);
        internal static readonly TimeSpan MaximumContinuousSuppression =
            TimeSpan.FromSeconds(30);

        private double _availableTokens = Capacity;
        private DateTimeOffset _lastRefillUtc;
        private DateTimeOffset? _suppressionStartedAtUtc;
        private int _pendingSuppressedEvents;

        public CopilotBackgroundShellOutputMonitorRateLimiter(
            DateTimeOffset startedAtUtc)
        {
            _lastRefillUtc = startedAtUtc;
        }

        public int TotalSuppressedEvents { get; private set; }

        public bool TryAcquire(
            DateTimeOffset now,
            out int suppressedEvents,
            out bool overloaded)
        {
            Refill(now);
            suppressedEvents = 0;
            overloaded = false;
            if (_availableTokens >= Capacity)
                _suppressionStartedAtUtc = null;
            if (_suppressionStartedAtUtc.HasValue
                && now - _suppressionStartedAtUtc.Value
                    >= MaximumContinuousSuppression)
            {
                overloaded = true;
                return false;
            }
            if (_availableTokens >= 1)
            {
                _availableTokens -= 1;
                suppressedEvents = _pendingSuppressedEvents;
                _pendingSuppressedEvents = 0;
                return true;
            }

            TotalSuppressedEvents++;
            _pendingSuppressedEvents++;
            _suppressionStartedAtUtc ??= now;
            return false;
        }

        private void Refill(DateTimeOffset now)
        {
            if (now <= _lastRefillUtc)
                return;
            var elapsed = now - _lastRefillUtc;
            _availableTokens = Math.Min(
                Capacity,
                _availableTokens
                + elapsed.TotalMilliseconds
                    / RefillInterval.TotalMilliseconds);
            _lastRefillUtc = now;
        }
    }
}
