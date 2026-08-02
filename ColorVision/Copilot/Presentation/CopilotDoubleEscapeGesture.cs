using System;

namespace ColorVision.Copilot
{
    internal sealed class CopilotDoubleEscapeGesture
    {
        internal static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(750);

        private readonly TimeSpan _interval;
        private DateTimeOffset? _firstEscapeAtUtc;

        internal CopilotDoubleEscapeGesture()
            : this(DefaultInterval)
        {
        }

        internal CopilotDoubleEscapeGesture(TimeSpan interval)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);

            _interval = interval;
        }

        internal bool Register(DateTimeOffset occurredAtUtc)
        {
            var firstEscapeAtUtc = _firstEscapeAtUtc;
            if (firstEscapeAtUtc.HasValue
                && occurredAtUtc >= firstEscapeAtUtc.Value
                && occurredAtUtc - firstEscapeAtUtc.Value <= _interval)
            {
                Reset();
                return true;
            }

            _firstEscapeAtUtc = occurredAtUtc;
            return false;
        }

        internal void Reset()
        {
            _firstEscapeAtUtc = null;
        }
    }
}
