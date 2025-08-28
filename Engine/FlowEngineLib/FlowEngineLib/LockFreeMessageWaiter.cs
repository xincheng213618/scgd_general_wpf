using System;
using System.Threading;
using System.Threading.Tasks;

namespace FlowEngineLib;

public class LockFreeMessageWaiter
{
	private volatile TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();
	private CancellationTokenSource _cts = new CancellationTokenSource();
	private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(60.0);
    private readonly object _lock = new object();

    public Task<bool> WaitForMessageAsync(TimeSpan? timeout = null)
    {
		lock (_lock)
		{
			TaskCompletionSource<bool> tcs = _tcs;
			_cts.CancelAfter(timeout ?? _defaultTimeout);
			_cts.Token.Register(delegate
			{
				tcs.TrySetResult(result: false);
			});
			return tcs.Task;
		}
	}

	public Task<bool> WaitForMessageAsync(int milliseconds = 6000)
	{
		return WaitForMessageAsync(TimeSpan.FromMilliseconds(milliseconds));
	}

	public Task<bool> WaitForMessage(int milliseconds = 6000)
	{
		Task<bool> task = WaitForMessageAsync(milliseconds);
		task.Wait();
		return task;
	}

	public void SignalMessageReceived()
	{
        lock (_lock)
        {
            _tcs.TrySetResult(true);
        }
    }

	public void Reset()
    {
		lock (_lock)
		{
			TaskCompletionSource<bool> tcs = _tcs;
			if (!tcs.Task.IsCompleted)
			{
				tcs.TrySetResult(result: false);
			}
			_tcs = new TaskCompletionSource<bool>();
			_cts.Dispose();
			_cts = new CancellationTokenSource();
		}
	}
}
