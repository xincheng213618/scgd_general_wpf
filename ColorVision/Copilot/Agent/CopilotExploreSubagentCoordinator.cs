#pragma warning disable CA1001
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed class CopilotExploreSubagentCoordinator
    {
        public const int MaximumConcurrentRuns = 2;
        public const int MaximumRunTokenBudget = 16_384;
        public const int MaximumTotalTokenBudget = MaximumConcurrentRuns * MaximumRunTokenBudget;

        private readonly object _syncRoot = new();
        private readonly SemaphoreSlim _slots = new(MaximumConcurrentRuns, MaximumConcurrentRuns);
        private readonly int _totalTokenBudget;
        private readonly int _perRunTokenBudget;
        private long _committedTokens;
        private int _reservedTokens;

        public CopilotExploreSubagentCoordinator(CopilotAgentRequest parentRequest)
        {
            ArgumentNullException.ThrowIfNull(parentRequest);
            var parentTokenBudget = CopilotAgentRunBudget.Resolve(parentRequest).RequestTokenBudget;
            _totalTokenBudget = Math.Max(
                CopilotAgentRunBudget.MinimumRequestTokenBudget,
                Math.Min(MaximumTotalTokenBudget, parentTokenBudget / 2));
            _perRunTokenBudget = _totalTokenBudget >= CopilotAgentRunBudget.MinimumRequestTokenBudget * MaximumConcurrentRuns
                ? Math.Min(MaximumRunTokenBudget, _totalTokenBudget / MaximumConcurrentRuns)
                : CopilotAgentRunBudget.MinimumRequestTokenBudget;
        }

        public async Task<CopilotExploreSubagentLease?> TryAcquireAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            await _slots.WaitAsync(cancellationToken);

            int tokenBudget;
            lock (_syncRoot)
            {
                var available = _totalTokenBudget - _committedTokens - _reservedTokens;
                if (available < CopilotAgentRunBudget.MinimumRequestTokenBudget)
                {
                    _slots.Release();
                    return null;
                }

                tokenBudget = (int)Math.Min(_perRunTokenBudget, available);
                _reservedTokens += tokenBudget;
            }

            stopwatch.Stop();
            return new CopilotExploreSubagentLease(
                this,
                "explore-" + Guid.NewGuid().ToString("N")[..12],
                tokenBudget,
                stopwatch.ElapsedMilliseconds);
        }

        private void Release(CopilotExploreSubagentLease lease, long? consumedTokens)
        {
            lock (_syncRoot)
            {
                _reservedTokens = Math.Max(0, _reservedTokens - lease.RequestTokenBudget);
                if (consumedTokens.HasValue)
                    _committedTokens += Math.Max(0, consumedTokens.Value);
            }
            _slots.Release();
        }

        internal sealed class CopilotExploreSubagentLease : IDisposable
        {
            private CopilotExploreSubagentCoordinator? _owner;
            private long? _consumedTokens;

            public CopilotExploreSubagentLease(
                CopilotExploreSubagentCoordinator owner,
                string runId,
                int requestTokenBudget,
                long queueDurationMs)
            {
                _owner = owner;
                RunId = runId;
                RequestTokenBudget = requestTokenBudget;
                QueueDurationMs = Math.Max(0, queueDurationMs);
            }

            public string RunId { get; }

            public int RequestTokenBudget { get; }

            public long QueueDurationMs { get; }

            public void Commit(long consumedTokens)
            {
                _consumedTokens = Math.Max(0, consumedTokens);
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _owner, null)?.Release(this, _consumedTokens);
            }
        }
    }
}
