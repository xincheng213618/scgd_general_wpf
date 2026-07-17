using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotChatStateSaveScheduler : IDisposable
    {
        private const int MaximumSaveAttempts = 2;
        private static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromMilliseconds(300);
        private static readonly TimeSpan SaveRetryDelay = TimeSpan.FromMilliseconds(50);
        private readonly Func<CancellationToken, Task> _saveAsync;
        private readonly Action<Exception> _onError;
        private readonly Action _onSaved;
        private readonly TimeSpan _debounceDelay;
        private readonly object _syncRoot = new();
        private readonly SemaphoreSlim _requestSignal = new(0);
        private readonly CancellationTokenSource _shutdown = new();
        private readonly Task _worker;
        private TaskCompletionSource _processedChanged = CreateCompletionSource();
        private long _requestedVersion;
        private long _processedVersion;
        private Exception? _lastProcessedError;
        private bool _immediateRequested;
        private bool _isDisposed;

        public CopilotChatStateSaveScheduler(
            Func<CancellationToken, Task> saveAsync,
            TimeSpan? debounceDelay = null,
            Action<Exception>? onError = null,
            Action? onSaved = null)
        {
            _saveAsync = saveAsync ?? throw new ArgumentNullException(nameof(saveAsync));
            _debounceDelay = debounceDelay ?? DefaultDebounceDelay;
            if (_debounceDelay < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(debounceDelay));

            _onError = onError ?? (exception => Trace.TraceError($"Copilot state persistence failed: {exception}"));
            _onSaved = onSaved ?? (() => { });
            _worker = Task.Run(ProcessRequestsAsync);
        }

        public void RequestSave(bool immediate = false)
        {
            lock (_syncRoot)
            {
                if (_isDisposed)
                    return;

                _requestedVersion++;
                _immediateRequested |= immediate;
            }

            _requestSignal.Release();
        }

        public Task FlushAsync(CancellationToken cancellationToken = default)
        {
            long targetVersion;
            lock (_syncRoot)
            {
                if (_isDisposed)
                    return Task.CompletedTask;

                if (_requestedVersion <= _processedVersion)
                {
                    if (_lastProcessedError == null)
                        return Task.CompletedTask;

                    // A previous flush exhausted its retries. Treat a later explicit flush as a
                    // request to capture the current state again instead of returning stale success.
                    _requestedVersion++;
                }

                targetVersion = _requestedVersion;
                _immediateRequested = true;
            }

            _requestSignal.Release();
            return WaitForProcessedVersionAsync(targetVersion, cancellationToken);
        }

        public void Dispose()
        {
            TaskCompletionSource processedChanged;
            lock (_syncRoot)
            {
                if (_isDisposed)
                    return;

                _isDisposed = true;
                processedChanged = _processedChanged;
                _processedChanged = CreateCompletionSource();
            }

            processedChanged.TrySetResult();
            _shutdown.Cancel();
            _requestSignal.Release();
        }

        private async Task ProcessRequestsAsync()
        {
            try
            {
                while (true)
                {
                    await _requestSignal.WaitAsync(_shutdown.Token).ConfigureAwait(false);
                    DrainRequestSignals();

                    var batch = TakePendingBatch();
                    if (batch.Version <= GetProcessedVersion())
                        continue;

                    while (!batch.Immediate
                        && await _requestSignal.WaitAsync(_debounceDelay, _shutdown.Token).ConfigureAwait(false))
                    {
                        DrainRequestSignals();
                        batch = TakePendingBatch();
                    }

                    var error = await SaveWithRetryAsync().ConfigureAwait(false);
                    MarkProcessed(batch.Version, error);
                }
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
            {
            }
        }

        private async Task<Exception?> SaveWithRetryAsync()
        {
            Exception? lastError = null;
            for (var attempt = 1; attempt <= MaximumSaveAttempts; attempt++)
            {
                try
                {
                    await _saveAsync(_shutdown.Token).ConfigureAwait(false);
                    ReportSaved();
                    return null;
                }
                catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    lastError = exception;
                    ReportError(exception);
                    if (attempt < MaximumSaveAttempts)
                        await Task.Delay(SaveRetryDelay, _shutdown.Token).ConfigureAwait(false);
                }
            }

            return lastError ?? new InvalidOperationException("Copilot state persistence failed without an error detail.");
        }

        private (long Version, bool Immediate) TakePendingBatch()
        {
            lock (_syncRoot)
            {
                var batch = (_requestedVersion, _immediateRequested);
                _immediateRequested = false;
                return batch;
            }
        }

        private long GetProcessedVersion()
        {
            lock (_syncRoot)
                return _processedVersion;
        }

        private void MarkProcessed(long version, Exception? error)
        {
            TaskCompletionSource processedChanged;
            lock (_syncRoot)
            {
                _processedVersion = Math.Max(_processedVersion, version);
                _lastProcessedError = error;
                processedChanged = _processedChanged;
                _processedChanged = CreateCompletionSource();
            }

            processedChanged.TrySetResult();
        }

        private async Task WaitForProcessedVersionAsync(long targetVersion, CancellationToken cancellationToken)
        {
            while (true)
            {
                Task? processedChanged = null;
                Exception? processedError = null;
                lock (_syncRoot)
                {
                    if (_processedVersion >= targetVersion)
                        processedError = _lastProcessedError;
                    else if (_isDisposed)
                        return;
                    else
                        processedChanged = _processedChanged.Task;
                }

                if (processedChanged == null)
                {
                    if (processedError != null)
                        throw processedError;
                    return;
                }

                await processedChanged.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private void DrainRequestSignals()
        {
            while (_requestSignal.Wait(0))
            {
            }
        }

        private void ReportError(Exception exception)
        {
            try
            {
                _onError(exception);
            }
            catch
            {
            }
        }

        private void ReportSaved()
        {
            try
            {
                _onSaved();
            }
            catch
            {
            }
        }

        private static TaskCompletionSource CreateCompletionSource() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
