using log4net;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotToolExecutionHookBackgroundActivitySnapshot(
        int RunningCount,
        int QueuedCount,
        int TimedOutRetainedCount,
        int MaximumConcurrency,
        int MaximumPending)
    {
        public int OutstandingCount => RunningCount + QueuedCount;

        public bool IsStructurallyValid() =>
            RunningCount >= 0
            && QueuedCount >= 0
            && TimedOutRetainedCount >= 0
            && MaximumConcurrency > 0
            && MaximumPending >= MaximumConcurrency
            && RunningCount <= MaximumConcurrency
            && OutstandingCount <= MaximumPending
            && TimedOutRetainedCount <= RunningCount;
    }

    /// <summary>
    /// Runs notification-only hooks without delaying the tool call that launched
    /// them. The shared limits prevent module hooks from creating an unbounded
    /// amount of in-process background work.
    /// </summary>
    internal sealed class CopilotToolExecutionHookBackgroundScheduler
    {
        internal const int MaxConcurrency = 4;
        internal const int MaxPending = 64;

        private static readonly ILog Log = LogManager.GetLogger(
            typeof(CopilotToolExecutionHookBackgroundScheduler));
        private readonly SemaphoreSlim _concurrency = new(MaxConcurrency, MaxConcurrency);
        private readonly object _activityGate = new();
        private int _pending;
        private int _running;
        private int _timedOutRetained;

        public static CopilotToolExecutionHookBackgroundScheduler Shared { get; } = new();

        public bool TrySchedule(
            string sourceId,
            CopilotToolExecutionHookPhase phase,
            string toolName,
            string callId,
            TimeSpan timeout,
            Func<CancellationToken, Task> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            if (!TryReservePendingSlot())
                return false;

            _ = Task.Run(() => RunAsync(
                sourceId,
                phase,
                toolName,
                callId,
                timeout,
                callback));
            return true;
        }

        public CopilotToolExecutionHookBackgroundActivitySnapshot GetActivitySnapshot()
        {
            lock (_activityGate)
            {
                return new CopilotToolExecutionHookBackgroundActivitySnapshot(
                    _running,
                    _pending - _running,
                    _timedOutRetained,
                    MaxConcurrency,
                    MaxPending);
            }
        }

        private bool TryReservePendingSlot()
        {
            lock (_activityGate)
            {
                if (_pending >= MaxPending)
                    return false;
                _pending++;
                return true;
            }
        }

        private async Task RunAsync(
            string sourceId,
            CopilotToolExecutionHookPhase phase,
            string toolName,
            string callId,
            TimeSpan timeout,
            Func<CancellationToken, Task> callback)
        {
            var entered = false;
            var reservationTransferred = false;
            CancellationTokenSource? cancellation = null;
            Task? callbackTask = null;
            try
            {
                await _concurrency.WaitAsync().ConfigureAwait(false);
                entered = true;
                lock (_activityGate)
                    _running++;
                cancellation = new CancellationTokenSource();
                callbackTask = callback(cancellation.Token) ?? Task.CompletedTask;
                await callbackTask.WaitAsync(timeout).ConfigureAwait(false);
                Log.Info(
                    $"Copilot async tool hook completed. Tool={toolName} CallId={callId} HookSource={sourceId} Phase={phase}");
            }
            catch (TimeoutException)
            {
                CancelAndDisposeWithoutWaiting(ref cancellation);
                CopilotCancellationBoundary.ObserveLateFault(callbackTask);
                if (entered && callbackTask is { IsCompleted: false })
                {
                    reservationTransferred = true;
                    lock (_activityGate)
                        _timedOutRetained++;
                    _ = ReleaseAfterCompletionAsync(callbackTask);
                }
                Log.Warn(
                    $"Copilot async tool hook timed out. Tool={toolName} CallId={callId} HookSource={sourceId} Phase={phase}");
            }
            catch (OperationCanceledException)
            {
                Log.Warn(
                    $"Copilot async tool hook cancelled itself. Tool={toolName} CallId={callId} HookSource={sourceId} Phase={phase}");
            }
            catch (Exception ex)
            {
                Log.Warn(
                    $"Copilot async tool hook failed. Tool={toolName} CallId={callId} HookSource={sourceId} Phase={phase} ErrorType={ex.GetType().FullName}");
            }
            finally
            {
                cancellation?.Dispose();
                if (!reservationTransferred)
                    ReleaseReservation(entered);
            }
        }

        private async Task ReleaseAfterCompletionAsync(Task callbackTask)
        {
            try
            {
                await callbackTask.ConfigureAwait(false);
            }
            catch
            {
            }
            finally
            {
                ReleaseReservation(entered: true, timedOutRetained: true);
            }
        }

        private void ReleaseReservation(bool entered, bool timedOutRetained = false)
        {
            lock (_activityGate)
            {
                if (timedOutRetained)
                    _timedOutRetained--;
                if (entered)
                    _running--;
                _pending--;
            }
            if (entered)
                _concurrency.Release();
        }

        private static void CancelAndDisposeWithoutWaiting(
            ref CancellationTokenSource? cancellation)
        {
            var ownedCancellation = Interlocked.Exchange(ref cancellation, null);
            if (ownedCancellation != null)
                _ = CancelAndDisposeAsync(ownedCancellation);
        }

        private static async Task CancelAndDisposeAsync(
            CancellationTokenSource cancellation)
        {
            try
            {
                await cancellation.CancelAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warn(
                    $"Copilot async tool hook cancellation failed. ErrorType={ex.GetType().FullName}");
            }
            finally
            {
                cancellation.Dispose();
            }
        }
    }
}
