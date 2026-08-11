using Conoscope.MVS;
using System.Collections.Concurrent;

namespace Conoscope.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MvsCaptureSessionTestGroup
{
    public const string Name = "Conoscope MVS capture lifecycle";
}

[Collection(MvsCaptureSessionTestGroup.Name)]
public sealed class MvsCaptureSessionTests
{
    private static bool WaitForThread(Thread? thread, TimeSpan _)
    {
        if (thread == null || !thread.IsAlive)
        {
            return true;
        }

        thread.Join();
        return true;
    }

    [Fact(Timeout = 30_000)]
    public async Task StartCallsNativeBeforeStartingBackgroundWorker()
    {
        await Task.Yield();

        ConcurrentQueue<string> calls = new();
        using ManualResetEventSlim workerEntered = new();
        using ManualResetEventSlim releaseWorker = new();

        MvsCaptureSession session = new(
            () => { calls.Enqueue("start-native"); return 0; },
            () => { calls.Enqueue("stop-native"); releaseWorker.Set(); return 0; },
            token =>
            {
                calls.Enqueue($"worker-background:{Thread.CurrentThread.IsBackground}");
                workerEntered.Set();
                releaseWorker.Wait(CancellationToken.None);
            },
            _ => { },
            Timeout.InfiniteTimeSpan,
            waitForThread: WaitForThread);

        MvsCaptureStartResult startResult = session.Start();

        Assert.True(startResult.Started);
        workerEntered.Wait(CancellationToken.None);
        Assert.Collection(
            calls,
            call => Assert.Equal("start-native", call),
            call => Assert.Equal("worker-background:True", call));

        MvsCaptureStopResult stopResult = session.Stop();
        Assert.True(stopResult.WorkerExited);
        Assert.Equal("stop-native", calls.Last());
    }

    [Fact(Timeout = 30_000)]
    public async Task StopCancelsGenerationThenUnblocksAndWaitsForWorker()
    {
        await Task.Yield();

        using ManualResetEventSlim workerEntered = new();
        using ManualResetEventSlim nativeUnblocked = new();
        CancellationToken workerToken = default;

        MvsCaptureSession session = new(
            () => 0,
            () =>
            {
                Assert.True(workerToken.IsCancellationRequested);
                nativeUnblocked.Set();
                return 17;
            },
            token =>
            {
                workerToken = token;
                workerEntered.Set();
                nativeUnblocked.Wait(CancellationToken.None);
            },
            _ => { },
            Timeout.InfiniteTimeSpan,
            waitForThread: WaitForThread);

        Assert.True(session.Start().Started);
        workerEntered.Wait(CancellationToken.None);

        MvsCaptureStopResult result = session.Stop();

        Assert.True(result.StopRequested);
        Assert.Equal(17, result.NativeResult);
        Assert.True(result.WorkerExited);
        Assert.False(session.IsWorkerAlive);
    }

    [Fact(Timeout = 30_000)]
    public async Task StopTimeoutDoesNotReportExitOrAllowImmediateRestart()
    {
        await Task.Yield();

        using ManualResetEventSlim workerEntered = new();
        using ManualResetEventSlim releaseWorker = new();
        using ManualResetEventSlim workerExited = new();
        int nativeStarts = 0;
        int simulatedTimeoutsRemaining = 1;

        MvsCaptureSession session = new(
            () => { Interlocked.Increment(ref nativeStarts); return 0; },
            () => 0,
            _ =>
            {
                workerEntered.Set();
                releaseWorker.Wait(CancellationToken.None);
                workerExited.Set();
            },
            _ => { },
            Timeout.InfiniteTimeSpan,
            scheduleOwnerAction: _ => { },
            waitForThread: (thread, _) =>
            {
                if (Interlocked.Exchange(ref simulatedTimeoutsRemaining, 0) != 0)
                {
                    return false;
                }

                return WaitForThread(thread, Timeout.InfiniteTimeSpan);
            });

        Assert.True(session.Start().Started);
        workerEntered.Wait(CancellationToken.None);

        MvsCaptureStopResult stopResult = session.Stop();

        Assert.False(stopResult.WorkerExited);
        MvsCaptureStartResult restartResult = session.Start();
        Assert.False(restartResult.Started);
        Assert.True(restartResult.AlreadyRunning);
        Assert.Equal(1, nativeStarts);

        releaseWorker.Set();
        workerExited.Wait(CancellationToken.None);
        Assert.True(session.WaitForExit(Timeout.InfiniteTimeSpan));
        Assert.True(session.Start().Started);
        Assert.Equal(2, nativeStarts);
        Assert.True(session.Stop().WorkerExited);
    }

