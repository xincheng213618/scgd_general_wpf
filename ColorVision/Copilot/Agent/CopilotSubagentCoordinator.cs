#pragma warning disable CA1001
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal static class CopilotSubagentCoordination
    {
        private static readonly ConditionalWeakTable<CopilotAgentRequest, CopilotSubagentCoordinator> Coordinators = new();

        public static CopilotSubagentCoordinator GetCoordinator(CopilotAgentRequest parentRequest)
        {
            ArgumentNullException.ThrowIfNull(parentRequest);
            return Coordinators.GetValue(parentRequest, static request => new CopilotSubagentCoordinator(request));
        }
    }

    internal sealed class CopilotSubagentCoordinator
    {
        public const int MaximumConcurrentRuns = 2;
        public const int MaximumRunTokenBudget = 16_384;
        public const int MaximumTotalTokenBudget = MaximumConcurrentRuns * MaximumRunTokenBudget;
        public const int MaximumTrackedCompletedRuns = 8;

        private readonly object _syncRoot = new();
        private readonly SemaphoreSlim _slots = new(MaximumConcurrentRuns, MaximumConcurrentRuns);
        private readonly HashSet<string> _activeRunIds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, CompletedSubagentRun> _completedRuns = new(StringComparer.Ordinal);
        private readonly Queue<string> _completedRunOrder = new();
        private readonly int _totalTokenBudget;
        private readonly int _perRunTokenBudget;
        private long _committedTokens;
        private int _reservedTokens;

        public CopilotSubagentCoordinator(CopilotAgentRequest parentRequest)
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

        public async Task<CopilotSubagentLease?> TryAcquireAsync(string roleId, CancellationToken cancellationToken)
        {
            var normalizedRoleId = NormalizeRoleId(roleId);
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
            var runId = normalizedRoleId + "-" + Guid.NewGuid().ToString("N")[..12];
            lock (_syncRoot)
                _activeRunIds.Add(runId);
            return new CopilotSubagentLease(
                this,
                runId,
                tokenBudget,
                stopwatch.ElapsedMilliseconds);
        }

        public bool TryResolveCompletedRun(
            string roleId,
            string runId,
            out CopilotAgentSessionCheckpoint? checkpoint,
            out CopilotToolFailureKind failureKind,
            out string errorMessage)
        {
            var normalizedRoleId = NormalizeRoleId(roleId);
            var normalizedRunId = (runId ?? string.Empty).Trim();
            checkpoint = null;
            failureKind = CopilotToolFailureKind.Validation;
            errorMessage = string.Empty;
            lock (_syncRoot)
            {
                if (_activeRunIds.Contains(normalizedRunId))
                {
                    failureKind = CopilotToolFailureKind.Conflict;
                    errorMessage = $"Subagent run '{normalizedRunId}' is still active and cannot be resumed.";
                    return false;
                }
                if (!_completedRuns.TryGetValue(normalizedRunId, out var completed))
                {
                    errorMessage = $"Subagent run '{normalizedRunId}' is not a completed run from this parent request.";
                    return false;
                }
                if (!string.Equals(completed.RoleId, normalizedRoleId, StringComparison.Ordinal))
                {
                    errorMessage = $"Subagent run '{normalizedRunId}' belongs to role '{completed.RoleId}', not '{normalizedRoleId}'.";
                    return false;
                }
                if (completed.Checkpoint?.IsStructurallyValid() != true)
                {
                    errorMessage = $"Subagent run '{normalizedRunId}' did not produce a structurally valid resumable checkpoint.";
                    return false;
                }

                checkpoint = completed.Checkpoint;
                failureKind = CopilotToolFailureKind.None;
                return true;
            }
        }

        public void RecordCompleted(
            string roleId,
            string runId,
            CopilotAgentSessionCheckpoint? checkpoint)
        {
            var normalizedRoleId = NormalizeRoleId(roleId);
            var normalizedRunId = (runId ?? string.Empty).Trim();
            lock (_syncRoot)
            {
                if (!_activeRunIds.Contains(normalizedRunId)
                    || _completedRuns.ContainsKey(normalizedRunId))
                {
                    return;
                }

                _completedRuns.Add(
                    normalizedRunId,
                    new CompletedSubagentRun(normalizedRoleId, checkpoint));
                _completedRunOrder.Enqueue(normalizedRunId);
                while (_completedRunOrder.Count > MaximumTrackedCompletedRuns)
                {
                    var expiredRunId = _completedRunOrder.Dequeue();
                    _completedRuns.Remove(expiredRunId);
                }
            }
        }

        private static string NormalizeRoleId(string roleId)
        {
            var normalized = (roleId ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized.Length == 0 || normalized.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
                throw new ArgumentException("Subagent role id must contain only ASCII letters, digits, or hyphens.", nameof(roleId));
            return normalized;
        }

        private void Release(CopilotSubagentLease lease, long? consumedTokens)
        {
            lock (_syncRoot)
            {
                _activeRunIds.Remove(lease.RunId);
                _reservedTokens = Math.Max(0, _reservedTokens - lease.RequestTokenBudget);
                if (consumedTokens.HasValue)
                    _committedTokens += Math.Max(0, consumedTokens.Value);
            }
            _slots.Release();
        }

        private sealed record CompletedSubagentRun(
            string RoleId,
            CopilotAgentSessionCheckpoint? Checkpoint);

        internal sealed class CopilotSubagentLease : IDisposable
        {
            private CopilotSubagentCoordinator? _owner;
            private long? _consumedTokens;

            public CopilotSubagentLease(
                CopilotSubagentCoordinator owner,
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
