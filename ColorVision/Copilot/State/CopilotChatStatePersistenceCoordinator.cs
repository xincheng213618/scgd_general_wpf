using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ColorVision.Copilot
{
    internal sealed class CopilotChatStatePersistenceCoordinator : IDisposable
    {
        private static readonly TimeSpan SnapshotUiSliceBudget = TimeSpan.FromMilliseconds(4);
        private readonly ICopilotChatStateStore _stateStore;
        private readonly Func<CopilotChatState> _stateProvider;
        private readonly Func<Dispatcher?> _dispatcherProvider;
        private readonly Action<Exception> _onError;
        private readonly CopilotChatStateSaveScheduler _scheduler;
        private readonly SemaphoreSlim _commitGate = new(1, 1);
        private int _lastSavePersisted;
        private int _stopState;

        public CopilotChatStatePersistenceCoordinator(
            ICopilotChatStateStore stateStore,
            Func<CopilotChatState> stateProvider,
            Func<Dispatcher?> dispatcherProvider,
            Action<Exception> onError,
            Action onSaved)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
            _dispatcherProvider = dispatcherProvider ?? throw new ArgumentNullException(nameof(dispatcherProvider));
            _onError = onError ?? throw new ArgumentNullException(nameof(onError));
            ArgumentNullException.ThrowIfNull(onSaved);
            _scheduler = new CopilotChatStateSaveScheduler(
                SaveSnapshotAsync,
                onError: onError,
                onSaved: () =>
                {
                    if (Interlocked.Exchange(ref _lastSavePersisted, 0) == 1)
                        onSaved();
                });
        }

        public void RequestSave(bool immediate = false) => _scheduler.RequestSave(immediate);

        public Task FlushAsync(CancellationToken cancellationToken = default) =>
            _scheduler.FlushAsync(cancellationToken);

        public void SaveSynchronouslyAndStop()
        {
            Stop();
            _commitGate.Wait();
            try
            {
                if (_stateStore is not CopilotChatStateStore stateStore || !stateStore.IsStatePersistenceBlocked)
                    _stateStore.Save(GetState());
            }
            catch (Exception exception)
            {
                ReportError(exception);
            }
            finally
            {
                _commitGate.Release();
            }
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        private async Task SaveSnapshotAsync(CancellationToken cancellationToken)
        {
            Interlocked.Exchange(ref _lastSavePersisted, 0);
            var captureVersion = _scheduler.RequestedVersion;
            var state = GetState();
            var dispatcher = _dispatcherProvider();
            CopilotChatStateSnapshot snapshot;
            if (dispatcher == null
                || dispatcher.CheckAccess()
                || _stateStore is not IIncrementalCopilotChatStateStore incrementalStateStore)
            {
                snapshot = _stateStore.CaptureSnapshot(state);
            }
            else
            {
                var beginCaptureOperation = dispatcher.InvokeAsync(
                    () => incrementalStateStore.BeginSnapshot(state),
                    DispatcherPriority.Background,
                    cancellationToken);
                var capture = await beginCaptureOperation.Task.ConfigureAwait(false);
                while (!capture.IsComplete)
                {
                    var captureSliceOperation = dispatcher.InvokeAsync(
                        () => CaptureSnapshotSlice(capture),
                        DispatcherPriority.Background,
                        cancellationToken);
                    await captureSliceOperation.Task.ConfigureAwait(false);
                }

                snapshot = capture.Complete();
            }

            if (_scheduler.RequestedVersion != captureVersion)
                return;

            var serializedState = await Task.Run(
                () => _stateStore.Serialize(snapshot),
                cancellationToken).ConfigureAwait(false);
            if (_scheduler.RequestedVersion != captureVersion)
                return;

            await _commitGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_scheduler.RequestedVersion != captureVersion)
                    return;

                await _stateStore.SaveSerializedAsync(serializedState, cancellationToken).ConfigureAwait(false);
                if (_scheduler.RequestedVersion == captureVersion)
                    Interlocked.Exchange(ref _lastSavePersisted, 1);
            }
            finally
            {
                _commitGate.Release();
            }
        }

        private CopilotChatState GetState() =>
            _stateProvider() ?? throw new InvalidOperationException("The Copilot chat state is unavailable.");

        private void Stop()
        {
            if (Interlocked.Exchange(ref _stopState, 1) == 1)
                return;

            _scheduler.Dispose();
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

        private static void CaptureSnapshotSlice(CopilotChatStateSnapshotCapture capture)
        {
            var startedAt = Stopwatch.GetTimestamp();
            do
            {
                capture.CaptureNextChunk();
            }
            while (!capture.IsComplete && Stopwatch.GetElapsedTime(startedAt) < SnapshotUiSliceBudget);
        }
    }
}