    [Fact(Timeout = 30_000)]
    public async Task NewGenerationSingleFrameSupersedesPendingOldUiCallback()
    {
        await Task.Yield();

        MvsFrameUiUpdateGate frameUiUpdateGate = new();
        ConcurrentQueue<(long Generation, Action Callback)> uiCallbacks = new();
        ConcurrentQueue<long> completedGenerations = new();
        ConcurrentQueue<Exception> failures = new();
        using ManualResetEventSlim firstFrameQueued = new();
        using ManualResetEventSlim secondFrameQueued = new();
        int workerRuns = 0;

        MvsCaptureSession session = new(
            () => 0,
            () => 0,
            cancellationToken =>
            {
                int workerRun = Interlocked.Increment(ref workerRuns);
                long generation = frameUiUpdateGate.BeginGeneration();
                if (frameUiUpdateGate.TryQueue(generation))
                {
                    uiCallbacks.Enqueue((generation, () =>
                    {
                        if (frameUiUpdateGate.TryComplete(generation))
                        {
                            completedGenerations.Enqueue(generation);
                        }
                    }));
                }

                (workerRun == 1 ? firstFrameQueued : secondFrameQueued).Set();
                cancellationToken.WaitHandle.WaitOne();
            },
            failures.Enqueue,
            Timeout.InfiniteTimeSpan,
            scheduleOwnerAction: _ => { },
            waitForThread: WaitForThread);

        Assert.True(session.Start().Started);
        firstFrameQueued.Wait(CancellationToken.None);
        Assert.Single(uiCallbacks);
        Assert.True(session.Stop().WorkerExited);

        Assert.True(session.Start().Started);
        secondFrameQueued.Wait(CancellationToken.None);
        Assert.Equal(2, uiCallbacks.Count);

        Assert.True(uiCallbacks.TryDequeue(out var oldFrame));
        Assert.True(uiCallbacks.TryDequeue(out var newFrame));
        Assert.True(newFrame.Generation > oldFrame.Generation);

        oldFrame.Callback();
        Assert.Empty(completedGenerations);

        newFrame.Callback();
        Assert.Equal(newFrame.Generation, Assert.Single(completedGenerations));

        Assert.True(session.Stop().WorkerExited);
        Assert.Empty(failures);
    }

    [Fact(Timeout = 30_000)]
    public async Task UnexpectedWorkerFaultStopsItsGenerationExactlyOnce()
    {
        await Task.Yield();

        ConcurrentQueue<Action> ownerActions = new();
        ConcurrentQueue<Exception> failures = new();
        using ManualResetEventSlim ownerActionQueued = new();
        int nativeStops = 0;
        int ownerFaults = 0;

        MvsCaptureSession session = new(
            () => 0,
            () => { Interlocked.Increment(ref nativeStops); return 0; },
            _ => throw new InvalidOperationException("capture failed"),
            exception => failures.Enqueue(exception),
            Timeout.InfiniteTimeSpan,
            action =>
            {
                ownerActions.Enqueue(action);
                ownerActionQueued.Set();
            },
            (exception, result) =>
            {
                Assert.Equal("capture failed", exception.Message);
                Assert.True(result.WorkerExited);
                Interlocked.Increment(ref ownerFaults);
            },
            WaitForThread);

        Assert.True(session.Start().Started);
        ownerActionQueued.Wait(CancellationToken.None);
        Assert.True(ownerActions.TryDequeue(out Action? ownerAction));

        ownerAction();
        ownerAction();

        Assert.Single(failures);
        Assert.Equal(1, nativeStops);
        Assert.Equal(1, ownerFaults);
        Assert.False(session.Stop().StopRequested);
        Assert.Equal(1, nativeStops);
    }

