using ColorVision.SocketProtocol;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class SocketManagerConfigReloadTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task DifferentEndpointReloadClosesC1AndStartsC2WithCompleteSettings()
    {
        int c1Port = GetAvailablePort();
        int c2Port = GetDifferentAvailablePort(c1Port);
        string rootDirectory = Path.Combine(Path.GetTempPath(), $"ColorVisionSocketReload-{Guid.NewGuid():N}");
        string configPath = Path.Combine(rootDirectory, "ColorVisionConfig.json");
        Directory.CreateDirectory(rootDirectory);
        try
        {
            WriteConfig(configPath, CreateConfig(c1Port, 4096, SocketPhraseType.Text, isEnabled: true));
            var configHandler = new ConfigHandler { ConfigFilePath = configPath };
            configHandler.LoadConfigs();
            SocketConfig c1 = configHandler.GetRequiredService<SocketConfig>();
            using var factory = new RecordingLoopbackFactory();
            SocketManager manager = CreateManager(c1, factory);
            ConfigReloadResult initialBinding = configHandler.RegisterReloadParticipants(manager);
            Assert.True(initialBinding.Succeeded, initialBinding.BuildFailureSummary());

            await new SocketInitializer(() => manager).InitializeAsync();
            RealLoopbackListener first = await factory.WaitForListenerAsync(0);
            Assert.True(await CanConnectAsync(c1Port));

            WriteConfig(configPath, CreateConfig(c2Port, 8192, SocketPhraseType.Json, isEnabled: true));
            ConfigReloadResult reloadResult = configHandler.LoadConfigsWithResult();
            SocketConfig c2 = configHandler.GetRequiredService<SocketConfig>();

            Assert.True(reloadResult.Succeeded, reloadResult.BuildFailureSummary());
            RealLoopbackListener second = await factory.WaitForListenerAsync(1);
            Assert.NotSame(c1, c2);
            Assert.Same(c2, manager.Config);
            Assert.False(await EventuallyCanConnectAsync(c1Port));
            Assert.True(await CanConnectAsync(c2Port));
            Assert.Equal(1, first.StopCalls);
            Assert.True(second.Started.IsSet);
            SocketServerSettings applied = factory.Settings.ToArray()[1];
            Assert.Equal(c2Port, applied.ServerPort);
            Assert.Equal(8192, applied.SocketBufferSize);
            Assert.Equal(SocketPhraseType.Json, applied.SocketPhraseType);
            Assert.True(applied.IsServerEnabled);
            Assert.True(manager.Shutdown(TestTimeout));
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
                Directory.Delete(rootDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task SameEndpointReloadDefersC2UntilFailedC1StopReleasesPort()
    {
        int port = GetAvailablePort();
        var c1 = CreateConfig(port, 4096, SocketPhraseType.Text, isEnabled: true);
        var c2 = CreateConfig(port, 12288, SocketPhraseType.Json, isEnabled: true);
        using var factory = new RetryThenReplacementLoopbackFactory(port);
        SocketManager manager = CreateManager(c1, factory);
        await new SocketInitializer(() => manager).InitializeAsync();
        Assert.True(factory.First.Started.Wait(TestTimeout));

        manager.BindCurrentConfig(new SocketConfigService(c2));

        Assert.True(factory.First.RetryStopEntered.Wait(TestTimeout));
        Assert.Equal(1, factory.CreateCalls);
        Assert.Null(factory.Replacement);
        Assert.Same(c2, manager.Config);

        factory.First.ReleaseRetryStop.Set();
        Assert.True(factory.First.StopCompleted.Wait(TestTimeout));
        Assert.True(SpinWait.SpinUntil(() => factory.Replacement?.Started.IsSet == true, TestTimeout));
        Assert.True(await CanConnectAsync(port));
        SocketServerSettings applied = factory.Settings.ToArray()[1];
        Assert.Equal(port, applied.ServerPort);
        Assert.Equal(12288, applied.SocketBufferSize);
        Assert.Equal(SocketPhraseType.Json, applied.SocketPhraseType);
        Assert.True(manager.Shutdown(TestTimeout));
    }

    [Fact]
    public async Task DisabledC2AndOldC1ToggleCannotReviveOldRuntimeButC2ToggleCanStart()
    {
        int c1Port = GetAvailablePort();
        int c2Port = GetDifferentAvailablePort(c1Port);
        var c1 = CreateConfig(c1Port, 4096, SocketPhraseType.Text, isEnabled: true);
        var c2 = CreateConfig(c2Port, 16384, SocketPhraseType.Json, isEnabled: false);
        using var factory = new RecordingLoopbackFactory();
        SocketManager manager = CreateManager(c1, factory);
        await new SocketInitializer(() => manager).InitializeAsync();
        _ = await factory.WaitForListenerAsync(0);

        manager.BindCurrentConfig(new SocketConfigService(c2));

        Assert.True(SpinWait.SpinUntil(
            () => manager.ServerState == SocketServerState.Disabled,
            TestTimeout));
        Assert.False(await EventuallyCanConnectAsync(c1Port));
        Assert.Equal(1, factory.CreateCalls);

        c1.IsServerEnabled = false;
        c1.IsServerEnabled = true;
        Assert.Equal(1, factory.CreateCalls);
        Assert.Same(c2, manager.Config);
        Assert.Equal(SocketServerState.Disabled, manager.ServerState);

        c2.IsServerEnabled = true;
        _ = await factory.WaitForListenerAsync(1);
        Assert.True(await CanConnectAsync(c2Port));
        SocketServerSettings applied = factory.Settings.ToArray()[1];
        Assert.Equal(16384, applied.SocketBufferSize);
        Assert.Equal(SocketPhraseType.Json, applied.SocketPhraseType);

        c2.IsServerEnabled = false;
        Assert.True(SpinWait.SpinUntil(
            () => manager.ServerState == SocketServerState.Disabled,
            TestTimeout));
        Assert.True(manager.Shutdown(TestTimeout));
    }

    [Fact]
    public async Task TerminalShutdownCancelsPendingReloadStartWithoutWaitingForConfigLock()
    {
        int port = GetAvailablePort();
        var c1 = CreateConfig(port, 4096, SocketPhraseType.Text, isEnabled: true);
        var c2 = CreateConfig(port, 8192, SocketPhraseType.Json, isEnabled: true);
        using var factory = new RetryThenReplacementLoopbackFactory(port);
        SocketManager manager = CreateManager(c1, factory);
        await new SocketInitializer(() => manager).InitializeAsync();
        Assert.True(factory.First.Started.Wait(TestTimeout));

        manager.BindCurrentConfig(new SocketConfigService(c2));
        Assert.True(factory.First.RetryStopEntered.Wait(TestTimeout));
        Assert.Equal(1, factory.CreateCalls);

        manager.BeginShutdown();
        factory.First.ReleaseRetryStop.Set();

        Assert.True(manager.Shutdown(TestTimeout));
        Assert.True(factory.First.StopCompleted.Wait(TestTimeout));
        Assert.Equal(1, factory.CreateCalls);
        Assert.Null(factory.Replacement);
        Assert.False(await EventuallyCanConnectAsync(port));
    }

    [Fact]
    public async Task BackgroundBindPublishesConfigOnWpfDispatcherAndInitializerRemainsStartBoundary()
    {
        int uiThreadId = WpfTestHost.Invoke(() => Environment.CurrentManagedThreadId);
        int port = GetAvailablePort();
        var c1 = CreateConfig(port, 4096, SocketPhraseType.Text, isEnabled: false);
        var c2 = CreateConfig(port, 8192, SocketPhraseType.Json, isEnabled: true);
        using var factory = new RecordingLoopbackFactory();
        SocketManager manager = CreateManager(c1, factory);
        var published = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SocketManager.Config))
                published.TrySetResult(Environment.CurrentManagedThreadId);
        };

        await Task.Run(() => manager.BindCurrentConfig(new SocketConfigService(c2)));

        Assert.Equal(uiThreadId, await published.Task.WaitAsync(TestTimeout));
        Assert.Same(c2, manager.Config);
        Assert.Equal(0, factory.CreateCalls);

        await new SocketInitializer(() => manager).InitializeAsync();
        _ = await factory.WaitForListenerAsync(0);
        Assert.True(await CanConnectAsync(port));
        Assert.Equal(350, manager.ConfigReloadOrder);
        Assert.True(manager.Shutdown(TestTimeout));
    }

    [Fact]
    public async Task ResolutionFailureStopsRuntimeAndSameReferenceRecoveryReattachesOwner()
    {
        int port = GetAvailablePort();
        var config = CreateConfig(port, 4096, SocketPhraseType.Text, isEnabled: true);
        using var factory = new RecordingLoopbackFactory();
        SocketManager manager = CreateManager(config, factory);
        await new SocketInitializer(() => manager).InitializeAsync();
        _ = await factory.WaitForListenerAsync(0);

        AggregateException failure = Assert.Throws<AggregateException>(
            () => manager.BindCurrentConfig(new FailingSocketConfigService()));

        Assert.Contains(
            failure.InnerExceptions,
            exception => exception.Message.Contains("simulated config resolution failure", StringComparison.Ordinal));
        Assert.False(manager.HasUsableConfig);
        Assert.False(await EventuallyCanConnectAsync(port));

        manager.BindCurrentConfig(new SocketConfigService(config));

        _ = await factory.WaitForListenerAsync(1);
        Assert.True(manager.HasUsableConfig);
        Assert.True(await CanConnectAsync(port));
        config.IsServerEnabled = false;
        Assert.True(SpinWait.SpinUntil(
            () => manager.ServerState == SocketServerState.Disabled,
            TestTimeout));
        Assert.True(manager.Shutdown(TestTimeout));
    }

    private static SocketConfig CreateConfig(
        int port,
        int bufferSize,
        SocketPhraseType phraseType,
        bool isEnabled) => new()
        {
            IPAddress = IPAddress.Loopback.ToString(),
            ServerPort = port,
            SocketBufferSize = bufferSize,
            SocketPhraseType = phraseType,
            IsServerEnabled = isEnabled,
        };

    private static void WriteConfig(string path, SocketConfig config)
    {
        var writer = new ConfigHandler { ConfigFilePath = path };
        writer.Configs[typeof(SocketConfig)] = config;
        writer.SaveConfigs();
    }

    private static SocketManager CreateManager(SocketConfig config, ISocketServerListenerFactory listenerFactory)
    {
        var tracker = new SocketWorkerTracker();
        var messageManager = (SocketMessageManager)RuntimeHelpers.GetUninitializedObject(typeof(SocketMessageManager));
        var jsonDispatcher = (SocketJsonDispatcher)RuntimeHelpers.GetUninitializedObject(typeof(SocketJsonDispatcher));
        var textDispatcher = (SocketTextDispatcher)RuntimeHelpers.GetUninitializedObject(typeof(SocketTextDispatcher));
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

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static int GetDifferentAvailablePort(int excludedPort)
    {
        int port;
        do
        {
            port = GetAvailablePort();
        }
        while (port == excludedPort);
        return port;
    }

    private static async Task<bool> CanConnectAsync(int port)
    {
        using var client = new TcpClient();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        try
        {
            await client.ConnectAsync(IPAddress.Loopback, port, cancellation.Token);
            return true;
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            return false;
        }
    }

    private static async Task<bool> EventuallyCanConnectAsync(int port)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(1);
        do
        {
            if (await CanConnectAsync(port))
                return true;
            await Task.Delay(20);
        }
        while (DateTime.UtcNow < deadline);
        return false;
    }

    private sealed class SocketConfigService(SocketConfig config) : IConfigService
    {
        public IConfig GetRequiredService(Type type) => type == typeof(SocketConfig)
            ? config
            : throw new InvalidOperationException($"Unexpected config type: {type.FullName}");

        public T GetRequiredService<T>() where T : IConfig =>
            typeof(T) == typeof(SocketConfig)
                ? (T)(IConfig)config
                : throw new InvalidOperationException($"Unexpected config type: {typeof(T).FullName}");

        public void SaveConfigs() { }
        public void LoadConfigs() { }
        public void Save<T>() where T : IConfig { }
    }

    private sealed class FailingSocketConfigService : IConfigService
    {
        public IConfig GetRequiredService(Type type) => throw CreateFailure();
        public T GetRequiredService<T>() where T : IConfig => throw CreateFailure();
        public void SaveConfigs() { }
        public void LoadConfigs() { }
        public void Save<T>() where T : IConfig { }

        private static InvalidOperationException CreateFailure() =>
            new("simulated config resolution failure");
    }

    private sealed class RecordingLoopbackFactory : ISocketServerListenerFactory, IDisposable
    {
        private readonly object _lock = new();
        private readonly List<RealLoopbackListener> _listeners = new();
        private int _createCalls;

        public ConcurrentQueue<SocketServerSettings> Settings { get; } = new();
        public int CreateCalls => Volatile.Read(ref _createCalls);

        public ISocketServerListener Create(SocketServerSettings settings)
        {
            Settings.Enqueue(settings);
            var listener = new RealLoopbackListener(settings.ServerPort);
            lock (_lock)
                _listeners.Add(listener);
            Interlocked.Increment(ref _createCalls);
            return listener;
        }

        public async Task<RealLoopbackListener> WaitForListenerAsync(int index)
        {
            RealLoopbackListener? listener = null;
            Assert.True(SpinWait.SpinUntil(() =>
            {
                lock (_lock)
                {
                    if (_listeners.Count <= index)
                        return false;
                    listener = _listeners[index];
                    return true;
                }
            }, TestTimeout));
            Assert.NotNull(listener);
            Assert.True(listener.Started.Wait(TestTimeout));
            await Task.Yield();
            return listener;
        }

        public void Dispose()
        {
            RealLoopbackListener[] listeners;
            lock (_lock)
                listeners = _listeners.ToArray();
            foreach (RealLoopbackListener listener in listeners)
                listener.Dispose();
        }
    }

    private sealed class RetryThenReplacementLoopbackFactory : ISocketServerListenerFactory, IDisposable
    {
        private int _createCalls;

        public RetryThenReplacementLoopbackFactory(int port)
        {
            First = new RetryStopLoopbackListener(port);
        }

        public RetryStopLoopbackListener First { get; }
        public RealLoopbackListener? Replacement { get; private set; }
        public ConcurrentQueue<SocketServerSettings> Settings { get; } = new();
        public int CreateCalls => Volatile.Read(ref _createCalls);

        public ISocketServerListener Create(SocketServerSettings settings)
        {
            Settings.Enqueue(settings);
            int call = Interlocked.Increment(ref _createCalls);
            if (call == 1)
                return First;
            Assert.Equal(2, call);
            return Replacement = new RealLoopbackListener(settings.ServerPort);
        }

        public void Dispose()
        {
            First.Dispose();
            Replacement?.Dispose();
        }
    }

    private class RealLoopbackListener : ISocketServerListener, IDisposable
    {
        private readonly TcpListener _listener;
        private int _stopCalls;

        public RealLoopbackListener(int port)
        {
            _listener = new TcpListener(IPAddress.Loopback, port);
        }

        public ManualResetEventSlim Started { get; } = new();
        public int StopCalls => Volatile.Read(ref _stopCalls);

        public virtual void Start()
        {
            _listener.Start();
            Started.Set();
        }

        public TcpClient AcceptTcpClient() => _listener.AcceptTcpClient();

        public virtual void Stop()
        {
            Interlocked.Increment(ref _stopCalls);
            _listener.Stop();
        }

        public void Dispose()
        {
            try
            {
                _listener.Stop();
            }
            catch
            {
            }
            Started.Dispose();
        }
    }

    private sealed class RetryStopLoopbackListener : ISocketServerListener, IDisposable
    {
        private readonly TcpListener _listener;
        private int _stopCalls;

        public RetryStopLoopbackListener(int port)
        {
            _listener = new TcpListener(IPAddress.Loopback, port);
        }

        public ManualResetEventSlim Started { get; } = new();
        public ManualResetEventSlim RetryStopEntered { get; } = new();
        public ManualResetEventSlim ReleaseRetryStop { get; } = new();
        public ManualResetEventSlim StopCompleted { get; } = new();

        public void Start()
        {
            _listener.Start();
            Started.Set();
        }

        public TcpClient AcceptTcpClient() => _listener.AcceptTcpClient();

        public void Stop()
        {
            int call = Interlocked.Increment(ref _stopCalls);
            if (call == 1)
                throw new IOException("simulated first stop failure");

            RetryStopEntered.Set();
            if (!ReleaseRetryStop.Wait(TestTimeout))
                throw new TimeoutException("Timed out waiting to release the retry stop.");
            _listener.Stop();
            StopCompleted.Set();
        }

        public void Dispose()
        {
            ReleaseRetryStop.Set();
            try
            {
                _listener.Stop();
            }
            catch
            {
            }
            Started.Dispose();
            RetryStopEntered.Dispose();
            ReleaseRetryStop.Dispose();
            StopCompleted.Dispose();
        }
    }
}
