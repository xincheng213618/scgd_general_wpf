using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.FlowProcessing;

/// <summary>
/// Tracks asynchronous callbacks that belong to one execution generation and
/// provides a deterministic drain signal without polling or fixed sleeps.
/// </summary>
internal sealed class AsyncOperationDrain
{
    private readonly object _sync = new();
    private long _generation;
    private int _pendingCount;
    private TaskCompletionSource<bool> _drained = CreateCompleted();

    public void Reset(long generation)
    {
        lock (_sync)
        {
            _drained.TrySetResult(true);
            _generation = generation;
            _pendingCount = 0;
            _drained = CreateCompleted();
        }
    }

    public bool Begin(long generation = 0)
    {
        lock (_sync)
        {
            if (generation != _generation)
                return false;
            if (_pendingCount == 0)
            {
                _drained = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }
            _pendingCount++;
            return true;
        }
    }

    public void Complete(long generation = 0)
    {
        lock (_sync)
        {
            if (generation != _generation || _pendingCount <= 0)
                return;
            _pendingCount--;
            if (_pendingCount == 0)
                _drained.TrySetResult(true);
        }
    }

    public async Task<bool> WaitAsync(
        TimeSpan timeout,
        long generation = 0)
    {
        Task drainedTask;
        lock (_sync)
        {
            if (generation != _generation || _pendingCount == 0)
                return true;
            drainedTask = _drained.Task;
        }

        if (timeout == Timeout.InfiniteTimeSpan)
        {
            await drainedTask.ConfigureAwait(false);
            return true;
        }
        if (timeout <= TimeSpan.Zero)
            return drainedTask.IsCompleted;

        Task completed = await Task.WhenAny(
                drainedTask,
                Task.Delay(timeout))
            .ConfigureAwait(false);
        return ReferenceEquals(completed, drainedTask);
    }

    private static TaskCompletionSource<bool> CreateCompleted()
    {
        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        completion.TrySetResult(true);
        return completion;
    }
}
