using ProjectARVRPro.Services;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace ProjectARVRPro.Tests;

public sealed class SocketRelayLifecycleTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task StopUnblocksAcceptAndReadAndWaitsForAllWorkers()
    {
        SocketRelayManager manager = CreateManager();
        using TcpClient flowClient = new();

        try
        {
            manager.StartServer("127.0.0.1", 0);
            IPEndPoint endpoint = await WaitForListeningEndpointAsync(manager);
            await flowClient.ConnectAsync(endpoint.Address, endpoint.Port);
            await WaitUntilAsync(() => manager.ActiveFlowConnectionId.HasValue);

            SocketRelayStopResult result = manager.StopServerAndWait(TimeSpan.FromSeconds(2));

            Assert.True(result.Completed);
            Assert.Equal(0, result.RemainingWorkerCount);
            Assert.Null(manager.ActiveGenerationId);
            Assert.False(manager.IsListening);
            Assert.False(manager.IsFlowConnected);
            await AssertConnectionClosedAsync(flowClient);
        }
        finally
        {
            manager.StopServerAndWait(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task ImmediateRestartWritesOnlyToTheCurrentGenerationConnection()
    {
        SocketRelayManager manager = CreateManager();
        using TcpClient oldFlowClient = new();
        using TcpClient currentFlowClient = new();

        try
        {
            manager.StartServer("127.0.0.1", 0);
            IPEndPoint oldEndpoint = await WaitForListeningEndpointAsync(manager);
            long oldGenerationId = Assert.IsType<long>(manager.ActiveGenerationId);
            await oldFlowClient.ConnectAsync(oldEndpoint.Address, oldEndpoint.Port);
            await WaitUntilAsync(() => manager.ActiveFlowConnectionId.HasValue);

            manager.StartServer("127.0.0.1", 0);

            IPEndPoint currentEndpoint = await WaitForListeningEndpointAsync(manager);
            long currentGenerationId = Assert.IsType<long>(manager.ActiveGenerationId);
            Assert.NotEqual(oldGenerationId, currentGenerationId);
            await AssertConnectionClosedAsync(oldFlowClient);

            await currentFlowClient.ConnectAsync(currentEndpoint.Address, currentEndpoint.Port);
            await WaitUntilAsync(() => manager.ActiveFlowConnectionId.HasValue && manager.IsFlowConnected);

            const string payload = "generation-two";
            manager.ForwardToFlow(payload);

            Assert.Equal(payload, await ReadTextAsync(currentFlowClient.GetStream(), payload.Length));
        }
        finally
        {
            manager.StopServerAndWait(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task QueuedOldGenerationStateCannotOverwriteRestartedServerState()
    {
        QueuedStateDispatcher dispatcher = new();
        SocketRelayManager manager = CreateManager(dispatcher.Enqueue);

        try
        {
            manager.StartServer("127.0.0.1", 0);
            await WaitForListeningEndpointAsync(manager);
            await WaitUntilAsync(() =>
            {
                dispatcher.RunAll();
                return manager.IsListening;
            });
            Assert.True(manager.IsListening);

            long oldGenerationId = Assert.IsType<long>(manager.ActiveGenerationId);
            Assert.True(manager.StopServerAndWait(TimeSpan.FromSeconds(2)).Completed);
            Action[] staleStopActions = dispatcher.Drain();
            Assert.NotEmpty(staleStopActions);

            manager.StartServer("127.0.0.1", 0);
            await WaitForListeningEndpointAsync(manager);
            await WaitUntilAsync(() =>
            {
                dispatcher.RunAll();
                return manager.IsListening;
            });

            Assert.NotEqual(oldGenerationId, Assert.IsType<long>(manager.ActiveGenerationId));
            Assert.True(manager.IsListening);

            foreach (Action staleStopAction in staleStopActions)
            {
                staleStopAction();
            }

            Assert.True(manager.IsListening);
        }
        finally
        {
            manager.StopServerAndWait(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task StopTimeoutCannotLetLateOldGenerationCallbacksOverwriteRestartedState()
    {
        using ManualResetEventSlim oldListeningEntered = new();
        using ManualResetEventSlim releaseOldListening = new();
        SocketRelayGeneration? oldGeneration = null;
        int generationNumber = 0;

        SocketRelayGeneration Factory(IPAddress address, int port)
        {
            SocketRelayGeneration generation = new(address, port);
            if (Interlocked.Increment(ref generationNumber) == 1)
            {
                oldGeneration = generation;
                generation.Listening += _ =>
                {
                    oldListeningEntered.Set();
                    releaseOldListening.Wait(TestTimeout);
                };
            }

            return generation;
        }

        SocketRelayManager manager = new(
            new SocketRelayConfig(),
            action => action(),
            Factory);

        try
        {
            manager.StartServer("127.0.0.1", 0);
            Assert.True(oldListeningEntered.Wait(TestTimeout));
            long oldGenerationId = Assert.IsType<long>(manager.ActiveGenerationId);

            Stopwatch stopWatch = Stopwatch.StartNew();
            SocketRelayStopResult timedOutStop = manager.StopServerAndWait(TimeSpan.FromMilliseconds(100));
            stopWatch.Stop();

            Assert.False(timedOutStop.Completed);
            Assert.True(timedOutStop.RemainingWorkerCount >= 1);
            Assert.True(stopWatch.Elapsed < TimeSpan.FromSeconds(1));

            manager.StartServer("127.0.0.1", 0);
            await WaitUntilAsync(() => manager.IsListening && manager.ActiveGenerationId != oldGenerationId);

            releaseOldListening.Set();
            Assert.NotNull(oldGeneration);
            Assert.True(oldGeneration.StopAndWait(TimeSpan.FromSeconds(2)).Completed);

            Assert.True(manager.IsListening);
            Assert.NotEqual(oldGenerationId, Assert.IsType<long>(manager.ActiveGenerationId));
        }
        finally
        {
            releaseOldListening.Set();
            manager.StopServerAndWait(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task StopWaitIncludesReaderAlreadyRemovedFromActiveConnectionOwnership()
    {
        using ManualResetEventSlim listening = new();
        using ManualResetEventSlim connected = new();
        using ManualResetEventSlim disconnectCallbackEntered = new();
        using ManualResetEventSlim releaseDisconnectCallback = new();
        using TcpClient flowClient = new();
        using SocketRelayGeneration generation = new(IPAddress.Loopback, 0);

        generation.Listening += _ => listening.Set();
        generation.FlowConnected += (_, _) => connected.Set();
        generation.FlowDisconnected += (_, _) =>
        {
            disconnectCallbackEntered.Set();
            releaseDisconnectCallback.Wait(TestTimeout);
        };

        try
        {
            generation.Start();
            Assert.True(listening.Wait(TestTimeout));
            IPEndPoint endpoint = Assert.IsType<IPEndPoint>(generation.ListeningEndpoint);

            await flowClient.ConnectAsync(endpoint.Address, endpoint.Port);
            Assert.True(connected.Wait(TestTimeout));
            flowClient.Client.Shutdown(SocketShutdown.Both);
            flowClient.Close();
            Assert.True(disconnectCallbackEntered.Wait(TestTimeout));
            Assert.Equal(0, generation.ActiveConnectionCount);

            Stopwatch stopWatch = Stopwatch.StartNew();
            SocketRelayStopResult timedOutStop = generation.StopAndWait(TimeSpan.FromMilliseconds(100));
            stopWatch.Stop();

            Assert.False(timedOutStop.Completed);
            Assert.Equal(1, timedOutStop.RemainingWorkerCount);
            Assert.True(stopWatch.Elapsed < TimeSpan.FromSeconds(1));

            releaseDisconnectCallback.Set();
            SocketRelayStopResult completedStop = generation.StopAndWait(TimeSpan.FromSeconds(2));
            Assert.True(completedStop.Completed);
            Assert.Equal(0, completedStop.RemainingWorkerCount);
        }
        finally
        {
            releaseDisconnectCallback.Set();
            generation.StopAndWait(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task ReplacedReaderCannotClearTheNewConnectionState()
    {
        SocketRelayManager manager = CreateManager();
        using TcpClient firstFlowClient = new();
        using TcpClient secondFlowClient = new();

        try
        {
            manager.StartServer("127.0.0.1", 0);
            IPEndPoint endpoint = await WaitForListeningEndpointAsync(manager);

            await firstFlowClient.ConnectAsync(endpoint.Address, endpoint.Port);
            await WaitUntilAsync(() => manager.ActiveFlowConnectionId.HasValue && manager.IsFlowConnected);
            long firstConnectionId = Assert.IsType<long>(manager.ActiveFlowConnectionId);

            await secondFlowClient.ConnectAsync(endpoint.Address, endpoint.Port);
            await WaitUntilAsync(() => manager.ActiveFlowConnectionId is long id && id != firstConnectionId);
            await AssertConnectionClosedAsync(firstFlowClient);
            await WaitUntilAsync(() => manager.ActiveFlowReaderCount == 1);

            Assert.True(manager.IsFlowConnected);

            const string payload = "current-connection";
            manager.ForwardToFlow(payload);
            Assert.Equal(payload, await ReadTextAsync(secondFlowClient.GetStream(), payload.Length));
        }
        finally
        {
            manager.StopServerAndWait(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task ReentrantPropertySubscriberStartWinsWithoutLeavingOuterListener()
    {
        int generationCount = 0;
        SocketRelayRuntime runtime = new()
        {
            StateDispatcher = action => action(),
            GenerationFactory = (address, port) =>
            {
                Interlocked.Increment(ref generationCount);
                return new SocketRelayGeneration(address, port);
            }
        };
        SocketRelayManager manager = new(new SocketRelayConfig(), runtime);
        long reentrantGenerationId = 0;
        int restartRequested = 0;

        try
        {
            manager.StartServer("127.0.0.1", 0);
            await WaitUntilAsync(() => manager.IsListening);
            long originalGenerationId = Assert.IsType<long>(manager.ActiveGenerationId);

            manager.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName != nameof(SocketRelayManager.IsListening) ||
                    manager.IsListening ||
                    Interlocked.Exchange(ref restartRequested, 1) != 0)
                {
                    return;
                }

                Task reentrantStart = Task.Run(() =>
                {
                    manager.StartServer("127.0.0.1", 0);
                    Volatile.Write(ref reentrantGenerationId, Assert.IsType<long>(manager.ActiveGenerationId));
                });

                Assert.True(
                    reentrantStart.Wait(TimeSpan.FromSeconds(1)),
                    "PropertyChanged was invoked while the lifecycle lock still blocked a reentrant start.");
                reentrantStart.GetAwaiter().GetResult();
            };

            manager.StartServer("127.0.0.2", 0);

            await WaitUntilAsync(() =>
                Volatile.Read(ref reentrantGenerationId) != 0 &&
                manager.ActiveGenerationId == Volatile.Read(ref reentrantGenerationId) &&
                manager.IsListening);

            Assert.NotEqual(originalGenerationId, Volatile.Read(ref reentrantGenerationId));
            Assert.Equal(2, Volatile.Read(ref generationCount));
            Assert.Equal("127.0.0.1", manager.Config.ListenIP);
            Assert.True(manager.StopServerAndWait(TimeSpan.FromSeconds(2)).Completed);
        }
        finally
        {
            manager.StopServerAndWait(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task PublicStopDeadlineIncludesSynchronousStateNotification()
    {
        using ManualResetEventSlim listeningStoppedEntered = new();
        using ManualResetEventSlim releaseListeningStopped = new();
        SocketRelayGeneration? generation = null;

        SocketRelayRuntime runtime = new()
        {
            StateDispatcher = action => action(),
            GenerationFactory = (address, port) =>
            {
                generation = new SocketRelayGeneration(address, port);
                generation.ListeningStopped += _ =>
                {
                    listeningStoppedEntered.Set();
                    releaseListeningStopped.Wait(TestTimeout);
                };
                return generation;
            }
        };
        SocketRelayManager manager = new(new SocketRelayConfig(), runtime);
        int delayedNotification = 0;

        try
        {
            manager.StartServer("127.0.0.1", 0);
            await WaitUntilAsync(() => manager.IsListening);
            manager.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(SocketRelayManager.IsListening) &&
                    !manager.IsListening &&
                    Interlocked.Exchange(ref delayedNotification, 1) == 0)
                {
                    Thread.Sleep(200);
                }
            };

            Stopwatch stopwatch = Stopwatch.StartNew();
            SocketRelayStopResult result = manager.StopServerAndWait(TimeSpan.FromMilliseconds(250));
            stopwatch.Stop();

            Assert.True(listeningStoppedEntered.IsSet);
            Assert.False(result.Completed);
            Assert.True(result.RemainingWorkerCount >= 1);
            Assert.InRange(stopwatch.Elapsed, TimeSpan.FromMilliseconds(180), TimeSpan.FromMilliseconds(400));
        }
        finally
        {
            releaseListeningStopped.Set();
            manager.StopServerAndWait(TimeSpan.FromSeconds(2));
            if (generation != null)
            {
                Assert.True(generation.StopAndWait(TimeSpan.FromSeconds(2)).Completed);
            }
        }
    }

    [Theory]
    [InlineData("normal-flow-message")]
    [InlineData("1")]
    public async Task OldFlowCallbackCannotPublishExternalOrUiEffectsAfterReentrantRestart(string payload)
    {
        ConcurrentQueue<string> externalWrites = new();
        ConcurrentQueue<string> socketMessages = new();
        SocketRelayGeneration? oldGeneration = null;
        int generationCount = 0;
        SocketRelayRuntime runtime = new()
        {
            StateDispatcher = action => action(),
            GenerationFactory = (address, port) =>
            {
                SocketRelayGeneration generation = new(address, port);
                if (Interlocked.Increment(ref generationCount) == 1)
                {
                    oldGeneration = generation;
                }

                return generation;
            },
            ExternalClientWriter = message =>
            {
                externalWrites.Enqueue(message);
                return new SocketRelayWriteResult(SocketRelayWriteStatus.Sent);
            },
            SocketMessagePublisher = message => socketMessages.Enqueue(message.Content ?? string.Empty)
        };
        SocketRelayManager manager = new(new SocketRelayConfig(), runtime);
        using TcpClient oldFlowClient = new();
        long replacementGenerationId = 0;
        int restarted = 0;

        try
        {
            manager.StartServer("127.0.0.1", 0);
            IPEndPoint endpoint = await WaitForListeningEndpointAsync(manager);
            await oldFlowClient.ConnectAsync(endpoint.Address, endpoint.Port);
            await WaitUntilAsync(() => manager.ActiveFlowConnectionId.HasValue && manager.IsFlowConnected);
            long originalGenerationId = Assert.IsType<long>(manager.ActiveGenerationId);

            manager.MessageReceived += message =>
            {
                if (message.Direction != RelayMessageDirection.FlowToRelay ||
                    message.Content != payload ||
                    Interlocked.Exchange(ref restarted, 1) != 0)
                {
                    return;
                }

                manager.StartServer("127.0.0.1", 0);
                Volatile.Write(ref replacementGenerationId, manager.ActiveGenerationId ?? -1);
            };

            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            await oldFlowClient.GetStream().WriteAsync(bytes);

            await WaitUntilAsync(() =>
                Volatile.Read(ref replacementGenerationId) != 0 &&
                manager.ActiveGenerationId == Volatile.Read(ref replacementGenerationId) &&
                manager.IsListening);
            Assert.NotEqual(originalGenerationId, Volatile.Read(ref replacementGenerationId));
            Assert.NotNull(oldGeneration);
            Assert.True(oldGeneration.StopAndWait(TimeSpan.FromSeconds(2)).Completed);

            Assert.Empty(externalWrites);
            Assert.Empty(socketMessages);
            Assert.DoesNotContain(
                manager.Messages,
                message => message.Direction == RelayMessageDirection.RelayToClient && message.Content == payload);
        }
        finally
        {
            manager.StopServerAndWait(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task SensorResetRetriesCurrentGenerationWithoutApplyingOldResult()
    {
        TaskCompletionSource firstResetStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirstReset = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondResetStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseSecondReset = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ConcurrentQueue<string> prompts = new();
        int resetCallCount = 0;

        async Task<SocketRelaySensorResetResult> ResetSensorAsync()
        {
            int call = Interlocked.Increment(ref resetCallCount);
            if (call == 1)
            {
                firstResetStarted.SetResult();
                await releaseFirstReset.Task;
                return new SocketRelaySensorResetResult(true, "old-generation-warning");
            }

            secondResetStarted.SetResult();
            await releaseSecondReset.Task;
            return new SocketRelaySensorResetResult(true, "current-generation-warning");
        }

        SocketRelayRuntime runtime = new()
        {
            StateDispatcher = action => action(),
            SensorResetOperation = ResetSensorAsync,
            SensorResetPrompt = prompts.Enqueue
        };
        SocketRelayManager manager = new(new SocketRelayConfig(), runtime);

        try
        {
            manager.StartServer("127.0.0.1", 0);
            await firstResetStarted.Task.WaitAsync(TestTimeout);
            long oldGenerationId = Assert.IsType<long>(manager.ActiveGenerationId);

            manager.StartServer("127.0.0.1", 0);
            long currentGenerationId = Assert.IsType<long>(manager.ActiveGenerationId);
            Assert.NotEqual(oldGenerationId, currentGenerationId);
            await WaitUntilAsync(() =>
                manager.IsListening &&
                manager.PendingSensorResetGenerationId == currentGenerationId);

            releaseFirstReset.SetResult();
            await secondResetStarted.Task.WaitAsync(TestTimeout);

            Assert.Equal(currentGenerationId, manager.ActiveGenerationId);
            Assert.False(manager.ActiveGenerationSensorResetCompleted);
            Assert.DoesNotContain("old-generation-warning", prompts);

            releaseSecondReset.SetResult();
            await WaitUntilAsync(() => manager.ActiveGenerationSensorResetCompleted);

            Assert.Equal(2, Volatile.Read(ref resetCallCount));
            Assert.DoesNotContain("old-generation-warning", prompts);
            Assert.Contains("current-generation-warning", prompts);
        }
        finally
        {
            releaseFirstReset.TrySetResult();
            releaseSecondReset.TrySetResult();
            manager.StopServerAndWait(TimeSpan.FromSeconds(2));
        }
    }

    private static SocketRelayManager CreateManager(Action<Action>? stateDispatcher = null)
    {
        return new SocketRelayManager(new SocketRelayConfig(), stateDispatcher ?? (action => action()));
    }

    private static async Task<IPEndPoint> WaitForListeningEndpointAsync(SocketRelayManager manager)
    {
        await WaitUntilAsync(() => manager.ListeningEndpoint != null);
        return manager.ListeningEndpoint!;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.Elapsed >= TestTimeout)
            {
                throw new TimeoutException("Timed out waiting for the socket relay test condition.");
            }

            await Task.Delay(10);
        }
    }

    private static async Task<string> ReadTextAsync(NetworkStream stream, int expectedByteCount)
    {
        byte[] buffer = new byte[expectedByteCount];
        int totalRead = 0;
        using CancellationTokenSource timeout = new(TestTimeout);

        while (totalRead < buffer.Length)
        {
            int bytesRead = await stream.ReadAsync(buffer.AsMemory(totalRead), timeout.Token);
            if (bytesRead == 0)
            {
                break;
            }

            totalRead += bytesRead;
        }

        return Encoding.UTF8.GetString(buffer, 0, totalRead);
    }

    private static async Task AssertConnectionClosedAsync(TcpClient client)
    {
        byte[] buffer = new byte[1];
        using CancellationTokenSource timeout = new(TestTimeout);

        try
        {
            int bytesRead = await client.GetStream().ReadAsync(buffer, timeout.Token);
            Assert.Equal(0, bytesRead);
        }
        catch (IOException)
        {
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private sealed class QueuedStateDispatcher
    {
        private readonly object _lock = new();
        private readonly List<Action> _actions = [];

        internal int Count
        {
            get
            {
                lock (_lock)
                {
                    return _actions.Count;
                }
            }
        }

        internal void Enqueue(Action action)
        {
            lock (_lock)
            {
                _actions.Add(action);
            }
        }

        internal Action[] Drain()
        {
            lock (_lock)
            {
                Action[] actions = _actions.ToArray();
                _actions.Clear();
                return actions;
            }
        }

        internal void RunAll()
        {
            foreach (Action action in Drain())
            {
                action();
            }
        }
    }
}
