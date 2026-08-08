#pragma warning disable CA1001
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal enum CopilotSubagentCancelResult
    {
        Requested,
        AlreadyRequested,
        NotFound,
    }

    internal static class CopilotSubagentCoordination
    {
        private static readonly ConditionalWeakTable<CopilotAgentRequest, CopilotSubagentCoordinator> Coordinators = new();
        private static readonly ConcurrentDictionary<string, CopilotSubagentActiveRun> ActiveRuns =
            new(StringComparer.Ordinal);

        public static CopilotSubagentCoordinator GetCoordinator(CopilotAgentRequest parentRequest)
        {
            ArgumentNullException.ThrowIfNull(parentRequest);
            return Coordinators.GetValue(parentRequest, static request => new CopilotSubagentCoordinator(request));
        }

        public static CopilotSubagentCancelResult RequestCancelActiveRun(
            string? conversationId,
            string? runId)
        {
            var normalizedRunId = (runId ?? string.Empty).Trim();
            if (normalizedRunId.Length == 0
                || !ActiveRuns.TryGetValue(normalizedRunId, out var activeRun)
                || !string.Equals(
                    activeRun.ConversationId,
                    (conversationId ?? string.Empty).Trim(),
                    StringComparison.Ordinal))
            {
                return CopilotSubagentCancelResult.NotFound;
            }

            return activeRun.RequestCancel();
        }

        public static CopilotSteeringAdmissionResult RequestSteerActiveRun(
            string? conversationId,
            string? runId,
            string? message)
        {
            var normalizedRunId = (runId ?? string.Empty).Trim();
            var normalizedMessage = (message ?? string.Empty).Trim();
            if (normalizedRunId.Length is 0 or > CopilotSteeringMessagePolicy.MaximumIdentifierCharacters
                || normalizedMessage.Length is 0 or > CopilotSteeringMessagePolicy.MaximumMessageCharacters)
            {
                return new CopilotSteeringAdmissionResult(
                    CopilotSteeringAdmissionReason.InvalidInput);
            }
            if (!ActiveRuns.TryGetValue(normalizedRunId, out var activeRun)
                || !string.Equals(
                    activeRun.ConversationId,
                    (conversationId ?? string.Empty).Trim(),
                    StringComparison.Ordinal))
            {
                return new CopilotSteeringAdmissionResult(
                    CopilotSteeringAdmissionReason.NoActiveTask);
            }

            return activeRun.RequestSteer(normalizedMessage);
        }

        internal static IDisposable? TryAttachSteeringTarget(
            string? conversationId,
            string? runId,
            Func<string, CopilotSteeringAdmissionResult> steeringTarget)
        {
            ArgumentNullException.ThrowIfNull(steeringTarget);
            var normalizedRunId = (runId ?? string.Empty).Trim();
            if (!ActiveRuns.TryGetValue(normalizedRunId, out var activeRun)
                || !string.Equals(
                    activeRun.ConversationId,
                    (conversationId ?? string.Empty).Trim(),
                    StringComparison.Ordinal))
            {
                return null;
            }

            return activeRun.TryAttachSteeringTarget(steeringTarget);
        }

        internal static CopilotSubagentActiveRun RegisterActiveRun(
            string conversationId,
            string roleId)
        {
            while (true)
            {
                var runId = roleId + "-" + Guid.NewGuid().ToString("N")[..12];
                var activeRun = new CopilotSubagentActiveRun(
                    (conversationId ?? string.Empty).Trim(),
                    runId);
                if (ActiveRuns.TryAdd(runId, activeRun))
                    return activeRun;

                activeRun.DisposeUnregistered();
            }
        }

        internal static void UnregisterActiveRun(CopilotSubagentActiveRun activeRun)
        {
            if (ActiveRuns.TryGetValue(activeRun.RunId, out var registered)
                && ReferenceEquals(registered, activeRun))
            {
                ActiveRuns.TryRemove(activeRun.RunId, out _);
            }
        }
    }

    internal sealed class CopilotSubagentActiveRun : IDisposable
    {
        private readonly CopilotNonBlockingCancellationSource _cancellation = new();
        private readonly object _syncRoot = new();
        private Func<string, CopilotSteeringAdmissionResult>? _steeringTarget;
        private int _cancelRequested;
        private int _disposed;

        public CopilotSubagentActiveRun(string conversationId, string runId)
        {
            ConversationId = conversationId;
            RunId = runId;
        }

        public string ConversationId { get; }

        public string RunId { get; }

        public CancellationToken Token => _cancellation.Token;

        public bool WasCancellationRequested => Volatile.Read(ref _cancelRequested) != 0;

        public CopilotSubagentCancelResult RequestCancel()
        {
            lock (_syncRoot)
            {
                if (_disposed != 0)
                    return CopilotSubagentCancelResult.NotFound;
                if (_cancelRequested != 0)
                    return CopilotSubagentCancelResult.AlreadyRequested;

                _cancelRequested = 1;
                _cancellation.RequestCancellation();
                return CopilotSubagentCancelResult.Requested;
            }
        }

        public CopilotSteeringAdmissionResult RequestSteer(string message)
        {
            Func<string, CopilotSteeringAdmissionResult>? steeringTarget;
            lock (_syncRoot)
            {
                if (_disposed != 0 || _cancelRequested != 0)
                {
                    return new CopilotSteeringAdmissionResult(
                        CopilotSteeringAdmissionReason.NoActiveTask);
                }
                steeringTarget = _steeringTarget;
            }
            if (steeringTarget == null)
            {
                return new CopilotSteeringAdmissionResult(
                    CopilotSteeringAdmissionReason.RuntimeUnavailable);
            }

            try
            {
                return steeringTarget(message);
            }
            catch (ObjectDisposedException)
            {
                return new CopilotSteeringAdmissionResult(
                    CopilotSteeringAdmissionReason.RuntimeUnavailable);
            }
            catch (InvalidOperationException)
            {
                return new CopilotSteeringAdmissionResult(
                    CopilotSteeringAdmissionReason.RuntimeUnavailable);
            }
        }

        internal IDisposable? TryAttachSteeringTarget(
            Func<string, CopilotSteeringAdmissionResult> steeringTarget)
        {
            lock (_syncRoot)
            {
                if (_disposed != 0 || _cancelRequested != 0)
                    return null;
                _steeringTarget = steeringTarget;
                return new SteeringTargetRegistration(this, steeringTarget);
            }
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_disposed != 0)
                    return;
                _disposed = 1;
                _steeringTarget = null;
            }

            CopilotSubagentCoordination.UnregisterActiveRun(this);
            _cancellation.Dispose();
        }

        internal void DisposeUnregistered()
        {
            lock (_syncRoot)
            {
                if (_disposed != 0)
                    return;
                _disposed = 1;
                _steeringTarget = null;
            }

            _cancellation.Dispose();
        }

        private void DetachSteeringTarget(
            Func<string, CopilotSteeringAdmissionResult> steeringTarget)
        {
            lock (_syncRoot)
            {
                if (ReferenceEquals(_steeringTarget, steeringTarget))
                    _steeringTarget = null;
            }
        }

        private sealed class SteeringTargetRegistration(
            CopilotSubagentActiveRun owner,
            Func<string, CopilotSteeringAdmissionResult> steeringTarget) : IDisposable
        {
            private CopilotSubagentActiveRun? _owner = owner;

            public void Dispose()
            {
                Interlocked.Exchange(ref _owner, null)?.DetachSteeringTarget(steeringTarget);
            }
        }
    }

    internal sealed class CopilotSubagentCoordinator
    {
        public const int DefaultMaximumConcurrentRuns = 2;
        public const int MaximumRunTokenBudget = 16_384;
        public const int MaximumTotalTokenBudget = DefaultMaximumConcurrentRuns * MaximumRunTokenBudget;
        public const int MaximumTrackedCompletedRuns = 8;

        private readonly object _syncRoot = new();
        private readonly SemaphoreSlim _slots;
        private readonly HashSet<string> _activeRunIds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, CompletedSubagentRun> _completedRuns = new(StringComparer.Ordinal);
        private readonly Queue<string> _completedRunOrder = new();
        private readonly int _totalTokenBudget;
        private readonly int _perRunTokenBudget;
        private readonly int _maximumConcurrentRuns;
        private readonly string _conversationId;
        private long _committedTokens;
        private int _reservedTokens;

        public CopilotSubagentCoordinator(CopilotAgentRequest parentRequest)
        {
            ArgumentNullException.ThrowIfNull(parentRequest);
            _conversationId = (parentRequest.ConversationId ?? string.Empty).Trim();
            _maximumConcurrentRuns = Math.Max(1, parentRequest.CodexMaximumConcurrentSubagentRuns);
            _slots = new SemaphoreSlim(_maximumConcurrentRuns, _maximumConcurrentRuns);
            var parentTokenBudget = CopilotAgentRunBudget.Resolve(parentRequest).RequestTokenBudget;
            _totalTokenBudget = Math.Max(
                CopilotAgentRunBudget.MinimumRequestTokenBudget,
                Math.Min(MaximumTotalTokenBudget, parentTokenBudget / 2));
            _perRunTokenBudget = _totalTokenBudget >= (long)CopilotAgentRunBudget.MinimumRequestTokenBudget * _maximumConcurrentRuns
                ? Math.Min(MaximumRunTokenBudget, _totalTokenBudget / _maximumConcurrentRuns)
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
            var activeRun = CopilotSubagentCoordination.RegisterActiveRun(
                _conversationId,
                normalizedRoleId);
            lock (_syncRoot)
                _activeRunIds.Add(activeRun.RunId);
            return new CopilotSubagentLease(
                this,
                activeRun,
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
                {
                    var normalizedCommittedTokens = Math.Clamp(
                        _committedTokens,
                        0L,
                        (long)_totalTokenBudget);
                    var normalizedConsumedTokens = Math.Max(0L, consumedTokens.Value);
                    _committedTokens = normalizedConsumedTokens >= _totalTokenBudget - normalizedCommittedTokens
                        ? _totalTokenBudget
                        : normalizedCommittedTokens + normalizedConsumedTokens;
                }
            }
            _slots.Release();
        }

        private sealed record CompletedSubagentRun(
            string RoleId,
            CopilotAgentSessionCheckpoint? Checkpoint);

        internal sealed class CopilotSubagentLease : IDisposable
        {
            private CopilotSubagentCoordinator? _owner;
            private readonly CopilotSubagentActiveRun _activeRun;
            private long? _consumedTokens;

            public CopilotSubagentLease(
                CopilotSubagentCoordinator owner,
                CopilotSubagentActiveRun activeRun,
                int requestTokenBudget,
                long queueDurationMs)
            {
                _owner = owner;
                _activeRun = activeRun;
                RunId = activeRun.RunId;
                RequestTokenBudget = requestTokenBudget;
                QueueDurationMs = Math.Max(0, queueDurationMs);
            }

            public string RunId { get; }

            public int RequestTokenBudget { get; }

            public long QueueDurationMs { get; }

            public CancellationToken CancellationToken => _activeRun.Token;

            public bool WasCancellationRequested => _activeRun.WasCancellationRequested;

            public void CompleteCancellationWindow() => _activeRun.Dispose();

            public void Commit(long consumedTokens)
            {
                _consumedTokens = Math.Max(0, consumedTokens);
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _owner, null)?.Release(this, _consumedTokens);
                _activeRun.Dispose();
            }
        }
    }
}
