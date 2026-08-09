using ColorVision.Copilot;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Windows.Threading;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotChatStatePersistenceCoordinatorTests
{
    [Fact]
    public async Task RequestAndFlushCaptureTheCurrentStateAndPersistTheSerializedSnapshot()
    {
        var store = new RecordingStateStore();
        var initialState = new CopilotChatState();
        var currentState = initialState;
        var savedCount = 0;
        using var coordinator = new CopilotChatStatePersistenceCoordinator(
            store,
            () => currentState,
            () => null,
            _ => { },
            () => Interlocked.Increment(ref savedCount));

        coordinator.RequestSave();
        var latestState = new CopilotChatState();
        currentState = latestState;
        await coordinator.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Same(latestState, store.CapturedState);
        Assert.NotSame(initialState, store.CapturedState);
        Assert.Equal("serialized-snapshot", store.SavedSerializedState);
        Assert.Equal(1, savedCount);
    }

    [Fact]
    public async Task IncrementalSnapshotCaptureRunsOnDispatcherAndPersistsAfterCompletion()
    {
        var dispatcherReady = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            dispatcherReady.TrySetResult(Dispatcher.CurrentDispatcher);
            Dispatcher.Run();
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var dispatcher = await dispatcherReady.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var store = new RecordingStateStore(dispatcher);
        var state = new CopilotChatState();
        state.Conversations.Add(CopilotConversationRecord.CreateEmpty("profile", "Profile"));
        try
        {
            using var coordinator = new CopilotChatStatePersistenceCoordinator(
                store,
                () => state,
                () => dispatcher,
                _ => { },
                () => { });

            coordinator.RequestSave(immediate: true);
            await coordinator.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(store.BeginSnapshotHadDispatcherAccess);
            Assert.Equal(0, store.DirectCaptureCount);
            Assert.Equal("serialized-snapshot", store.SavedSerializedState);
        }
        finally
        {
            dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            Assert.True(thread.Join(TimeSpan.FromSeconds(2)));
        }
    }

    [Fact]
    public async Task FailedFlushIsReportedAndTheNextRequestCanRetry()
    {
        var store = new RecordingStateStore
        {
            AsyncSaveException = new InvalidOperationException("Disk unavailable"),
        };
        var errors = new List<Exception>();
        using var coordinator = new CopilotChatStatePersistenceCoordinator(
            store,
            () => new CopilotChatState(),
            () => null,
            errors.Add,
            () => { });

        coordinator.RequestSave(immediate: true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.FlushAsync());
        store.AsyncSaveException = null;
        coordinator.RequestSave(immediate: true);
        await coordinator.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(3, store.AsyncSaveCount);
        Assert.Equal(2, errors.Count);
        Assert.All(errors, error => Assert.Equal("Disk unavailable", error.Message));
    }

    [Fact]
    public void SynchronousShutdownSaveUsesTheLatestStateAndReportsFailures()
    {
        var store = new RecordingStateStore
        {
            SyncSaveException = new IOException("Read-only state directory"),
        };
        var initialState = new CopilotChatState();
        var state = initialState;
        Exception? observedError = null;
        using var coordinator = new CopilotChatStatePersistenceCoordinator(
            store,
            () => state,
            () => null,
            exception => observedError = exception,
            () => { });

        var latestState = new CopilotChatState();
        state = latestState;
        coordinator.SaveSynchronouslyAndStop();
        coordinator.RequestSave(immediate: true);

        Assert.Same(latestState, store.SynchronouslySavedState);
        Assert.NotSame(initialState, store.SynchronouslySavedState);
        Assert.IsType<IOException>(observedError);
        Assert.Equal(0, store.AsyncSaveCount);
    }

    private sealed class RecordingStateStore : IIncrementalCopilotChatStateStore
    {
        private readonly Dispatcher? _dispatcher;

        public RecordingStateStore(Dispatcher? dispatcher = null)
        {
            _dispatcher = dispatcher;
        }

        public string AttachmentDirectoryPath => string.Empty;

        public CopilotChatState? CapturedState { get; private set; }

        public CopilotChatState? SynchronouslySavedState { get; private set; }

        public string SavedSerializedState { get; private set; } = string.Empty;

        public Exception? AsyncSaveException { get; set; }

        public Exception? SyncSaveException { get; set; }

        public int AsyncSaveCount { get; private set; }

        public int DirectCaptureCount { get; private set; }

        public bool BeginSnapshotHadDispatcherAccess { get; private set; }

        public CopilotChatState Load() => new();

        public void Save(CopilotChatState state)
        {
            SynchronouslySavedState = state;
            if (SyncSaveException != null)
                throw SyncSaveException;
        }

        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState state)
        {
            DirectCaptureCount++;
            CapturedState = state;
            return new CopilotChatStateSnapshot(new JObject());
        }

        public CopilotChatStateSnapshotCapture BeginSnapshot(CopilotChatState state)
        {
            CapturedState = state;
            BeginSnapshotHadDispatcherAccess = _dispatcher?.CheckAccess() == true;
            return new CopilotChatStateSnapshotCapture(state, new JsonSerializerSettings());
        }

        public string Serialize(CopilotChatStateSnapshot snapshot) => "serialized-snapshot";

        public string Serialize(CopilotChatState state) => "serialized-state";

        public Task SaveSerializedAsync(
            string serializedState,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AsyncSaveCount++;
            if (AsyncSaveException != null)
                return Task.FromException(AsyncSaveException);

            SavedSerializedState = serializedState;
            return Task.CompletedTask;
        }

        public int CleanupOrphanedAttachments(CopilotChatState state) => 0;
    }
}
