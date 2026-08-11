using ColorVision.SocketProtocol;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace ColorVision.UI.Tests;

public sealed class SocketServerLifecycleTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task NormalStartIgnoresDuplicateAndStopsCleanly()
    {
        var work = new ManualWorkQueue();
        var listener = new BlockingListener();
        var factory = new SequencedListenerFactory(listener);
        var transitions = new ConcurrentQueue<SocketServerTransition>();
        var lifecycle = CreateLifecycle(work, factory, transitions);
        SocketServerSettings settings = CreateSettings();

        Assert.True(lifecycle.Start(settings));
        Assert.False(lifecycle.Start(settings with { ServerPort = 7777 }));
        Assert.Equal(SocketServerState.Starting, lifecycle.State);
        Assert.Equal(1, work.Count);

        Task serverWorker = Task.Run(work.Dequeue());
        Assert.True(listener.Started.Wait(TestTimeout));
        Assert.True(listener.AcceptEntered.Wait(TestTimeout));
        Assert.Equal(SocketServerState.Running, lifecycle.State);
        Assert.Equal(1, factory.CreateCalls);

        Assert.True(lifecycle.Stop(isServerEnabled: true));
        Assert.Equal(SocketServerState.Stopping, lifecycle.State);
        work.RunNext();
        await serverWorker.WaitAsync(TestTimeout);

        Assert.Equal(SocketServerState.Stopped, lifecycle.State);
        Assert.Equal(1, listener.StartCalls);
        Assert.Equal(1, listener.StopCalls);
        Assert.Equal(
            [SocketServerState.Starting, SocketServerState.Running, SocketServerState.Stopping, SocketServerState.Stopped],
            transitions.Select(item => item.State));
    }

    [Fact]
    public void StopBeforeQueuedStartPreventsListenerCreation()
    {
        var work = new ManualWorkQueue();
        var factory = new SequencedListenerFactory(new BlockingListener());
        var transitions = new ConcurrentQueue<SocketServerTransition>();
        var lifecycle = CreateLifecycle(work, factory, transitions);

        Assert.True(lifecycle.Start(CreateSettings()));
        Assert.True(lifecycle.Stop(isServerEnabled: false));

        work.RunNext();
        work.RunNext();

        Assert.Equal(0, factory.CreateCalls);
        Assert.Equal(SocketServerState.Disabled, lifecycle.State);
        Assert.Equal(
            [SocketServerState.Starting, SocketServerState.Stopping, SocketServerState.Disabled],
            transitions.Select(item => item.State));
    }

    [Fact]
    public async Task StartStopStartRunsOnlyTheNewestQueuedGeneration()
    {
        var work = new ManualWorkQueue();
        var listener = new BlockingListener();
        var factory = new SequencedListenerFactory(listener);
        var transitions = new ConcurrentQueue<SocketServerTransition>();
        var lifecycle = CreateLifecycle(work, factory, transitions);

        Assert.True(lifecycle.Start(CreateSettings(6001)));
        Assert.True(lifecycle.Stop(isServerEnabled: true));
        Assert.True(lifecycle.Start(CreateSettings(6002)));

        work.RunNext();
        work.RunNext();
        Assert.Equal(SocketServerState.Starting, lifecycle.State);
        Assert.Equal(0, factory.CreateCalls);

        Task currentWorker = Task.Run(work.Dequeue());
        Assert.True(listener.Started.Wait(TestTimeout));
        Assert.True(listener.AcceptEntered.Wait(TestTimeout));
        Assert.Equal(SocketServerState.Running, lifecycle.State);
        Assert.Equal(6002, Assert.Single(factory.Settings).ServerPort);
        Assert.DoesNotContain(transitions, item => item.State == SocketServerState.Error);

        lifecycle.Stop(isServerEnabled: true);
        work.RunNext();
        await currentWorker.WaitAsync(TestTimeout);
    }

    [Fact]
    public async Task RunningStartStopStartReleasesOldListenerBeforeNewBind()
    {
        var work = new ManualWorkQueue();
        var oldListener = new BlockingListener();
        var currentListener = new BlockingListener();
        var factory = new SequencedListenerFactory(oldListener, currentListener);
        var transitions = new ConcurrentQueue<SocketServerTransition>();
        var lifecycle = CreateLifecycle(work, factory, transitions);

        lifecycle.Start(CreateSettings(6050));
        Task oldWorker = Task.Run(work.Dequeue());
        Assert.True(oldListener.Started.Wait(TestTimeout));
        Assert.True(oldListener.AcceptEntered.Wait(TestTimeout));

        lifecycle.Stop(isServerEnabled: true);
        Assert.Equal(1, oldListener.StopCalls);
        lifecycle.Start(CreateSettings(6050));

        Action oldStopCleanup = work.Dequeue();
        Task currentWorker = Task.Run(work.Dequeue());
        Assert.True(currentListener.Started.Wait(TestTimeout));
        Assert.True(currentListener.AcceptEntered.Wait(TestTimeout));
        await oldWorker.WaitAsync(TestTimeout);
        Assert.Equal(SocketServerState.Running, lifecycle.State);

        oldStopCleanup();
        Assert.Equal(SocketServerState.Running, lifecycle.State);
        Assert.DoesNotContain(transitions, item => item.State == SocketServerState.Error);

        lifecycle.Stop(isServerEnabled: true);
        work.RunNext();
        await currentWorker.WaitAsync(TestTimeout);
    }

    [Fact]
    public async Task OldWorkerFailureCannotOverwriteNewRunningState()
    {
        var work = new ManualWorkQueue();
        var oldListener = new GatedFailingStartListener();
        var currentListener = new BlockingListener();
        var factory = new SequencedListenerFactory(oldListener, currentListener);
        var transitions = new ConcurrentQueue<SocketServerTransition>();
        var lifecycle = CreateLifecycle(work, factory, transitions);

        lifecycle.Start(CreateSettings(6101));
        Task oldWorker = Task.Run(work.Dequeue());
        Assert.True(oldListener.StartEntered.Wait(TestTimeout));

        long startVersion = lifecycle.OperationVersion;
        Task<bool> stopCall = Task.Run(() => lifecycle.Stop(isServerEnabled: true));
        Assert.True(SpinWait.SpinUntil(() => lifecycle.OperationVersion > startVersion, TestTimeout));
        oldListener.ReleaseStart.Set();
        Assert.True(await stopCall.WaitAsync(TestTimeout));
        lifecycle.Start(CreateSettings(6102));
        await oldWorker.WaitAsync(TestTimeout);

        work.RunNext();
        Assert.Equal(SocketServerState.Starting, lifecycle.State);

        Task currentWorker = Task.Run(work.Dequeue());
        Assert.True(currentListener.Started.Wait(TestTimeout));
        Assert.True(currentListener.AcceptEntered.Wait(TestTimeout));
        Assert.Equal(SocketServerState.Running, lifecycle.State);
        Assert.DoesNotContain(transitions, item => item.State == SocketServerState.Error);

        lifecycle.Stop(isServerEnabled: true);
        work.RunNext();
        await currentWorker.WaitAsync(TestTimeout);
    }

    [Fact]
    public void BindFailureTransitionsToErrorWithAttemptedSettings()
    {
        var work = new ManualWorkQueue();
        var bindException = new SocketException((int)SocketError.AddressAlreadyInUse);
        var listener = new FailingStartListener(bindException);
        var factory = new SequencedListenerFactory(listener);
        var transitions = new ConcurrentQueue<SocketServerTransition>();
        var lifecycle = CreateLifecycle(work, factory, transitions);
        SocketServerSettings settings = CreateSettings(6201);

        lifecycle.Start(settings);
        work.RunNext();

        Assert.Equal(SocketServerState.Error, lifecycle.State);
        SocketServerTransition failure = Assert.Single(transitions, item => item.State == SocketServerState.Error);
        Assert.Same(bindException, failure.Exception);
        Assert.Equal(SocketServerFailureStage.Start, failure.FailureStage);
        Assert.Equal(settings, failure.Settings);
        Assert.Equal(1, listener.StopCalls);
    }

    [Fact]
    public void InlineStartPreservesSynchronousCompatibilityWithoutQueueingWorker()
    {
        var work = new ManualWorkQueue();
        var listener = new FailingStartListener(new InvalidOperationException("simulated inline failure"));
        var factory = new SequencedListenerFactory(listener);
        var transitions = new ConcurrentQueue<SocketServerTransition>();
        var lifecycle = CreateLifecycle(work, factory, transitions);

        Assert.True(lifecycle.StartInline(CreateSettings()));

        Assert.Equal(0, work.Count);
        Assert.Equal(SocketServerState.Error, lifecycle.State);
        Assert.Single(transitions, item => item.State == SocketServerState.Error);
    }

    [Fact]
    public async Task QueuedWorkerUsesConfigSnapshotAndStopClearsRegisteredClients()
    {
        var work = new ManualWorkQueue();
        using var client = new TcpClient();
        var listener = new BlockingListener(client);
        var factory = new SequencedListenerFactory(listener);
        var accepted = new TaskCompletionSource<SocketServerClient>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closedClients = new ConcurrentQueue<SocketServerClient>();
        var config = new SocketConfig
        {
            IPAddress = "127.0.0.1",
            ServerPort = 6301,
            SocketBufferSize = 2048,
            SocketPhraseType = SocketPhraseType.Text,
            IsServerEnabled = true
        };
        var lifecycle = new SocketServerLifecycle(
            SocketServerState.Stopped,
            factory,
            work.Enqueue,
            _ => { },
            connection => accepted.SetResult(connection),
            closedClients.Enqueue);

        lifecycle.Start(SocketServerSettings.Capture(config));
        config.IPAddress = "0.0.0.0";
        config.ServerPort = 6302;
        config.SocketBufferSize = 8192;
        config.SocketPhraseType = SocketPhraseType.Json;

        Task serverWorker = Task.Run(work.Dequeue());
        SocketServerClient connection = await accepted.Task.WaitAsync(TestTimeout);
        Assert.Equal("127.0.0.1", connection.Settings.IPAddress);
        Assert.Equal(6301, connection.Settings.ServerPort);
        Assert.Equal(2048, connection.Settings.SocketBufferSize);
        Assert.Equal(SocketPhraseType.Text, connection.Settings.SocketPhraseType);
        Assert.Equal(connection.Settings, Assert.Single(factory.Settings));
        Assert.Equal(1, connection.Session.ClientCount);

        lifecycle.Stop(isServerEnabled: false);
        work.RunNext();
        await serverWorker.WaitAsync(TestTimeout);

        Assert.Equal(SocketServerState.Disabled, lifecycle.State);
        Assert.Equal(0, connection.Session.ClientCount);
        Assert.Same(client, Assert.Single(closedClients).Client);
        lifecycle.ReleaseClient(connection);
        Assert.Single(closedClients);
    }

    [Fact]
    public async Task StopFailureStillClearsClientsAndTransitionsToError()
    {
        var work = new ManualWorkQueue();
        using var client = new TcpClient();
        var stopException = new InvalidOperationException("simulated stop failure");
        var listener = new BlockingListener(client, stopException);
        var factory = new SequencedListenerFactory(listener);
        var accepted = new TaskCompletionSource<SocketServerClient>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closedClients = new ConcurrentQueue<SocketServerClient>();
        var transitions = new ConcurrentQueue<SocketServerTransition>();
        var lifecycle = new SocketServerLifecycle(
            SocketServerState.Stopped,
            factory,
            work.Enqueue,
            transitions.Enqueue,
            connection => accepted.SetResult(connection),
            closedClients.Enqueue);

        lifecycle.Start(CreateSettings());
        Task serverWorker = Task.Run(work.Dequeue());
        SocketServerClient connection = await accepted.Task.WaitAsync(TestTimeout);

        lifecycle.Stop(isServerEnabled: true);
        work.RunNext();
        await serverWorker.WaitAsync(TestTimeout);

        Assert.Equal(SocketServerState.Error, lifecycle.State);
        Assert.Equal(0, connection.Session.ClientCount);
        Assert.Same(client, Assert.Single(closedClients).Client);
        SocketServerTransition failure = Assert.Single(transitions, item => item.State == SocketServerState.Error);
        Assert.Same(stopException, failure.Exception);
        Assert.Equal(SocketServerFailureStage.Stop, failure.FailureStage);
    }

    private static SocketServerLifecycle CreateLifecycle(
        ManualWorkQueue work,
        ISocketServerListenerFactory factory,
        ConcurrentQueue<SocketServerTransition> transitions) => new(
            SocketServerState.Stopped,
            factory,
            work.Enqueue,
            transitions.Enqueue,
            _ => { },
            connection => connection.Client.Dispose());

    private static SocketServerSettings CreateSettings(int port = 6000) => new(
        "127.0.0.1",
        port,
        4096,
        SocketPhraseType.Json,
        true);

    private sealed class ManualWorkQueue
    {
        private readonly Queue<Action> _actions = new();

        public int Count
        {
            get
            {
                lock (_actions)
                    return _actions.Count;
            }
        }

        public void Enqueue(Action action)
        {
            lock (_actions)
                _actions.Enqueue(action);
        }

        public Action Dequeue()
        {
            lock (_actions)
                return _actions.Dequeue();
        }

        public void RunNext() => Dequeue()();
    }

    private sealed class SequencedListenerFactory(params ISocketServerListener[] listeners) : ISocketServerListenerFactory
    {
        private readonly Queue<ISocketServerListener> _listeners = new(listeners);
        private int _createCalls;

        public int CreateCalls => Volatile.Read(ref _createCalls);
        public ConcurrentQueue<SocketServerSettings> Settings { get; } = new();

        public ISocketServerListener Create(SocketServerSettings settings)
        {
            Interlocked.Increment(ref _createCalls);
            Settings.Enqueue(settings);
            lock (_listeners)
                return _listeners.Dequeue();
        }
    }

    private sealed class BlockingListener : ISocketServerListener
    {
        private readonly ConcurrentQueue<TcpClient> _clients = new();
        private readonly ManualResetEventSlim _stopped = new();
        private readonly Exception? _stopException;
        private int _startCalls;
        private int _stopCalls;

        public BlockingListener(TcpClient? client = null, Exception? stopException = null)
        {
            if (client != null)
                _clients.Enqueue(client);
            _stopException = stopException;
        }

        public ManualResetEventSlim Started { get; } = new();
        public ManualResetEventSlim AcceptEntered { get; } = new();
        public int StartCalls => Volatile.Read(ref _startCalls);
        public int StopCalls => Volatile.Read(ref _stopCalls);

        public void Start()
        {
            Interlocked.Increment(ref _startCalls);
            Started.Set();
        }

        public TcpClient AcceptTcpClient()
        {
            AcceptEntered.Set();
            if (_clients.TryDequeue(out TcpClient? client))
                return client;

            if (!_stopped.Wait(TestTimeout))
                throw new TimeoutException("The fake listener was not stopped.");

            throw new ObjectDisposedException(nameof(BlockingListener));
        }

        public void Stop()
        {
            Interlocked.Increment(ref _stopCalls);
            _stopped.Set();
            if (_stopException != null)
                throw _stopException;
        }
    }

    private sealed class FailingStartListener(Exception exception) : ISocketServerListener
    {
        private int _stopCalls;

        public int StopCalls => Volatile.Read(ref _stopCalls);
        public void Start() => throw exception;
        public TcpClient AcceptTcpClient() => throw new NotSupportedException();
        public void Stop() => Interlocked.Increment(ref _stopCalls);
    }

    private sealed class GatedFailingStartListener : ISocketServerListener
    {
        public ManualResetEventSlim StartEntered { get; } = new();
        public ManualResetEventSlim ReleaseStart { get; } = new();

        public void Start()
        {
            StartEntered.Set();
            if (!ReleaseStart.Wait(TestTimeout))
                throw new TimeoutException("The test did not release listener startup.");

            throw new InvalidOperationException("simulated stale start failure");
        }

        public TcpClient AcceptTcpClient() => throw new NotSupportedException();
        public void Stop() { }
    }
}
