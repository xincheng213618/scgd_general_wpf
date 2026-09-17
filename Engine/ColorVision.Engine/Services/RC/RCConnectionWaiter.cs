using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.RC;

internal static class RCConnectionWaiter
{
    public static async Task<bool> WaitAsync(
        Func<bool> isConnected,
        Action<EventHandler> subscribe,
        Action<EventHandler> unsubscribe,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(isConnected);
        ArgumentNullException.ThrowIfNull(subscribe);
        ArgumentNullException.ThrowIfNull(unsubscribe);

        if (isConnected())
            return true;

        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            if (isConnected())
                completion.TrySetResult(true);
        };

        subscribe(handler);
        try
        {
            if (isConnected())
                return true;

            try
            {
                return await completion.Task.WaitAsync(timeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                return false;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
        }
        finally
        {
            unsubscribe(handler);
        }
    }
}
