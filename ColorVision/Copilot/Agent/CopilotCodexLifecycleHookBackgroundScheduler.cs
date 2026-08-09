#pragma warning disable CA1001 // Process-lifetime singleton; disposing would race scheduled hooks.
using log4net;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal interface ICopilotCodexLifecycleHookBackgroundScheduler
    {
        bool TrySchedule(
            string sourceId,
            string eventName,
            string turnId,
            TimeSpan timeout,
            Func<CancellationToken, Task> callback);
    }

    internal sealed class CopilotCodexLifecycleHookBackgroundScheduler :
        ICopilotCodexLifecycleHookBackgroundScheduler
    {
        internal const int MaxConcurrency = 4;
        internal const int MaxPending = 64;

        private static readonly ILog Log = LogManager.GetLogger(
            typeof(CopilotCodexLifecycleHookBackgroundScheduler));
        private readonly SemaphoreSlim _concurrency = new(MaxConcurrency, MaxConcurrency);
        private readonly object _gate = new();
        private int _pending;

        public static CopilotCodexLifecycleHookBackgroundScheduler Shared { get; } = new();

        public bool TrySchedule(
            string sourceId,
            string eventName,
            string turnId,
            TimeSpan timeout,
            Func<CancellationToken, Task> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            lock (_gate)
            {
                if (_pending >= MaxPending)
                    return false;
                _pending++;
            }

            _ = Task.Run(() => RunAsync(
                sourceId,
                eventName,
                turnId,
                timeout,
                callback));
            return true;
        }

        private async Task RunAsync(
            string sourceId,
            string eventName,
            string turnId,
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
                cancellation = new CancellationTokenSource();
                callbackTask = callback(cancellation.Token) ?? Task.CompletedTask;
                await callbackTask.WaitAsync(timeout).ConfigureAwait(false);
                Log.Info(
                    $"Copilot async lifecycle hook completed. Event={eventName} Turn={turnId} HookSource={sourceId}");
            }
            catch (TimeoutException)
            {
                CancelAndDisposeWithoutWaiting(ref cancellation);
                CopilotCancellationBoundary.ObserveLateFault(callbackTask);
                if (entered && callbackTask is { IsCompleted: false })
                {
                    reservationTransferred = true;
                    _ = ReleaseAfterCompletionAsync(callbackTask);
                }
                Log.Warn(
                    $"Copilot async lifecycle hook timed out. Event={eventName} Turn={turnId} HookSource={sourceId}");
            }
            catch (OperationCanceledException)
            {
                Log.Warn(
                    $"Copilot async lifecycle hook cancelled itself. Event={eventName} Turn={turnId} HookSource={sourceId}");
            }
            catch (Exception ex)
            {
                Log.Warn(
                    $"Copilot async lifecycle hook failed. Event={eventName} Turn={turnId} HookSource={sourceId} ErrorType={ex.GetType().FullName}");
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
                ReleaseReservation(entered: true);
            }
        }

        private void ReleaseReservation(bool entered)
        {
            if (entered)
                _concurrency.Release();
            lock (_gate)
                _pending--;
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
                    $"Copilot async lifecycle hook cancellation failed. ErrorType={ex.GetType().FullName}");
            }
            finally
            {
                cancellation.Dispose();
            }
        }
    }
}
