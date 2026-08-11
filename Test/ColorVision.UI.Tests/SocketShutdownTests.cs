using ColorVision.SocketProtocol;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class SocketShutdownTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void WorkerTrackerUsesOneFiniteDeadlineAndRepeatedShutdownConverges()
    {
        var tracker = new SocketWorkerTracker();
        Assert.True(tracker.TryRegister(out SocketWorkerLease? worker));
        tracker.BeginShutdown();

        Stopwatch stopwatch = Stopwatch.StartNew();
        Assert.False(tracker.Wait(TimeSpan.FromMilliseconds(30)));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
        Assert.Equal(1, tracker.ActiveWorkers);
        Assert.False(tracker.TryRegister(out _));
        Assert.False(tracker.Wait(TimeSpan.Zero));

        worker.Dispose();
        Assert.True(tracker.Wait(TestTimeout));
        Assert.Equal(0, tracker.ActiveWorkers);
        tracker.BeginShutdown();
        Assert.True(tracker.Wait(TimeSpan.Zero));
    }

    [Fact]
    public void ShutdownWithoutWorkersCompletesWithoutCreatingWork()
    {
        var tracker = new SocketWorkerTracker();

        tracker.BeginShutdown();

        Assert.True(tracker.Wait(TimeSpan.Zero));
        Assert.Equal(0, tracker.ActiveWorkers);
        Assert.False(tracker.TryRegister(out _));
    }

    [Fact]
    public void ShutdownWithoutManagerDoesNotConstructSingletonServices()
    {
        var lifetime = new SocketManagerApplicationLifetime();
        int factoryCalls = 0;

        Assert.True(lifetime.ShutdownExisting(TimeSpan.Zero));
        Assert.True(lifetime.ShutdownExisting(TimeSpan.Zero));
        Assert.Throws<InvalidOperationException>(() => lifetime.GetOrCreate(() =>
        {
            Interlocked.Increment(ref factoryCalls);
            throw new InvalidOperationException("factory must not run");
        }));
        Assert.Equal(0, factoryCalls);
    }

    [Fact]
    public async Task GatedLateFactoryDoesNotConsumeDeadlineOrPublishAfterShutdown()
    {
        var lifetime = new SocketManagerApplicationLifetime();
        var tracker = new SocketWorkerTracker();
        using var listener = new GatedLoopbackListener(gateStop: true);
        var factory = new SingleListenerFactory(listener);
        using var factoryEntered = new ManualResetEventSlim();
        using var releaseFactory = new ManualResetEventSlim();
        SocketManager? candidate = null;
        var waiterCompletion = new TaskCompletionSource<SocketManager>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var waiterEntered = new ManualResetEventSlim();
        Task<SocketManager> creating = Task.Run(() => lifetime.GetOrCreate(() =>
        {
            candidate = CreateManager(tracker, factory);
            candidate.StartServer();
            Assert.True(listener.Started.Wait(TestTimeout));
            factoryEntered.Set();
            Assert.True(releaseFactory.Wait(TestTimeout));
            return candidate;
        }));
        var waiter = new Thread(() =>
        {
            waiterEntered.Set();
            try
            {
                waiterCompletion.TrySetResult(lifetime.GetOrCreate(
                    () => throw new InvalidOperationException("waiter factory must not run")));
            }
            catch (Exception exception)
            {
                waiterCompletion.TrySetException(exception);
            }
        })
        {
            IsBackground = true
        };

        try
        {
            Assert.True(factoryEntered.Wait(TestTimeout));
            waiter.Start();
            Assert.True(waiterEntered.Wait(TestTimeout));
            Assert.True(SpinWait.SpinUntil(
                () => (waiter.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0,
                TestTimeout));
            Stopwatch stopwatch = Stopwatch.StartNew();
            Assert.False(lifetime.ShutdownExisting(TimeSpan.FromMilliseconds(30)));
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
            Assert.False(creating.IsCompleted);
            Assert.Throws<InvalidOperationException>(() => lifetime.GetOrCreate(
                () => throw new InvalidOperationException("shutdown must reject every later get")));
        }
        finally
        {
            releaseFactory.Set();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await creating.WaitAsync(TestTimeout));
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await waiterCompletion.Task.WaitAsync(TestTimeout));
        Assert.True(waiter.Join(TestTimeout));
        Assert.NotNull(candidate);
        Assert.True(listener.StopEntered.Wait(TestTimeout));
        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Assert.False(lifetime.ShutdownExisting(TimeSpan.FromMilliseconds(30)));
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
            candidate!.StartServer();
            candidate.StopServer();
            Assert.Equal(1, listener.StopCalls);
        }
        finally
        {
            listener.ReleaseStop.Set();
        }

        Assert.True(lifetime.ShutdownExisting(TestTimeout));
        Assert.Equal(1, listener.StopCalls);
        Assert.Equal(1, factory.CreateCalls);
        Assert.Equal(0, tracker.ActiveWorkers);
    }

    [Fact]
    public void PublishedManagerIsNotReturnedAfterLifetimeShutdown()
    {
        var lifetime = new SocketManagerApplicationLifetime();
        var tracker = new SocketWorkerTracker();
        using var listener = new LoopbackListener();
        SocketManager manager = lifetime.GetOrCreate(
            () => CreateManager(tracker, new SingleListenerFactory(listener)));

        Assert.True(lifetime.ShutdownExisting(TestTimeout));
        int laterFactoryCalls = 0;
        Assert.Throws<InvalidOperationException>(() => lifetime.GetOrCreate(() =>
        {
            Interlocked.Increment(ref laterFactoryCalls);
            return manager;
        }));
        Assert.Equal(0, laterFactoryCalls);
    }

    [Fact]
    public async Task ConcurrentGetOrCreateSharesOneFactoryAttempt()
    {
        var lifetime = new SocketManagerApplicationLifetime();
        var tracker = new SocketWorkerTracker();
        using var listener = new LoopbackListener();
        using var factoryEntered = new ManualResetEventSlim();
        using var releaseFactory = new ManualResetEventSlim();
        int factoryCalls = 0;
        Func<SocketManager> create = () =>
        {
            Interlocked.Increment(ref factoryCalls);
            factoryEntered.Set();
            Assert.True(releaseFactory.Wait(TestTimeout));
            return CreateManager(tracker, new SingleListenerFactory(listener));
        };

        Task<SocketManager> first = Task.Run(() => lifetime.GetOrCreate(create));
        var secondCompletion = new TaskCompletionSource<SocketManager>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var secondEntered = new ManualResetEventSlim();
        var second = new Thread(() =>
        {
            secondEntered.Set();
            try
            {
                secondCompletion.TrySetResult(lifetime.GetOrCreate(create));
            }
            catch (Exception exception)
            {
                secondCompletion.TrySetException(exception);
            }
        })
        {
            IsBackground = true
        };
        try
        {
            Assert.True(factoryEntered.Wait(TestTimeout));
            second.Start();
            Assert.True(secondEntered.Wait(TestTimeout));
            Assert.True(SpinWait.SpinUntil(
                () => (second.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0,
                TestTimeout));
        }
        finally
        {
            releaseFactory.Set();
        }

        SocketManager[] managers = await Task.WhenAll(first, secondCompletion.Task).WaitAsync(TestTimeout);
        Assert.True(second.Join(TestTimeout));
        Assert.Same(managers[0], managers[1]);
        Assert.Equal(1, factoryCalls);
        Assert.True(lifetime.ShutdownExisting(TestTimeout));
    }

    [Fact]
    public void FailedFactoryAttemptCanBeRetriedBeforeShutdown()
    {
        var lifetime = new SocketManagerApplicationLifetime();
        var tracker = new SocketWorkerTracker();
        using var listener = new LoopbackListener();
        int factoryCalls = 0;

        Assert.Throws<InvalidOperationException>(() => lifetime.GetOrCreate(() =>
        {
            Interlocked.Increment(ref factoryCalls);
            throw new InvalidOperationException("simulated construction failure");
        }));
        SocketManager manager = lifetime.GetOrCreate(() =>
        {
            Interlocked.Increment(ref factoryCalls);
            return CreateManager(tracker, new SingleListenerFactory(listener));
        });

        Assert.NotNull(manager);
        Assert.Equal(2, factoryCalls);
        Assert.True(lifetime.ShutdownExisting(TestTimeout));
    }

    [Fact]
    public async Task LifetimeLockCannotExtendShutdownDeadline()
    {
        var lifetime = new SocketManagerApplicationLifetime();
        object lifetimeLock = Assert.IsType<object>(typeof(SocketManagerApplicationLifetime)
            .GetField("_lock", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(lifetime));
        using var lockEntered = new ManualResetEventSlim();
        using var releaseLock = new ManualResetEventSlim();
        Task holder = Task.Run(() =>
        {
            lock (lifetimeLock)
            {
                lockEntered.Set();
                Assert.True(releaseLock.Wait(TestTimeout));
            }
        });

        try
        {
            Assert.True(lockEntered.Wait(TestTimeout));
            Stopwatch stopwatch = Stopwatch.StartNew();
            Assert.False(lifetime.ShutdownExisting(TimeSpan.FromMilliseconds(30)));
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
        }
        finally
        {
            releaseLock.Set();
        }

        await holder.WaitAsync(TestTimeout);
        Assert.True(lifetime.ShutdownExisting(TestTimeout));
    }

    [Fact]
    public void ManagerShutdownUsesOneDeadlineAndRepeatedCallWaitsForSameWorkers()
    {
        var tracker = new SocketWorkerTracker();
        Assert.True(tracker.TryRegister(out SocketWorkerLease? heldWorker));
        using var listener = new LoopbackListener();
        var factory = new SingleListenerFactory(listener);
        SocketManager manager = CreateManager(tracker, factory);

        Stopwatch stopwatch = Stopwatch.StartNew();
        Assert.False(manager.Shutdown(TimeSpan.FromMilliseconds(30)));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
        Assert.False(manager.Shutdown(TimeSpan.Zero));
        Assert.Equal(1, tracker.ActiveWorkers);

        heldWorker.Dispose();
        Assert.True(manager.Shutdown(TestTimeout));
        Assert.Equal(0, tracker.ActiveWorkers);
        manager.StartServer();
        manager.StopServer();
        Assert.Equal(0, factory.CreateCalls);
    }

    [Fact]
    public void ShutdownSchedulingFailureCanBeRetriedWithoutFalseSuccess()
    {
        var tracker = new SocketWorkerTracker();
        using var listener = new LoopbackListener();
        int scheduleCalls = 0;
        SocketManager manager = CreateManager(
            tracker,
            new SingleListenerFactory(listener),
            queueShutdownWork: action =>
            {
                if (Interlocked.Increment(ref scheduleCalls) == 1)
                    throw new InvalidOperationException("simulated scheduling failure");
                _ = Task.Run(action);
            });

        Assert.False(manager.Shutdown(TimeSpan.FromMilliseconds(30)));
        Assert.Equal(1, tracker.ActiveWorkers);
        Assert.True(manager.Shutdown(TestTimeout));
        Assert.Equal(0, tracker.ActiveWorkers);
        Assert.Equal(2, scheduleCalls);
        Assert.True(manager.Shutdown(TimeSpan.Zero));
    }

    [Fact]
    public async Task GatedListenerStartAndConcurrentStopCannotExtendLifetimeShutdownDeadline()
    {
        var lifetime = new SocketManagerApplicationLifetime();
        var tracker = new SocketWorkerTracker();
        using var listener = new GatedLoopbackListener(gateStart: true);
        var factory = new SingleListenerFactory(listener);
        SocketManager manager = lifetime.GetOrCreate(() => CreateManager(tracker, factory));
        manager.StartServer();
        Assert.True(listener.StartEntered.Wait(TestTimeout));

        Task stop = Task.Run(manager.StopServer);
        try
        {
            Assert.True(SpinWait.SpinUntil(() => tracker.ActiveWorkers >= 2, TestTimeout));
            Stopwatch stopwatch = Stopwatch.StartNew();
            Assert.False(lifetime.ShutdownExisting(TimeSpan.FromMilliseconds(30)));
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
            Assert.False(stop.IsCompleted);
        }
        finally
        {
            listener.ReleaseStart.Set();
        }

        await stop.WaitAsync(TestTimeout);
        Assert.True(lifetime.ShutdownExisting(TestTimeout));
        Assert.Equal(1, listener.StopCalls);
        Assert.Equal(0, tracker.ActiveWorkers);
    }

    [Fact]
    public async Task GatedListenerStopClosesClientFirstAndDoesNotBlockShutdownOrDuplicateCleanup()
    {
        var lifetime = new SocketManagerApplicationLifetime();
        var tracker = new SocketWorkerTracker();
        using var listener = new GatedLoopbackListener(gateStop: true);
        var factory = new SingleListenerFactory(listener);
        SocketManager manager = lifetime.GetOrCreate(() => CreateManager(tracker, factory));
        using var clientProjected = new ManualResetEventSlim();
        manager.TcpClients.CollectionChanged += (_, _) => clientProjected.Set();

        manager.StartServer();
        Assert.True(listener.Started.Wait(TestTimeout));
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, listener.Port).WaitAsync(TestTimeout);
        Assert.True(clientProjected.Wait(TestTimeout));
        Assert.True(SpinWait.SpinUntil(() => tracker.ActiveWorkers >= 2, TestTimeout));

        Task stop = Task.Run(manager.StopServer);
        try
        {
            Assert.True(listener.StopEntered.Wait(TestTimeout));
            Assert.Equal(1, listener.StopCalls);

            byte[] buffer = new byte[1];
            try
            {
                int bytesRead = await client.GetStream().ReadAsync(buffer).AsTask().WaitAsync(TestTimeout);
                Assert.Equal(0, bytesRead);
            }
            catch (IOException)
            {
                // A reset proves the client transport was closed before listener.Stop was released.
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            Assert.False(lifetime.ShutdownExisting(TimeSpan.FromMilliseconds(30)));
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
            Assert.False(stop.IsCompleted);
            Assert.Equal(1, listener.StopCalls);
        }
        finally
        {
            listener.ReleaseStop.Set();
        }

        await stop.WaitAsync(TestTimeout);
        Assert.True(lifetime.ShutdownExisting(TestTimeout));
        Assert.Equal(1, listener.StopCalls);
        Assert.Equal(0, tracker.ActiveWorkers);
    }

    [Fact]
    public async Task ManagerLoopbackShutdownClosesIdleReadAndProductionWorkers()
    {
        var tracker = new SocketWorkerTracker();
        using var listener = new LoopbackListener();
        var factory = new SingleListenerFactory(listener);
        SocketManager manager = CreateManager(tracker, factory);
        using var clientProjected = new ManualResetEventSlim();
        manager.TcpClients.CollectionChanged += (_, _) => clientProjected.Set();

        manager.StartServer();
        Assert.True(listener.Started.Wait(TestTimeout));
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, listener.Port).WaitAsync(TestTimeout);
        Assert.True(clientProjected.Wait(TestTimeout));
        Assert.True(SpinWait.SpinUntil(() => tracker.ActiveWorkers >= 2, TestTimeout));

        Thread backgroundProbe = SocketManager.CreateClientThread(() => { });
        Assert.True(backgroundProbe.IsBackground);
        Stopwatch stopwatch = Stopwatch.StartNew();
        Assert.True(manager.Shutdown(TimeSpan.FromSeconds(2)));

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
        Assert.Equal(0, tracker.ActiveWorkers);
        Assert.Equal(1, listener.StopCalls);
        Assert.Equal(1, factory.CreateCalls);
        Assert.True(manager.Shutdown(TimeSpan.Zero));
        manager.StartServer();
        manager.StopServer();
        Assert.Equal(1, factory.CreateCalls);

        byte[] buffer = new byte[1];
        try
        {
            int bytesRead = await client.GetStream().ReadAsync(buffer).AsTask().WaitAsync(TestTimeout);
            Assert.Equal(0, bytesRead);
        }
        catch (IOException)
        {
            // A reset is also a completed disconnect on Windows.
        }
    }

    [Fact]
    public async Task EquivalentAddressSuccessorWaitsUntilGatedListenerReleaseCompletes()
    {
        var tracker = new SocketWorkerTracker();
        int port = GetAvailablePort();
        using var oldListener = new GatedLoopbackListener(port, gateStop: true);
        using var successor = new LoopbackListener(port);
        var factory = new SequencedListenerFactory(oldListener, successor);
        SocketManager manager = CreateManager(tracker, factory, port);
        SocketServerLifecycle lifecycle = GetLifecycle(manager);

        manager.StartServer();
        Assert.True(oldListener.Started.Wait(TestTimeout));
        Task stop = Task.Run(manager.StopServer);
        try
        {
            Assert.True(oldListener.StopEntered.Wait(TestTimeout));
            manager.Config.IPAddress = "127.1";
            manager.StartServer();
            Assert.Equal(SocketServerState.Stopping, lifecycle.State);
            Assert.Equal(1, factory.CreateCalls);
            Assert.False(successor.Started.IsSet);
        }
        finally
        {
            oldListener.ReleaseStop.Set();
        }

        await stop.WaitAsync(TestTimeout);
        Assert.True(successor.Started.Wait(TestTimeout));
        Assert.Equal(2, factory.CreateCalls);
        Assert.True(manager.Shutdown(TestTimeout));
        Assert.Equal(1, oldListener.StopCalls);
        Assert.Equal(1, successor.StopCalls);
    }

    [Fact]
    public async Task FailedStopBlocksSameEndpointUntilTrackedRetryReleasesRealPort()
    {
        var tracker = new SocketWorkerTracker();
        int port = GetAvailablePort();
        using var factory = new RetryThenReplacementFactory(port);
        SocketManager manager = CreateManager(tracker, factory, port);
        SocketServerLifecycle lifecycle = GetLifecycle(manager);

        manager.StartServer();
        Assert.True(factory.First.Started.Wait(TestTimeout));
        manager.StopServer();
        try
        {
            Assert.True(factory.First.RetryStopEntered.Wait(TestTimeout));
            manager.StartServer();
            manager.CheckUpdate();
            Assert.Equal(SocketServerState.Stopping, lifecycle.State);
            Assert.Equal(1, factory.CreateCalls);
            Assert.Null(factory.Replacement);
        }
        finally
        {
            factory.First.ReleaseRetryStop.Set();
        }

        Assert.True(factory.First.StopCompleted.Wait(TestTimeout));
        Assert.Equal(2, factory.First.StopCalls);
        Assert.True(SpinWait.SpinUntil(() => factory.Replacement?.Started.IsSet == true, TestTimeout));
        Assert.Equal(SocketServerState.Running, lifecycle.State);
        Assert.Equal(factory.First.Port, factory.Replacement!.Port);
        Assert.Equal(2, factory.CreateCalls);
        Assert.True(manager.Shutdown(TestTimeout));
        Assert.Equal(0, tracker.ActiveWorkers);
    }

    [Fact]
    public async Task SocketInitializerSingleEnableToggleStartsLatestSettingsAfterPendingRelease()
    {
        var tracker = new SocketWorkerTracker();
        int port = GetAvailablePort();
        using var factory = new RetryThenReplacementFactory(port);
        var config = new SocketConfig
        {
            IPAddress = IPAddress.Loopback.ToString(),
            ServerPort = port,
            SocketBufferSize = 4096,
            SocketPhraseType = SocketPhraseType.Text,
            IsServerEnabled = true
        };
        SocketManager manager = CreateManager(tracker, factory, config: config);
        var initializer = new SocketInitializer(config, () => manager);

        await initializer.InitializeAsync();
        Assert.True(factory.First.Started.Wait(TestTimeout));
        Assert.True(factory.First.AcceptEntered.Wait(TestTimeout));
        config.IsServerEnabled = false;
        Assert.True(factory.First.RetryStopEntered.Wait(TestTimeout));

        config.SocketBufferSize = 8192;
        config.SocketPhraseType = SocketPhraseType.Json;
        config.IsServerEnabled = true;
        Assert.Equal(1, factory.CreateCalls);
        Assert.Null(factory.Replacement);

        factory.First.ReleaseRetryStop.Set();
        Assert.True(factory.First.StopCompleted.Wait(TestTimeout));
        Assert.True(SpinWait.SpinUntil(() => factory.Replacement?.Started.IsSet == true, TestTimeout));
        Assert.Equal(2, factory.CreateCalls);
        SocketServerSettings latest = factory.Settings.ToArray()[1];
        Assert.Equal(port, latest.ServerPort);
        Assert.Equal(8192, latest.SocketBufferSize);
        Assert.Equal(SocketPhraseType.Json, latest.SocketPhraseType);
        Assert.True(latest.IsServerEnabled);
        Assert.True(manager.Shutdown(TestTimeout));
        Assert.Equal(0, tracker.ActiveWorkers);
    }

    [Fact]
    public async Task TerminalShutdownCancelsPendingInitializerEnableIntent()
    {
        var tracker = new SocketWorkerTracker();
        int port = GetAvailablePort();
        using var factory = new RetryThenReplacementFactory(port);
        var config = new SocketConfig
        {
            IPAddress = IPAddress.Loopback.ToString(),
            ServerPort = port,
            SocketBufferSize = 4096,
            SocketPhraseType = SocketPhraseType.Text,
            IsServerEnabled = true
        };
        SocketManager manager = CreateManager(tracker, factory, config: config);
        var initializer = new SocketInitializer(config, () => manager);

        await initializer.InitializeAsync();
        Assert.True(factory.First.Started.Wait(TestTimeout));
        Assert.True(factory.First.AcceptEntered.Wait(TestTimeout));
        config.IsServerEnabled = false;
        Assert.True(factory.First.RetryStopEntered.Wait(TestTimeout));
        config.IsServerEnabled = true;
        Assert.Equal(1, factory.CreateCalls);

        manager.BeginShutdown();
        factory.First.ReleaseRetryStop.Set();
        Assert.True(manager.Shutdown(TestTimeout));
        Assert.Equal(1, factory.CreateCalls);
        Assert.Null(factory.Replacement);
        Assert.Equal(0, tracker.ActiveWorkers);
    }

    [Fact]
    public async Task LatestInitializerDisableCancelsPendingEnableIntent()
    {
        var tracker = new SocketWorkerTracker();
        int port = GetAvailablePort();
        using var factory = new RetryThenReplacementFactory(port);
        var config = new SocketConfig
        {
            IPAddress = IPAddress.Loopback.ToString(),
            ServerPort = port,
            SocketBufferSize = 4096,
            SocketPhraseType = SocketPhraseType.Text,
            IsServerEnabled = true
        };
        SocketManager manager = CreateManager(tracker, factory, config: config);
        var initializer = new SocketInitializer(config, () => manager);

        await initializer.InitializeAsync();
        Assert.True(factory.First.Started.Wait(TestTimeout));
        Assert.True(factory.First.AcceptEntered.Wait(TestTimeout));
        config.IsServerEnabled = false;
        Assert.True(factory.First.RetryStopEntered.Wait(TestTimeout));
        config.IsServerEnabled = true;
        config.IsServerEnabled = false;

        factory.First.ReleaseRetryStop.Set();
        Assert.True(factory.First.StopCompleted.Wait(TestTimeout));
        Assert.True(SpinWait.SpinUntil(() => tracker.ActiveWorkers == 0, TestTimeout));
        Assert.Equal(1, factory.CreateCalls);
        Assert.Null(factory.Replacement);
        Assert.True(manager.Shutdown(TestTimeout));
    }

    [Fact]
    public async Task FailedTrackedRetryRemainsOwnedForTerminalCleanup()
    {
        var tracker = new SocketWorkerTracker();
        int port = GetAvailablePort();
        using var listener = new FailBeforeReleaseLoopbackListener(
            failuresBeforeRelease: 2,
            port: port);
        var factory = new SingleListenerFactory(listener);
        SocketManager manager = CreateManager(tracker, factory, port);

        manager.StartServer();
        Assert.True(listener.Started.Wait(TestTimeout));
        Assert.True(listener.AcceptEntered.Wait(TestTimeout));
        manager.StopServer();
        Assert.True(listener.SecondFailureObserved.Wait(TestTimeout));
        manager.StartServer();
        Assert.Equal(1, factory.CreateCalls);

        Assert.True(manager.Shutdown(TestTimeout));
        await listener.AcceptExited.Task.WaitAsync(TestTimeout);
        Assert.Equal(3, listener.StopCalls);
        Assert.Equal(0, tracker.ActiveWorkers);
    }

    [Fact]
    public async Task TerminalCleanupFailureIsRetriedByNextShutdownCall()
    {
        var tracker = new SocketWorkerTracker();
        int port = GetAvailablePort();
        using var listener = new FailBeforeReleaseLoopbackListener(
            failuresBeforeRelease: 1,
            gateSuccessfulStop: true,
            port: port);
        var factory = new SingleListenerFactory(listener);
        SocketManager manager = CreateManager(tracker, factory, port);
        SocketServerLifecycle lifecycle = GetLifecycle(manager);

        manager.StartServer();
        Assert.True(listener.Started.Wait(TestTimeout));
        Assert.True(listener.AcceptEntered.Wait(TestTimeout));

        Assert.False(manager.Shutdown(TimeSpan.FromMilliseconds(30)));
        Assert.True(SpinWait.SpinUntil(
            () => listener.StopCalls == 1 && lifecycle.ShutdownException != null,
            TestTimeout));
        Assert.False(listener.AcceptExited.Task.IsCompleted);
        Assert.True(tracker.ActiveWorkers > 0);

        Task<bool> retry = Task.Run(() => manager.Shutdown(TestTimeout));
        try
        {
            Assert.True(listener.RetryStopEntered.Wait(TestTimeout));
            Assert.False(retry.IsCompleted);
            Assert.Equal(2, listener.StopCalls);
        }
        finally
        {
            listener.ReleaseRetryStop.Set();
        }

        Assert.True(await retry.WaitAsync(TestTimeout));
        await listener.AcceptExited.Task.WaitAsync(TestTimeout);
        Assert.Null(lifecycle.ShutdownException);
        Assert.Equal(0, tracker.ActiveWorkers);
        Assert.Equal(2, listener.StopCalls);

        var portProbe = new TcpListener(IPAddress.Loopback, port);
        try
        {
            portProbe.Start();
        }
        finally
        {
            portProbe.Stop();
        }

        Assert.True(manager.Shutdown(TimeSpan.Zero));
        Assert.Equal(2, listener.StopCalls);
    }

    [Fact]
    public async Task ConcurrentShutdownRequestQueuesRetryAfterGatedFailure()
    {
        var tracker = new SocketWorkerTracker();
        using var listener = new FailBeforeReleaseLoopbackListener(
            failuresBeforeRelease: 1,
            gateFirstFailure: true);
        var factory = new SingleListenerFactory(listener);
        SocketManager manager = CreateManager(tracker, factory);
        SocketServerLifecycle lifecycle = GetLifecycle(manager);

        manager.StartServer();
        Assert.True(listener.Started.Wait(TestTimeout));
        Assert.True(listener.AcceptEntered.Wait(TestTimeout));
        Task<bool> firstShutdown = Task.Run(() => manager.Shutdown(TestTimeout));
        Assert.True(listener.FirstFailureStopEntered.Wait(TestTimeout));

        Assert.False(manager.Shutdown(TimeSpan.Zero));
        listener.ReleaseFirstFailure.Set();

        Assert.True(await firstShutdown.WaitAsync(TestTimeout));
        await listener.AcceptExited.Task.WaitAsync(TestTimeout);
        Assert.Equal(2, listener.StopCalls);
        Assert.Equal(0, tracker.ActiveWorkers);
        Assert.Null(lifecycle.ShutdownException);
        Assert.True(manager.Shutdown(TimeSpan.Zero));
    }

    [Fact]
    public async Task ActiveLoopbackReadIsClosedAndAllWorkersFinishWithinBudget()
    {
        var tracker = new SocketWorkerTracker();
        using var listener = new LoopbackListener();
        var factory = new SingleListenerFactory(listener);
        var accepted = new TaskCompletionSource<SocketServerClient>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handlerEntered = new ManualResetEventSlim();
        using var handlerExited = new ManualResetEventSlim();
        SocketServerLifecycle? lifecycle = null;
        Thread? handlerThread = null;
        int closedClients = 0;
        lifecycle = new SocketServerLifecycle(
            SocketServerState.Stopped,
            factory,
            action => _ = Task.Run(action),
            tracker,
            _ => { },
            connection =>
            {
                Assert.True(tracker.TryRegister(out SocketWorkerLease? worker));
                handlerThread = SocketManager.CreateClientThread(() =>
                {
                    using (worker)
                    {
                        handlerEntered.Set();
                        try
                        {
                            _ = connection.Client.GetStream().ReadByte();
                        }
                        catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException)
                        {
                        }
                        finally
                        {
                            lifecycle.ReleaseClient(connection);
                            handlerExited.Set();
                        }
                    }
                });
                handlerThread.Start();
                accepted.SetResult(connection);
            },
            connection =>
            {
                Interlocked.Increment(ref closedClients);
                try
                {
                    connection.Client.Client.Shutdown(SocketShutdown.Both);
                }
                catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
                {
                }
                connection.Client.Dispose();
            });

        Assert.True(lifecycle.Start(new SocketServerSettings(
            IPAddress.Loopback.ToString(),
            0,
            4096,
            SocketPhraseType.Text,
            true)));
        Assert.True(listener.Started.Wait(TestTimeout));
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, listener.Port).WaitAsync(TestTimeout);
        _ = await accepted.Task.WaitAsync(TestTimeout);
        Assert.True(handlerEntered.Wait(TestTimeout));
        Assert.True(handlerThread!.IsBackground);

        Stopwatch stopwatch = Stopwatch.StartNew();
        lifecycle.BeginShutdown();
        Assert.True(tracker.Wait(TimeSpan.FromSeconds(2)));

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
        Assert.True(handlerExited.IsSet);
        Assert.Equal(0, tracker.ActiveWorkers);
        Assert.Equal(1, Volatile.Read(ref closedClients));
        Assert.Equal(1, listener.StopCalls);
        Assert.Null(lifecycle.ShutdownException);
        Assert.False(lifecycle.Start(new SocketServerSettings("127.0.0.1", 6500, 4096, SocketPhraseType.Json, true)));
        lifecycle.BeginShutdown();
        Assert.True(tracker.Wait(TimeSpan.Zero));

        byte[] buffer = new byte[1];
        try
        {
            int bytesRead = await client.GetStream().ReadAsync(buffer).AsTask().WaitAsync(TestTimeout);
            Assert.Equal(0, bytesRead);
        }
        catch (IOException)
        {
            // A reset is also a completed disconnect on Windows.
        }
    }

    private static SocketManager CreateManager(
        SocketWorkerTracker tracker,
        ISocketServerListenerFactory listenerFactory,
        int serverPort = 0,
        Action<Action>? queueShutdownWork = null,
        SocketConfig? config = null)
    {
        var messageManager = (SocketMessageManager)RuntimeHelpers.GetUninitializedObject(typeof(SocketMessageManager));
        var jsonDispatcher = (SocketJsonDispatcher)RuntimeHelpers.GetUninitializedObject(typeof(SocketJsonDispatcher));
        var textDispatcher = (SocketTextDispatcher)RuntimeHelpers.GetUninitializedObject(typeof(SocketTextDispatcher));
        config ??= new SocketConfig
        {
            IPAddress = IPAddress.Loopback.ToString(),
            ServerPort = serverPort,
            SocketBufferSize = 4096,
            SocketPhraseType = SocketPhraseType.Text,
            IsServerEnabled = true
        };
        return new SocketManager(
            config,
            listenerFactory,
            action => _ = Task.Run(action),
            tracker,
            jsonDispatcher,
            textDispatcher,
            messageManager,
            refreshNetworkAccessStatus: false,
            queueShutdownWork: queueShutdownWork);
    }

    private static SocketServerLifecycle GetLifecycle(SocketManager manager) =>
        Assert.IsType<SocketServerLifecycle>(typeof(SocketManager)
            .GetField("_serverLifecycle", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(manager));

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class SingleListenerFactory(ISocketServerListener listener) : ISocketServerListenerFactory
    {
        private int _created;

        public int CreateCalls => Volatile.Read(ref _created);

        public ISocketServerListener Create(SocketServerSettings settings)
        {
            Assert.Equal(1, Interlocked.Increment(ref _created));
            return listener;
        }
    }

    private sealed class SequencedListenerFactory(params ISocketServerListener[] listeners) : ISocketServerListenerFactory
    {
        private readonly Queue<ISocketServerListener> _listeners = new(listeners);
        private int _created;

        public int CreateCalls => Volatile.Read(ref _created);

        public ISocketServerListener Create(SocketServerSettings settings)
        {
            Interlocked.Increment(ref _created);
            lock (_listeners)
                return _listeners.Dequeue();
        }
    }

    private sealed class GatedLoopbackListener : ISocketServerListener, IDisposable
    {
        private readonly TcpListener _listener;
        private int _stopCalls;

        public GatedLoopbackListener(int port = 0, bool gateStart = false, bool gateStop = false)
        {
            _listener = new TcpListener(IPAddress.Loopback, port);
            if (!gateStart)
                ReleaseStart.Set();
            if (!gateStop)
                ReleaseStop.Set();
        }

        public ManualResetEventSlim StartEntered { get; } = new();
        public ManualResetEventSlim ReleaseStart { get; } = new();
        public ManualResetEventSlim Started { get; } = new();
        public ManualResetEventSlim StopEntered { get; } = new();
        public ManualResetEventSlim ReleaseStop { get; } = new();
        public int Port { get; private set; }
        public int StopCalls => Volatile.Read(ref _stopCalls);

        public void Start()
        {
            StartEntered.Set();
            if (!ReleaseStart.Wait(TestTimeout))
                throw new TimeoutException("The test did not release listener.Start.");
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Started.Set();
        }

        public TcpClient AcceptTcpClient() => _listener.AcceptTcpClient();

        public void Stop()
        {
            Interlocked.Increment(ref _stopCalls);
            StopEntered.Set();
            if (!ReleaseStop.Wait(TestTimeout))
                throw new TimeoutException("The test did not release listener.Stop.");
            _listener.Stop();
        }

        public void Dispose()
        {
            ReleaseStart.Set();
            ReleaseStop.Set();
            _listener.Stop();
        }
    }

    private sealed class RetryThenReplacementFactory : ISocketServerListenerFactory, IDisposable
    {
        private int _created;

        public RetryThenReplacementFactory(int port)
        {
            First = new FailBeforeReleaseLoopbackListener(
                failuresBeforeRelease: 1,
                gateSuccessfulStop: true,
                port: port);
        }

        public FailBeforeReleaseLoopbackListener First { get; }
        public LoopbackListener? Replacement { get; private set; }
        public ConcurrentQueue<SocketServerSettings> Settings { get; } = new();
        public int CreateCalls => Volatile.Read(ref _created);

        public ISocketServerListener Create(SocketServerSettings settings)
        {
            Settings.Enqueue(settings);
            int call = Interlocked.Increment(ref _created);
            if (call == 1)
                return First;
            if (call == 2)
                return Replacement = new LoopbackListener(First.Port);
            throw new InvalidOperationException("Unexpected listener generation.");
        }

        public void Dispose()
        {
            First.ReleaseRetryStop.Set();
            First.Dispose();
            Replacement?.Dispose();
        }
    }

    private sealed class FailBeforeReleaseLoopbackListener : ISocketServerListener, IDisposable
    {
        private readonly TcpListener _listener;
        private readonly int _failuresBeforeRelease;
        private readonly bool _gateFirstFailure;
        private readonly bool _gateSuccessfulStop;
        private int _stopCalls;

        public FailBeforeReleaseLoopbackListener(
            int failuresBeforeRelease,
            bool gateSuccessfulStop = false,
            bool gateFirstFailure = false,
            int port = 0)
        {
            _listener = new TcpListener(IPAddress.Loopback, port);
            _failuresBeforeRelease = failuresBeforeRelease;
            _gateSuccessfulStop = gateSuccessfulStop;
            _gateFirstFailure = gateFirstFailure;
            if (!gateFirstFailure)
                ReleaseFirstFailure.Set();
            if (!gateSuccessfulStop)
                ReleaseRetryStop.Set();
        }

        public ManualResetEventSlim Started { get; } = new();
        public ManualResetEventSlim AcceptEntered { get; } = new();
        public ManualResetEventSlim FirstFailureStopEntered { get; } = new();
        public ManualResetEventSlim ReleaseFirstFailure { get; } = new();
        public ManualResetEventSlim RetryStopEntered { get; } = new();
        public ManualResetEventSlim ReleaseRetryStop { get; } = new();
        public ManualResetEventSlim StopCompleted { get; } = new();
        public ManualResetEventSlim SecondFailureObserved { get; } = new();
        public TaskCompletionSource<bool> AcceptExited { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public int Port { get; private set; }
        public int StopCalls => Volatile.Read(ref _stopCalls);

        public void Start()
        {
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Started.Set();
        }

        public TcpClient AcceptTcpClient()
        {
            AcceptEntered.Set();
            try
            {
                return _listener.AcceptTcpClient();
            }
            finally
            {
                AcceptExited.TrySetResult(true);
            }
        }

        public void Stop()
        {
            int call = Interlocked.Increment(ref _stopCalls);
            if (call <= _failuresBeforeRelease)
            {
                if (call == 1)
                {
                    FirstFailureStopEntered.Set();
                    if (_gateFirstFailure && !ReleaseFirstFailure.Wait(TestTimeout))
                        throw new TimeoutException("The test did not release the first failing listener.Stop.");
                }
                if (call == 2)
                    SecondFailureObserved.Set();
                throw new InvalidOperationException($"simulated stop failure {call}");
            }

            RetryStopEntered.Set();
            if (_gateSuccessfulStop && !ReleaseRetryStop.Wait(TestTimeout))
                throw new TimeoutException("The test did not release the retrying listener.Stop.");
            _listener.Stop();
            StopCompleted.Set();
        }

        public void Dispose()
        {
            ReleaseFirstFailure.Set();
            ReleaseRetryStop.Set();
            _listener.Stop();
        }
    }

    private sealed class LoopbackListener : ISocketServerListener, IDisposable
    {
        private readonly TcpListener _listener;
        private int _stopCalls;

        public LoopbackListener(int port = 0)
        {
            _listener = new TcpListener(IPAddress.Loopback, port);
        }

        public ManualResetEventSlim Started { get; } = new();
        public int Port { get; private set; }
        public int StopCalls => Volatile.Read(ref _stopCalls);

        public void Start()
        {
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Started.Set();
        }

        public TcpClient AcceptTcpClient() => _listener.AcceptTcpClient();

        public void Stop()
        {
            Interlocked.Increment(ref _stopCalls);
            _listener.Stop();
        }

        public void Dispose() => _listener.Stop();
    }
}
