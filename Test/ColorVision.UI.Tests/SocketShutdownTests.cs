using ColorVision.SocketProtocol;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
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
        ISocketServerListenerFactory listenerFactory)
    {
        var messageManager = (SocketMessageManager)RuntimeHelpers.GetUninitializedObject(typeof(SocketMessageManager));
        var jsonDispatcher = (SocketJsonDispatcher)RuntimeHelpers.GetUninitializedObject(typeof(SocketJsonDispatcher));
        var textDispatcher = (SocketTextDispatcher)RuntimeHelpers.GetUninitializedObject(typeof(SocketTextDispatcher));
        var config = new SocketConfig
        {
            IPAddress = IPAddress.Loopback.ToString(),
            ServerPort = 0,
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
            refreshNetworkAccessStatus: false);
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

    private sealed class LoopbackListener : ISocketServerListener, IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private int _stopCalls;

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
