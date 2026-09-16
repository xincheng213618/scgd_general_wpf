using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.Output
{
    /// <summary>Serializes WPF snapshot rendering on one reusable STA dispatcher.</summary>
    internal static class SnapshotStaWorker
    {
        private static readonly Task<Dispatcher> DispatcherTask = StartDispatcher();

        internal static async Task RunAsync(Action action, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(action);
            cancellationToken.ThrowIfCancellationRequested();

            Dispatcher dispatcher = await DispatcherTask.ConfigureAwait(false);
            DispatcherOperation operation = dispatcher.InvokeAsync(
                action,
                DispatcherPriority.Normal,
                cancellationToken);
            await operation.Task.ConfigureAwait(false);
        }

        internal static async Task<TResult> RunAsync<TResult>(
            Func<TResult> action,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(action);
            cancellationToken.ThrowIfCancellationRequested();

            Dispatcher dispatcher = await DispatcherTask.ConfigureAwait(false);
            DispatcherOperation<TResult> operation = dispatcher.InvokeAsync(
                action,
                DispatcherPriority.Normal,
                cancellationToken);
            return await operation.Task.ConfigureAwait(false);
        }

        private static Task<Dispatcher> StartDispatcher()
        {
            TaskCompletionSource<Dispatcher> completion = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Thread thread = new(() =>
            {
                try
                {
                    Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                    completion.TrySetResult(dispatcher);
                    Dispatcher.Run();
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            })
            {
                IsBackground = true,
                Name = "ColorVision Image Snapshot Renderer",
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }
    }
}