    [Fact(Timeout = 30_000)]
    public async Task StaleWorkerFaultCannotStopANewerGeneration()
    {
        await Task.Yield();

        ConcurrentQueue<Action> ownerActions = new();
        using ManualResetEventSlim ownerActionQueued = new();
        using ManualResetEventSlim secondWorkerEntered = new();
        using ManualResetEventSlim releaseSecondWorker = new();
        int workerRuns = 0;
        int nativeStops = 0;
        int ownerFaults = 0;

        MvsCaptureSession session = new(
            () => 0,
            () => { Interlocked.Increment(ref nativeStops); return 0; },
            _ =>
            {
                if (Interlocked.Increment(ref workerRuns) == 1)
                {
                    throw new InvalidOperationException("old generation failed");
                }

                secondWorkerEntered.Set();
                releaseSecondWorker.Wait(CancellationToken.None);
            },
            _ => { },
            Timeout.InfiniteTimeSpan,
            action =>
            {
                ownerActions.Enqueue(action);
                ownerActionQueued.Set();
            },
            (_, _) => Interlocked.Increment(ref ownerFaults),
            WaitForThread);

        Assert.True(session.Start().Started);
        ownerActionQueued.Wait(CancellationToken.None);
        Assert.True(session.Stop().WorkerExited);

        Assert.True(session.Start().Started);
        secondWorkerEntered.Wait(CancellationToken.None);
        Assert.True(ownerActions.TryDequeue(out Action? staleOwnerAction));
        staleOwnerAction();

        Assert.True(session.IsWorkerAlive);
        Assert.Equal(1, nativeStops);
        Assert.Equal(0, ownerFaults);

        releaseSecondWorker.Set();
        Assert.True(session.Stop().WorkerExited);
        Assert.Equal(2, nativeStops);
    }

    [Fact(Timeout = 30_000)]
    public async Task StartIsRejectedUntilThePreviousNativeStopReturns()
    {
        await Task.Yield();

        using ManualResetEventSlim workerEntered = new();
        using ManualResetEventSlim stopEntered = new();
        using ManualResetEventSlim releaseStop = new();
        int nativeStarts = 0;

        MvsCaptureSession session = new(
            () => { Interlocked.Increment(ref nativeStarts); return 0; },
            () =>
            {
                stopEntered.Set();
                releaseStop.Wait(CancellationToken.None);
                return 0;
            },
            _ => workerEntered.Set(),
            _ => { },
            Timeout.InfiniteTimeSpan,
            scheduleOwnerAction: _ => { },
            waitForThread: WaitForThread);

        Assert.True(session.Start().Started);
        workerEntered.Wait(CancellationToken.None);

        Task<MvsCaptureStopResult> stopTask = Task.Run(session.Stop);
        stopEntered.Wait(CancellationToken.None);

        MvsCaptureStartResult restartResult = session.Start();
        Assert.False(restartResult.Started);
        Assert.True(restartResult.AlreadyRunning);
        Assert.Equal(1, nativeStarts);

        releaseStop.Set();
        Assert.True((await stopTask).WorkerExited);
    }

