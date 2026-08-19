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
    public async Task IncrementalSnapshotSkipsASupersededCutAndPersistsTheLatestBatch()
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
        var store = new RecordingStateStore(dispatcher)
        {
            BlockFirstIncrementalSnapshot = true,
            SerializeActualSnapshots = true,
        };
        var state = new CopilotChatState
        {
            ActiveConversationId = "before-capture",
        };
        state.Conversations.Add(CopilotConversationRecord.CreateEmpty("profile", "Profile"));
        var savedCount = 0;
        try
        {
            using var coordinator = new CopilotChatStatePersistenceCoordinator(
                store,
                () => state,
                () => dispatcher,
                _ => { },
                () => Interlocked.Increment(ref savedCount));

            coordinator.RequestSave(immediate: true);
            await store.FirstIncrementalSnapshotStarted.WaitAsync(TimeSpan.FromSeconds(5));
            state.ActiveConversationId = "after-capture";
            coordinator.RequestSave(immediate: true);
            store.ReleaseBlockedSnapshot();
            await coordinator.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));

            var persisted = JObject.Parse(store.SavedSerializedState);
            Assert.Equal("after-capture", persisted[nameof(CopilotChatState.ActiveConversationId)]?.Value<string>());
            Assert.Equal(2, store.BeginSnapshotCount);
            Assert.Equal(1, store.AsyncSaveCount);
            Assert.Equal(1, savedCount);
        }
        finally
        {
            store.ReleaseBlockedSnapshot();
            dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            Assert.True(thread.Join(TimeSpan.FromSeconds(2)));
        }
    }

    [Fact]
    public async Task SerializationSkipsASupersededCutBeforeCommittingTheLatestBatch()
    {
        var store = new RecordingStateStore
        {
            BlockFirstSerialization = true,
            SerializeActualSnapshots = true,
        };
        var state = new CopilotChatState
        {
            ActiveConversationId = "before-serialization",
        };
        var savedCount = 0;
        using var coordinator = new CopilotChatStatePersistenceCoordinator(
            store,
            () => state,
            () => null,
            _ => { },
            () => Interlocked.Increment(ref savedCount));

        try
        {
            coordinator.RequestSave(immediate: true);
            await store.FirstSerializationStarted.WaitAsync(TimeSpan.FromSeconds(5));
            state.ActiveConversationId = "after-serialization";
            coordinator.RequestSave(immediate: true);
            store.ReleaseBlockedSerialization();
            await coordinator.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));

            var persisted = JObject.Parse(store.SavedSerializedState);
            Assert.Equal("after-serialization", persisted[nameof(CopilotChatState.ActiveConversationId)]?.Value<string>());
            Assert.Equal(2, store.SerializeCount);
            Assert.Equal(1, store.AsyncSaveCount);
            Assert.Equal(1, savedCount);
        }
        finally
        {
            store.ReleaseBlockedSerialization();
        }
    }

    [Fact]
    public async Task FlushWaitsForASupersedingSnapshotToReachDurableStorage()
    {
        var store = new RecordingStateStore
        {
            BlockFirstSerialization = true,
            BlockSecondSerialization = true,
            SerializeActualSnapshots = true,
        };
        var state = new CopilotChatState
        {
            ActiveConversationId = "before-flush",
        };
        using var coordinator = new CopilotChatStatePersistenceCoordinator(
            store,
            () => state,
            () => null,
            _ => { },
            () => { });

        try
        {
            coordinator.RequestSave(immediate: true);
            var flush = coordinator.FlushAsync();
            await store.FirstSerializationStarted.WaitAsync(TimeSpan.FromSeconds(5));
            state.ActiveConversationId = "after-flush";
            coordinator.RequestSave(immediate: true);
            store.ReleaseBlockedSerialization();
            await store.SecondSerializationStarted.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(flush.IsCompleted);
            Assert.Equal(0, store.AsyncSaveCount);

            store.ReleaseSecondBlockedSerialization();
            await flush.WaitAsync(TimeSpan.FromSeconds(5));

            var persisted = JObject.Parse(store.SavedSerializedState);
            Assert.Equal("after-flush", persisted[nameof(CopilotChatState.ActiveConversationId)]?.Value<string>());
        }
        finally
        {
            store.ReleaseBlockedSerialization();
            store.ReleaseSecondBlockedSerialization();
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

    [Fact]
    public async Task SynchronousShutdownSaveCommitsAfterAnInFlightOlderAsyncSave()
    {
        var store = new RecordingStateStore
        {
            BlockFirstAsyncSave = true,
        };
        var state = new CopilotChatState
        {
            ActiveConversationId = "older-async-cut",
        };
        using var coordinator = new CopilotChatStatePersistenceCoordinator(
            store,
            () => state,
            () => null,
            _ => { },
            () => { });

        coordinator.RequestSave(immediate: true);
        await store.FirstAsyncSaveStarted.WaitAsync(TimeSpan.FromSeconds(5));
        var latestState = new CopilotChatState
        {
            ActiveConversationId = "latest-shutdown-cut",
        };
        state = latestState;

        var shutdownSave = Task.Run(coordinator.SaveSynchronouslyAndStop);
        await Task.Delay(100);

        Assert.False(shutdownSave.IsCompleted);
        Assert.Null(store.SynchronouslySavedState);
        store.ReleaseBlockedAsyncSave();
        await shutdownSave.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Same(latestState, store.SynchronouslySavedState);
        Assert.Equal(["async", "sync"], store.SaveOrder);
    }

    private sealed class RecordingStateStore : IIncrementalCopilotChatStateStore
    {
        private readonly Dispatcher? _dispatcher;
        private readonly TaskCompletionSource _continueIncrementalSnapshot =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _firstIncrementalSnapshotStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _continueFirstSerialization =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _firstSerializationStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _continueSecondSerialization =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _secondSerializationStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _continueFirstAsyncSave =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _firstAsyncSaveStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

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

        public bool BlockFirstIncrementalSnapshot { get; init; }

        public bool SerializeActualSnapshots { get; init; }

        public bool BlockFirstSerialization { get; init; }

        public bool BlockSecondSerialization { get; init; }

        public bool BlockFirstAsyncSave { get; init; }

        public int BeginSnapshotCount { get; private set; }

        public int SerializeCount { get; private set; }

        public Task FirstIncrementalSnapshotStarted => _firstIncrementalSnapshotStarted.Task;

        public Task FirstSerializationStarted => _firstSerializationStarted.Task;

        public Task SecondSerializationStarted => _secondSerializationStarted.Task;

        public Task FirstAsyncSaveStarted => _firstAsyncSaveStarted.Task;

        public List<string> SaveOrder { get; } = [];

        public CopilotChatState Load() => new();

        public void Save(CopilotChatState state)
        {
            SynchronouslySavedState = state;
            SaveOrder.Add("sync");
            if (SyncSaveException != null)
                throw SyncSaveException;
        }

        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState state)
        {
            DirectCaptureCount++;
            CapturedState = state;
            if (SerializeActualSnapshots)
            {
                var capture = new CopilotChatStateSnapshotCapture(state, new JsonSerializerSettings());
                while (capture.CaptureNextChunk())
                {
                }
                return capture.Complete();
            }
            return new CopilotChatStateSnapshot(new JObject());
        }

        public CopilotChatStateSnapshotCapture BeginSnapshot(CopilotChatState state)
        {
            CapturedState = state;
            BeginSnapshotHadDispatcherAccess = _dispatcher?.CheckAccess() == true;
            var capture = new CopilotChatStateSnapshotCapture(state, new JsonSerializerSettings());
            BeginSnapshotCount++;
            if (BlockFirstIncrementalSnapshot && BeginSnapshotCount == 1)
            {
                _firstIncrementalSnapshotStarted.TrySetResult();
                if (!_continueIncrementalSnapshot.Task.Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("Timed out waiting to release the incremental snapshot test gate.");
            }

            return capture;
        }

        public string Serialize(CopilotChatStateSnapshot snapshot)
        {
            SerializeCount++;
            if (BlockFirstSerialization && SerializeCount == 1)
            {
                _firstSerializationStarted.TrySetResult();
                if (!_continueFirstSerialization.Task.Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("Timed out waiting to release the serialization test gate.");
            }
            if (BlockSecondSerialization && SerializeCount == 2)
            {
                _secondSerializationStarted.TrySetResult();
                if (!_continueSecondSerialization.Task.Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("Timed out waiting to release the second serialization test gate.");
            }

            return SerializeActualSnapshots
                ? snapshot.Document.ToString(Formatting.None)
                : "serialized-snapshot";
        }

        public string Serialize(CopilotChatState state) => "serialized-state";

        public async Task SaveSerializedAsync(
            string serializedState,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AsyncSaveCount++;
            if (AsyncSaveException != null)
                throw AsyncSaveException;
            if (BlockFirstAsyncSave && AsyncSaveCount == 1)
            {
                _firstAsyncSaveStarted.TrySetResult();
                await _continueFirstAsyncSave.Task;
            }

            SavedSerializedState = serializedState;
            SaveOrder.Add("async");
        }

        public int CleanupOrphanedAttachments(CopilotChatState state) => 0;

        public void ReleaseBlockedSnapshot() => _continueIncrementalSnapshot.TrySetResult();

        public void ReleaseBlockedSerialization() => _continueFirstSerialization.TrySetResult();

        public void ReleaseSecondBlockedSerialization() => _continueSecondSerialization.TrySetResult();

        public void ReleaseBlockedAsyncSave() => _continueFirstAsyncSave.TrySetResult();
    }
}
