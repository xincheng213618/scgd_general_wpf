using System;
using System.Threading.Tasks;

namespace FlowEngineLib;

public class LockFreeMessageWaiter
{
	private volatile TaskCompletionSource<bool> _tcs =
		CreateCompletion();

	private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(60.0);

	public async Task<bool> WaitForMessageAsync(
		TimeSpan? timeout = null)
	{
		TaskCompletionSource<bool> tcs = _tcs;
		try
		{
			return await tcs.Task
				.WaitAsync(timeout ?? _defaultTimeout)
				.ConfigureAwait(false);
		}
		catch (TimeoutException)
		{
			return false;
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
		_tcs.TrySetResult(result: true);
	}

	public void Reset()
	{
		TaskCompletionSource<bool> tcs = _tcs;
		if (!tcs.Task.IsCompleted)
		{
			tcs.TrySetResult(result: false);
		}
		_tcs = CreateCompletion();
	}

	private static TaskCompletionSource<bool> CreateCompletion()
	{
		return new TaskCompletionSource<bool>(
			TaskCreationOptions.RunContinuationsAsynchronously);
	}
}
