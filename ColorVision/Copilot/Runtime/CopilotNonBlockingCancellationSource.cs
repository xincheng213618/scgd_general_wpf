using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    // CancellationTokenSource.Cancel executes callbacks on the requesting thread. This owner uses
    // CancelAsync and defers source disposal so untrusted callbacks cannot block the UI or race CTS disposal.
    internal sealed class CopilotNonBlockingCancellationSource : IDisposable
    {
        private readonly object _gate = new();
        private readonly CancellationToken _token;
        private readonly TaskCompletionSource _disposalCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CancellationTokenSource? _source = new();
        private Task _cancellationTask = Task.CompletedTask;

        public CopilotNonBlockingCancellationSource()
        {
            _token = _source.Token;
        }

        public CancellationToken Token => _token;

        public bool IsCancellationRequested => _token.IsCancellationRequested;

        internal Task DisposalCompletion => _disposalCompletion.Task;

        public void RequestCancellation()
        {
            Task cancellationTask;
            lock (_gate)
            {
                var source = _source;
                if (source == null || source.IsCancellationRequested)
                    return;

                try
                {
                    cancellationTask = source.CancelAsync();
                }
                catch (Exception ex)
                {
                    TraceCancellationFailure(ex);
                    return;
                }

                _cancellationTask = cancellationTask;
            }

            if (!cancellationTask.IsCompletedSuccessfully)
                _ = ObserveCancellationAsync(cancellationTask);
        }

        public void Dispose()
        {
            CancellationTokenSource? source;
            Task cancellationTask;
            lock (_gate)
            {
                source = _source;
                if (source == null)
                    return;

                _source = null;
                cancellationTask = _cancellationTask;
            }

            if (cancellationTask.IsCompleted)
            {
                DisposeSource(source);
                return;
            }

            _ = DisposeSourceAfterCancellationAsync(source, cancellationTask);
        }

        private async Task DisposeSourceAfterCancellationAsync(
            CancellationTokenSource source,
            Task cancellationTask)
        {
            try
            {
                await cancellationTask.ConfigureAwait(false);
            }
            catch
            {
            }

            DisposeSource(source);
        }

        private void DisposeSource(CancellationTokenSource source)
        {
            try
            {
                source.Dispose();
                _disposalCompletion.TrySetResult();
            }
            catch (Exception ex)
            {
                _disposalCompletion.TrySetException(ex);
                Trace.TraceWarning(
                    "Copilot cancellation source disposal failed: {0}",
                    CopilotUserFacingErrorFormatter.Sanitize(ex.Message));
            }
        }

        private static async Task ObserveCancellationAsync(Task cancellationTask)
        {
            try
            {
                await cancellationTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                TraceCancellationFailure(ex);
            }
        }

        private static void TraceCancellationFailure(Exception exception)
        {
            Trace.TraceWarning(
                "Copilot cancellation callback failed: {0}",
                CopilotUserFacingErrorFormatter.Sanitize(exception.Message));
        }
    }
}
