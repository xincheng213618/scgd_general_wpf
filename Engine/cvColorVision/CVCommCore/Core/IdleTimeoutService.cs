using System;
using System.Threading;
using System.Threading.Tasks;

namespace CVCommCore.Core;

public class IdleTimeoutService : IDisposable
{
	private readonly TimeSpan _timeoutDuration;

	private CancellationTokenSource _cts;

	private readonly Action _timeoutAction;

	private readonly Action<string> _logAction;

	public string Duration => _timeoutDuration.ToString();

	public IdleTimeoutService(Action timeoutAction, Action<string> logAction = null, int seconds = 600)
	{
		_timeoutDuration = TimeSpan.FromSeconds(seconds);
		_timeoutAction = timeoutAction ?? throw new ArgumentNullException("timeoutAction");
		_logAction = logAction;
		ResetTimeout();
		_logAction?.Invoke($"Begin Idle Timeout => {seconds} sec");
	}

	public void ResetTimeout()
	{
		_cts?.Cancel();
		_cts?.Dispose();
		_cts = new CancellationTokenSource();
		Task.Delay(_timeoutDuration, _cts.Token).ContinueWith(delegate(Task t)
		{
			if (!t.IsCanceled)
			{
				_timeoutAction?.Invoke();
			}
			else
			{
				_logAction?.Invoke("Timeout => " + _timeoutDuration.ToString());
			}
		}, TaskScheduler.Default);
	}

	public void Dispose()
	{
		_cts?.Cancel();
		_cts?.Dispose();
		_logAction?.Invoke("IdleTimeoutService Dispose");
	}
}
