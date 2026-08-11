using ColorVision.UI.Desktop.LanRemote;
using ColorVision.UI.Desktop.Operations;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class LanRemoteControlServiceConfigReloadTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        $"ColorVisionLanRemoteReload-{Guid.NewGuid():N}");

    public LanRemoteControlServiceConfigReloadTests()
    {
        Directory.CreateDirectory(_rootDirectory);
    }

    [Fact]
    public async Task LoadConfigsMovesListenerAndTokenToCurrentConfigGeneration()
    {
        using var listenerFactory = new ReservedListenerFactory(2);
        int firstPort = listenerFactory.Ports[0];
        int secondPort = listenerFactory.Ports[1];
        int firstSecurePort = GetDistinctSecurePort(firstPort, secondPort, 41001);
        int secondSecurePort = GetDistinctSecurePort(firstPort, secondPort, firstSecurePort + 1);
        string configPath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
        const string firstToken = "lan-config-generation-one";
        const string secondToken = "lan-config-generation-two";

        WriteConfig(configPath, CreateConfig(firstPort, firstSecurePort, firstToken));
        var configHandler = new ConfigHandler { ConfigFilePath = configPath };
        configHandler.LoadConfigs();
        LanRemoteControlConfig firstConfig = configHandler.GetRequiredService<LanRemoteControlConfig>();

        var operationsHost = new FakeOperationsHost();
        using var service = new LanRemoteControlService(
            listenerFactory.Create,
            () => operationsHost,
            () => new[] { "127.0.0.1" },
            cancellationTokenSourceFactory: CreateThrowingCancellationTokenSource);
        ConfigReloadResult initialBind = configHandler.RegisterReloadParticipants(service);

        Assert.True(initialBind.Succeeded, initialBind.BuildFailureSummary());
        Assert.Equal(HttpStatusCode.OK, await GetStatusAsync(firstPort, firstToken));

        WriteConfig(configPath, CreateConfig(secondPort, secondSecurePort, secondToken));
        ConfigReloadResult reloadResult = configHandler.LoadConfigsWithResult();

        Assert.True(reloadResult.Succeeded, reloadResult.BuildFailureSummary());
        Assert.NotSame(firstConfig, configHandler.GetRequiredService<LanRemoteControlConfig>());
        await AssertPortClosedAsync(firstPort);
        Assert.Equal(HttpStatusCode.Unauthorized, await GetStatusAsync(secondPort, firstToken));
        Assert.Equal(HttpStatusCode.OK, await GetStatusAsync(secondPort, secondToken));

        firstConfig.IsEnabled = false;
        firstConfig.Port = firstPort;
        firstConfig.PairingToken = "retired-config-token";

        Assert.Equal(HttpStatusCode.OK, await GetStatusAsync(secondPort, secondToken));
    }

    [Fact]
    public void ConstructorDoesNotInvokeListenerAddressOrSecureHostFactories()
    {
        int listenerFactoryCalls = 0;
        int operationsHostFactoryCalls = 0;
        int addressProviderCalls = 0;

        using var service = new LanRemoteControlService(
            (_, _) =>
            {
                listenerFactoryCalls++;
                throw new InvalidOperationException("listener factory should not run during construction");
            },
            () =>
            {
                operationsHostFactoryCalls++;
                throw new InvalidOperationException("Operations host factory should not run during construction");
            },
            () =>
            {
                addressProviderCalls++;
                return new[] { "127.0.0.1" };
            });

        Assert.Equal(0, listenerFactoryCalls);
        Assert.Equal(0, operationsHostFactoryCalls);
        Assert.Equal(0, addressProviderCalls);
    }

    [Fact]
    public void SecureHostFactoryFailureIsCapturedByParticipantBinding()
    {
        var configHandler = new ConfigHandler { ConfigFilePath = Path.Combine(_rootDirectory, "factory-failure.json") };
        configHandler.Configs[typeof(LanRemoteControlConfig)] = CreateConfig(47652, 47653, "lan-factory-failure-token");
        int operationsHostFactoryCalls = 0;
        using var service = new LanRemoteControlService(
            (_, _) => throw new InvalidOperationException("plain listener must not start before secure host construction"),
            () =>
            {
                operationsHostFactoryCalls++;
                throw new InvalidOperationException("secure-host-factory-failure");
            },
            () => new[] { "127.0.0.1" });

        ConfigReloadResult result = configHandler.RegisterReloadParticipants(service);

        Assert.False(result.Succeeded);
        ConfigReloadFailure failure = Assert.Single(result.Failures);
        Assert.Equal(nameof(LanRemoteControlService), failure.OwnerName);
        Assert.Contains("secure-host-factory-failure", failure.Exception.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, operationsHostFactoryCalls);
        Assert.False(service.IsRunning);
    }

    [Fact]
    public void SecureHostSubscriptionFailureStopsNewHostAndIsCapturedByParticipantBinding()
    {
        var configHandler = new ConfigHandler { ConfigFilePath = Path.Combine(_rootDirectory, "subscription-failure.json") };
        configHandler.Configs[typeof(LanRemoteControlConfig)] = CreateConfig(47654, 47655, "lan-subscription-failure-token");
        var operationsHost = new SubscriptionFailingOperationsHost();
        int listenerFactoryCalls = 0;
        using var service = new LanRemoteControlService(
            (_, _) =>
            {
                listenerFactoryCalls++;
                throw new InvalidOperationException("plain listener must not start after subscription failure");
            },
            () => operationsHost,
            () => new[] { "127.0.0.1" });

        ConfigReloadResult result = configHandler.RegisterReloadParticipants(service);

        Assert.False(result.Succeeded);
        ConfigReloadFailure failure = Assert.Single(result.Failures);
        Assert.Contains("secure-host-subscription-failure", failure.Exception.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, operationsHost.StopCount);
        Assert.Equal(0, listenerFactoryCalls);
        Assert.False(service.IsRunning);
    }

    [Theory]
    [InlineData(SecureStartFailureMode.NotRunning)]
    [InlineData(SecureStartFailureMode.WrongPort)]
    public async Task SecureHostReportedFailureStopsPlainListenerAndIsAggregated(SecureStartFailureMode failureMode)
    {
        using var listenerFactory = new ReservedListenerFactory(1);
        int port = listenerFactory.Ports[0];
        int securePort = GetDistinctSecurePort(port, port, 47653);
        var configHandler = new ConfigHandler { ConfigFilePath = Path.Combine(_rootDirectory, $"secure-{failureMode}.json") };
        configHandler.Configs[typeof(LanRemoteControlConfig)] = CreateConfig(port, securePort, "lan-secure-startup-failure-token");
        var operationsHost = new FakeOperationsHost(failureMode);
        using var service = new LanRemoteControlService(
            listenerFactory.Create,
            () => operationsHost,
            () => new[] { "127.0.0.1" });

        ConfigReloadResult result = configHandler.RegisterReloadParticipants(service);

        Assert.False(result.Succeeded);
        ConfigReloadFailure failure = Assert.Single(result.Failures);
        Assert.Equal(nameof(LanRemoteControlService), failure.OwnerName);
        Assert.Contains("安全通道启动失败", failure.Exception.Message, StringComparison.Ordinal);
        Assert.False(service.IsRunning);
        Assert.False(operationsHost.IsRunning);
        Assert.True(operationsHost.StopCount >= 2);
        await AssertPortClosedAsync(port);
    }

    [Fact]
    public void ListenerStartupFailureIsCapturedByParticipantBinding()
    {
        var configHandler = new ConfigHandler { ConfigFilePath = Path.Combine(_rootDirectory, "startup-failure.json") };
        configHandler.Configs[typeof(LanRemoteControlConfig)] = CreateConfig(47652, 47653, "lan-startup-failure-token");
        var operationsHost = new FakeOperationsHost();
        using var service = new LanRemoteControlService(
            (_, _) => throw new SocketException((int)SocketError.AddressAlreadyInUse),
            () => operationsHost,
            () => new[] { "127.0.0.1" });

        ConfigReloadResult result = configHandler.RegisterReloadParticipants(service);

        Assert.False(result.Succeeded);
        ConfigReloadFailure failure = Assert.Single(result.Failures);
        Assert.Contains("启动失败", failure.Exception.Message, StringComparison.Ordinal);
        Assert.False(service.IsRunning);
    }

    [Fact]
    public async Task AddressResolutionFailureDuringReloadStopsC1AndIsAggregated()
    {
        using var listenerFactory = new ReservedListenerFactory(1);
        int port = listenerFactory.Ports[0];
        int securePort = GetDistinctSecurePort(port, port, 47656);
        string configPath = Path.Combine(_rootDirectory, "address-provider-failure.json");
        WriteConfig(configPath, CreateConfig(port, securePort, "lan-address-provider-c1"));
        var configHandler = new ConfigHandler { ConfigFilePath = configPath };
        configHandler.LoadConfigs();
        var operationsHost = new FakeOperationsHost();
        int failAddressResolution = 0;
        using var service = new LanRemoteControlService(
            listenerFactory.Create,
            () => operationsHost,
            () => Volatile.Read(ref failAddressResolution) == 0
                ? new[] { "127.0.0.1" }
                : throw new InvalidOperationException("address-provider-c2-failure"));
        ConfigReloadResult initialBind = configHandler.RegisterReloadParticipants(service);
        Assert.True(initialBind.Succeeded, initialBind.BuildFailureSummary());
        Assert.Equal(HttpStatusCode.OK, await GetStatusAsync(port, "lan-address-provider-c1"));

        WriteConfig(configPath, CreateConfig(port, securePort, "lan-address-provider-c2"));
        Volatile.Write(ref failAddressResolution, 1);
        ConfigReloadResult reloadResult = configHandler.LoadConfigsWithResult();

        Assert.False(reloadResult.Succeeded);
        ConfigReloadFailure failure = Assert.Single(
            reloadResult.Failures,
            item => item.OwnerName == nameof(LanRemoteControlService));
        Assert.Contains("address-provider-c2-failure", failure.Exception.ToString(), StringComparison.Ordinal);
        Assert.False(service.IsRunning);
        Assert.False(operationsHost.IsRunning);
        Assert.DoesNotContain("lan-address-provider-c1", service.GetConnectionUrl(), StringComparison.Ordinal);
        await AssertPortClosedAsync(port);
    }

    [Fact]
    public async Task RequestUsesOneCompleteRuntimeSnapshotWhileSamePortReloadCompletes()
    {
        using var listenerFactory = new ReservedListenerFactory(1);
        int port = listenerFactory.Ports[0];
        int securePort = GetDistinctSecurePort(port, port, 47654);
        string configPath = Path.Combine(_rootDirectory, "snapshot.json");
        const string firstToken = "lan-snapshot-generation-one";
        const string secondToken = "lan-snapshot-generation-two";
        const string firstHost = "10.44.0.1";
        const string secondHost = "10.44.0.2";
        using var snapshotCaptured = new ManualResetEventSlim();
        using var releaseRequest = new ManualResetEventSlim();
        int blockNextRequest = 0;

        WriteConfig(configPath, CreateConfig(port, securePort, firstToken, firstHost));
        var configHandler = new ConfigHandler { ConfigFilePath = configPath };
        configHandler.LoadConfigs();
        var operationsHost = new FakeOperationsHost();
        operationsHost.SetStatus(pairedDeviceCount: 1, relayStatus: "operations-c1");
        using var service = new LanRemoteControlService(
            listenerFactory.Create,
            () => operationsHost,
            () => new[] { firstHost, secondHost },
            () =>
            {
                if (Interlocked.Exchange(ref blockNextRequest, 0) != 1)
                    return;

                snapshotCaptured.Set();
                if (!releaseRequest.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Timed out waiting to release the captured LAN request snapshot.");
            });
        ConfigReloadResult initialBind = configHandler.RegisterReloadParticipants(service);
        Assert.True(initialBind.Succeeded, initialBind.BuildFailureSummary());

        Volatile.Write(ref blockNextRequest, 1);
        Task<(HttpStatusCode StatusCode, string Body)> firstRequest = GetStatusResponseAsync(port, firstToken);
        Assert.True(snapshotCaptured.Wait(TimeSpan.FromSeconds(5)), "The request did not capture its C1 runtime snapshot.");

        string firstStartedAt;
        try
        {
            operationsHost.SetStatus(pairedDeviceCount: 2, relayStatus: "operations-c2");
            WriteConfig(configPath, CreateConfig(port, securePort, secondToken, secondHost));
            ConfigReloadResult reloadResult = configHandler.LoadConfigsWithResult();
            Assert.True(reloadResult.Succeeded, reloadResult.BuildFailureSummary());
        }
        finally
        {
            releaseRequest.Set();
        }

        (HttpStatusCode firstStatusCode, string firstBody) = await firstRequest;
        Assert.Equal(HttpStatusCode.OK, firstStatusCode);
        using (JsonDocument document = JsonDocument.Parse(firstBody))
        {
            JsonElement root = document.RootElement;
            Assert.Equal($"http://{firstHost}:{port}", root.GetProperty("endpoint").GetString());
            Assert.Equal(port, root.GetProperty("port").GetInt32());
            Assert.True(root.GetProperty("isRunning").GetBoolean());
            Assert.Contains(firstHost, root.GetProperty("statusMessage").GetString(), StringComparison.Ordinal);
            Assert.Equal(1, root.GetProperty("secureOperations").GetProperty("pairedDeviceCount").GetInt32());
            Assert.Equal("operations-c1", root.GetProperty("secureOperations").GetProperty("relayStatus").GetString());
            firstStartedAt = root.GetProperty("startedAt").GetString() ?? string.Empty;
            Assert.NotEmpty(firstStartedAt);
        }

        (HttpStatusCode secondStatusCode, string secondBody) = await GetStatusResponseAsync(port, secondToken);
        Assert.Equal(HttpStatusCode.OK, secondStatusCode);
        using (JsonDocument document = JsonDocument.Parse(secondBody))
        {
            JsonElement root = document.RootElement;
            Assert.Equal($"http://{secondHost}:{port}", root.GetProperty("endpoint").GetString());
            Assert.Equal(port, root.GetProperty("port").GetInt32());
            Assert.True(root.GetProperty("isRunning").GetBoolean());
            Assert.Contains(secondHost, root.GetProperty("statusMessage").GetString(), StringComparison.Ordinal);
            Assert.Equal(2, root.GetProperty("secureOperations").GetProperty("pairedDeviceCount").GetInt32());
            Assert.Equal("operations-c2", root.GetProperty("secureOperations").GetProperty("relayStatus").GetString());
            Assert.Equal(firstStartedAt, root.GetProperty("startedAt").GetString());
        }

        Assert.Equal(HttpStatusCode.Unauthorized, await GetStatusAsync(port, firstToken));
    }

    private static LanRemoteControlConfig CreateConfig(
        int port,
        int securePort,
        string pairingToken,
        string preferredHost = "127.0.0.1")
    {
        return new LanRemoteControlConfig
        {
            IsEnabled = true,
            Port = port,
            SecurePort = securePort,
            PreferredHost = preferredHost,
            PairingToken = pairingToken,
        };
    }

    private static int GetDistinctSecurePort(int firstPort, int secondPort, int candidate)
    {
        while (candidate == firstPort || candidate == secondPort)
            candidate++;
        return candidate <= 65535 ? candidate : 40999;
    }

    private static void WriteConfig(string path, LanRemoteControlConfig config)
    {
        var writer = new ConfigHandler { ConfigFilePath = path };
        writer.Configs[typeof(LanRemoteControlConfig)] = config;
        writer.SaveConfigs();
    }

    private static async Task<HttpStatusCode> GetStatusAsync(int port, string pairingToken)
    {
        (HttpStatusCode statusCode, _) = await GetStatusResponseAsync(port, pairingToken);
        return statusCode;
    }

    private static async Task<(HttpStatusCode StatusCode, string Body)> GetStatusResponseAsync(
        int port,
        string pairingToken)
    {
        using var handler = new SocketsHttpHandler { UseProxy = false };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        using HttpResponseMessage response = await client.GetAsync(
            $"http://127.0.0.1:{port}/api/status?token={Uri.EscapeDataString(pairingToken)}");
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task AssertPortClosedAsync(int port)
    {
        using var client = new TcpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await client.ConnectAsync(IPAddress.Loopback, port, timeout.Token));
    }

    private static CancellationTokenSource CreateThrowingCancellationTokenSource()
    {
        var source = new CancellationTokenSource();
        _ = source.Token.Register(static () => throw new InvalidOperationException("test cancellation callback failure"));
        return source;
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
            Directory.Delete(_rootDirectory, recursive: true);
    }

    private sealed class ReservedListenerFactory : IDisposable
    {
        private readonly Dictionary<int, TcpListener> _listeners = new();

        public ReservedListenerFactory(int count)
        {
            var ports = new List<int>();
            for (int index = 0; index < count; index++)
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                _listeners.Add(port, listener);
                ports.Add(port);
            }
            Ports = ports;
        }

        public List<int> Ports { get; }

        public TcpListener Create(IPAddress address, int port)
        {
            if (!_listeners.Remove(port, out TcpListener? listener))
                throw new InvalidOperationException($"No reserved listener is available for port {port}.");
            return listener;
        }

        public void Dispose()
        {
            foreach (TcpListener listener in _listeners.Values)
                listener.Stop();
            _listeners.Clear();
        }
    }

    public enum SecureStartFailureMode
    {
        None,
        NotRunning,
        WrongPort,
    }

    private sealed class FakeOperationsHost : ILanRemoteOperationsHost
    {
        private readonly SecureStartFailureMode _failureMode;
        private int _pairedDeviceCount;
        private string _relayStatus = string.Empty;

        public FakeOperationsHost(SecureStartFailureMode failureMode = SecureStartFailureMode.None)
        {
            _failureMode = failureMode;
        }

        public event EventHandler? StateChanged;

        public bool IsRunning { get; private set; }

        public int RunningPort { get; private set; }

        public string LastStatusMessage { get; private set; } = "fake Operations host is stopped";

        public int StopCount { get; private set; }

        public OperationsSecureHostService? PublicService => null;

        public void Start(int port, Func<object> snapshotProvider)
        {
            switch (_failureMode)
            {
                case SecureStartFailureMode.NotRunning:
                    RunningPort = 0;
                    IsRunning = false;
                    LastStatusMessage = "fake secure Start swallowed a startup error";
                    break;
                case SecureStartFailureMode.WrongPort:
                    RunningPort = port + 1;
                    IsRunning = true;
                    LastStatusMessage = "fake secure Start reported the wrong port";
                    break;
                default:
                    RunningPort = port;
                    IsRunning = true;
                    LastStatusMessage = "fake Operations host is running";
                    break;
            }
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Stop()
        {
            StopCount++;
            RunningPort = 0;
            IsRunning = false;
            LastStatusMessage = "fake Operations host is stopped";
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public string CreatePairingPayload(string endpoint) => endpoint;

        public LanRemoteOperationsHostStatus CaptureStatus()
        {
            return new LanRemoteOperationsHostStatus
            {
                IsRunning = IsRunning,
                PairedDeviceCount = Volatile.Read(ref _pairedDeviceCount),
                RelayStatus = Volatile.Read(ref _relayStatus),
            };
        }

        public void SetStatus(int pairedDeviceCount, string relayStatus)
        {
            Volatile.Write(ref _pairedDeviceCount, pairedDeviceCount);
            Volatile.Write(ref _relayStatus, relayStatus);
        }
    }

    private sealed class SubscriptionFailingOperationsHost : ILanRemoteOperationsHost
    {
        public event EventHandler? StateChanged
        {
            add => throw new InvalidOperationException("secure-host-subscription-failure");
            remove { }
        }

        public bool IsRunning => false;

        public int RunningPort => 0;

        public string LastStatusMessage => "fake Operations host subscription failed";

        public int StopCount { get; private set; }

        public OperationsSecureHostService? PublicService => null;

        public void Start(int port, Func<object> snapshotProvider) => throw new NotSupportedException();

        public void Stop() => StopCount++;

        public string CreatePairingPayload(string endpoint) => throw new NotSupportedException();

        public LanRemoteOperationsHostStatus CaptureStatus() => new();
    }
}
