using ColorVisionServiceHost;
using System.ServiceProcess;

namespace ColorVision.UI.Tests;

public sealed class ColorVisionServiceHostServiceLifecycleTests
{
    [Fact]
    public async Task StopRemainsPendingPastNormalBudgetUntilAllOwnedWorkDrains()
    {
        ManualTimeProvider timeProvider = new();
        FakePipeServer pipeServer = new();
        FakeScanProtectionLifetime scanProtection = new();
        TaskCompletionSource overBudget = NewCompletionSource();
        int overBudgetReports = 0;
        FakeScmController? scm = null;
        using TestService service = new(
            () => pipeServer,
            scanProtection,
            timeProvider,
            CreateBudgetCrossingWaiter(timeProvider, overBudget.Task),
            _ => scm!.RecordAdditionalTimeRequest(),
            _ =>
            {
                Interlocked.Increment(ref overBudgetReports);
                overBudget.TrySetResult();
            });
        scm = new FakeScmController(service);

        scm.Start();
        Task firstStop = scm.StopAsync();
        Task? repeatedStop = null;
        try
        {
            await overBudget.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(FakeScmState.StopPending, scm.State);
            Assert.False(firstStop.IsCompleted);
            Assert.Equal(1, Volatile.Read(ref overBudgetReports));
            Assert.True(scm.AdditionalTimeRequests > 0);
            Assert.Equal(1, pipeServer.StopCalls);
            Assert.Equal(1, scanProtection.StopCalls);
            Assert.Equal(0, pipeServer.DisposeCalls);
            Assert.Equal(0, scanProtection.DisposeCalls);

            repeatedStop = scm.StopAsync();
            Assert.False(repeatedStop.IsCompleted);

            pipeServer.CompleteStop();
            await Task.Yield();
            Assert.False(firstStop.IsCompleted);
            Assert.Equal(FakeScmState.StopPending, scm.State);
            Assert.Equal(0, pipeServer.DisposeCalls);
            Assert.Equal(0, scanProtection.DisposeCalls);

            scanProtection.CompleteStop();
            await Task.WhenAll(firstStop, repeatedStop).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(FakeScmState.Stopped, scm.State);
            Assert.Equal(1, pipeServer.StopCalls);
            Assert.Equal(1, scanProtection.StopCalls);
            Assert.Equal(1, pipeServer.DisposeCalls);
            Assert.Equal(1, scanProtection.DisposeCalls);

            await scm.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, pipeServer.StopCalls);
            Assert.Equal(1, scanProtection.StopCalls);
            Assert.Equal(1, pipeServer.DisposeCalls);
            Assert.Equal(1, scanProtection.DisposeCalls);
        }
        finally
        {
            pipeServer.CompleteStop();
            scanProtection.CompleteStop();
            await firstStop.WaitAsync(TimeSpan.FromSeconds(5));
            if (repeatedStop != null)
                await repeatedStop.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task StartupCleanupDoesNotHoldStartPendingAndIsOwnedByStop()
    {
        FakePipeServer pipeServer = new();
        TaskCompletionSource startupCleanup = NewCompletionSource();
        FakeScanProtectionLifetime scanProtection = new(
            startupTask: startupCleanup.Task,
            stopTask: startupCleanup.Task);
        using TestService service = new(
            () => pipeServer,
            scanProtection,
            requestAdditionalTime: _ => { });
        FakeScmController scm = new(service);
        Task start = Task.Factory.StartNew(
            scm.Start,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        Task? stop = null;
        try
        {
            await start.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(FakeScmState.Running, scm.State);
            Assert.False(startupCleanup.Task.IsCompleted);
            Assert.Equal(1, scanProtection.StartCalls);

            stop = scm.StopAsync();
            Assert.True(SpinWait.SpinUntil(
                () => scm.State == FakeScmState.StopPending,
                TimeSpan.FromSeconds(5)));
            Assert.False(stop.IsCompleted);
            Assert.Equal(0, scanProtection.DisposeCalls);

            pipeServer.CompleteStop();
            Assert.False(stop.IsCompleted);
            startupCleanup.TrySetResult();
            await stop.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(FakeScmState.Stopped, scm.State);
            Assert.Equal(1, scanProtection.StopCalls);
            Assert.Equal(1, scanProtection.DisposeCalls);
        }
        finally
        {
            pipeServer.CompleteStop();
            startupCleanup.TrySetResult();
            await start.WaitAsync(TimeSpan.FromSeconds(5));
            stop ??= scm.StopAsync();
            await stop.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task StartFailureDrainsPartiallyStartedCleanupBeforeReportingFailure()
    {
        InvalidOperationException marker = new("pipe startup failed");
        FakePipeServer pipeServer = new(
            stopInitiallyCompleted: true,
            runFailure: marker);
        TaskCompletionSource startupCleanup = NewCompletionSource();
        FakeScanProtectionLifetime scanProtection = new(
            startupTask: startupCleanup.Task,
            stopTask: startupCleanup.Task);
        using TestService service = new(
            () => pipeServer,
            scanProtection,
            requestAdditionalTime: _ => { });

        Task<Exception> start = Task.Factory.StartNew(
            () => Record.Exception(service.StartForTest),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        try
        {
            Assert.True(SpinWait.SpinUntil(
                () => scanProtection.StopCalls == 1,
                TimeSpan.FromSeconds(5)));
            Assert.False(start.IsCompleted);
            Assert.Equal(0, pipeServer.DisposeCalls);
            Assert.Equal(0, scanProtection.DisposeCalls);

            startupCleanup.TrySetResult();
            Exception observed = await start.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Same(marker, observed);
            Assert.Equal(1, pipeServer.StopCalls);
            Assert.Equal(1, scanProtection.StopCalls);
            Assert.Equal(1, pipeServer.DisposeCalls);
            Assert.Equal(1, scanProtection.DisposeCalls);
        }
        finally
        {
            startupCleanup.TrySetResult();
            await start.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task ConsoleCancellationClosesPipeAndScanAdmissionTogether()
    {
        using CancellationTokenSource pipeCancellation = new();
        FakeScanProtectionLifetime scanProtection = new();
        int pipeCancellationObserved = 0;
        using CancellationTokenRegistration registration = pipeCancellation.Token.Register(
            () => Interlocked.Increment(ref pipeCancellationObserved));

        Task shutdown = Program.BeginConsoleShutdown(pipeCancellation, scanProtection);
        try
        {
            Assert.True(pipeCancellation.IsCancellationRequested);
            Assert.Equal(1, Volatile.Read(ref pipeCancellationObserved));
            Assert.Equal(1, scanProtection.StopCalls);
            Assert.False(shutdown.IsCompleted);

            scanProtection.CompleteStop();
            await shutdown.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            scanProtection.CompleteStop();
            await shutdown.WaitAsync(TimeSpan.FromSeconds(5));
            scanProtection.Dispose();
        }
    }

    [Fact]
    public async Task ShutdownFailureIsObservedWithoutRestoringScmToRunning()
    {
        InvalidOperationException marker = new("pipe drain failed");
        FakePipeServer pipeServer = new();
        FakeScanProtectionLifetime scanProtection = new(stopInitiallyCompleted: true);
        List<Exception> observedFailures = [];
        using TestService service = new(
            () => pipeServer,
            scanProtection,
            requestAdditionalTime: _ => { },
            reportShutdownFailure: failure => observedFailures.Add(failure));
        FakeScmController scm = new(service);
        scm.Start();

        Task stop = scm.StopAsync();
        pipeServer.FailStop(marker);
        await stop.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(FakeScmState.Stopped, scm.State);
        Exception observed = Assert.Single(observedFailures);
        Assert.Contains(marker.Message, observed.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, pipeServer.DisposeCalls);
        Assert.Equal(1, scanProtection.DisposeCalls);
    }

    [Fact]
    public async Task ResourceDisposalRemainsInsideTheSameObservedStopBudget()
    {
        ManualTimeProvider timeProvider = new();
        FakePipeServer pipeServer = new(completeOnStop: true);
        using ManualResetEventSlim releaseDispose = new(initialState: false);
        FakeScanProtectionLifetime scanProtection = new(
            stopInitiallyCompleted: true,
            disposeRelease: releaseDispose);
        TaskCompletionSource overBudget = NewCompletionSource();
        FakeScmController? scm = null;
        using TestService service = new(
            () => pipeServer,
            scanProtection,
            timeProvider,
            CreateBudgetCrossingWaiter(timeProvider, overBudget.Task),
            _ => scm!.RecordAdditionalTimeRequest(),
            _ => overBudget.TrySetResult());
        scm = new FakeScmController(service);
        scm.Start();

        Task stop = scm.StopAsync();
        try
        {
            await Task.WhenAll(overBudget.Task, scanProtection.DisposeStarted)
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(FakeScmState.StopPending, scm.State);
            Assert.False(stop.IsCompleted);
            Assert.Equal(1, pipeServer.DisposeCalls);
            Assert.Equal(1, scanProtection.DisposeCalls);

            releaseDispose.Set();
            await stop.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(FakeScmState.Stopped, scm.State);
        }
        finally
        {
            releaseDispose.Set();
            await stop.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task SelfUpdateCallerKeepsOwnershipUntilServiceReallyStops()
    {
        ManualTimeProvider timeProvider = new();
        FakePipeServer pipeServer = new();
        FakeScanProtectionLifetime scanProtection = new(stopInitiallyCompleted: true);
        TaskCompletionSource overBudget = NewCompletionSource();
        List<string> events = [];
        object eventsSync = new();
        FakeScmController? scm = null;
        using TestService service = new(
            () => pipeServer,
            scanProtection,
            timeProvider,
            CreateBudgetCrossingWaiter(timeProvider, overBudget.Task),
            _ => scm!.RecordAdditionalTimeRequest(),
            _ => overBudget.TrySetResult());
        scm = new FakeScmController(
            service,
            onStopped: () => AddEvent("stopped"));
        scm.Start();
        int stageOwned = 1;

        Task selfUpdateCaller = Task.Factory.StartNew(
            () =>
            {
                scm.Stop();
                AddEvent("copy");
                Volatile.Write(ref stageOwned, 0);
                AddEvent("restart");
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        try
        {
            await overBudget.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(FakeScmState.StopPending, scm.State);
            Assert.Equal(1, Volatile.Read(ref stageOwned));
            lock (eventsSync)
                Assert.Empty(events);

            pipeServer.CompleteStop();
            await selfUpdateCaller.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(0, Volatile.Read(ref stageOwned));
            lock (eventsSync)
                Assert.Equal(["stopped", "copy", "restart"], events);
        }
        finally
        {
            pipeServer.CompleteStop();
            await selfUpdateCaller.WaitAsync(TimeSpan.FromSeconds(5));
        }

        void AddEvent(string value)
        {
            lock (eventsSync)
                events.Add(value);
        }
    }

    private static Func<Task, TimeSpan, bool> CreateBudgetCrossingWaiter(
        ManualTimeProvider timeProvider,
        Task overBudgetTask)
    {
        int waits = 0;
        return (task, waitDuration) =>
        {
            if (Interlocked.Increment(ref waits) == 1)
            {
                timeProvider.Advance(TimeSpan.FromMinutes(2));
                return task.IsCompleted;
            }

            if (!overBudgetTask.IsCompleted)
                return task.IsCompleted;

            return task.Wait(TimeSpan.FromSeconds(5));
        };
    }

    private static TaskCompletionSource NewCompletionSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class TestService : ColorVisionServiceHostService
    {
        public TestService(
            Func<IServiceHostPipeServerLifetime> serverFactory,
            IApplicationUpdateScanProtectionLifetime scanProtection,
            TimeProvider? timeProvider = null,
            Func<Task, TimeSpan, bool>? waitForCompletion = null,
            Action<int>? requestAdditionalTime = null,
            Action<TimeSpan>? reportOverBudget = null,
            Action<Exception>? reportShutdownFailure = null)
            : base(
                serverFactory,
                scanProtection,
                timeProvider,
                waitForCompletion,
                requestAdditionalTime,
                reportOverBudget,
                reportShutdownFailure)
        {
        }

        public void StartForTest() => OnStart([]);
    }

    private enum FakeScmState
    {
        Stopped,
        StartPending,
        Running,
        StopPending,
    }

    private sealed class FakeScmController
    {
        private readonly TestService _service;
        private readonly Action? _onStopped;
        private int _state = (int)FakeScmState.Stopped;
        private int _additionalTimeRequests;

        public FakeScmController(TestService service, Action? onStopped = null)
        {
            _service = service;
            _onStopped = onStopped;
        }

        public FakeScmState State => (FakeScmState)Volatile.Read(ref _state);

        public int AdditionalTimeRequests => Volatile.Read(ref _additionalTimeRequests);

        public void Start()
        {
            Volatile.Write(ref _state, (int)FakeScmState.StartPending);
            try
            {
                _service.StartForTest();
                Volatile.Write(ref _state, (int)FakeScmState.Running);
            }
            catch
            {
                Volatile.Write(ref _state, (int)FakeScmState.Stopped);
                throw;
            }
        }

        public Task StopAsync() => Task.Factory.StartNew(
            Stop,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        public void Stop()
        {
            Volatile.Write(ref _state, (int)FakeScmState.StopPending);
            try
            {
                _service.Stop();
                Volatile.Write(ref _state, (int)FakeScmState.Stopped);
                _onStopped?.Invoke();
            }
            catch
            {
                // This mirrors ServiceBase.DeferredStop: an OnStop exception restores
                // the previous Running state instead of leaving SCM in StopPending.
                Volatile.Write(ref _state, (int)FakeScmState.Running);
                throw;
            }
        }

        public void RecordAdditionalTimeRequest()
        {
            Assert.Equal(FakeScmState.StopPending, State);
            Interlocked.Increment(ref _additionalTimeRequests);
        }
    }

    private sealed class FakePipeServer : IServiceHostPipeServerLifetime
    {
        private readonly TaskCompletionSource _stopCompletion = NewCompletionSource();
        private readonly Exception? _runFailure;
        private readonly bool _completeOnStop;
        private int _stopCalls;
        private int _disposeCalls;

        public FakePipeServer(
            bool stopInitiallyCompleted = false,
            Exception? runFailure = null,
            bool completeOnStop = false)
        {
            _runFailure = runFailure;
            _completeOnStop = completeOnStop;
            if (stopInitiallyCompleted)
                _stopCompletion.TrySetResult();
        }

        public int StopCalls => Volatile.Read(ref _stopCalls);

        public int DisposeCalls => Volatile.Read(ref _disposeCalls);

        public Task RunAsync(CancellationToken cancellationToken)
        {
            if (_runFailure != null)
                return Task.FromException(_runFailure);
            return _stopCompletion.Task;
        }

        public Task StopAsync()
        {
            Interlocked.Increment(ref _stopCalls);
            if (_completeOnStop)
                _stopCompletion.TrySetResult();
            return _stopCompletion.Task;
        }

        public void CompleteStop() => _stopCompletion.TrySetResult();

        public void FailStop(Exception exception) => _stopCompletion.TrySetException(exception);

        public void Dispose() => Interlocked.Increment(ref _disposeCalls);
    }

    private sealed class FakeScanProtectionLifetime : IApplicationUpdateScanProtectionLifetime
    {
        private readonly Task _startupTask;
        private readonly TaskCompletionSource? _ownedStopCompletion;
        private readonly Task _stopTask;
        private readonly ManualResetEventSlim? _disposeRelease;
        private readonly TaskCompletionSource _disposeStarted = NewCompletionSource();
        private int _startCalls;
        private int _stopCalls;
        private int _disposeCalls;

        public FakeScanProtectionLifetime(
            Task? startupTask = null,
            Task? stopTask = null,
            bool stopInitiallyCompleted = false,
            ManualResetEventSlim? disposeRelease = null)
        {
            _startupTask = startupTask ?? Task.CompletedTask;
            _disposeRelease = disposeRelease;
            if (stopTask != null)
            {
                _stopTask = stopTask;
            }
            else
            {
                _ownedStopCompletion = NewCompletionSource();
                if (stopInitiallyCompleted)
                    _ownedStopCompletion.TrySetResult();
                _stopTask = _ownedStopCompletion.Task;
            }
        }

        public int StartCalls => Volatile.Read(ref _startCalls);

        public int StopCalls => Volatile.Read(ref _stopCalls);

        public int DisposeCalls => Volatile.Read(ref _disposeCalls);

        public Task DisposeStarted => _disposeStarted.Task;

        public Task Start()
        {
            Interlocked.Increment(ref _startCalls);
            return _startupTask;
        }

        public Task StopAsync()
        {
            Interlocked.Increment(ref _stopCalls);
            return _stopTask;
        }

        public void CompleteStop() => _ownedStopCompletion?.TrySetResult();

        public void Dispose()
        {
            Interlocked.Increment(ref _disposeCalls);
            _disposeStarted.TrySetResult();
            _disposeRelease?.Wait();
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Volatile.Read(ref _timestamp);

        public void Advance(TimeSpan elapsed) => Interlocked.Add(ref _timestamp, elapsed.Ticks);
    }
}