    [Fact(Timeout = 30_000)]
    public async Task DeferredCleanupWaitsForExitAndRunsExactlyOnceOnABackgroundThread()
    {
        await Task.Yield();

        using ManualResetEventSlim waitEntered = new();
        using ManualResetEventSlim releaseWait = new();
        using ManualResetEventSlim cleanupCompleted = new();
        ConcurrentQueue<Exception> failures = new();
        int cleanupCount = 0;
        bool cleanupThreadIsBackground = false;
        TimeSpan observedTimeout = default;

        MvsDeferredCleanup deferredCleanup = new(
            timeout =>
            {
                observedTimeout = timeout;
                cleanupThreadIsBackground = Thread.CurrentThread.IsBackground;
                waitEntered.Set();
                releaseWait.Wait(CancellationToken.None);
                return true;
            },
            () =>
            {
                Interlocked.Increment(ref cleanupCount);
                cleanupCompleted.Set();
            },
            exception =>
            {
                failures.Enqueue(exception);
                cleanupCompleted.Set();
            });

        deferredCleanup.Schedule();
        deferredCleanup.Schedule();

        waitEntered.Wait(CancellationToken.None);
        Assert.Equal(Timeout.InfiniteTimeSpan, observedTimeout);
        Assert.Equal(0, Volatile.Read(ref cleanupCount));

        releaseWait.Set();
        cleanupCompleted.Wait(CancellationToken.None);
        deferredCleanup.Schedule();

        Assert.True(cleanupThreadIsBackground);
        Assert.Equal(1, Volatile.Read(ref cleanupCount));
        Assert.Empty(failures);
    }

    [Fact(Timeout = 30_000)]
    public async Task DeferredOldWindowReleaseCannotFinalizeANewerSdkLease()
    {
        await Task.Yield();

        int initializeCount = 0;
        int finalizeCount = 0;
        ConcurrentQueue<Exception> failures = new();
        using ManualResetEventSlim oldCleanupEntered = new();
        using ManualResetEventSlim releaseOldCleanup = new();
        using ManualResetEventSlim oldCleanupCompleted = new();

        MvsSdkLifetime lifetime = new(
            () => { Interlocked.Increment(ref initializeCount); return 0; },
            () => { Interlocked.Increment(ref finalizeCount); return 0; });

        MvsSdkAcquireResult oldWindow = lifetime.Acquire();
        Assert.True(oldWindow.Acquired);

        MvsDeferredCleanup deferredCleanup = new(
            _ =>
            {
                oldCleanupEntered.Set();
                releaseOldCleanup.Wait(CancellationToken.None);
                return true;
            },
            () =>
            {
                oldWindow.Lease!.Release();
                oldCleanupCompleted.Set();
            },
            exception =>
            {
                failures.Enqueue(exception);
                oldCleanupCompleted.Set();
            });

        deferredCleanup.Schedule();
        oldCleanupEntered.Wait(CancellationToken.None);

        MvsSdkAcquireResult newWindow = lifetime.Acquire();
        Assert.True(newWindow.Acquired);
        Assert.Equal(1, initializeCount);
        Assert.Equal(2, lifetime.ActiveLeaseCount);

        releaseOldCleanup.Set();
        oldCleanupCompleted.Wait(CancellationToken.None);

        Assert.Empty(failures);
        Assert.Equal(0, finalizeCount);
        Assert.Equal(1, lifetime.ActiveLeaseCount);

        Assert.Equal(0, newWindow.Lease!.Release());
        Assert.Equal(1, finalizeCount);
        Assert.Equal(0, lifetime.ActiveLeaseCount);
    }

    [Fact(Timeout = 30_000)]
    public async Task ReleasedOldLeaseCannotFinalizeANewerSdkGeneration()
    {
        await Task.Yield();

        int initializeCount = 0;
        int finalizeCount = 0;
        MvsSdkLifetime lifetime = new(
            () => { Interlocked.Increment(ref initializeCount); return 0; },
            () => { Interlocked.Increment(ref finalizeCount); return 0; });

        MvsSdkLease oldLease = lifetime.Acquire().Lease!;
        Assert.Equal(0, oldLease.Release());
        Assert.Equal(1, initializeCount);
        Assert.Equal(1, finalizeCount);

        MvsSdkLease newLease = lifetime.Acquire().Lease!;
        Assert.Equal(2, initializeCount);

        Assert.Equal(0, oldLease.Release());
        Assert.Equal(1, finalizeCount);
        Assert.Equal(1, lifetime.ActiveLeaseCount);

        Assert.Equal(0, newLease.Release());
        Assert.Equal(2, finalizeCount);
    }
}
