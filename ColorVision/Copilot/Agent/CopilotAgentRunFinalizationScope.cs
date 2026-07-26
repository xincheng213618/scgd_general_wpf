using System;
using System.Threading;

namespace ColorVision.Copilot
{
    internal sealed class CopilotAgentRunFinalizationScope : IDisposable
    {
        internal static readonly TimeSpan DefaultInterruptedTimeout = TimeSpan.FromSeconds(5);

        private readonly CancellationTokenSource? _timeoutCancellation;

        private CopilotAgentRunFinalizationScope(
            CancellationTokenSource? timeoutCancellation,
            CancellationToken token)
        {
            Token = token;
            _timeoutCancellation = timeoutCancellation;
        }

        public CancellationToken Token { get; }

        public bool IsTimeoutCancellationRequested =>
            _timeoutCancellation?.IsCancellationRequested == true;

        public static CopilotAgentRunFinalizationScope Create(
            CopilotAgentControlIntent controlIntent,
            bool timeBudgetExhausted,
            CancellationToken runCancellationToken,
            TimeSpan? interruptedTimeout = null)
        {
            if (controlIntent == CopilotAgentControlIntent.None && !timeBudgetExhausted)
                return new CopilotAgentRunFinalizationScope(null, runCancellationToken);

            var timeout = interruptedTimeout ?? DefaultInterruptedTimeout;
            if (timeout <= TimeSpan.Zero || timeout == Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(interruptedTimeout), "Interrupted finalization timeout must be finite and positive.");

            var timeoutCancellation = new CancellationTokenSource(timeout);
            return new CopilotAgentRunFinalizationScope(timeoutCancellation, timeoutCancellation.Token);
        }

        public void Dispose() => _timeoutCancellation?.Dispose();
    }
}
