using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal static class CopilotCancellationBoundary
    {
        public static Task<T> RunTaskAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(operation);
            cancellationToken.ThrowIfCancellationRequested();

            var operationTask = Task.Run(() => operation(cancellationToken), cancellationToken);
            return WaitAsync(operationTask, cancellationToken);
        }

        public static Task<T> RunSynchronousAsync<T>(
            Func<CancellationToken, T> operation,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(operation);
            cancellationToken.ThrowIfCancellationRequested();

            var operationTask = Task.Run(() => operation(cancellationToken), cancellationToken);
            return WaitAsync(operationTask, cancellationToken);
        }

        public static async Task<T> WaitAsync<T>(
            Task<T> operationTask,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(operationTask);
            try
            {
                return await operationTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                ObserveLateFault(operationTask);
                throw;
            }
        }

        public static void ObserveLateFault(Task? task)
        {
            if (task == null || task.IsCompletedSuccessfully || task.IsCanceled)
                return;
            if (task.IsFaulted)
            {
                _ = task.Exception;
                return;
            }

            _ = task.ContinueWith(
                static completedTask => _ = completedTask.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }
}
